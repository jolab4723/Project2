using System;
using System.Collections;
using System.Linq;
using ItemSystem;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>명시적 --mirror-smoke-role 실행에서만 로비 요청과 접속 상태를 검증하는 테스트 도구다.</summary>
public sealed class MirrorSessionSmokeDriver_MirrorTest : MonoBehaviour
{
#if UNITY_EDITOR
    private static string[] editorArguments;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ConsumeEditorArguments()
    {
        // 다음 Play 한 번만 적용한다. Domain Reload를 꺼도 매 Play에서 이전 인자를 비운다.
        const string key = "SW.MirrorSmoke.Arguments";
        string configured = UnityEditor.SessionState.GetString(key, string.Empty);
        UnityEditor.SessionState.EraseString(key);
        editorArguments = string.IsNullOrEmpty(configured) ? null : configured.Split('\n');
        // MPPM은 프로세스 인수 대신 플레이어 태그를 전달한다. 명시적 검사 태그가
        // 있는 시나리오에서만 기존 검사기를 실행하고 일반 2/4 Player 실행은 수동으로 둔다.
        var tags = Unity.Multiplayer.PlayMode.CurrentPlayer.Tags.SelectMany(tag => tag.Split(';')).ToArray();
        if (editorArguments == null && tags.Contains("mirror-validation"))
            editorArguments = tags.Where(tag => tag.StartsWith("--mirror-", StringComparison.Ordinal) && tag.Contains("="))
                .SelectMany(tag => tag.Split(new[] { '=' }, 2)).ToArray();
    }
#endif

    private MirrorTestNetworkManager manager;
    private double startedAt;
    private double nextReportAt;
    private string role;
    private int expectedMembers;
    private double duration;
    private bool characterRequested;
    private bool readyRequested;
    private bool startRequested;
    private bool nodeRequested;
    private bool inventoryProbe;
    private bool inventorySceneRequested;
    private bool inventoryRoutineStarted;
    private bool inventoryValidationPassed;
    private bool runProbe;
    private bool firstRunStarted;
    private bool nextRunReadyAllowed;
    private bool runValidationPassed;
    private bool firstNodeProbe;
    private bool splitVoteProbe;
    private bool splitVoteCast;
    private bool firstNodeValidationPassed;
    private string expectedSelectedNodeId;
    private string expectedRejection;
    private bool sawExpectedRejection;
    private bool wasAdmitted;
    private bool observedSession;
    private bool passiveValidationPassed;
    private bool inventoryTurn;
    private InventorySmokeStep contention;
    private int inventorySlot;
    private bool inventoryTurnSent;
    private string contentionItemId;
    private int contentionPrice;
    private int contentionBudget;
    private uint contentionShopRevision;
    private readonly System.Collections.Generic.HashSet<int> inventoryReadySlots = new();
    private readonly System.Collections.Generic.Dictionary<int, MirrorTestShopRequestResult> contentionResults = new();
    private readonly System.Collections.Generic.Dictionary<int, uint> contentionInventoryRevisions = new();
    private readonly System.Collections.Generic.Dictionary<int, string> inventoryRetainedIds = new();
    private readonly System.Collections.Generic.HashSet<string> verifiedEnemyData = new();

    // 명시적 검사 전용: 0=개인 검사 순번, 1=개인 완료, 2=동일 재고 경쟁, 3=최종 확인, 4=판매할 경쟁 아이템 확인.
    public struct InventorySmokeStep : NetworkMessage
    {
        public byte Phase;
        public string ItemId;
        public double At;
        public int Budget;
        public MirrorTestShopRequestResult Result;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachWhenRequested()
    {
        if (string.IsNullOrEmpty(Argument("--mirror-smoke-role"))) return;
        if (NetworkManager.singleton is MirrorTestNetworkManager manager)
        {
            var driver = manager.gameObject.AddComponent<MirrorSessionSmokeDriver_MirrorTest>();
            driver.manager = manager;
            driver.ConfigureLatencyFixture();
        }
    }

    private void Start()
    {
        manager = GetComponent<MirrorTestNetworkManager>();
        role = Argument("--mirror-smoke-role");
        expectedMembers = int.TryParse(Argument("--mirror-smoke-count"), out int count) ? count : 1;
        duration = double.TryParse(Argument("--mirror-smoke-duration"), out double seconds) ? seconds : 60;
        inventoryProbe = Argument("--mirror-smoke-inventory") == "true";
        runProbe = Argument("--mirror-smoke-travel") == "full-run";
        firstNodeProbe = Argument("--mirror-smoke-travel") == "first-node";
        splitVoteProbe = Argument("--mirror-smoke-vote") == "split";
        string rejection = Argument("--mirror-smoke-expect-rejection");
        expectedRejection = rejection switch
        {
            null => null,
            "invalid-token" => "재접속 자격을 확인할 수 없습니다.",
            "expired-token" => "재접속 예약이 없거나 만료되었습니다.",
            "run-started" => "이미 출발한 세션에는 새로 참가할 수 없습니다.",
            "full" => "세션 정원이 찼습니다.",
            "version" => "서버와 클라이언트의 빌드 버전이 다릅니다.",
            "passive" => "패시브 프로필을 확인할 수 없습니다.",
            _ => string.Empty
        };
        if (expectedRejection == string.Empty || expectedRejection != null &&
            (role != "client" && role != "resume" || inventoryProbe || runProbe || firstNodeProbe))
        {
            Debug.LogError("[MirrorSmoke] FAIL invalid rejection expectation/options");
            Finish(false);
            return;
        }
        if (splitVoteProbe && (!firstNodeProbe || expectedMembers != 4 || !Debug.isDebugBuild && !Application.isEditor))
        {
            Debug.LogError("[MirrorVoteSmoke] FAIL split requires development build, first-node and count=4");
            Finish(false);
            return;
        }
        // 시작층은 원본 규칙상 항상 한 노드다. split은 실제 분기층까지 기존 진행 검사를 재사용한다.
        if (splitVoteProbe) { runProbe = true; firstNodeProbe = false; }
        if (runProbe && (!Debug.isDebugBuild && !Application.isEditor || inventoryProbe))
        {
            Debug.LogError("[MirrorRunSmoke] FAIL 전체 진행 검사는 개발 빌드에서 인벤토리 검사와 별도로 실행하세요.");
            Finish(false);
            return;
        }
        if (inventoryProbe && !Debug.isDebugBuild && !Application.isEditor)
        {
            Debug.LogError("[MirrorInventorySmoke] FAIL development build required");
            Finish(false);
            return;
        }
        startedAt = Time.realtimeSinceStartupAsDouble;
        manager.networkAddress = Argument("--mirror-address") ?? "127.0.0.1";
        manager.ClientDisplayName = MirrorReconnectProfile_MirrorTest.GetProfileName();
        manager.AdmissionStatusChanged += ReportAdmission;
        Application.targetFrameRate = 30;
        if (role == "server")
        {
            if (!NetworkServer.active) manager.StartServer();
        }
        else if (role == "host") manager.StartHost();
        else if (role == "client") manager.StartClient();
        else if (role == "resume")
        {
            manager.RequestedReconnectProfile = MirrorReconnectProfile_MirrorTest.Load(out string reason);
            if (manager.RequestedReconnectProfile == null)
            {
                Debug.LogError("[MirrorSmoke] 재접속 프로필 없음: " + reason);
                Application.Quit(2);
                return;
            }
            if (Argument("--mirror-smoke-token") == "invalid")
                manager.RequestedReconnectProfile.ReconnectToken = new string('A', 44);
            manager.StartClient();
        }
        else
        {
            Debug.LogError("[MirrorSmoke] 지원하지 않는 실행 역할");
            Application.Quit(2);
        }
        if (inventoryProbe)
        {
            if (NetworkServer.active) NetworkServer.RegisterHandler<InventorySmokeStep>(HandleInventoryServerStep);
            if (role != "server") NetworkClient.RegisterHandler<InventorySmokeStep>(HandleInventoryClientStep);
        }
        if (runProbe)
        {
            Debug.Log("[MirrorRunSmoke] route/data/reward check; development finishing damage, NOT real combat validation");
            StartCoroutine(Guard(ValidateEnemyData()));
            if (NetworkServer.active) StartCoroutine(Guard(ValidateServerRunReset()));
            if (role != "server") StartCoroutine(Guard(ValidateCompleteRun()));
        }
        if (firstNodeProbe) StartCoroutine(Guard(ValidateFirstNode()));
        if (NetworkServer.active && Argument("--mirror-smoke-buff-clock") == "true" &&
            (Debug.isDebugBuild || Application.isEditor))
            StartCoroutine(Guard(ValidateBuffClock()));
        if (expectedRejection == null && role != "server" && PassiveFixtureJson() != null)
            StartCoroutine(Guard(ValidatePassiveAndInitialization()));
    }

    private IEnumerator ValidateBuffClock()
    {
        yield return WaitFor(() => manager.ServerRoster.RunStarted &&
            manager.ServerPlayerContexts.Count == expectedMembers &&
            manager.ServerPlayerContexts.All(p => p.RuntimeState.HasSnapshot), "buff clock players", 90);
        PlayerContext owner = manager.ServerPlayerContexts.First();
        BuffDefinitionSO buff = Resources.Load<BuffDefinitionSO>("DataFiles/BuffData/3. GeneratedAssets/buff.attack_up");
        Require(buff != null && buff.duration == 10, "actual ten-second buff asset");
        owner.Buffs.ApplyBuff(buff);
        yield return new WaitForSecondsRealtime(12);
        Require(!owner.Buffs.ActiveBuffs.Any(b => b.source == buff), "connected buff expires");
        Debug.Log("[MirrorBuffClock] PASS connected expiry after 12 real seconds");
        owner.Buffs.ApplyBuff(buff);
        Debug.Log("[MirrorBuffClock] DISCONNECT_NOW second ten-second buff applied");
        yield return WaitFor(() => Time.timeScale == 0 &&
            owner.GetComponent<MirrorSpawnedPlayerBinder>().IsTemporarilyAbsent, "all absent pause", 90);
        float before = owner.Buffs.ActiveBuffs.FirstOrDefault(b => b.source == buff)?.remainingTime ?? 0;
        Require(before > 0, "buff expired before disconnect; retry with an earlier disconnect");
        yield return new WaitForSecondsRealtime(12);
        float after = owner.Buffs.ActiveBuffs.FirstOrDefault(b => b.source == buff)?.remainingTime ?? 0;
        Debug.Log($"[MirrorBuffClock] {(after <= 0 ? "PASS" : "FAIL")} all-absent server-time expiry before={before:F3} after={after:F3} realSeconds=12 timeScale={Time.timeScale}");
        yield return WaitFor(() => !owner.GetComponent<MirrorSpawnedPlayerBinder>().IsTemporarilyAbsent, "buff clock resume", 120);
        Require(manager.ServerPlayerContexts.Contains(owner), "reconnect retains buff owner");
        yield return new WaitForSecondsRealtime(12);
        Require(!owner.Buffs.ActiveBuffs.Any(b => b.source == buff), "resumed buff expires");
        Debug.Log("[MirrorBuffClock] PASS retained owner and resumed expiry");
    }

    private void Update()
    {
        wasAdmitted |= manager.ClientCompatibilityConfirmed;
        observedSession |= role == "server"
            ? NetworkServer.active && manager.ServerRoster.RunStarted && manager.ServerPlayerContexts.Count == expectedMembers
            : NetworkClient.isConnected && manager.ClientCompatibilityConfirmed && manager.ClientLobby.RunStarted &&
              manager.ClientLobby.Members?.Length == expectedMembers && manager.LocalPlayerContext?.RuntimeState.HasSnapshot == true;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now - startedAt >= duration)
        {
            ReportState();
            Debug.Log("[MirrorSmoke] END role=" + role);
            bool sessionPassed = expectedRejection != null
                ? sawExpectedRejection && !wasAdmitted && !NetworkClient.isConnected
                : observedSession;
            bool passed = sessionPassed && (!inventoryProbe || inventoryValidationPassed) &&
                          (!runProbe || runValidationPassed) && (!firstNodeProbe || firstNodeValidationPassed);
            if (Argument("--mirror-smoke-combat") == "true")
                passed &= MirrorCombatSmoke_MirrorTest.Completed && MirrorCombatSmoke_MirrorTest.Passed;
            if (Argument("--mirror-smoke-platform") == "true")
                passed &= MirrorPlatformSmoke_MirrorTest.Completed && MirrorPlatformSmoke_MirrorTest.Passed;
            if (expectedRejection == null && role != "server" && PassiveFixtureJson() != null)
                passed &= passiveValidationPassed;
            Debug.Log($"[MirrorSmoke] {(sessionPassed ? "PASS" : "FAIL")} admission/session role={role} expectedRejection={expectedRejection ?? "none"}");
            if (!passed) Debug.LogError("[MirrorSmoke] FAIL 요청한 검증이 완료되지 않았습니다.");
            Finish(passed);
            return;
        }
        if (manager.ClientCompatibilityConfirmed && !manager.ClientLobby.RunStarted &&
            (!runProbe || !firstRunStarted || nextRunReadyAllowed))
        {
            MirrorLobbyMember_MirrorTest[] members = manager.ClientLobby.Members;
            if (members != null)
            {
                MirrorLobbyMember_MirrorTest local = members.FirstOrDefault(member => member.ParticipantId == manager.LocalParticipantId);
                if (!characterRequested && !string.IsNullOrEmpty(local.ParticipantId))
                {
                    characterRequested = manager.RequestLobbyChange(MirrorLobbyOperation_MirrorTest.Character,
                        Argument("--mirror-smoke-class") == "Gunner" ? CharacterClass.Gunner : CharacterClass.Fighter);
                }
                if (local.HasCharacterChoice && !readyRequested)
                    readyRequested = manager.RequestLobbyChange(MirrorLobbyOperation_MirrorTest.Ready, ready: true);
                if (!startRequested && local.IsLeader && members.Length == expectedMembers &&
                    members.All(member => member.HasCharacterChoice && member.IsReady))
                    startRequested = manager.RequestStartSession();
            }
        }
        if (now >= nextReportAt)
        {
            nextReportAt = now + 5;
            ReportState();
        }
        if (inventoryProbe && NetworkServer.active && !inventorySceneRequested &&
            manager.ServerRoster.RunStarted && manager.IsSessionSelectionActive &&
            manager.ServerPlayerContexts.Count == expectedMembers && manager.TryGetRunSnapshot(out StageMapSaveData inventoryRun))
        {
            // 명시적 인벤토리 검사에서만 첫 노드를 Camp fixture로 바꾼다.
            // 실제 진입은 아래의 소유 클라이언트 노드 선택 요청과 서버 배치 경로를 모두 거친다.
            StageNodeSaveData camp = inventoryRun.nodes.First(node => node.floor == inventoryRun.clearedFloor + 1);
            camp.type = StageNodeType.Camp;
            camp.sceneName = "Act1_Camp";
            inventorySceneRequested = manager.ServerPublishRunSnapshot(inventoryRun);
        }
        if (inventoryProbe && !nodeRequested && manager.CanLocalClientVote && manager.IsSessionSelectionActive &&
            manager.TryGetRunSnapshot(out StageMapSaveData campRun))
        {
            StageNodeSaveData camp = campRun.nodes.FirstOrDefault(node => node.floor == campRun.clearedFloor + 1 && node.type == StageNodeType.Camp);
            if (camp != null) nodeRequested = manager.RequestStageNodeSelection(camp.id);
        }
        if (inventoryProbe && !inventoryRoutineStarted && manager.LocalPlayerContext?.RuntimeState.HasSnapshot == true &&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == MirrorTestNetworkManager.SessionCampGameplayScene &&
            manager.LocalPlayerContext.Inventory.PlayerGrid.HasView)
        {
            inventoryRoutineStarted = true;
            StartCoroutine(Guard(ValidateInventoryTurn()));
        }
    }

    // 명시적 개발 검사만 임시 입력을 만든다. 실제 로컬 저장 파일과 전역 프로필은 변경하지 않는다.
    public static string PassiveFixtureJson()
    {
        string fixture = Argument("--mirror-smoke-passive");
        if (fixture == null || string.IsNullOrEmpty(Argument("--mirror-smoke-role")) ||
            !Debug.isDebugBuild && !Application.isEditor) return null;
        Require(fixture is "empty" or "attack1" or "attack5-shop" or "all-max" or "invalid-rank", "known passive fixture");
        var tree = new Core.PassiveSkillTreeData();
        if (fixture == "all-max")
        {
            var database = NetworkManager.singleton.GetComponent<MirrorSessionAuthenticator_MirrorTest>().PassiveDatabase;
            foreach (Core.PassiveSkillId id in Enum.GetValues(typeof(Core.PassiveSkillId)))
            {
                var definition = database.Get(id);
                if (definition != null) tree.learnedSkills.Add(new Core.PassiveSkillEntry
                    { id = id, currentLevel = definition.maxLevel, unlockedLevel = definition.maxLevel });
            }
            return JsonUtility.ToJson(tree);
        }
        int level = fixture == "attack1" ? 1 : fixture == "attack5-shop" ? 5 : fixture == "invalid-rank" ? 999 : 0;
        if (level > 0) tree.learnedSkills.Add(new Core.PassiveSkillEntry
            { id = Core.PassiveSkillId.AttackPower, currentLevel = level, unlockedLevel = level });
        if (fixture == "attack5-shop") tree.learnedSkills.Add(new Core.PassiveSkillEntry
            { id = Core.PassiveSkillId.ShopEnhance, currentLevel = 1, unlockedLevel = 1 });
        return JsonUtility.ToJson(tree);
    }

    private void ConfigureLatencyFixture()
    {
        if (!float.TryParse(Argument("--mirror-smoke-latency-ms"), out float latency)) return;
        Require((Debug.isDebugBuild || Application.isEditor) && !NetworkServer.active && !NetworkClient.active &&
            latency >= 0f && latency <= 500f, "latency fixture before connection, 0..500ms");
        // Awake에 wrap이 필요하므로 비활성 자식에서 구성한 뒤 활성화한다.
        var root = new GameObject("MirrorSmoke_Latency");
        root.SetActive(false);
        root.transform.SetParent(manager.transform, false);
        var simulation = root.AddComponent<LatencySimulation>();
        simulation.wrap = manager.transport;
        simulation.latency = latency;
        simulation.jitter = 0.02f;
        simulation.unreliableLoss = 2f;
        simulation.unreliableScramble = 2f;
        manager.transport = simulation;
        Transport.active = simulation;
        root.SetActive(true);
        Debug.Log($"[MirrorSmoke] latency fixture per outbound leg={latency}ms jitter<=20ms unreliable loss/scramble=2%");
    }

    private IEnumerator ValidatePassiveAndInitialization()
    {
        yield return WaitFor(() => manager.LocalPlayerContext?.RuntimeState.HasSnapshot == true, "passive owner snapshot", 90);
        yield return new WaitForSecondsRealtime(1f);
        PlayerContext owner = manager.LocalPlayerContext;
        var database = manager.GetComponent<MirrorSessionAuthenticator_MirrorTest>().PassiveDatabase;
        string fixture = Argument("--mirror-smoke-passive");
        int rank = fixture == "attack1" ? 1 : fixture is "attack5-shop" or "all-max" ? 5 : 0;
        float expected = database.Get(Core.PassiveSkillId.AttackPower).GetValue(rank);
        Require(MirrorPassiveProfile_MirrorTest.TryValidate(PassiveFixtureJson(), database, out var profile, out _), "valid passive fixture");
        owner.Stats.GetLayerStatSets(out StatSet character, out StatSet equipment, out StatSet buff, out StatSet passive);
        Require(Mathf.Approximately(passive.attackPowerPercent, expected), "owner personal passive raw layer");
        Require(JsonUtility.ToJson(passive) == JsonUtility.ToJson(profile.Stats), "all personal passive stat fields replicated");
        var expectedStats = new PlayerStat();
        expectedStats.Recalculate(character, equipment, buff, profile.Stats);
        Require(owner.RuntimeState.AttackPower == expectedStats.attackPower, "owner final attack reflects personal rank");
        var shop = owner.GetComponent<NetworkShopPlayerState_MirrorTest>();
        Require(shop.ShopEnhanceLevel == profile.ShopLevel, "owner shop rank");
        if (profile.ShopLevel > 0)
            Require(Mathf.Approximately(shop.DiscountPercent, database.Get(Core.PassiveSkillId.ShopEnhance).GetValue(1) / 100f) &&
                shop.ExtraRerollCount == database.Get(Core.PassiveSkillId.ShopEnhance).extraRerollCount, "owner discount/reroll source");

        PlayerStat original = owner.Stats.Stat;
        int level = original.currentLevel;
        float exp = original.currentExp, health = owner.Health.CurrentHealth, mana = owner.Mana.CurrentMana;
        Require(ReferenceEquals(original, owner.Stats.EnsureInitialized()) &&
            ReferenceEquals(original, owner.Stats.EnsureInitialized()), "idempotent stat identity");
        var status = owner.GetComponent<WBH_PlayerStatus>();
        int calls = 0;
        void Changed(float value) { calls++; }
        status.OnAtkSpeedChanged += Changed;
        original.NotifyValuesChanged();
        int baselineCalls = calls;
        status.Initialize(owner.Controller);
        status.Initialize(owner.Controller);
        status.enabled = false;
        status.enabled = true;
        calls = 0;
        original.NotifyValuesChanged();
        status.OnAtkSpeedChanged -= Changed;
        Require(baselineCalls > 0 && calls == baselineCalls, "one owned subscription after repeated initialize/re-enable");
        Require(ReferenceEquals(original, owner.Stats.Stat) && original.currentLevel == level && original.currentExp == exp &&
            owner.Health.CurrentHealth == health && owner.Mana.CurrentMana == mana, "initialization preserves stat/resources");
        passiveValidationPassed = true;
        Debug.Log($"[MirrorPassiveSmoke] PASS role={role} fixture={fixture} netId={shop.netId} rank={rank} attack={owner.RuntimeState.AttackPower} callbacks={calls} init/re-enable/resources");
    }

    private void OnDestroy()
    {
        if (manager != null) manager.AdmissionStatusChanged -= ReportAdmission;
        if (inventoryProbe)
        {
            NetworkServer.UnregisterHandler<InventorySmokeStep>();
            NetworkClient.UnregisterHandler<InventorySmokeStep>();
        }
    }

    /// <summary>
    /// 명시적 개발 검사에서만 실제 생성된 적에게 종료 피해를 준다. 피해 계산·스킬 품질 검사가 아니라
    /// 적 사망→웨이브→포탈→결과→새 런의 연결 검사이며 진행 스냅샷을 직접 완료시키지 않는다.
    /// </summary>
    private IEnumerator ValidateServerRunReset()
    {
        bool sawRun = false;
        bool returned = false;
        string splitExpectedNodeId = null;
        var fundedPlayers = new System.Collections.Generic.HashSet<uint>();
        var enemySeenAt = new System.Collections.Generic.Dictionary<uint, double>();
        var rewardedKinds = new System.Collections.Generic.HashSet<string>();
        while (true)
        {
            if (manager.ServerRoster.RunStarted)
            {
                sawRun = true;
                if (splitVoteProbe)
                {
                    if (manager.IsSessionSelectionActive && manager.TryGetRunSnapshot(out StageMapSaveData votingRun))
                    {
                        StageNodeSaveData last = votingRun.nodes.FirstOrDefault(node => node.id == votingRun.lastClearedNodeId);
                        var accessible = votingRun.nodes.Where(node => node.floor == votingRun.clearedFloor + 1 &&
                            (last == null || last.nextNodeIds.Contains(node.id))).OrderBy(node => node.id, StringComparer.Ordinal).ToArray();
                        if (accessible.Length >= 2) splitExpectedNodeId = accessible[1].id;
                    }
                    else if (!string.IsNullOrEmpty(splitExpectedNodeId) && manager.TryGetPendingStageNode(out StageNodeSaveData winner))
                    {
                        Require(winner.id == splitExpectedNodeId, "split 서버 최다 득표 B 확정");
                        Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == ExpectedScene(winner), "split 서버 실제 씬 이동");
                        if (role == "server") runValidationPassed = true;
                        Debug.Log($"[MirrorVoteSmoke] PASS split-server winner={winner.id} floor={winner.floor} path={ExpectedScene(winner)}");
                        yield break;
                    }
                }
                if (returned)
                {
                    if (manager.IsSessionSelectionActive && manager.ServerPlayerContexts.Count == expectedMembers)
                    {
                        Require(!manager.IsRunCompleted && manager.TryGetRunSnapshot(out StageMapSaveData fresh) &&
                                fresh.clearedFloor == 0 && string.IsNullOrEmpty(fresh.pendingNodeId), "새 런 진행도 초기화");
                        foreach (PlayerContext player in manager.ServerPlayerContexts)
                            Require(!fundedPlayers.Contains(player.GetComponent<NetworkIdentity>().netId) &&
                                    player.Wallet.Gold == 0 && player.Inventory.GetAllInventoryItems().Count == 0,
                                "새 서버 캐릭터·골드·인벤토리 초기화");
                        if (role == "server") runValidationPassed = true;
                        Debug.Log("[MirrorRunSmoke] PASS server-reset: same connections, new runtime, no previous run gold/items/progress");
                        yield break;
                    }
                }
                else
                {
                    foreach (PlayerContext player in manager.ServerPlayerContexts)
                        if (fundedPlayers.Add(player.GetComponent<NetworkIdentity>().netId))
                            player.GetComponent<NetworkShopPlayerState_MirrorTest>().ServerSetGold(5000);
                    foreach (NetworkEnemyAuthority_MirrorTest enemy in FindObjectsByType<NetworkEnemyAuthority_MirrorTest>(FindObjectsSortMode.None))
                        if (enemy.isServer && !enemy.IsDead)
                        {
                            if (!enemySeenAt.TryGetValue(enemy.netId, out double seenAt))
                                enemySeenAt[enemy.netId] = Time.realtimeSinceStartupAsDouble;
                            else if (Time.realtimeSinceStartupAsDouble - seenAt >= 1)
                            {
                                WBH_EnemyStatus enemyStatus = enemy.GetComponent<WBH_EnemyStatus>();
                                PlayerContext attacker = rewardedKinds.Add(enemy.EnemyInfo.enemyId)
                                    ? manager.ServerPlayerContexts.First() : null;
                                int previousGold = attacker?.Wallet.Gold ?? 0;
                                var damage = new WBH_DamageResult(attacker?.Controller, enemy.MaxHealth + 1, false, ElementType.Fire);
                                enemyStatus.TakeDamage(damage);
                                if (attacker != null)
                                {
                                    enemyStatus.TakeDamage(damage);
                                    Require(enemy.KillRewardCount == 1 && attacker.Wallet.Gold == previousGold + enemy.EnemyInfo.credit,
                                        "적 처치 보상 크레딧 1회 지급");
                                    Debug.Log($"[MirrorRunSmoke] PASS kill-credit id={enemy.EnemyInfo.enemyId} amount={enemy.EnemyInfo.credit} gold={attacker.Wallet.Gold}");
                                }
                            }
                        }
                }
            }
            else if (sawRun)
            {
                Require(manager.ServerPlayerContexts.Count == 0 && !manager.HasRunSnapshot, "로비 복귀 시 이전 런타임·진행도 제거");
                returned = true;
            }
            yield return new WaitForSecondsRealtime(0.2f);
        }
    }

    private IEnumerator ValidateEnemyData()
    {
        while (!runValidationPassed)
        {
            if (manager.TryGetPendingStageNode(out StageNodeSaveData node))
            {
                WBH_EnemyDataProvider provider = FindFirstObjectByType<WBH_EnemyDataProvider>();
                foreach (NetworkEnemyAuthority_MirrorTest enemy in FindObjectsByType<NetworkEnemyAuthority_MirrorTest>(FindObjectsSortMode.None))
                {
                    if (enemy.IsDead || enemy.MaxHealth <= 0) continue;
                    WBH_EnemyInfo actual = enemy.EnemyInfo;
                    string key = node.floor + "/" + actual?.enemyId;
                    if (verifiedEnemyData.Contains(key)) continue;
                    WBH_EnemyInfo expected = null;
                    Require(provider != null && provider.TryCreateEnemyInfo(actual?.enemyId,
                        new WBH_EnemyStatContext(node.floor, "normal", expectedMembers), out expected), "적 Provider 조회");
                    Require(JsonUtility.ToJson(actual) == JsonUtility.ToJson(expected) &&
                            Mathf.Approximately(enemy.MaxHealth, expected.maxHP), "적 데이터·인원·층 배율 동기화");
                    verifiedEnemyData.Add(key);
                    Debug.Log($"[MirrorRunSmoke] PASS enemy-data role={role} floor={node.floor} id={actual.enemyId} type={actual.enemyAttackType} HP={enemy.MaxHealth}");
                }
            }
            yield return null;
        }
    }

    private IEnumerator ValidateCompleteRun()
    {
        yield return WaitFor(() => manager.ClientLobby.RunStarted && manager.LocalPlayerContext?.RuntimeState.HasSnapshot == true,
            "첫 런 캐릭터 생성", 90);
        firstRunStarted = true;
        string participantId = manager.LocalParticipantId;
        uint firstNetId = manager.LocalPlayerContext.GetComponent<NetworkIdentity>().netId;
        int visited = 0;
        var combatScenes = new System.Collections.Generic.HashSet<string>();
        bool visitedCamp = false, visitedEvent = false, visitedBoss = false;
        Require(manager.RequestLobbyChange(MirrorLobbyOperation_MirrorTest.ReturnToLobby), "클리어 전 복귀 거절 검사 요청");
        yield return new WaitForSecondsRealtime(0.5f);
        Require(manager.ClientLobby.RunStarted && !manager.IsRunCompleted, "클리어 전 복귀 거절");

        while (!manager.IsRunCompleted)
        {
            yield return WaitFor(() => manager.IsSessionSelectionActive && manager.TryGetRunSnapshot(out StageMapSaveData snapshot) &&
                                      snapshot.nodes.Count > 0, "선택 화면 진행도", 90);
            yield return VoteForNode(splitVoteProbe);
            yield return WaitFor(() => !manager.IsSessionSelectionActive && manager.CurrentSessionRoute != MirrorSessionRoute.Unknown,
                "파티 노드 이동", 90);
            yield return WaitFor(() => manager.TryGetPendingStageNode(out _), "현재 노드 스냅샷");
            manager.TryGetPendingStageNode(out StageNodeSaveData pending);
            Require(pending.id == expectedSelectedNodeId, "전원 동일 투표의 선택 결과");
            visited++;
            Debug.Log($"[MirrorRunSmoke] node={pending.id} type={pending.type} floor={pending.floor} role={role}");
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            Require(scene == ExpectedScene(pending), "서버 배정 sceneName과 실제 SW 씬 일치");
            Debug.Log($"[MirrorRunSmoke] PASS route role={role} node={pending.id} sceneName={pending.sceneName} path={scene}");
            if (splitVoteCast)
            {
                if (pending.type != StageNodeType.Event)
                    yield return WaitFor(() => manager.LocalPlayerContext?.Controller.IsControlEnabled == true &&
                        manager.LocalPlayerContext.GetComponent<MirrorSpawnedPlayerBinder>().IsSceneStartConfirmed &&
                        manager.LocalPlayerContext.Controller.agent.enabled && manager.LocalPlayerContext.Controller.agent.isOnNavMesh,
                        "split 선택 씬 Controller/NavMesh 복구", 30);
                runValidationPassed = true;
                Debug.Log($"[MirrorVoteSmoke] PASS split role={role} winner={pending.id} floor={pending.floor} path={scene}; leader A / other three B");
                yield break;
            }
            if (manager.CurrentSessionRoute != MirrorSessionRoute.Event)
            {
                PlayerContext arriving = manager.LocalPlayerContext;
                yield return WaitFor(() => arriving.GetComponent<MirrorSpawnedPlayerBinder>().IsSceneStartConfirmed &&
                    arriving.Controller.IsControlEnabled && arriving.Controller.agent.enabled && arriving.Controller.agent.isOnNavMesh,
                    "실제 씬 Controller/NavMesh 복구", 30);
                Debug.Log($"[MirrorRunSmoke] PASS controller-navmesh role={role} sceneName={pending.sceneName}");
            }
            yield return new WaitForEndOfFrame();
            CaptureRunScreen(pending.id + "_" + pending.sceneName);
            if (manager.CurrentSessionRoute == MirrorSessionRoute.Event)
            {
                visitedEvent = true;
                if (manager.CanLocalClientControlSession)
                {
                    Button choice = null;
                    yield return WaitFor(() => (choice = FindObjectsByType<YJ_ChoiceButton>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Select(view => view.GetComponent<Button>()).FirstOrDefault(button => button != null && button.isActiveAndEnabled && button.interactable)) != null,
                        "미지 선택 UI", 40);
                    choice.onClick.Invoke();
                }
            }
            else
            {
                PlayerContext owner = manager.LocalPlayerContext;
                if (manager.CurrentSessionRoute == MirrorSessionRoute.Combat)
                {
                    if (pending.type == StageNodeType.Boss) visitedBoss = true;
                    else if (combatScenes.Count < 6)
                        Require(combatScenes.Add(pending.sceneName), "첫 6개 일반 전투 맵 중복 없는 순회");
                    yield return WaitFor(() => FindFirstObjectByType<NetworkEnemyWaveSpawner_MirrorTest>()?.SessionPhase == MirrorTestSessionPhase.Completed,
                        "실제 적 사망 후 웨이브 완료", 90);
                    Require(verifiedEnemyData.Any(key => key.StartsWith(pending.floor + "/", StringComparison.Ordinal)),
                        "현재 전투 씬 적 데이터 검증 완료");
                    if (pending.type == StageNodeType.Boss) break;
                }
                else visitedCamp = true;
                if (pending.sceneName == "Act1_Stage5") yield return RideStage5Elevator(owner);
                MirrorStagePortalAdapter_MirrorTest portal = FindFirstObjectByType<MirrorStagePortalAdapter_MirrorTest>();
                Require(portal != null, "실제 스테이지 포탈");
                YJ_PortalActive portalVisual = portal.GetComponentInParent<YJ_PortalActive>();
                yield return WaitFor(() => portalVisual != null && portalVisual.IsPortalActive,
                    "전원 처치 후 포탈 활성화 복제", 5);
                ParticleSystem[] portalParticles = portalVisual.GetComponentsInChildren<ParticleSystem>(true);
                Require(portalParticles.Length > 0 && portalParticles.All(p =>
                    p.isPlaying && p.GetComponent<Renderer>().enabled), "포탈 원형 파티클 재생");
                Require(portalVisual.transform.position.y > portal.transform.position.y + 0.1f,
                    "포탈 원형 이펙트의 본체 위 배치");
                Debug.Log($"[MirrorRunSmoke] portal-visual role={role} scene={pending.sceneName} particles={portalParticles.Length} height={portalVisual.transform.position.y - portal.transform.position.y:F3}");
                Vector3 destination = portal.GetComponent<Collider>().bounds.center;
                destination.y = owner.transform.position.y;
                Require(UnityEngine.AI.NavMesh.SamplePosition(destination, out UnityEngine.AI.NavMeshHit hit, 3,
                    owner.Controller.agent.areaMask), "포탈 이동 영역");
                var path = new UnityEngine.AI.NavMeshPath();
                Require(owner.Controller.agent.CalculatePath(hit.position, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete,
                    "시작점에서 포탈까지 연결된 이동 경로");
                Vector3 beforePortalMove = owner.transform.position;
                owner.Controller.MoveCommand(hit.position);
                Debug.Log($"[MirrorRunSmoke] portal-input role={role} node={pending.id} from={beforePortalMove} target={hit.position} confirmed={owner.GetComponent<MirrorSpawnedPlayerBinder>().IsSceneStartConfirmed} state={owner.StateMachine.CurrentState} pending={owner.Controller.agent.pathPending} hasPath={owner.Controller.agent.hasPath} stopped={owner.Controller.agent.isStopped}");
                yield return WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scene ||
                    Vector3.Distance(owner.transform.position, beforePortalMove) > 0.15f || Vector3.Distance(owner.transform.position, hit.position) < 0.5f,
                    "포탈 이동 명령 후 실제 위치 전진", 3);
            }
            yield return WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scene,
                "실제 포탈 또는 미지 선택 완료 후 파티 복귀", 100);
        }

        NetworkBossHealthBar_MirrorTest result = null;
        Require(!splitVoteProbe, "런 종료 전 실제 분기층 3:1 투표 수행");
        yield return WaitFor(() => manager.IsRunCompleted && (result = FindFirstObjectByType<NetworkBossHealthBar_MirrorTest>()) != null && result.IsClearVisible,
            "보스 완료 결과 화면");
        Require(combatScenes.SetEquals(Enumerable.Range(1, 6).Select(index => "Act1_Stage" + index)) &&
                visitedCamp && visitedEvent && visitedBoss, "Stage1~6/Camp/Event/Boss 실제 씬 방문 집합");
        Debug.Log($"[MirrorRunSmoke] PASS coverage role={role} combat={string.Join(",", combatScenes.OrderBy(value => value))} camp={visitedCamp} event={visitedEvent} boss={visitedBoss}");
        Button returnButton = result.GetComponentsInChildren<Button>(true).Single(button => button.name == "ReturnToLobbyButton");
        CaptureRunScreen("clear");
        Require(verifiedEnemyData.Any(key => key.EndsWith("/enemy.normal.melee.working_machine")) &&
                verifiedEnemyData.Any(key => key.EndsWith("/enemy.normal.ranged.patrol_drone")) &&
                verifiedEnemyData.Any(key => key.EndsWith("/enemy.boss.boss.SpiderX")), "근거리·원거리·보스 데이터 수신");
        if (!manager.ClientIsSessionLeader)
        {
            Require(!returnButton.interactable, "비방장 결과 버튼 비활성");
            Require(manager.RequestLobbyChange(MirrorLobbyOperation_MirrorTest.ReturnToLobby), "비방장 우회 요청 검사");
            yield return new WaitForSecondsRealtime(1);
            Require(manager.ClientLobby.RunStarted && manager.IsRunCompleted, "비방장 복귀 요청 서버 거절");
        }
        else
        {
            yield return new WaitForSecondsRealtime(3);
            Require(returnButton.interactable, "방장 결과 버튼 활성");
            returnButton.onClick.Invoke();
        }
        yield return WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == MirrorTestNetworkManager.SessionLobbyScene &&
                                  !manager.ClientLobby.RunStarted, "결과 버튼으로 KY 로비 복귀", 90);
        yield return null;
        Require(!manager.HasRunSnapshot && manager.LocalPlayerContext == null && NetworkClient.localPlayer == null &&
                manager.ClientLobby.Members.All(member => !member.IsReady), "클라이언트 이전 상태 제거·준비 해제");
        TMPReadyCheck();
        CaptureRunScreen("lobby");
        yield return new WaitForSecondsRealtime(2);
        characterRequested = true;
        readyRequested = false;
        startRequested = false;
        nextRunReadyAllowed = true;
        yield return WaitFor(() => manager.ClientLobby.RunStarted && manager.IsSessionSelectionActive &&
                                  manager.LocalPlayerContext?.RuntimeState.HasSnapshot == true, "같은 연결로 다음 런 시작", 90);
        PlayerContext freshPlayer = manager.LocalPlayerContext;
        Require(manager.LocalParticipantId == participantId && freshPlayer.GetComponent<NetworkIdentity>().netId != firstNetId &&
                freshPlayer.Wallet.Gold == 0 && freshPlayer.Inventory.GetAllInventoryItems().Count == 0 &&
                manager.TryGetRunSnapshot(out StageMapSaveData freshRun) && freshRun.clearedFloor == 0,
            "새 런 클라이언트 캐릭터·골드·아이템·진행도 초기화");
        runValidationPassed = true;
        Debug.Log($"[MirrorRunSmoke] PASS role={role} visited={visited} lobby/nodes/portal/boss/result/lobby/new-run; oldNetId={firstNetId} newNetId={freshPlayer.GetComponent<NetworkIdentity>().netId}");
    }

    private IEnumerator VoteForNode(bool split)
    {
        YJ_StageNodeHover[] candidates = null;
        yield return WaitFor(() =>
        {
            if (!manager.CanLocalClientVote || !manager.TryGetRunSnapshot(out StageMapSaveData snapshot)) return false;
            candidates = FindObjectsByType<YJ_StageNodeHover>(FindObjectsSortMode.None)
                .Where(node => node.IsInteractable && node.NodeData != null && node.NodeData.floor == snapshot.clearedFloor + 1)
                .OrderBy(node => !runProbe || splitVoteProbe ? 0 : node.NodeData.type == StageNodeType.Camp ? 0 : node.NodeData.type == StageNodeType.Event ? 1 : 2)
                .ThenBy(node => node.NodeData.id, StringComparer.Ordinal).ToArray();
            return candidates.Length > 0;
        }, "투표 가능한 실제 노드 UI", 90);
        split = split && candidates.Length >= 2;
        splitVoteCast = split;
        if (splitVoteProbe && !split)
            Debug.Log($"[MirrorVoteSmoke] advance single-choice floor={candidates[0].NodeData.floor}; split NOT tested yet");
        var selected = candidates[split && !manager.ClientIsSessionLeader ? 1 : 0];
        string votedNode = selected.NodeData.id;
        expectedSelectedNodeId = candidates[split ? 1 : 0].NodeData.id;
        // 원본 UI → SW 어댑터 → RequestStageNodeSelection → 서버 투표를 실제로 통과한다.
        selected.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        yield return WaitFor(() => manager.ClientStageVotes.OwnNodeId == votedNode ||
                                  manager.TryGetPendingStageNode(out StageNodeSaveData pending) && pending.id == expectedSelectedNodeId,
            "원본 UI의 노드 투표 서버 수신", 10);
        Debug.Log($"[MirrorVoteSmoke] PASS ui-vote role={role} leader={manager.ClientIsSessionLeader} voted={votedNode} expected={expectedSelectedNodeId} split={split}");
    }

    private IEnumerator ValidateFirstNode()
    {
        yield return WaitFor(() => manager.IsSessionSelectionActive && manager.TryGetRunSnapshot(out StageMapSaveData snapshot) &&
                                  snapshot.nodes.Count > 0, "first-node 지도", 90);
        if (role == "server")
        {
            manager.TryGetRunSnapshot(out StageMapSaveData snapshot);
            var nodes = snapshot.nodes.Where(node => node.floor == snapshot.clearedFloor + 1)
                .OrderBy(node => node.id, StringComparer.Ordinal).ToArray();
            Require(nodes.Length > 0, "서버 첫 층 선택지 존재");
            expectedSelectedNodeId = nodes[0].id;
        }
        else
        {
            yield return WaitFor(() => manager.LocalPlayerContext?.RuntimeState.HasSnapshot == true, "first-node 로컬 캐릭터", 90);
            yield return VoteForNode(false);
        }
        yield return WaitFor(() => !manager.IsSessionSelectionActive && manager.TryGetPendingStageNode(out StageNodeSaveData pending) &&
                                  UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == ExpectedScene(pending),
            "first-node 실제 씬 이동", 90);
        manager.TryGetPendingStageNode(out StageNodeSaveData result);
        Require(result.id == expectedSelectedNodeId, "first-node 최다 득표 노드 확정");
        if (role != "server" && result.type != StageNodeType.Event)
            yield return WaitFor(() => manager.LocalPlayerContext?.Controller.IsControlEnabled == true &&
                                      manager.LocalPlayerContext.GetComponent<MirrorSpawnedPlayerBinder>().IsSceneStartConfirmed &&
                                      manager.LocalPlayerContext.Controller.agent.enabled && manager.LocalPlayerContext.Controller.agent.isOnNavMesh,
                "first-node Controller/NavMesh 복구", 30);
        firstNodeValidationPassed = true;
        Debug.Log($"[MirrorVoteSmoke] PASS first-node role={role} split={splitVoteProbe} winner={result.id} path={ExpectedScene(result)}");
    }

    private static string ExpectedScene(StageNodeSaveData node)
    {
        return "Assets/SW/TEST/MirrorCombat/Scenes/" + node.sceneName + "_MirrorSessionTest.unity";
    }

    private IEnumerator RideStage5Elevator(PlayerContext owner)
    {
        var elevator = FindFirstObjectByType<MirrorFourPlayerElevator_MirrorTest>();
        Require(elevator != null && elevator.BoardingTrigger is BoxCollider, "Stage5 실제 승강기 탑승 BoxCollider");
        Require(manager.ClientLobby.Members.Count(member => member.IsConnected && !member.HasForfeited) == expectedMembers,
            "Stage5 탑승 전 원래 참가자 전원 연결");
        if (NetworkServer.active)
            Require(manager.ServerPlayerContexts.Count(player => player.GetComponent<NetworkIdentity>().connectionToClient != null && !player.RuntimeState.IsDead) == expectedMembers,
                "Stage5 서버 탑승 대상 전원 생존/연결");
        float startHeight = owner.transform.position.y;
        var boardingBox = (BoxCollider)elevator.BoardingTrigger;
        Vector3 boarding = boardingBox.transform.TransformPoint(boardingBox.center);
        Debug.Log($"[MirrorRunSmoke] stage5-board netId={owner.GetComponent<NetworkIdentity>().netId} target={boarding} triggerEnabled={boardingBox.enabled} connected={expectedMembers}");
        boarding.y = startHeight;
        Require(UnityEngine.AI.NavMesh.SamplePosition(boarding, out UnityEngine.AI.NavMeshHit hit, 2,
            owner.Controller.agent.areaMask), "Stage5 탑승 NavMesh");
        var path = new UnityEngine.AI.NavMeshPath();
        Require(owner.Controller.agent.CalculatePath(hit.position, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete,
            "Stage5 탑승점까지 실제 이동 경로");
        owner.Controller.MoveCommand(hit.position);
        yield return WaitFor(() => !owner.Controller.IsControlEnabled, "Stage5 전원 탑승 후 조작 잠금", 60);
        yield return WaitFor(() => owner.transform.position.y > startHeight + 3 && owner.Controller.IsControlEnabled &&
                                  owner.Controller.agent.enabled && owner.Controller.agent.isOnNavMesh,
            "Stage5 상승 후 위층 NavMesh/조작 복구", 60);
        yield return new WaitForFixedUpdate();
        Require(manager.ClientLobby.Members.Count(member => member.IsConnected && !member.HasForfeited) == expectedMembers,
            "Stage5 상승 완료까지 원래 참가자 전원 연결");
        Debug.Log($"[MirrorRunSmoke] PASS stage5-elevator netId={owner.GetComponent<NetworkIdentity>().netId} fromY={startHeight} toY={owner.transform.position.y} connected={expectedMembers}");
    }

    private static void TMPReadyCheck()
    {
        KY_MultiplayerLobbyController lobby = FindFirstObjectByType<KY_MultiplayerLobbyController>(FindObjectsInactive.Include);
        Require(lobby != null, "실제 KY 로비 복귀");
        Button ready = lobby.GetComponentsInChildren<Button>(true)
            .Single(button => button.transform.parent != null && button.transform.parent.name == "ReadyButton");
        TMPro.TMP_Text label = ready.transform.parent.GetComponentsInChildren<TMPro.TMP_Text>(true).Single();
        Require(label.text == "READY", "READY 표시·준비 버튼 참조");
    }

    private static void CaptureRunScreen(string suffix)
    {
        MirrorTestPlayerHud diagnostics = FindFirstObjectByType<MirrorTestPlayerHud>();
        if (diagnostics != null) diagnostics.enabled = false;
        string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "RunValidation"));
        System.IO.Directory.CreateDirectory(directory);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(directory, MirrorReconnectProfile_MirrorTest.GetProfileName() + "_" + suffix + ".png"));
    }

    private static void Finish(bool passed)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit(passed ? 0 : 2);
#endif
    }

    private void HandleInventoryClientStep(InventorySmokeStep step)
    {
        if (step.Phase == 0) inventoryTurn = true;
        else if (step.Phase == 2) contention = step;
        else if (step.Phase == 3)
        {
            inventoryValidationPassed = true;
            Debug.Log($"[MirrorInventorySmoke] PASS server-confirmed role={role} serialized owners + one shared-item winner");
        }
    }

    private void HandleInventoryServerStep(NetworkConnectionToClient connection, InventorySmokeStep step)
    {
        try
        {
            var member = manager.ServerRoster.FindByConnection(connection.connectionId);
            Require(member?.RuntimeContext != null && manager.CurrentSessionRoute == MirrorSessionRoute.Camp,
                "인벤토리 검사 요청의 실제 접속 소유자와 Camp");
            PlayerContext owner = member.RuntimeContext;
            if (step.Phase == 0)
                inventoryReadySlots.Add(member.Slot);
            else if (step.Phase == 4)
            {
                Require(inventoryTurnSent && member.Slot == 0 && inventorySlot == 0 && contentionItemId == null,
                    "경쟁 재고 후보는 첫 슬롯의 실제 판매 직전에 한 번 확인");
                InventoryItem funding = Owned(owner, step.ItemId);
                Require(funding?.itemData?.definition != null, "판매할 경쟁 아이템의 서버 소유 데이터");
                contentionItemId = funding.itemData.instanceId;
                contentionPrice = Mathf.Max(0, funding.itemData.definition.sellPrice);
                contentionBudget = Mathf.Max(1, contentionPrice);
                Debug.Log($"[MirrorInventorySmoke] contention-price item={contentionItemId} serverItemPrice={contentionPrice} budget={contentionBudget}");
            }
            else if (step.Phase == 1)
            {
                Require(inventoryTurnSent && member.Slot == inventorySlot, "인벤토리 완료 슬롯 순서");
                var retained = owner.Inventory.GetAllInventoryItems();
                Require(retained.Count == 1 && retained[0].itemData.upgradeLevel == 1 &&
                        !string.IsNullOrEmpty(step.ItemId) && retained[0].itemData.instanceId != step.ItemId,
                    "개인 검사 최종 서버 소유 아이템 1개·강화 1회");
                inventoryRetainedIds.Add(member.Slot, retained[0].itemData.instanceId);
                Require(contentionItemId != null && (member.Slot != 0 || step.ItemId == contentionItemId),
                    "경쟁 후보와 실제 판매한 funding 아이템 일치");
                Debug.Log($"[MirrorInventorySmoke] PASS server-owner slot={member.Slot} item={retained[0].itemData.instanceId} gold={owner.Wallet.Gold}");
                inventorySlot++;
                inventoryTurnSent = false;
                if (inventorySlot == expectedMembers)
                {
                    var shop = owner.GetComponent<NetworkShopPlayerState_MirrorTest>().ResolveShopState();
                    Require(shop != null && shop.PlayerSoldStockCount == expectedMembers, "전원 개인 검사 후 판매 재고 수");
                    contentionShopRevision = shop.StateRevision;
                    foreach (var player in manager.ServerRoster.Members)
                    {
                        player.RuntimeContext.GetComponent<NetworkShopPlayerState_MirrorTest>().ServerSetGold(contentionBudget);
                        contentionInventoryRevisions[player.Slot] = player.RuntimeContext.GetComponent<PlayerInventorySync_MirrorTest>().StateRevision;
                    }
                    NetworkServer.SendToAll(new InventorySmokeStep { Phase = 2, ItemId = contentionItemId, Budget = contentionBudget, At = NetworkTime.time + 5 });
                }
            }
            else if (step.Phase == 2)
            {
                Require(inventorySlot == expectedMembers && step.ItemId == contentionItemId && !contentionResults.ContainsKey(member.Slot),
                    "동일 재고 경쟁 결과 중복 없음");
                Require(step.Result == MirrorTestShopRequestResult.Success || step.Result == MirrorTestShopRequestResult.ShopStateChanged ||
                        step.Result == MirrorTestShopRequestResult.ItemUnavailable, "동일 재고 경쟁의 성공 또는 정상 충돌 거절");
                contentionResults.Add(member.Slot, step.Result);
                if (contentionResults.Count == expectedMembers)
                {
                    Require(contentionResults.Values.Count(result => result == MirrorTestShopRequestResult.Success) == 1,
                        "공유 아이템 구매 성공 정확히 1명");
                    int owners = 0;
                    foreach (var player in manager.ServerRoster.Members)
                    {
                        PlayerContext context = player.RuntimeContext;
                        var wallet = context.GetComponent<NetworkShopPlayerState_MirrorTest>();
                        bool won = contentionResults[player.Slot] == MirrorTestShopRequestResult.Success;
                        bool owns = Owned(context, contentionItemId) != null;
                        if (owns) owners++;
                        int price = Mathf.Max(0, Mathf.CeilToInt(Mathf.Max(0, contentionPrice) * (1 - Mathf.Clamp(wallet.DiscountPercent, 0, 0.95f))));
                        Require(owns == won && Owned(context, inventoryRetainedIds[player.Slot])?.itemData.upgradeLevel == 1 &&
                                context.Inventory.GetAllInventoryItems().Count == (won ? 2 : 1) && wallet.Gold == contentionBudget - (won ? price : 0) &&
                                context.GetComponent<PlayerInventorySync_MirrorTest>().StateRevision == contentionInventoryRevisions[player.Slot] + (won ? 1u : 0u),
                            "경쟁 후 서버 소유권·기존 아이템·골드·개인 revision 보존");
                    }
                    Require(owners == 1 && owner.GetComponent<NetworkShopPlayerState_MirrorTest>().ResolveShopState().StateRevision == contentionShopRevision + 1,
                        "공유 소유자 1명·공유 revision 1회");
                    inventoryValidationPassed = true;
                    NetworkServer.SendToAll(new InventorySmokeStep { Phase = 3 });
                    Debug.Log($"[MirrorInventorySmoke] PASS contention-server item={contentionItemId} owners=1 successes=1 participants={expectedMembers}");
                }
            }
            if (!inventoryTurnSent && inventorySlot < expectedMembers && inventoryReadySlots.Contains(inventorySlot))
            {
                var next = manager.ServerRoster.Members.First(player => player.Slot == inventorySlot);
                inventoryTurnSent = true;
                NetworkServer.connections[next.ConnectionId].Send(new InventorySmokeStep { Phase = 0 });
            }
        }
        catch (Exception error)
        {
            Debug.LogError("[MirrorInventorySmoke] FAIL " + error);
            enabled = false;
            Finish(false);
        }
    }

    private IEnumerator ValidateInventoryTurn()
    {
        NetworkClient.Send(new InventorySmokeStep { Phase = 0 });
        yield return WaitFor(() => inventoryTurn, "서버 인벤토리 슬롯 순번", 480);
        yield return ValidateInventoryInput();
    }

    private IEnumerator ValidateInventoryInput()
    {
        PlayerContext owner = manager.LocalPlayerContext;
        PlayerInventorySync_MirrorTest sync = owner.GetComponent<PlayerInventorySync_MirrorTest>();
        NetworkShopPlayerState_MirrorTest wallet = owner.GetComponent<NetworkShopPlayerState_MirrorTest>();
        InventoryGrid grid = owner.Inventory.PlayerGrid;
        var originalEquipment = owner.Equipment.GetEquippedItems()
            .Select(pair => new { Slot = pair.Key, Id = pair.Value.itemData.instanceId }).ToArray();
        yield return WaitFor(() => owner.GetComponent<MirrorSpawnedPlayerBinder>().IsSceneStartConfirmed &&
            owner.Controller.IsControlEnabled && owner.Controller.agent.enabled && owner.Controller.agent.isOnNavMesh,
            "노드 선택 후 Camp 시작점과 이동 입력 복구");
        Vector3 beforeMove = owner.transform.position;
        Vector3 destination = beforeMove;
        var path = new UnityEngine.AI.NavMeshPath();
        foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            if (!UnityEngine.AI.NavMesh.SamplePosition(beforeMove + direction, out UnityEngine.AI.NavMeshHit hit, 0.3f,
                    owner.Controller.agent.areaMask) || Vector3.Distance(beforeMove, hit.position) < 0.6f ||
                !owner.Controller.agent.CalculatePath(hit.position, path) || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) continue;
            destination = hit.position;
            break;
        }
        Require(destination != beforeMove, "Camp의 도달 가능한 이동 목적지");
        owner.Controller.MoveCommand(destination);
        yield return WaitFor(() => Vector3.Distance(beforeMove, owner.transform.position) > 0.4f, "실제 Controller 이동");
        owner.Controller.StopMovement();
        InventoryPartView windows = FindFirstObjectByType<InventoryPartView>(FindObjectsInactive.Include);
        windows.OpenShop();
        yield return new WaitForSecondsRealtime(1);
        Require(grid.GetAllItems().Count == 0, "새 테스트 인벤토리는 비어 있어야 합니다.");
        for (int index = 1; index <= 2; index++)
        {
            Require(sync.RequestGrantDistinctTestItem(), "개발 지급 요청 시작");
            int expected = index;
            yield return WaitFor(() => sync.PendingRequestCount == 0 && grid.GetAllItems().Count == expected, "서버 지급");
        }

        string firstId = grid.GetAllItems()[0].itemData.instanceId;
        string secondId = grid.GetAllItems()[1].itemData.instanceId;
        InventoryItem first = Owned(owner, firstId);
        InventoryItem second = Owned(owner, secondId);
        ItemUI ui = ItemUIFinder.FindInGrid(grid, first);
        Require(ui != null && ui.HasExternalInput, "실제 Mirror Item UI 입력 연결");

        // 실제 OnBegin/OnEndDrag 경로에서 Host도 승인 전 모델이 그대로인지 확인한다.
        uint revision = sync.StateRevision;
        DragTo(ui, grid, new Vector2Int(0, 3), true);
        Require(first.x == 0 && first.y == 0 && !first.isRotated && grid.ContainsItem(first), "회전 드래그 전 소유 모델 보존");
        yield return WaitFor(() => sync.StateRevision > revision && sync.PendingRequestCount == 0, "회전 이동 승인");
        first = Owned(owner, firstId);
        Require(first.x == 0 && first.y == 3 && first.isRotated, "승인된 회전 이동");

        // 같은 방향으로 돌린 다음 상대 위치에 놓아 두 OP_SET의 교환을 검사한다.
        revision = sync.StateRevision;
        Vector2Int swapCell = new(second.x, second.y);
        DragTo(ItemUIFinder.FindInGrid(grid, first), grid, swapCell, true);
        yield return WaitFor(() => sync.StateRevision > revision && sync.PendingRequestCount == 0, "아이템 교환 승인");
        first = Owned(owner, firstId);
        second = Owned(owner, secondId);
        Require(first != null && second != null && grid.GetAllItems().Count == 2 &&
                grid.ContainsItem(first) && grid.ContainsItem(second) && first.x == swapCell.x && first.y == swapCell.y &&
                second.x == 0 && second.y == grid.GridHeight - second.CurrentHeight,
            $"교환 후 위치·소유권: first=({first?.x},{first?.y}), second=({second?.x},{second?.y}); 아래 가장자리 보존");

        revision = sync.StateRevision;
        MirrorTestInventoryRequestResult? rejected = null;
        void RecordRejection(MirrorTestInventoryRequestCompleted result) => rejected = result.Result;
        sync.RequestCompleted += RecordRejection;
        try
        {
            Require(sync.TryRequestEquipmentChange(firstId, true, EquipSlotType.Weapon, -1, -1, false, out _), "잘못된 장비 슬롯 요청");
            yield return WaitFor(() => rejected.HasValue && sync.PendingRequestCount == 0, "장비 슬롯 거절 응답");
            Require(rejected != MirrorTestInventoryRequestResult.Success && sync.StateRevision == revision &&
                    grid.ContainsItem(first) && !first.isEquipped, "거절 후 원본 모델·상태 번호 보존");
        }
        finally { sync.RequestCompleted -= RecordRejection; }

        // 우클릭 장착은 입력 프레임에는 소유 상태를 바꾸지 않는다.
        revision = sync.StateRevision;
        ui = ItemUIFinder.FindInGrid(grid, first);
        ui.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right });
        Require(grid.ContainsItem(first) && !first.isEquipped, "장착 승인 전 원본 보존");
        yield return WaitFor(() => sync.StateRevision > revision && sync.PendingRequestCount == 0, "장착 승인");
        first = Owned(owner, firstId);
        Require(first != null && first.isEquipped && !grid.ContainsItem(first), "서버 장착 상태");
        yield return null;
        ui = FindObjectsByType<ItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(view => view.Item == first && view.CurrentEquipSlot != null);
        Require(ui != null, "장비 슬롯 UI");
        revision = sync.StateRevision;
        ui.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right });
        Require(first.isEquipped, "해제 승인 전 원본 보존");
        yield return WaitFor(() => sync.StateRevision > revision && sync.PendingRequestCount == 0, "해제 승인");
        first = Owned(owner, firstId);
        Require(first != null && !first.isEquipped && grid.ContainsItem(first), "서버 해제 상태");

        // 장착 검사는 기존 기본 투구와 교환한다. 원래 장비를 삭제하지 않고 다시 장착한다.
        foreach (var original in originalEquipment)
        {
            if (owner.Equipment.TryGetEquippedItem(original.Slot, out var equipped) &&
                equipped.itemData.instanceId == original.Id) continue;
            revision = sync.StateRevision;
            Require(sync.TryRequestEquipmentChange(original.Id, true, original.Slot, -1, -1, false, out _),
                "검사 전 기본 장비 재장착 요청");
            yield return WaitFor(() => sync.StateRevision > revision && sync.PendingRequestCount == 0 &&
                owner.Equipment.TryGetEquippedItem(original.Slot, out var restored) &&
                restored.itemData.instanceId == original.Id, "기본 장비 인스턴스와 장착 상태 보존");
        }

        ShopController shop = ShopController.Instance;
        Require(shop != null && shop.BoundPlayer == owner.Inventory, "동일 플레이어 상점 연결");
        Require(shop.TryResolveSellPosition(first, 0, 0, out InventoryPlacementSnapshot sale), "공유 상점 판매 공간");
        int goldBefore = wallet.Gold;
        int price = first.itemData.definition.sellPrice;
        revision = sync.StateRevision;
        DragTo(ItemUIFinder.FindInGrid(grid, first), shop.ShopGrid, new Vector2Int(sale.Rect.X, sale.Rect.Y), sale.IsRotated != first.isRotated);
        Require(grid.ContainsItem(first) && wallet.Gold == goldBefore, "판매 승인 전 아이템·골드 보존");
        yield return WaitFor(() => sync.StateRevision > revision && wallet.PendingRequestCount == 0, "서버 판매 승인");
        Require(Owned(owner, firstId) == null && wallet.Gold == goldBefore + price, "판매 대금·소유권");
        yield return WaitFor(() => shop.ShopGrid.GetAllItems().Any(item => item.itemData.instanceId == firstId), "판매 재고 UI");
        InventoryItem stock = shop.ShopGrid.GetAllItems().First(item => item.itemData.instanceId == firstId);
        ui = ItemUIFinder.FindInGrid(shop.ShopGrid, stock);
        Require(ui != null, "판매된 재고 아이템 UI");
        revision = sync.StateRevision;
        ui.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right });
        Require(Owned(owner, firstId) == null && wallet.Gold == goldBefore + price, "구매 승인 전 소유권·골드 보존");
        yield return WaitFor(() => sync.StateRevision > revision && wallet.PendingRequestCount == 0, "서버 재구매 승인");
        int repurchasePrice = Mathf.Max(0, Mathf.CeilToInt(Mathf.Max(0, price) * (1f - Mathf.Clamp(wallet.DiscountPercent, 0f, 0.95f))));
        Require(Owned(owner, firstId) != null && wallet.Gold == goldBefore + price - repurchasePrice, "개인 할인 재구매 단일 소유권·대금");

        revision = sync.StateRevision;
        Require(sync.TryRequestRemoveInventoryItem(secondId, out _), "서버 삭제 요청 시작");
        yield return WaitFor(() => sync.StateRevision > revision && sync.PendingRequestCount == 0, "서버 삭제 승인");
        Require(Owned(owner, secondId) == null && Owned(owner, firstId) != null && grid.GetAllItems().Count == 1, "선택 아이템만 삭제");

        // 표시가 새 스냅샷으로 바뀌는 프레임의 취소는 원본 아이템을 재생성하거나 이동시키지 않는다.
        first = Owned(owner, firstId);
        InventoryPlacementSnapshot beforeCancel = InventoryPlacementSnapshot.Capture(grid, first);
        ui = ItemUIFinder.FindInGrid(grid, first);
        var interrupted = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, ui.Rect.position) };
        ItemDragHandler interruptedDrag = ui.GetComponent<ItemDragHandler>();
        interruptedDrag.OnBeginDrag(interrupted);
        Require(ui.IsDragPreviewActive && sync.RequestGrantDistinctTestItem(), "드래그 중 서버 상태 변경 요청");
        interruptedDrag.OnEndDrag(interrupted);
        yield return WaitFor(() => sync.PendingRequestCount == 0 && grid.GetAllItems().Count == 2, "새 스냅샷과 취소 복구");
        first = Owned(owner, firstId);
        Require(first.x == beforeCancel.Rect.X && first.y == beforeCancel.Rect.Y && first.isRotated == beforeCancel.IsRotated,
            "중단 드래그 원본 위치 보존");

        InventoryItem funding = grid.GetAllItems().First(item => item.itemData.instanceId != firstId);
        Require(shop.TryResolveSellPosition(funding, 0, 0, out sale), "강화 자금용 판매 공간");
        if (manager.ClientLobby.Members.Single(member => member.ParticipantId == manager.LocalParticipantId).Slot == 0)
            NetworkClient.Send(new InventorySmokeStep { Phase = 4, ItemId = funding.itemData.instanceId });
        revision = sync.StateRevision;
        DragTo(ItemUIFinder.FindInGrid(grid, funding), shop.ShopGrid, new Vector2Int(sale.Rect.X, sale.Rect.Y), sale.IsRotated != funding.isRotated);
        yield return WaitFor(() => sync.StateRevision > revision && wallet.PendingRequestCount == 0, "강화 자금 판매");

        windows.OpenUpgrade();
        yield return new WaitForSecondsRealtime(0.5f);
        UpgradeController upgrade = FindFirstObjectByType<UpgradeController>();
        UpgradeDropSlot upgradeSlot = FindFirstObjectByType<UpgradeDropSlot>();
        Require(upgrade != null && upgradeSlot != null, "실제 강화 창 연결");
        first = Owned(owner, firstId);
        DragTo(ItemUIFinder.FindInGrid(grid, first), null, default, false, upgradeSlot.gameObject);
        yield return WaitFor(() => upgrade.SelectedItem?.instanceId == firstId, "드래그 강화 아이템 선택");
        Button upgradeButton = FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(button =>
            Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(index =>
                button.onClick.GetPersistentTarget(index) == upgrade && button.onClick.GetPersistentMethodName(index) == nameof(UpgradeController.TryUpgrade)));
        Require(upgradeButton != null && upgradeButton.interactable, "강화 버튼 연결");
        Require(UpgradeService.TryGetUpgradeCost(first.itemData, out int cost), "강화 비용");
        int goldBeforeUpgrade = wallet.Gold;
        int previousLevel = first.itemData.upgradeLevel;
        revision = sync.StateRevision;
        upgradeButton.onClick.Invoke();
        yield return WaitFor(() => sync.StateRevision > revision && sync.PendingRequestCount == 0, "강화 승인");
        first = Owned(owner, firstId);
        Require(first.itemData.upgradeLevel == previousLevel + 1 && wallet.Gold == goldBeforeUpgrade - cost, "강화 1회·비용 1회 적용");

        revision = sync.StateRevision;
        DragTo(ItemUIFinder.FindInGrid(grid, first), null, default, false);
        yield return WaitFor(() => sync.StateRevision > revision && sync.PendingRequestCount == 0 && Owned(owner, firstId) == null, "월드 드롭 승인");
        NetworkWorldItem_MirrorTest dropped = null;
        yield return WaitFor(() => (dropped = FindObjectsByType<NetworkWorldItem_MirrorTest>(FindObjectsSortMode.None)
            .FirstOrDefault(item => item.CreateItemInstance()?.instanceId == firstId)) != null, "같은 instance 월드 아이템");
        yield return new WaitForFixedUpdate();
        Ray pickupRay = default;
        string pickupBlocker = null;
        if (!TryFindPickupRay(dropped, out pickupRay, out pickupBlocker))
            Debug.Log($"[MirrorInventorySmoke] pickup-ray waiting netId={dropped.netId} blocker={pickupBlocker} pending={sync.PendingRequestCount}");
        yield return WaitFor(() => TryFindPickupRay(dropped, out pickupRay, out pickupBlocker),
            "월드 아이템을 첫 충돌로 맞히는 실제 Raycast", 5);
        revision = sync.StateRevision;
        Debug.Log($"[MirrorInventorySmoke] pickup-ray netId={dropped.netId} origin={pickupRay.origin} direction={pickupRay.direction} pending={sync.PendingRequestCount}");
        Require(sync.TryRequestPickup(pickupRay), "실제 Raycast 획득 요청");
        yield return WaitFor(() => sync.StateRevision > revision && sync.PendingRequestCount == 0 && Owned(owner, firstId) != null, "월드 재획득 승인");
        Require(Owned(owner, firstId).itemData.upgradeLevel == previousLevel + 1 && grid.GetAllItems().Count == 1 &&
                wallet.Gold == goldBeforeUpgrade - cost, "드롭·획득 후 강화·소유권·골드 보존");
        Debug.Log($"[MirrorInventorySmoke] PASS role={role} netId={sync.netId} camp/move/rotate/swap/reject/equip/unequip/sell/buy/remove/cancel/upgrade/drop/pickup; retained={firstId}; gold={wallet.Gold}; upgrade={previousLevel + 1}");
        NetworkClient.Send(new InventorySmokeStep { Phase = 1, ItemId = funding.itemData.instanceId });
        yield return WaitFor(() => contention.Phase == 2, "전원 개인 검사 완료 후 공유 재고 경쟁", 480);
        windows.OpenShop();
        InventoryItem contested = null;
        yield return WaitFor(() => wallet.Gold == contention.Budget && (contested = shop.ShopGrid.GetAllItems()
            .FirstOrDefault(item => item.itemData.instanceId == contention.ItemId)) != null, "공유 경쟁 재고·골드 동기화");
        Require(NetworkTime.time < contention.At, "공유 경쟁 시작 시각 전 준비");
        int targetY = grid.GridHeight - contested.CurrentHeight;
        Require(grid.CanPlaceItem(0, targetY, contested.CurrentWidth, contested.CurrentHeight), "공유 구매의 빈 목적지");
        int buyPrice = Mathf.Max(0, Mathf.CeilToInt(Mathf.Max(0, contested.itemData.definition.sellPrice) * (1 - Mathf.Clamp(wallet.DiscountPercent, 0, 0.95f))));
        uint beforeBuyRevision = sync.StateRevision;
        MirrorTestShopRequestCompleted? completion = null;
        void RecordBuy(MirrorTestShopRequestCompleted result) { if (result.Operation == MirrorTestShopOperation.Buy) completion = result; }
        wallet.RequestCompleted += RecordBuy;
        try
        {
            yield return WaitFor(() => NetworkTime.time >= contention.At, "공유 구매 시작", 10);
            Require(wallet.TryRequestBuy(contention.ItemId, 0, targetY, contested.isRotated, out uint requestId), "동일 instance 전원 구매 요청");
            yield return WaitFor(() => completion.HasValue && completion.Value.RequestId == requestId && wallet.PendingRequestCount == 0,
                "공유 구매 성공/거절 응답");
            Require(completion.Value.Result == MirrorTestShopRequestResult.Success ||
                    completion.Value.Result == MirrorTestShopRequestResult.ShopStateChanged ||
                    completion.Value.Result == MirrorTestShopRequestResult.ItemUnavailable,
                "공유 구매의 성공 또는 정상 충돌 거절");
            bool won = completion.Value.Result == MirrorTestShopRequestResult.Success;
            Require((Owned(owner, contention.ItemId) != null) == won && Owned(owner, firstId)?.itemData.upgradeLevel == previousLevel + 1 &&
                    grid.GetAllItems().Count == (won ? 2 : 1) && wallet.Gold == contention.Budget - (won ? buyPrice : 0) &&
                    sync.StateRevision == beforeBuyRevision + (won ? 1u : 0u), "공유 구매 후 로컬 소유·골드·revision");
            Debug.Log($"[MirrorInventorySmoke] PASS contention-local role={role} netId={sync.netId} result={completion.Value.Result} item={contention.ItemId} gold={wallet.Gold}");
            NetworkClient.Send(new InventorySmokeStep { Phase = 2, ItemId = contention.ItemId, Result = completion.Value.Result });
            yield return WaitFor(() => inventoryValidationPassed, "공유 재고 서버 전수 검증", 30);
        }
        finally { wallet.RequestCompleted -= RecordBuy; }
    }

    internal static bool TryFindPickupRay(NetworkWorldItem_MirrorTest target, out Ray ray, out string blocker)
    {
        ray = default;
        blocker = "no enabled collider";
        foreach (Collider collider in target.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled) continue;
            foreach (Vector3 direction in new[] { Vector3.up, Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                Ray candidate = new(collider.bounds.center + direction * 2, -direction);
                if (!Physics.Raycast(candidate, out RaycastHit hit, 500, ~0, QueryTriggerInteraction.Collide)) continue;
                if (hit.collider.GetComponentInParent<NetworkWorldItem_MirrorTest>() != target)
                {
                    blocker = hit.collider.name;
                    continue;
                }
                // 다른 참가자의 Collider가 수직 Ray를 가리면 실제로 노출된 측면을 클릭한다.
                ray = candidate;
                return true;
            }
        }
        return false;
    }

    private static InventoryItem Owned(PlayerContext owner, string id)
    {
        return owner.Inventory.GetAllInventoryItems().Concat(owner.Equipment.GetEquippedItems().Select(pair => pair.Value))
            .FirstOrDefault(item => item.itemData.instanceId == id);
    }

    private static void DragTo(ItemUI ui, InventoryGrid targetGrid, Vector2Int cell, bool rotate, GameObject dropTarget = null)
    {
        Require(ui != null && ui.isActiveAndEnabled, "드래그 대상 UI 활성");
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, ui.Rect.position),
            pointerCurrentRaycast = new RaycastResult { gameObject = dropTarget != null ? dropTarget : targetGrid?.GridRect.gameObject }
        };
        ItemDragHandler drag = ui.GetComponent<ItemDragHandler>();
        drag.OnBeginDrag(pointer);
        Require(ui.IsDragPreviewActive, "외부 드래그 미리보기 시작");
        if (rotate) ui.RotateDraggingItem();
        if (targetGrid != null)
        {
            ui.Rect.position = targetGrid.ItemsContainer.TransformPoint(new Vector3(cell.x * targetGrid.Step, -cell.y * targetGrid.Step));
            pointer.position = RectTransformUtility.WorldToScreenPoint(null,
                ui.Rect.position + new Vector3(ui.SizeDelta.x * 0.1f, -ui.SizeDelta.y * 0.1f));
        }
        else pointer.position = new Vector2(-100, -100);
        drag.OnEndDrag(pointer);
        Require(!ui.IsDragPreviewActive, "드래그 미리보기 종료");
    }

    private static IEnumerator WaitFor(Func<bool> predicate, string step, double timeout = 12)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + timeout;
        do
        {
            yield return null;
            if (predicate()) yield break;
        } while (Time.realtimeSinceStartupAsDouble < deadline);
        throw new InvalidOperationException("[MirrorSmoke] TIMEOUT " + step);
    }

    private IEnumerator Guard(IEnumerator routine)
    {
        // Unity가 중첩 IEnumerator 예외를 별도로 삼키기 전에 실패 종료를 보장한다.
        var stack = new System.Collections.Generic.Stack<IEnumerator>();
        stack.Push(routine);
        while (stack.Count > 0)
        {
            object current = null;
            Exception failure = null;
            try
            {
                if (!stack.Peek().MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                current = stack.Peek().Current;
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
            }
            catch (Exception error)
            {
                failure = error;
            }
            if (failure != null)
            {
                Debug.LogError($"[MirrorSmoke] FAIL role={role} {failure}");
                enabled = false;
                Finish(false);
                yield break;
            }
            yield return current;
        }
    }

    private static void Require(bool condition, string step)
    {
        if (!condition) throw new InvalidOperationException("[MirrorSmoke] FAIL " + step);
    }

    private void ReportAdmission(string reason)
    {
        wasAdmitted |= reason == "참가 승인 완료";
        sawExpectedRejection |= expectedRejection != null && reason == expectedRejection;
        Debug.Log("[MirrorSmoke] admission=" + reason);
    }

    private void ReportState()
    {
        int networkLoops = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop().subSystemList
            .SelectMany(system => system.subSystemList ?? Array.Empty<UnityEngine.LowLevel.PlayerLoopSystem>())
            .Count(system => system.type == typeof(NetworkLoop));
        Debug.Log($"[MirrorSmoke] role={role} connected={NetworkClient.isConnected} admitted={manager.ClientCompatibilityConfirmed} " +
            $"run={manager.ClientLobby.RunStarted} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
            $"mode={manager.mode} clientActive={NetworkClient.active} serverActive={NetworkServer.active} " +
            $"transport={Transport.active?.GetType().Name} transportEnabled={Transport.active?.enabled} networkLoops={networkLoops} status={manager.CompatibilityStatusMessage}");
        PlayerContext local = manager.LocalPlayerContext;
        if (local != null)
        {
            UnityEngine.AI.NavMeshAgent agent = local.GetComponent<UnityEngine.AI.NavMeshAgent>();
            bool sampled = UnityEngine.AI.NavMesh.SamplePosition(local.transform.position, out UnityEngine.AI.NavMeshHit navHit, 4, UnityEngine.AI.NavMesh.AllAreas);
            Debug.Log($"[MirrorSmoke] localNetId={local.GetComponent<NetworkIdentity>().netId} " +
                $"snapshot={local.RuntimeState.HasSnapshot} absent={local.GetComponent<MirrorSpawnedPlayerBinder>().IsTemporarilyAbsent} " +
                $"controller={local.Controller.enabled} control={local.Controller.IsControlEnabled} sceneStartConfirmed={local.GetComponent<MirrorSpawnedPlayerBinder>().IsSceneStartConfirmed} " +
                $"agent={agent != null && agent.enabled} onNavMesh={agent != null && agent.enabled && agent.isOnNavMesh} inventoryView={local.Inventory.PlayerGrid.HasView} " +
                $"position={local.transform.position} agentType={agent?.agentTypeID} navSample={sampled} hit={navHit.position} " +
                $"controllerAgentMatches={local.Controller.agent == agent}");
            Debug.Log($"[MirrorSmoke] localGold={local.Wallet.Gold} localHP={local.Health.CurrentHealth}/{local.Health.MaxHealth} " +
                $"localMP={local.Mana.CurrentMana}/{local.Mana.MaxMana} " +
                $"localItems={string.Join(",", local.Inventory.GetAllInventoryItems().Select(item => item.itemData.instanceId))} " +
                $"localUpgrades={string.Join(",", local.Inventory.GetAllInventoryItems().Select(item => item.itemData.upgradeLevel))}");
        }
        if (Argument("--mirror-smoke-nameplates") == "true" && local != null)
        {
            var nameplates = FindObjectsByType<PlayerNameplate_MirrorTest>(FindObjectsSortMode.None);
            var members = manager.ClientLobby.Members ?? Array.Empty<MirrorLobbyMember_MirrorTest>();
            bool matches = nameplates.Length == expectedMembers && nameplates.All(view =>
                view.BoundPlayer != null && members.Any(member =>
                    member.Slot == view.BoundPlayer.ParticipantSlot && member.DisplayName == view.BoundPlayer.ParticipantDisplayName) &&
                view.DisplayedName == $"P{view.BoundPlayer.ParticipantSlot + 1} {view.BoundPlayer.ParticipantDisplayName}");
            Debug.Log($"[MirrorNameplateSmoke] matches={matches} count={nameplates.Length} names={string.Join(",", nameplates.Select(view => view.DisplayedName))}");
        }
        if (!NetworkServer.active) return;
        foreach (MirrorSessionRoster_MirrorTest.Member member in manager.ServerRoster.Members)
        {
            PlayerContext context = member.RuntimeContext;
            uint netId = context != null ? context.GetComponent<NetworkIdentity>().netId : 0;
            string itemIds = context != null
                ? string.Join(",", context.Inventory.GetAllInventoryItems().Select(item => item.itemData.instanceId)) : string.Empty;
            Debug.Log($"[MirrorSmoke] member={member.ParticipantId} slot={member.Slot} class={member.CharacterClass} " +
                $"connection={member.ConnectionId} netId={netId} absent={context?.GetComponent<MirrorSpawnedPlayerBinder>().IsTemporarilyAbsent} " +
                $"hp={context?.Health.CurrentHealth}/{context?.Health.MaxHealth} mp={context?.Mana.CurrentMana}/{context?.Mana.MaxMana} " +
                $"gold={context?.Wallet.Gold} revives={context?.Controller.reviveCount} items={itemIds} " +
                $"profileShopLevel={member.PassiveProfile?.ShopLevel} " +
                $"buffs={(context != null ? string.Join(",", context.Buffs.ActiveBuffs.Select(b => b.remainingTime.ToString("F3"))) : string.Empty)} " +
                $"upgrades={(context != null ? string.Join(",", context.Inventory.GetAllInventoryItems().Select(item => item.itemData.upgradeLevel)) : string.Empty)}");
        }
    }

    internal static string Argument(string key)
    {
#if UNITY_EDITOR
        string[] args = editorArguments ?? Environment.GetCommandLineArgs();
#else
        string[] args = Environment.GetCommandLineArgs();
#endif
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

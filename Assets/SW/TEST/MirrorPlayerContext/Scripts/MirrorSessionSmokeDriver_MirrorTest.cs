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
    private readonly System.Collections.Generic.HashSet<string> verifiedEnemyData = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachWhenRequested()
    {
        if (string.IsNullOrEmpty(Argument("--mirror-smoke-role"))) return;
        if (NetworkManager.singleton is MirrorTestNetworkManager manager)
            manager.gameObject.AddComponent<MirrorSessionSmokeDriver_MirrorTest>();
    }

    private void Start()
    {
        manager = GetComponent<MirrorTestNetworkManager>();
        role = Argument("--mirror-smoke-role");
        expectedMembers = int.TryParse(Argument("--mirror-smoke-count"), out int count) ? count : 1;
        duration = double.TryParse(Argument("--mirror-smoke-duration"), out double seconds) ? seconds : 60;
        inventoryProbe = Argument("--mirror-smoke-inventory") == "true";
        runProbe = Argument("--mirror-smoke-travel") == "full-run";
        if (runProbe && (!Debug.isDebugBuild && !Application.isEditor || inventoryProbe))
            throw new InvalidOperationException("전체 진행 검사는 개발 빌드에서 인벤토리 검사와 별도로 실행하세요.");
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
        if (runProbe)
        {
            StartCoroutine(ValidateEnemyData());
            if (NetworkServer.active) StartCoroutine(ValidateServerRunReset());
            if (role != "server") StartCoroutine(ValidateCompleteRun());
        }
    }

    private void Update()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (now - startedAt >= duration)
        {
            ReportState();
            Debug.Log("[MirrorSmoke] END role=" + role);
            bool passed = (!inventoryProbe || role == "server" || inventoryValidationPassed) &&
                          (!runProbe || runValidationPassed);
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
        if (inventoryProbe && !nodeRequested && manager.CanLocalClientControlSession && manager.IsSessionSelectionActive &&
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
            StartCoroutine(ValidateInventoryInput());
        }
        if (!nodeRequested && Argument("--mirror-smoke-travel") == "first-node" &&
            manager.CanLocalClientControlSession && manager.IsSessionSelectionActive &&
            manager.LocalPlayerContext?.RuntimeState.HasSnapshot == true &&
            manager.TryGetRunSnapshot(out StageMapSaveData snapshot))
        {
            StageNodeSaveData node = snapshot.nodes.FirstOrDefault(value => value.floor == snapshot.clearedFloor + 1);
            if (node != null) nodeRequested = manager.RequestStageNodeSelection(node.id);
        }
    }

    private void OnDestroy()
    {
        if (manager != null) manager.AdmissionStatusChanged -= ReportAdmission;
    }

    /// <summary>
    /// 명시적 개발 검사에서만 실제 생성된 적에게 종료 피해를 준다. 피해 계산·스킬 품질 검사가 아니라
    /// 적 사망→웨이브→포탈→결과→새 런의 연결 검사이며 진행 스냅샷을 직접 완료시키지 않는다.
    /// </summary>
    private IEnumerator ValidateServerRunReset()
    {
        bool sawRun = false;
        bool returned = false;
        var fundedPlayers = new System.Collections.Generic.HashSet<uint>();
        var enemySeenAt = new System.Collections.Generic.Dictionary<uint, double>();
        var rewardedKinds = new System.Collections.Generic.HashSet<string>();
        while (true)
        {
            if (manager.ServerRoster.RunStarted)
            {
                sawRun = true;
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
        Require(manager.RequestLobbyChange(MirrorLobbyOperation_MirrorTest.ReturnToLobby), "클리어 전 복귀 거절 검사 요청");
        yield return new WaitForSecondsRealtime(0.5f);
        Require(manager.ClientLobby.RunStarted && !manager.IsRunCompleted, "클리어 전 복귀 거절");

        while (!manager.IsRunCompleted)
        {
            yield return WaitFor(() => manager.IsSessionSelectionActive && manager.TryGetRunSnapshot(out StageMapSaveData snapshot) &&
                                      snapshot.nodes.Count > 0, "선택 화면 진행도", 90);
            if (manager.CanLocalClientControlSession)
            {
                manager.TryGetRunSnapshot(out StageMapSaveData currentRun);
                int nextFloor = currentRun.clearedFloor + 1;
                YJ_StageNodeHover selected = null;
                yield return WaitFor(() => (selected = FindObjectsByType<YJ_StageNodeHover>(FindObjectsSortMode.None)
                    .Where(node => node.IsInteractable && node.NodeData != null && node.NodeData.floor == nextFloor)
                    .OrderBy(node => node.NodeData.type == StageNodeType.Camp ? 0 : node.NodeData.type == StageNodeType.Event ? 1 : 2)
                    .ThenBy(node => node.NodeData.id).FirstOrDefault()) != null, "선택 가능한 실제 노드", 30);
                Require(manager.RequestStageNodeSelection(selected.NodeData.id), "정상 노드 선택 요청");
            }
            yield return WaitFor(() => !manager.IsSessionSelectionActive && manager.CurrentSessionRoute != MirrorSessionRoute.Unknown,
                "파티 노드 이동", 90);
            yield return WaitFor(() => manager.TryGetPendingStageNode(out _), "현재 노드 스냅샷");
            manager.TryGetPendingStageNode(out StageNodeSaveData pending);
            visited++;
            Debug.Log($"[MirrorRunSmoke] node={pending.id} type={pending.type} floor={pending.floor} role={role}");
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (manager.CurrentSessionRoute == MirrorSessionRoute.Event)
            {
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
                if (manager.CurrentSessionRoute == MirrorSessionRoute.Combat)
                {
                    yield return WaitFor(() => FindFirstObjectByType<NetworkEnemyWaveSpawner_MirrorTest>()?.SessionPhase == MirrorTestSessionPhase.Completed,
                        "실제 적 사망 후 웨이브 완료", 90);
                    if (pending.type == StageNodeType.Boss) break;
                }
                PlayerContext owner = manager.LocalPlayerContext;
                yield return WaitFor(() => owner.Controller.IsControlEnabled && owner.Controller.agent.enabled && owner.Controller.agent.isOnNavMesh,
                    "포탈 이동 입력 복구");
                MirrorStagePortalAdapter_MirrorTest portal = FindFirstObjectByType<MirrorStagePortalAdapter_MirrorTest>();
                Require(portal != null, "실제 스테이지 포탈");
                Vector3 destination = portal.GetComponent<Collider>().bounds.center;
                destination.y = owner.transform.position.y;
                Require(UnityEngine.AI.NavMesh.SamplePosition(destination, out UnityEngine.AI.NavMeshHit hit, 3,
                    owner.Controller.agent.areaMask), "포탈 이동 영역");
                var path = new UnityEngine.AI.NavMeshPath();
                Require(owner.Controller.agent.CalculatePath(hit.position, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete,
                    "시작점에서 포탈까지 연결된 이동 경로");
                owner.Controller.MoveCommand(hit.position);
            }
            yield return WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scene,
                "실제 포탈 또는 미지 선택 완료 후 파티 복귀", 100);
        }

        NetworkBossHealthBar_MirrorTest result = null;
        yield return WaitFor(() => manager.IsRunCompleted && (result = FindFirstObjectByType<NetworkBossHealthBar_MirrorTest>()) != null && result.IsClearVisible,
            "보스 완료 결과 화면");
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

    private IEnumerator ValidateInventoryInput()
    {
        PlayerContext owner = manager.LocalPlayerContext;
        PlayerInventorySync_MirrorTest sync = owner.GetComponent<PlayerInventorySync_MirrorTest>();
        NetworkShopPlayerState_MirrorTest wallet = owner.GetComponent<NetworkShopPlayerState_MirrorTest>();
        InventoryGrid grid = owner.Inventory.PlayerGrid;
        yield return WaitFor(() => owner.Controller.IsControlEnabled && owner.Controller.agent.enabled && owner.Controller.agent.isOnNavMesh,
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
        Require(Owned(owner, firstId) != null && wallet.Gold == goldBefore, "재구매 단일 소유권·대금");

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
        Collider pickupCollider = dropped.GetComponentInChildren<Collider>();
        Require(pickupCollider != null, "월드 아이템 획득 Collider");
        revision = sync.StateRevision;
        Require(sync.TryRequestPickup(new Ray(pickupCollider.bounds.center + Vector3.up * 2, Vector3.down)), "실제 Raycast 획득 요청");
        yield return WaitFor(() => sync.StateRevision > revision && sync.PendingRequestCount == 0 && Owned(owner, firstId) != null, "월드 재획득 승인");
        Require(Owned(owner, firstId).itemData.upgradeLevel == previousLevel + 1 && grid.GetAllItems().Count == 1 &&
                wallet.Gold == goldBeforeUpgrade - cost, "드롭·획득 후 강화·소유권·골드 보존");
        inventoryValidationPassed = true;
        Debug.Log($"[MirrorInventorySmoke] PASS role={role} netId={sync.netId} camp/move/rotate/swap/reject/equip/unequip/sell/buy/remove/cancel/upgrade/drop/pickup; retained={firstId}; gold={wallet.Gold}; upgrade={previousLevel + 1}");
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

    private static void Require(bool condition, string step)
    {
        if (!condition) throw new InvalidOperationException("[MirrorSmoke] FAIL " + step);
    }

    private static void ReportAdmission(string reason) => Debug.Log("[MirrorSmoke] admission=" + reason);

    private void ReportState()
    {
        Debug.Log($"[MirrorSmoke] role={role} connected={NetworkClient.isConnected} admitted={manager.ClientCompatibilityConfirmed} " +
            $"run={manager.ClientLobby.RunStarted} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        PlayerContext local = manager.LocalPlayerContext;
        if (local != null)
        {
            UnityEngine.AI.NavMeshAgent agent = local.GetComponent<UnityEngine.AI.NavMeshAgent>();
            bool sampled = UnityEngine.AI.NavMesh.SamplePosition(local.transform.position, out UnityEngine.AI.NavMeshHit navHit, 4, UnityEngine.AI.NavMesh.AllAreas);
            Debug.Log($"[MirrorSmoke] localNetId={local.GetComponent<NetworkIdentity>().netId} " +
                $"snapshot={local.RuntimeState.HasSnapshot} absent={local.GetComponent<MirrorSpawnedPlayerBinder>().IsTemporarilyAbsent} " +
                $"controller={local.Controller.enabled} control={local.Controller.IsControlEnabled} " +
                $"agent={agent != null && agent.enabled} onNavMesh={agent != null && agent.enabled && agent.isOnNavMesh} inventoryView={local.Inventory.PlayerGrid.HasView} " +
                $"position={local.transform.position} agentType={agent?.agentTypeID} navSample={sampled} hit={navHit.position} " +
                $"controllerAgentMatches={local.Controller.agent == agent}");
            Debug.Log($"[MirrorSmoke] localGold={local.Wallet.Gold} localHP={local.Health.CurrentHealth}/{local.Health.MaxHealth} " +
                $"localMP={local.Mana.CurrentMana}/{local.Mana.MaxMana} " +
                $"localItems={string.Join(",", local.Inventory.GetAllInventoryItems().Select(item => item.itemData.instanceId))} " +
                $"localUpgrades={string.Join(",", local.Inventory.GetAllInventoryItems().Select(item => item.itemData.upgradeLevel))}");
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
                $"upgrades={(context != null ? string.Join(",", context.Inventory.GetAllInventoryItems().Select(item => item.itemData.upgradeLevel)) : string.Empty)}");
        }
    }

    private static string Argument(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct MppmStepMessage : NetworkMessage
{
    public int Step;
    public uint ActorNetId;
    public uint TargetNetId;
    public Vector3 AimPosition;
    public string Detail;
}

public struct MppmClientAckMessage : NetworkMessage
{
    public int Step;
    public bool Success;
    public string Detail;
}

/// <summary>
/// Unity MPPM 2인 환경(Host 1명 + Virtual Player Client 1명)에서 전투, 메타데이터, 고유효과 필터, 처치 귀속을 자동 검증한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MirrorMppmCombatRunner_MirrorTest : MonoBehaviour
{
    private const string ValidationTag = "mppm-p1-validation";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static bool IsRunning { get; private set; }
    public static bool Completed { get; private set; }
    public static bool Passed { get; private set; }
    public static int TotalChecksPassed { get; private set; }
    public static string FailureReason { get; private set; }

    private MirrorTestNetworkManager manager;
    private bool isMainEditor;
    private PlayerContext playerA; // Host
    private PlayerContext playerB; // Remote Client
    private int checkCount;
    private bool clientAckReceived;
    private MppmClientAckMessage lastClientAck;

    public static bool ShouldRun
    {
        get
        {
            var tags = Unity.Multiplayer.PlayMode.CurrentPlayer.Tags;
            if (tags.Any(t => t.IndexOf(ValidationTag, StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("SW.RunMppmValidation", false))
                return true;
#endif
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsRunning = false;
        Completed = false;
        Passed = false;
        TotalChecksPassed = 0;
        FailureReason = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnSceneLoaded()
    {
        if (!ShouldRun) return;
        if (FindFirstObjectByType<MirrorMppmCombatRunner_MirrorTest>() != null) return;

        var go = new GameObject("MirrorMppmCombatRunner_MirrorTest");
        DontDestroyOnLoad(go);
        go.AddComponent<MirrorMppmCombatRunner_MirrorTest>();
    }

    private void Start()
    {
        manager = FindFirstObjectByType<MirrorTestNetworkManager>();
        if (manager == null)
        {
            Debug.LogError("[MirrorMppmCombatRunner] MirrorTestNetworkManager를 찾을 수 없습니다.");
            return;
        }

        isMainEditor = Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor;
        IsRunning = true;
        checkCount = 0;

        if (isMainEditor)
        {
            Debug.Log("[MirrorMppmCombatRunner] Main Editor (Host) 시작: 2인 검증 러너 대기...");
            StartCoroutine(RunHostLifecycle());
        }
        else
        {
            Debug.Log("[MirrorMppmCombatRunner] Virtual Player (Client) 시작: Host 연결 대기...");
            StartCoroutine(RunClientLifecycle());
        }
    }

    // =========================================================================
    // Virtual Player (Client) Routine
    // =========================================================================
    private IEnumerator RunClientLifecycle()
    {
        // 호스트가 첫 StepMessage를 보내기 전에 핸들러를 먼저 등록합니다.
        NetworkClient.RegisterHandler<MppmStepMessage>(OnClientStepMessage);

        yield return new WaitForSeconds(1.5f);
        float retryUntil = Time.realtimeSinceStartup + 180f;
        while (!NetworkClient.isConnected && Time.realtimeSinceStartup < retryUntil)
        {
            if (!NetworkClient.active)
            {
                manager.networkAddress = "localhost";
                manager.StartClient();
            }
            yield return new WaitForSeconds(2f);
        }

        yield return new WaitUntil(() => NetworkClient.isConnected);
        Debug.Log("[MirrorMppmCombatRunner-Client] Server 연결 완료.");

        yield return new WaitUntil(() => manager.LocalPlayerContext != null &&
                                         manager.LocalPlayerContext.RuntimeState?.HasSnapshot == true);

        Debug.Log($"[MirrorMppmCombatRunner-Client] 로컬 플레이어 초기화 완료 (NetId: {manager.LocalPlayerContext.CombatAuthority.netId})");
        NetworkClient.Send(new MppmClientAckMessage
        {
            Step = 0,
            Success = true,
            Detail = "Client runner ready"
        });
    }

    private void OnClientStepMessage(MppmStepMessage msg)
    {
        Debug.Log($"[MirrorMppmCombatRunner-Client] StepMessage 수신: Step={msg.Step}, Detail={msg.Detail}");
        StartCoroutine(HandleClientStep(msg));
    }

    private IEnumerator HandleClientStep(MppmStepMessage msg)
    {
        var local = manager.LocalPlayerContext;
        if (local == null)
        {
            NetworkClient.Send(new MppmClientAckMessage { Step = msg.Step, Success = false, Detail = "LocalPlayerContext is null" });
            yield break;
        }

        if (msg.Step == 1) // T01: Client basic attack
        {
            float startWait = Time.realtimeSinceStartup;
            while (!local.StateMachine.Is(PlayerState.Idle) && Time.realtimeSinceStartup - startWait < 3f)
                yield return null;

            Vector3 aim = msg.AimPosition;
            Vector3 dir = aim - local.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                local.transform.rotation = Quaternion.LookRotation(dir.normalized);

            bool attackStarted = local.CombatAuthority.TryBeginLocalAttack(aim);
            Debug.Log($"[MirrorMppmCombatRunner-Client] T01 Client TryBeginLocalAttack: {attackStarted} -> {aim}");

            NetworkClient.Send(new MppmClientAckMessage
            {
                Step = msg.Step,
                Success = attackStarted,
                Detail = attackStarted ? "Attack started" : "TryBeginLocalAttack returned false"
            });
        }
    }

    // =========================================================================
    // Main Editor (Host) Routine
    // =========================================================================
    private IEnumerator RunHostLifecycle()
    {
        if (!NetworkServer.active)
        {
            manager.StartHost();
        }

        yield return new WaitUntil(() => NetworkServer.active && NetworkClient.isConnected);
        Debug.Log("[MirrorMppmCombatRunner-Host] Host 시작 완료. Client 연결 대기 중...");

        NetworkServer.RegisterHandler<MppmClientAckMessage>(OnServerClientAck);

        // Client 연결 대기 (총 연결 수 2 이상)
        // 최초 가상 프로젝트 생성·임포트가 포함된 콜드 스타트도 기다릴 수 있게 합니다.
        float connectTimeout = Time.realtimeSinceStartup + 180f;
        while ((NetworkServer.connections.Count < 2 || NetworkServer.connections.Values.Any(conn => !conn.isAuthenticated)) &&
               Time.realtimeSinceStartup < connectTimeout)
            yield return null;

        if (NetworkServer.connections.Count < 2 || NetworkServer.connections.Values.Any(conn => !conn.isAuthenticated))
        {
            Fail("Virtual Player (Client) 연결 또는 인증 시간 초과 (180초)");
            yield break;
        }

        Debug.Log($"[MirrorMppmCombatRunner-Host] 2인 연결 감지 (연결 수: {NetworkServer.connections.Count}). 세션 및 캐릭터 스폰 준비.");

        // ServerRoster 멤버 캐릭터 및 준비 설정
        EnsureServerRosterReady();

        // 플레이어 스폰 대기
        float spawnTimeout = Time.realtimeSinceStartup + 15f;
        while (manager.ServerPlayerContexts.Count < 2 && Time.realtimeSinceStartup < spawnTimeout)
        {
            EnsureServerRosterReady();
            yield return new WaitForSeconds(0.2f);
        }

        if (manager.ServerPlayerContexts.Count < 2)
        {
            Fail($"플레이어 스폰 시간 초과: 현재 스폰된 플레이어 {manager.ServerPlayerContexts.Count}/2");
            yield break;
        }

        playerA = manager.ServerPlayerContexts.FirstOrDefault(p => p.CombatAuthority != null && p.CombatAuthority.isLocalPlayer);
        playerB = manager.ServerPlayerContexts.FirstOrDefault(p => p.CombatAuthority != null && !p.CombatAuthority.isLocalPlayer);

        if (playerA == null || playerB == null)
        {
            Fail("Host(Player A) 또는 Remote Client(Player B)를 구분하지 못했습니다.");
            yield break;
        }

        // 스냅샷 준비 대기
        yield return new WaitUntil(() => playerA.RuntimeState?.HasSnapshot == true && playerB.RuntimeState?.HasSnapshot == true);

        // 플레이어 레벨 로드 (경험치 보상 반영 준비)
        playerA.GetComponent<PlayerLevelManager>()?.Load();
        playerB.GetComponent<PlayerLevelManager>()?.Load();

        // 원격 러너가 메시지 핸들러와 로컬 스냅샷 준비를 끝낸 뒤 시나리오를 시작합니다.
        float clientReadyTimeout = Time.realtimeSinceStartup + 15f;
        while ((!clientAckReceived || lastClientAck.Step != 0 || !lastClientAck.Success) &&
               Time.realtimeSinceStartup < clientReadyTimeout)
            yield return null;

        Check(clientAckReceived && lastClientAck.Step == 0 && lastClientAck.Success,
              "T00: Remote Client runner ready");

        Debug.Log($"[MirrorMppmCombatRunner-Host] 2인 세션 준비 완료: Player A (Host NetId: {playerA.CombatAuthority.netId}), Player B (Client NetId: {playerB.CombatAuthority.netId})");

        // ---------------------------------------------------------------------
        // T00 ~ T07 순차 실행
        // ---------------------------------------------------------------------
        yield return RunT00();
        if (Failed) yield break;

        yield return RunT01();
        if (Failed) yield break;

        yield return RunT02();
        if (Failed) yield break;

        yield return RunT03();
        if (Failed) yield break;

        yield return RunT04();
        if (Failed) yield break;

        yield return RunT05();
        if (Failed) yield break;

        yield return RunT06();
        if (Failed) yield break;

        yield return RunT07();
        if (Failed) yield break;

        // 전체 통과 보고
        Passed = true;
        Completed = true;
        TotalChecksPassed = checkCount;
        Debug.Log($"<color=green><b>[MirrorMppmCombatRunner] ALL SCENARIOS T00~T07 PASSED! Total Checks Passed: {checkCount}</b></color>");

        yield return new WaitForSeconds(2f);

        // MPPM 세션 종료 요청
        StopMppmSession();
    }

    private void EnsureServerRosterReady()
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            // Mirror 인증이 끝나기 전에 Ready/Spawn 메시지를 보내면 원격 클라이언트가 연결을 끊습니다.
            if (!conn.isAuthenticated)
                continue;

            var member = manager.ServerRoster.FindByConnection(conn.connectionId);
            if (member == null)
            {
                manager.ServerRoster.TryJoin(conn.connectionId, conn.connectionId == 0 ? "HostPlayer" : "ClientPlayer", out member, out _);
            }
            if (member != null)
            {
                member.CharacterClass = CharacterClass.Fighter;
                member.HasCharacterChoice = true;
                member.IsReady = true;
            }
        }

        typeof(MirrorSessionRoster_MirrorTest).GetProperty("RunStarted", BindingFlags.Public | BindingFlags.Instance)
            ?.SetValue(manager.ServerRoster, true);

        var attachMethod = typeof(MirrorTestNetworkManager).GetMethod("AttachReadyParticipant", PrivateInstance);
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (!conn.isAuthenticated)
                continue;

            conn.isReady = true;
            if (conn.identity == null)
            {
                attachMethod?.Invoke(manager, new object[] { conn });
            }
        }
    }

    private void OnServerClientAck(NetworkConnectionToClient conn, MppmClientAckMessage msg)
    {
        Debug.Log($"[MirrorMppmCombatRunner-Host] Client Ack 수신: Step={msg.Step}, Success={msg.Success}, Detail={msg.Detail}");
        clientAckReceived = true;
        lastClientAck = msg;
    }

    // =========================================================================
    // T00: 2인 접속 및 초기 상태 확인
    // =========================================================================
    private IEnumerator RunT00()
    {
        Debug.Log("[MPPM T00] START: 2인 접속 및 초기 상태 확인");
        Check(manager.ServerPlayerContexts.Count == 2, "T00: ServerPlayerContexts count == 2");
        Check(playerA != null && playerA.CombatAuthority != null && playerA.CombatAuthority.isLocalPlayer, "T00: Player A is Host and localPlayer");
        Check(playerB != null && playerB.CombatAuthority != null && !playerB.CombatAuthority.isLocalPlayer, "T00: Player B is Remote Client");
        Check(!playerA.RuntimeState.IsDead && playerA.RuntimeState.HasSnapshot, "T00: Player A alive with snapshot");
        Check(!playerB.RuntimeState.IsDead && playerB.RuntimeState.HasSnapshot, "T00: Player B alive with snapshot");
        Debug.Log($"[MPPM T00] PASS: Host (NetId: {playerA.CombatAuthority.netId}) + Client (NetId: {playerB.CombatAuthority.netId}) 연결 및 초기화 완료.");
        yield return null;
    }

    // =========================================================================
    // T01: 기본 공격 실제 발사 및 단일 타격 검증 (복수 콜라이더)
    // =========================================================================
    private IEnumerator RunT01()
    {
        Debug.Log("[MPPM T01] START: 기본 공격 실제 발사 및 단일 타격 검증 (복수 콜라이더)");

        Vector3 spawnPos = playerA.transform.position + playerA.transform.forward * 1.5f;
        var target = SpawnEnemy(spawnPos);
        AddDuplicateCollider(target.gameObject);

        // 1) Host 기본 공격
        float initialHp = target.CurrentHealth;
        uint initialHits = target.ReceivedDamagePresentationCount;

        playerA.transform.LookAt(target.transform.position);
        bool hostAttackStarted = playerA.CombatAuthority.TryBeginLocalAttack(target.transform.position);
        Check(hostAttackStarted, "T01: Host TryBeginLocalAttack started");

        float waitStart = Time.realtimeSinceStartup;
        while (target.CurrentHealth >= initialHp && Time.realtimeSinceStartup - waitStart < 5f)
            yield return null;

        Check(target.CurrentHealth < initialHp, "T01: Enemy received damage from Host attack");
        Check(target.ReceivedDamagePresentationCount == initialHits + 1, "T01: Host attack resulted in exactly 1 hit presentation (multi-collider collapsed)");
        Check(target.LastAttackerNetId == playerA.CombatAuthority.netId, "T01: Target last attacker is Player A");

        // 2) Remote Client 기본 공격
        target.transform.position = playerB.transform.position + playerB.transform.forward * 1.5f;
        Physics.SyncTransforms();

        float prevHp = target.CurrentHealth;
        uint prevHits = target.ReceivedDamagePresentationCount;
        clientAckReceived = false;

        var clientConn = NetworkServer.connections.Values.FirstOrDefault(c =>
            c.identity == playerB.CombatAuthority.netIdentity);
        Check(clientConn != null, "T01: Client connection found for attack command");

        clientConn.Send(new MppmStepMessage
        {
            Step = 1,
            ActorNetId = playerB.CombatAuthority.netId,
            TargetNetId = target.netId,
            AimPosition = target.transform.position,
            Detail = "T01 Remote Client Attack"
        });

        waitStart = Time.realtimeSinceStartup;
        while (!clientAckReceived && Time.realtimeSinceStartup - waitStart < 4f)
            yield return null;

        Check(clientAckReceived && lastClientAck.Success, "T01: Remote Client acknowledged attack start");

        waitStart = Time.realtimeSinceStartup;
        while (target.CurrentHealth >= prevHp && Time.realtimeSinceStartup - waitStart < 5f)
            yield return null;

        Check(target.CurrentHealth < prevHp, "T01: Enemy received damage from Remote Client attack");
        Check(target.ReceivedDamagePresentationCount == prevHits + 1, "T01: Remote Client attack resulted in exactly 1 hit presentation (multi-collider collapsed)");
        Check(target.LastAttackerNetId == playerB.CombatAuthority.netId, "T01: Target last attacker is Player B");

        DestroyEnemy(target);
        Debug.Log("[MPPM T01] PASS: Host와 Remote Client 실제 기본 공격의 복수 콜라이더 단일 타격 처리 확인.");
    }

    // =========================================================================
    // T02: 동일 시퀀스 attackId 독립성
    // =========================================================================
    private IEnumerator RunT02()
    {
        Debug.Log("[MPPM T02] START: 동일 시퀀스 attackId 독립성 검증");

        var target = SpawnEnemy(playerA.transform.position + Vector3.forward * 2f);
        var eCombat = target.GetComponent<WBH_EnemyController>();
        var eStatus = target.GetComponent<WBH_EnemyStatus>();

        // Host와 Remote Client가 동일한 attackId = 200u로 각각 공격
        bool hitA = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult resA, DamageCause.Direct, 200u);
        Check(hitA, "T02: Player A attack with ID 200 succeeded");
        Check(resA.AttackId == 200u && resA.DamageCause == DamageCause.Direct, "T02: Result A has AttackId 200 and Direct");

        bool duplicateA = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult duplicateResult, DamageCause.Direct, 200u);
        Check(!duplicateA && duplicateResult.AttackId == 0, "T02: Player A duplicate attack-target pair rejected");
        Check(target.ReceivedDamagePresentationCount == 1, "T02: Duplicate attack did not produce a second hit");

        bool hitB = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerB, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult resB, DamageCause.Direct, 200u);
        Check(hitB, "T02: Player B attack with ID 200 succeeded independently");
        Check(resB.AttackId == 200u && resB.DamageCause == DamageCause.Direct, "T02: Result B has AttackId 200 and Direct");

        Check(target.ReceivedDamagePresentationCount == 2, "T02: Target recorded both independent hits");

        DestroyEnemy(target);
        Debug.Log("[MPPM T02] PASS: Host와 Remote Client의 동일 attackId(200u) 독립 처리 확인.");
        yield return null;
    }

    // =========================================================================
    // T03: 다인 환경 고유효과 필터 (Direct vs Effect/DoT)
    // =========================================================================
    private IEnumerator RunT03()
    {
        Debug.Log("[MPPM T03] START: 다인 환경 고유효과 필터 검증");

        var effectDamage = ScriptableObject.CreateInstance<TriggeredBuffUniqueEffectSO>();
        effectDamage.name = "MppmEffect_OnDamageDealt";
        effectDamage.triggerCondition = TriggerCondition.OnDamageDealt;
        effectDamage.buffSpec = new BuffSpec { duration = 10f, stackBehavior = BuffStackBehavior.Stack, maxStack = 10 };

        var defDamage = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        defDamage.uniqueEffect = effectDamage;
        defDamage.mainOptions = Array.Empty<FixedStatValue>();

        var itemA = new ItemInstance { instanceId = "t03_dmg_a", definition = defDamage };
        var itemB = new ItemInstance { instanceId = "t03_dmg_b", definition = defDamage };

        EquipItemFixture(playerA, itemA);
        EquipItemFixture(playerB, itemB);

        var target = SpawnEnemy(playerA.transform.position + Vector3.forward * 2f);
        var eCombat = target.GetComponent<WBH_EnemyController>();
        var eStatus = target.GetComponent<WBH_EnemyStatus>();

        Check(GetBuffStack(playerA, effectDamage) == 0, "T03: Player A initial 0 stacks");
        Check(GetBuffStack(playerB, effectDamage) == 0, "T03: Player B initial 0 stacks");

        // 1) Player A Direct 공격 -> Player A만 1스택, Player B는 0
        WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Direct, 301u);
        Check(GetBuffStack(playerA, effectDamage) == 1, "T03: Player A received 1 stack from Direct hit");
        Check(GetBuffStack(playerB, effectDamage) == 0, "T03: Player B unaffected by Player A's hit");

        // 2) Player B Direct 공격 -> Player B만 1스택, Player A는 1 유지
        WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerB, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Direct, 302u);
        Check(GetBuffStack(playerB, effectDamage) == 1, "T03: Player B received 1 stack from Direct hit");
        Check(GetBuffStack(playerA, effectDamage) == 1, "T03: Player A stack maintained at 1");

        // 3) Player A Skill, Effect 및 DoT 추가타 -> 피해는 정상 처리되나 Player A 스택 증가 없음 (필터 차단)
        float hpBeforeSkill = eStatus.CurrentHp;
        bool hitSkill = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Skill, 303u);
        Check(hitSkill && eStatus.CurrentHp < hpBeforeSkill, "T03: Skill damage processed to target");
        Check(GetBuffStack(playerA, effectDamage) == 1, "T03: Skill damage did NOT trigger OnDamageDealt on Player A");

        float hpBeforeEffect = eStatus.CurrentHp;
        bool hitEffectA = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Effect, 304u);
        Check(hitEffectA && eStatus.CurrentHp < hpBeforeEffect, "T03: Effect damage processed to target");
        Check(GetBuffStack(playerA, effectDamage) == 1, "T03: Effect damage did NOT trigger OnDamageDealt on Player A");

        float hpBeforeDoT = eStatus.CurrentHp;
        bool hitDoT = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.DoT, 305u);
        Check(hitDoT && eStatus.CurrentHp < hpBeforeDoT, "T03: DoT damage processed to target");
        Check(GetBuffStack(playerA, effectDamage) == 1, "T03: DoT damage did NOT trigger OnDamageDealt on Player A");

        // 4) Player B Effect 추가타 -> 피해는 정상 처리되나 Player B 스택 증가 없음 (필터 차단)
        float hpBeforeEffectB = eStatus.CurrentHp;
        bool hitEffectB = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerB, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Effect, 306u);
        Check(hitEffectB && eStatus.CurrentHp < hpBeforeEffectB, "T03: Effect damage processed to target for B");
        Check(GetBuffStack(playerB, effectDamage) == 1, "T03: Effect damage did NOT trigger OnDamageDealt on Player B");

        UnequipItemFixture(playerA);
        UnequipItemFixture(playerB);
        DestroyImmediate(effectDamage);
        DestroyImmediate(defDamage);
        DestroyEnemy(target);

        Debug.Log("[MPPM T03] PASS: Direct 피해만 OnDamageDealt 발동, Effect/DoT는 엄격 차단 확인.");
        yield return null;
    }

    // =========================================================================
    // T04: 다인 처치 단독 귀속 (A 선타 -> B Effect 막타)
    // =========================================================================
    private IEnumerator RunT04()
    {
        Debug.Log("[MPPM T04] START: 다인 처치 단독 귀속 검증");

        var effectKill = ScriptableObject.CreateInstance<TriggeredBuffUniqueEffectSO>();
        effectKill.name = "MppmEffect_OnKill";
        effectKill.triggerCondition = TriggerCondition.OnKill;
        effectKill.buffSpec = new BuffSpec { duration = 10f, stackBehavior = BuffStackBehavior.Stack, maxStack = 10 };

        var defKill = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        defKill.uniqueEffect = effectKill;
        defKill.mainOptions = Array.Empty<FixedStatValue>();

        var itemA = new ItemInstance { instanceId = "t04_kill_a", definition = defKill };
        var itemB = new ItemInstance { instanceId = "t04_kill_b", definition = defKill };

        EquipItemFixture(playerA, itemA);
        EquipItemFixture(playerB, itemB);

        var target = SpawnEnemy(playerA.transform.position + Vector3.forward * 2f, info =>
        {
            info.maxHP = 100f;
            info.exp = 150;
            info.credit = 80;
        });
        var eCombat = target.GetComponent<WBH_EnemyController>();
        var eStatus = target.GetComponent<WBH_EnemyStatus>();

        int initGoldA = playerA.Wallet.Gold;
        int initGoldB = playerB.Wallet.Gold;
        int initExpA = (int)playerA.Stats.CurrentExp;
        int initExpB = (int)playerB.Stats.CurrentExp;
        int initLevelB = playerB.Stats.CurrentLevel;

        // 1) Player A가 먼저 Direct 피해를 주되 대상은 생존
        float hpBeforeA = eStatus.CurrentHp;
        bool hitA = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult resA, DamageCause.Direct, 401u);
        Check(hitA && resA.DamageCause == DamageCause.Direct, "T04: Player A dealt Direct damage");
        // 런타임 공격력에 따라 실제 피해량이 달라지므로 고정 HP 50을 가정하지 않습니다.
        Check(eStatus.CurrentHp < hpBeforeA && eStatus.CurrentHp > 0f && !eStatus.IsDead,
              "T04: Target took non-lethal damage and remained alive");
        var lastAttackerA = typeof(NetworkEnemyAuthority_MirrorTest).GetField("lastAttackerContext", PrivateInstance).GetValue(target);
        Check((PlayerContext)lastAttackerA == playerA, "T04: Last attacker context is Player A");

        // 2) Player B가 Effect 피해로 마무리
        bool hitB = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerB, eCombat, ElementType.Fire, 1000f, null, out WBH_DamageResult resB, DamageCause.Effect, 402u);
        Check(hitB && resB.DamageCause == DamageCause.Effect, "T04: Player B dealt Effect damage (lethal)");
        Check(eStatus.IsDead, "T04: Target died from Player B's Effect hit");
        Check(target.KillRewardCount == 1, "T04: Natural death path granted the kill reward once");
        var lastAttackerB = typeof(NetworkEnemyAuthority_MirrorTest).GetField("lastAttackerContext", PrivateInstance).GetValue(target);
        Check((PlayerContext)lastAttackerB == playerB, "T04: Last attacker context updated to Player B");

        // 3) 처치 보상 수동 호출(Authority 내부 로직)
        typeof(NetworkEnemyAuthority_MirrorTest).GetMethod("GrantKillRewardOnce", PrivateInstance).Invoke(target, null);
        Check(target.KillRewardCount == 1, "T04: Repeated reward call remained idempotent");

        // Player B만 보상 획득
        Check(playerB.Wallet.Gold == initGoldB + 80, "T04: Player B received 80 Gold");
        Check(playerB.Stats.CurrentLevel > initLevelB || playerB.Stats.CurrentExp > initExpB, "T04: Player B received EXP");
        Check(GetBuffStack(playerB, effectKill) == 1, "T04: Player B received OnKill buff stack");

        // Player A는 0 보상
        Check(playerA.Wallet.Gold == initGoldA, "T04: Player A received ZERO Gold");
        Check(playerA.Stats.CurrentExp == initExpA, "T04: Player A received ZERO EXP");
        Check(GetBuffStack(playerA, effectKill) == 0, "T04: Player A received ZERO OnKill buffs");

        UnequipItemFixture(playerA);
        UnequipItemFixture(playerB);
        DestroyImmediate(effectKill);
        DestroyImmediate(defKill);
        DestroyEnemy(target);

        Debug.Log("[MPPM T04] PASS: Player B 단독 처치 귀속(OnKill 버프/골드/경험치) 및 Player A 보상 0 확인.");
        yield return null;
    }

    // =========================================================================
    // T05: 큐 동기 처리 및 중간 사망
    // =========================================================================
    private IEnumerator RunT05()
    {
        Debug.Log("[MPPM T05] START: 큐 동기 처리 및 중간 사망 검증");

        var target = SpawnEnemy(playerA.transform.position + Vector3.forward * 2f, info => info.maxHP = 100f);
        var eCombat = target.GetComponent<WBH_EnemyController>();
        var eStatus = target.GetComponent<WBH_EnemyStatus>();
        WBH_EnemyInfo info = target.EnemyInfo.Clone();

        var hits = new List<WBH_DamageResult>();
        eStatus.OnDamaged += (res) =>
        {
            hits.Add(res);
            if (res.AttackId == 500u)
            {
                WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(playerA, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 501u);
                WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(playerA, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 502u);
            }
        };

        // 1) FIFO 실행 순서 검증
        WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Direct, 500u);

        Check(hits.Count == 3, "T05: Exactly 3 hits processed (Direct + 2 follow-ups)");
        Check(hits[0].AttackId == 500u && hits[0].DamageCause == DamageCause.Direct, "T05: Hit 0 is 500 Direct");
        Check(hits[1].AttackId == 501u && hits[1].DamageCause == DamageCause.Effect, "T05: Hit 1 is 501 Effect (FIFO)");
        Check(hits[2].AttackId == 502u && hits[2].DamageCause == DamageCause.Effect, "T05: Hit 2 is 502 Effect (FIFO)");

        // 2) 중간 사망 시 잔여 큐 안전 스킵
        // 런타임 공격력·치명타와 무관하게 510은 비치명, 511은 치명이 되도록 경계를 명시합니다.
        info.maxHP = 10000f;
        eStatus.Initialize(info);
        hits.Clear();

        eStatus.OnDamaged += (res) =>
        {
            if (res.AttackId == 510u)
            {
                WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(playerA, eCombat, ElementType.Fire, 10000f, null, DamageCause.Effect, 511u);
                WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(playerA, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 512u);
            }
        };

        WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Direct, 510u);

        Check(eStatus.IsDead, "T05: Target died during queue processing");
        Check(hits.Count == 2, "T05: Only 2 hits executed (510 Direct, 511 lethal Effect); 512 skipped due to death");
        Check(hits[0].AttackId == 510u && hits[1].AttackId == 511u, "T05: Hits were exactly 510 then 511");

        DestroyEnemy(target);
        Debug.Log("[MPPM T05] PASS: 큐 FIFO 순서 실행 및 대상 사망 시 잔여 큐 스킵 확인.");
        yield return null;
    }

    // =========================================================================
    // T06: 경계 거절 및 예외 복구
    // =========================================================================
    private IEnumerator RunT06()
    {
        Debug.Log("[MPPM T06] START: 경계 거절 및 예외 복구 검증");

        var target = SpawnEnemy(playerA.transform.position + Vector3.forward * 2f);
        var eCombat = target.GetComponent<WBH_EnemyController>();
        var eStatus = target.GetComponent<WBH_EnemyStatus>();

        // 1) 경계 밖 EnqueueFollowUpDamage 거절
        bool outOfBoundsEnqueued = WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 600u);
        Check(!outOfBoundsEnqueued, "T06: EnqueueFollowUpDamage outside resolution rejected (returned false)");

        // 2) 재진입 거절 및 HP 보존
        float hpBeforeReentrancy = eStatus.CurrentHp;
        bool reentrancyRejected = false;
        Action<WBH_DamageResult> reentrancyAttempt = (res) =>
        {
            if (res.AttackId == 610u)
            {
                bool reenterSuccess = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    playerA, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Direct, 611u);
                if (!reenterSuccess) reentrancyRejected = true;
            }
        };
        eStatus.OnDamaged += reentrancyAttempt;

        WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult initialRes, DamageCause.Direct, 610u);
        Check(reentrancyRejected, "T06: Re-entrant TryProcessPlayerDamage rejected (returned false)");
        Check(initialRes.FinalDamage > 0f &&
              Mathf.Approximately(eStatus.CurrentHp, hpBeforeReentrancy - initialRes.FinalDamage),
            "T06: Target HP reflects only initial hit, re-entrancy dealt no damage");

        // 3) 예외 복구 및 후속 정상 공격
        bool shouldThrow = true;
        Action<WBH_DamageResult> thrower = (res) =>
        {
            if (shouldThrow && res.AttackId == 620u)
            {
                WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(playerA, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 621u);
                throw new InvalidOperationException("Simulated exception in damage processing");
            }
        };
        eStatus.OnDamaged += thrower;

        bool caught = false;
        try
        {
            WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                playerA, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Direct, 620u);
        }
        catch (InvalidOperationException)
        {
            caught = true;
        }

        Check(caught, "T06: Controlled exception caught cleanly");

        // 후속 정상 공격 성공 확인
        shouldThrow = false;
        float hpBeforeCleanHit = eStatus.CurrentHp;
        bool cleanHit = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            playerA, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult cleanRes, DamageCause.Direct, 630u);
        Check(cleanHit && cleanRes.AttackId == 630u, "T06: Clean attack succeeded after exception recovery");
        Check(cleanRes.FinalDamage > 0f &&
              Mathf.Approximately(eStatus.CurrentHp, hpBeforeCleanHit - cleanRes.FinalDamage),
            "T06: Queued damage was purged after exception");

        DestroyEnemy(target);
        Debug.Log("[MPPM T06] PASS: 경계 밖 거절, 재진입 거절, 예외 안전 복구 및 후속 공격 확인.");
        yield return null;
    }

    // =========================================================================
    // T07: 벽 차단 및 투사체 동기화 회귀 검증
    // =========================================================================
    private IEnumerator RunT07()
    {
        Debug.Log("[MPPM T07] START: 벽 차단 및 투사체 동기화 회귀 검증");

        Vector3 spawnPos = playerA.transform.position + playerA.transform.forward * 5f;
        var target = SpawnEnemy(spawnPos);
        float initialHp = target.CurrentHealth;
        uint initialHits = target.ReceivedDamagePresentationCount;

        // 1) 벽 생성 (Player A와 Target 사이)
        Vector3 wallPos = playerA.transform.position + playerA.transform.forward * 2.5f;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "MppmValidation_Wall";
        wall.layer = LayerMask.NameToLayer("Wall");
        wall.transform.position = wallPos;
        wall.transform.localScale = new Vector3(4f, 4f, 0.5f);
        Physics.SyncTransforms();

        // Player A를 임시로 Gunner로 설정하여 투사체 검증 수행
        playerA.Equipment.SetActiveCharacterClass(CharacterClass.Gunner);

        // 투사체 발사 (벽 방향)
        Vector3 fireDir = (target.transform.position - playerA.transform.position).normalized;
        SpawnTestProjectile(playerA, playerA.transform.position + Vector3.up, fireDir, 701u);

        // 투사체 이동 및 벽 충돌 대기
        yield return new WaitForSeconds(1.5f);

        Check(Mathf.Approximately(target.CurrentHealth, initialHp), "T07: Enemy HP unchanged after projectile hit wall");
        Check(target.ReceivedDamagePresentationCount == initialHits, "T07: Enemy hit presentation count unchanged");

        // 2) 벽 제거 후 재발사 -> 정상 적중 확인
        DestroyImmediate(wall);
        Physics.SyncTransforms();

        SpawnTestProjectile(playerA, playerA.transform.position + Vector3.up, fireDir, 702u);

        yield return new WaitForSeconds(1.5f);

        Check(target.CurrentHealth < initialHp, "T07: Enemy received damage without wall obstruction");
        Check(target.ReceivedDamagePresentationCount == initialHits + 1, "T07: Enemy presentation count incremented by 1");

        // 복구
        playerA.Equipment.SetActiveCharacterClass(CharacterClass.Fighter);
        DestroyEnemy(target);

        Debug.Log("[MPPM T07] PASS: 벽 차단으로 인한 투사체 소멸 및 피해 0, 벽 제거 후 정상 적중 확인.");
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================
    private NetworkEnemyAuthority_MirrorTest SpawnEnemy(Vector3 position, Action<WBH_EnemyInfo> configure = null)
    {
        GameObject prefab = manager.spawnPrefabs.FirstOrDefault(p => p != null && p.name == "Normal_Melee_MirrorTest");
        if (prefab == null)
            throw new InvalidOperationException("Normal_Melee_MirrorTest 프리팹을 찾을 수 없습니다.");

        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        var authority = go.GetComponent<NetworkEnemyAuthority_MirrorTest>();
        WBH_EnemyInfo info = authority.EnemyInfo.Clone();
        info.maxHP = 1000f;
        info.attack = 0f;
        info.defense = 0f;
        info.moveSpeed = 0f;
        info.exp = 0;
        info.credit = 0;
        // NetworkEnemyAuthority의 데이터 변경 계약에 맞춰 모든 fixture 설정을 Spawn 전에 적용합니다.
        configure?.Invoke(info);
        authority.ServerSetEnemyInfo(info);
        NetworkServer.Spawn(go);
        go.GetComponent<WBH_EnemyPattern_MirrorTest>().StopServer();
        return authority;
    }

    private void AddDuplicateCollider(GameObject targetGo)
    {
        var duplicate = new GameObject("DuplicateCollider");
        duplicate.layer = 10;
        duplicate.transform.SetParent(targetGo.transform, false);
        duplicate.transform.localPosition = Vector3.up;
        duplicate.AddComponent<BoxCollider>().size = new Vector3(0.8f, 1.5f, 0.8f);
        Physics.SyncTransforms();
    }

    private void DestroyEnemy(NetworkEnemyAuthority_MirrorTest target)
    {
        if (target != null && target.gameObject != null)
        {
            NetworkServer.Destroy(target.gameObject);
        }
    }

    private void SpawnTestProjectile(PlayerContext owner, Vector3 origin, Vector3 dir, uint attackId)
    {
        GameObject projPrefab = manager.spawnPrefabs.FirstOrDefault(p => p != null && p.name == "NormalEnemyProjectile_MirrorTest");
        if (projPrefab == null)
            throw new InvalidOperationException("NormalEnemyProjectile_MirrorTest 프리팹을 찾을 수 없습니다.");

        GameObject projGo = Instantiate(projPrefab, origin, Quaternion.LookRotation(dir));
        var projectile = projGo.GetComponent<NetworkEnemyProjectile_MirrorTest>();
        projectile.InitializePlayerServer(owner, GunnerWeaponType.Rifle, "test_rifle", ElementType.Fire, dir, 15f, 20f, origin + dir * 10f, 1f, attackId);
        NetworkServer.Spawn(projGo);
    }

    private void EquipItemFixture(PlayerContext player, ItemInstance item)
    {
        var eqDict = (Dictionary<EquipSlotType, InventoryItem>)typeof(EquipmentSystem)
            .GetField("equippedItems", PrivateInstance)
            .GetValue(player.Equipment);
        eqDict[EquipSlotType.Weapon] = new InventoryItem(item);
    }

    private void UnequipItemFixture(PlayerContext player)
    {
        var eqDict = (Dictionary<EquipSlotType, InventoryItem>)typeof(EquipmentSystem)
            .GetField("equippedItems", PrivateInstance)
            .GetValue(player.Equipment);
        eqDict.Remove(EquipSlotType.Weapon);
        player.Buffs.ClearAllBuffs();
    }

    private int GetBuffStack(PlayerContext player, UniqueEffectSO effect)
    {
        var buff = player.Buffs.ActiveBuffs.FirstOrDefault(b => ReferenceEquals(b.source, effect));
        return buff?.stackCount ?? 0;
    }

    private void Check(bool condition, string message)
    {
        if (!condition)
        {
            Fail(message);
            throw new InvalidOperationException("[MirrorMppmCombatRunner FAIL] " + message);
        }
        checkCount++;
        Debug.Log($"<color=cyan>[MPPM CHECK #{checkCount}]</color> {message}");
    }

    private bool Failed => !string.IsNullOrEmpty(FailureReason);

    private void Fail(string reason)
    {
        if (Completed)
            return;

        FailureReason = reason;
        Completed = true;
        Passed = false;
        Debug.LogError($"<color=red><b>[MirrorMppmCombatRunner FAILED] {reason}</b></color>");
        StartCoroutine(StopMppmSessionAfterDelay());
    }

    private IEnumerator StopMppmSessionAfterDelay()
    {
        // 실패 로그가 Main Editor에 전달될 시간을 준 뒤 가상 플레이어까지 자동 정리합니다.
        yield return new WaitForSecondsRealtime(2f);
        StopMppmSession();
    }

    private void StopMppmSession()
    {
#if UNITY_EDITOR
        try
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "UnityEditor.MultiplayerModule");
            var runnerType = asm?.GetType("Unity.Multiplayer.PlayMode.Editor.ScenarioRunner");
            runnerType?.GetMethod("StopScenario", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[MirrorMppmCombatRunner] StopScenario invocation: " + ex.Message);
            UnityEditor.EditorApplication.isPlaying = false;
        }
#endif
    }
}

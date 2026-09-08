using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

public enum MirrorTestSessionPhase : byte
{
    Waiting = 0,
    Playing = 1,
    Completed = 2,
    Resetting = 3,
}

/// <summary>
/// Act1과 비슷한 소규모 일반 적 웨이브를 서버에서만 생성하는 Mirror 테스트 Spawner다.
/// <para>원본 SpawnManager의 임의 플레이어 <c>Find</c>와 로컬 풀을 실행하지 않고,
/// 생성·등록·제거를 <c>Instantiate → NetworkServer.Spawn → NetworkServer.Destroy</c>로 고정한다.</para>
/// <para>6-A에서는 서버 시작과 동시에 적을 생성하지 않는다. 호환 확인과 Player 생성이 끝난 Client의
/// 시작 요청을 서버가 한 번 승인해야 <c>Waiting → Playing</c>으로 바뀌며, 마지막 웨이브가 끝나면
/// <c>Completed</c>가 된다. 모든 플레이어 이탈 뒤에는 NetworkManager의 기존 프로세스 재시작이
/// <c>Resetting → Waiting</c> 초기화를 담당한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class NetworkEnemyWaveSpawner_MirrorTest : NetworkBehaviour
{
    [SerializeField] private NetworkEnemyAuthority_MirrorTest meleePrefab;
    [SerializeField] private NetworkEnemyAuthority_MirrorTest rangedPrefab;
    [SerializeField] private NetworkEnemyAuthority_MirrorTest bossPrefab;
    [SerializeField] private WBH_EnemyDataProvider enemyDataProvider;
    [SerializeField, Min(1)] private int waveCount = 2;
    [SerializeField, Min(1)] private int enemiesPerWave = 5;
    [SerializeField, Min(0f)] private float initialDelay = 1.5f;
    [SerializeField, Min(0f)] private float spawnInterval = 0.35f;
    [SerializeField, Min(0f)] private float nextWaveDelay = 2.5f;
    [SerializeField, Min(1f)] private float spawnRadius = 8f;

    [SyncVar] private int currentWave;
    [SyncVar] private uint totalSpawnCount;
    [SyncVar] private uint completedWaveCount;
    [SyncVar] private MirrorTestSessionPhase sessionPhase;
    [SyncVar] private uint sessionStateRevision;
    [SyncVar] private bool bossSession;

    private readonly List<NetworkEnemyAuthority_MirrorTest> aliveEnemies = new();
    private Coroutine waveRoutine;
    private bool waveSpawnFinished;
    private int activeWaveCount;
    private WBH_EnemyInfo[] activeEnemyInfos;

    public int CurrentWave => currentWave;
    public int AliveEnemyCount
    {
        get
        {
            int count = 0;
            foreach (NetworkEnemyAuthority_MirrorTest enemy in aliveEnemies)
            {
                if (enemy != null && !enemy.IsDead)
                    count++;
            }

            return count;
        }
    }
    public uint TotalSpawnCount => totalSpawnCount;
    public uint CompletedWaveCount => completedWaveCount;
    public MirrorTestSessionPhase SessionPhase => sessionPhase;
    public uint SessionStateRevision => sessionStateRevision;
    public bool IsBossSession => bossSession;

    private void Awake()
    {
        RegisterBossPrefabForClient();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        sessionPhase = MirrorTestSessionPhase.Waiting;
        sessionStateRevision = 0;
        activeWaveCount = waveCount;
        bossSession = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RegisterBossPrefabForClient();
    }

    public override void OnStopServer()
    {
        if (waveRoutine != null)
            StopCoroutine(waveRoutine);
        aliveEnemies.Clear();
        base.OnStopServer();
    }

    private void Update()
    {
        if (!isServer || !waveSpawnFinished || waveRoutine != null)
            return;

        RemoveFinishedEnemies();
        if (aliveEnemies.Count > 0)
            return;

        if (currentWave < activeWaveCount)
        {
            completedWaveCount++;
            waveSpawnFinished = false;
            waveRoutine = StartCoroutine(SpawnWaveAfter(nextWaveDelay));
            return;
        }

        if (bossSession)
        {
            MirrorTestNetworkManager manager = NetworkManager.singleton as MirrorTestNetworkManager;
            if (manager == null || !manager.ServerTryCompletePendingBossWithoutSceneChange())
            {
                Debug.LogError(
                    "[NetworkEnemyWaveSpawner_MirrorTest] Final boss clear snapshot could not be published.",
                    this);
                return;
            }
        }

        completedWaveCount++;
        waveSpawnFinished = false;
        sessionPhase = MirrorTestSessionPhase.Completed;
        sessionStateRevision++;
        Debug.Log(
            $"[NetworkEnemyWaveSpawner_MirrorTest] 전투 세션 완료 | 웨이브={currentWave}, 누적 생성={totalSpawnCount}");
    }

    /// <summary>
    /// 서버가 대기 중인 전투 세션을 한 번만 시작한다.
    /// 중복 버튼이나 여러 Client의 동시 요청은 첫 요청만 상태를 바꾸고 나머지는 false를 반환한다.
    /// </summary>
    [Server]
    public bool ServerTryStartSession()
    {
        if (!CanStartSession(sessionPhase, waveRoutine != null))
            return false;

        MirrorTestNetworkManager manager = NetworkManager.singleton as MirrorTestNetworkManager;
        StageNodeSaveData pendingNode = null;
        if (manager != null)
            manager.TryGetPendingStageNode(out pendingNode);

        bossSession = pendingNode?.type == StageNodeType.Boss;
        activeWaveCount = ResolveWaveCount(pendingNode, waveCount);
        if (bossSession && bossPrefab == null)
        {
            Debug.LogError(
                "[NetworkEnemyWaveSpawner_MirrorTest] Boss 노드용 네트워크 Prefab이 비어 있습니다.",
                this);
            return false;
        }

        if (!TryPrepareEnemyInfos(manager, pendingNode))
            return false;

        sessionPhase = MirrorTestSessionPhase.Playing;
        sessionStateRevision++;
        waveRoutine = StartCoroutine(SpawnWaveAfter(initialDelay));
        return true;
    }

    /// <summary>
    /// 마지막 플레이어 이탈 뒤 전용 서버 프로세스가 종료되기 직전 상태다.
    /// 실제 적·상점·드롭 정리는 실행 스크립트가 새 프로세스를 시작하며 수행하므로
    /// 여기서 각 시스템에 중복 초기화 코드를 추가하지 않는다.
    /// </summary>
    [Server]
    public void ServerMarkSessionResetting()
    {
        if (sessionPhase == MirrorTestSessionPhase.Resetting)
            return;

        sessionPhase = MirrorTestSessionPhase.Resetting;
        sessionStateRevision++;
    }

    [Server]
    private bool TryPrepareEnemyInfos(MirrorTestNetworkManager manager, StageNodeSaveData pendingNode)
    {
        if (enemyDataProvider == null)
        {
            Debug.LogError("[NetworkEnemyWaveSpawner_MirrorTest] EnemyDataProvider가 연결되지 않았습니다.", this);
            return false;
        }

        // 현재 Mirror 세션은 normal 난이도이며, 재접속 예약을 포함한 출발 인원으로 배율을 고정한다.
        var context = new WBH_EnemyStatContext(
            Mathf.Max(1, pendingNode?.floor ?? 1), "normal",
            Mathf.Max(1, manager?.ServerRoster.Members.Count ?? 1));
        var prefabs = bossSession ? new[] { bossPrefab } : new[] { meleePrefab, rangedPrefab };
        activeEnemyInfos = new WBH_EnemyInfo[prefabs.Length];
        for (int index = 0; index < prefabs.Length; index++)
        {
            string enemyId = prefabs[index]?.EnemyInfo?.enemyId;
            if (!enemyDataProvider.TryCreateEnemyInfo(enemyId, context, out activeEnemyInfos[index]))
            {
                Debug.LogError($"[NetworkEnemyWaveSpawner_MirrorTest] 적 데이터 생성 실패: {enemyId}", this);
                return false;
            }
        }

        return true;
    }

    [Server]
    private IEnumerator SpawnWaveAfter(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        currentWave++;
        waveSpawnFinished = false;

        int spawnCount = ResolveSpawnCount(bossSession, enemiesPerWave);
        for (int index = 0; index < spawnCount; index++)
        {
            NetworkEnemyAuthority_MirrorTest prefab = bossSession
                ? bossPrefab
                : index % 2 == 0 ? meleePrefab : rangedPrefab;
            if (prefab == null)
            {
                Debug.LogError("[NetworkEnemyWaveSpawner_MirrorTest] 전투 적 Prefab이 비어 있습니다.", this);
                continue;
            }

            Vector3 position = ResolveSpawnPosition(index, spawnCount);
            NetworkEnemyAuthority_MirrorTest enemy = Instantiate(prefab, position, Quaternion.identity);
            enemy.ServerSetEnemyInfo(activeEnemyInfos[index % activeEnemyInfos.Length]);
            NetworkServer.Spawn(enemy.gameObject);
            aliveEnemies.Add(enemy);
            totalSpawnCount++;

            if (spawnInterval > 0f && index < spawnCount - 1)
                yield return new WaitForSeconds(spawnInterval);
        }

        waveSpawnFinished = true;
        waveRoutine = null;
    }

    private Vector3 ResolveSpawnPosition(int index, int spawnCount)
    {
        float angle = spawnCount > 0 ? 360f * index / spawnCount : 0f;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * spawnRadius;
        Vector3 candidate = transform.position + offset;
        return NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas)
            ? hit.position
            : candidate;
    }

    private void RemoveFinishedEnemies()
    {
        for (int index = aliveEnemies.Count - 1; index >= 0; index--)
        {
            NetworkEnemyAuthority_MirrorTest enemy = aliveEnemies[index];
            if (enemy == null || enemy.IsDead)
                aliveEnemies.RemoveAt(index);
        }
    }

    private static bool CanStartSession(
        MirrorTestSessionPhase phase,
        bool hasRunningRoutine)
    {
        return phase == MirrorTestSessionPhase.Waiting && !hasRunningRoutine;
    }

    private static int ResolveWaveCount(
        StageNodeSaveData pendingNode,
        int configuredMaximumWaveCount)
    {
        int maximumWaveCount = Mathf.Max(1, configuredMaximumWaveCount);
        if (pendingNode == null)
            return maximumWaveCount;
        if (pendingNode.type == StageNodeType.Boss)
            return 1;

        int floor = Mathf.Max(1, pendingNode.floor);
        int floorWaveCount = 1 + (floor - 1) / 3;
        if (pendingNode.type == StageNodeType.Elite)
            floorWaveCount++;

        return Mathf.Clamp(floorWaveCount, 1, maximumWaveCount);
    }

    private static int ResolveSpawnCount(bool isBossSession, int configuredEnemyCount)
    {
        return isBossSession ? 1 : Mathf.Max(1, configuredEnemyCount);
    }

    private void RegisterBossPrefabForClient()
    {
        if (!NetworkClient.active || bossPrefab == null ||
            !bossPrefab.TryGetComponent(out NetworkIdentity identity) ||
            identity.assetId == 0)
        {
            return;
        }

        if (!NetworkClient.GetPrefab(identity.assetId, out _))
            NetworkClient.RegisterPrefab(bossPrefab.gameObject);

        bossPrefab.RegisterClientPrefabs();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 대기 상태의 첫 요청만 시작할 수 있고 Playing·Completed·Resetting 또는 이미 실행 중인
    /// Coroutine에서는 중복 시작할 수 없는지 확인하는 최소 회귀 검사다.
    /// </summary>
    [ContextMenu("Mirror 테스트/세션 시작 규칙 검사")]
    private void ValidateSessionStartRule()
    {
        Debug.Assert(CanStartSession(MirrorTestSessionPhase.Waiting, false));
        Debug.Assert(!CanStartSession(MirrorTestSessionPhase.Waiting, true));
        Debug.Assert(!CanStartSession(MirrorTestSessionPhase.Playing, false));
        Debug.Assert(!CanStartSession(MirrorTestSessionPhase.Completed, false));
        Debug.Assert(!CanStartSession(MirrorTestSessionPhase.Resetting, false));
        Debug.Assert(ResolveWaveCount(
            new StageNodeSaveData { floor = 11, type = StageNodeType.Boss }, 3) == 1);
        Debug.Assert(ResolveSpawnCount(true, 8) == 1);
        Debug.Assert(ResolveWaveCount(
            new StageNodeSaveData { floor = 1, type = StageNodeType.Battle }, 3) == 1);
        Debug.Assert(ResolveWaveCount(
            new StageNodeSaveData { floor = 5, type = StageNodeType.Battle }, 3) == 2);
        Debug.Assert(ResolveWaveCount(
            new StageNodeSaveData { floor = 6, type = StageNodeType.Elite }, 3) == 3);
        Debug.Assert(ResolveWaveCount(
            new StageNodeSaveData { floor = 9, type = StageNodeType.Elite }, 3) == 3);
        Debug.Assert(ResolveWaveCount(null, 3) == 3);
        Debug.Assert(ResolveSpawnCount(false, 8) == 8);
        Debug.Log("[NetworkEnemyWaveSpawner_MirrorTest] 세션 시작 규칙 검사 통과");
    }
#endif
}

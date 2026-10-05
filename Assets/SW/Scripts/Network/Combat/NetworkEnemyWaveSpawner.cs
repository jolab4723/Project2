using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

public enum MirrorSessionPhase : byte
{
    Waiting = 0,
    Playing = 1,
    Completed = 2,
    Resetting = 3,
}

/// <summary>
/// 정식 씬의 공유 웨이브 설정으로 적을 서버에서만 생성한다.
/// <para>원본 SpawnManager의 임의 플레이어 <c>Find</c>와 로컬 풀을 실행하지 않고,
/// 생성·등록·제거를 <c>Instantiate → NetworkServer.Spawn → NetworkServer.Destroy</c>로 고정한다.</para>
/// <para>6-A에서는 서버 시작과 동시에 적을 생성하지 않는다. 호환 확인과 Player 생성이 끝난 Client의
/// 시작 요청을 서버가 한 번 승인해야 <c>Waiting → Playing</c>으로 바뀌며, 마지막 웨이브가 끝나면
/// <c>Completed</c>가 된다. 모든 플레이어 이탈 뒤에는 NetworkManager의 기존 프로세스 재시작이
/// <c>Resetting → Waiting</c> 초기화를 담당한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class NetworkEnemyWaveSpawner : NetworkBehaviour
{
    [SerializeField] private WBH_EnemyDataProvider enemyDataProvider;
    [SerializeField, Min(0f)] private float initialDelay = 1.5f;
    [SerializeField] private MirrorBossIntro bossIntro;
    [Header("정식 씬의 공유 웨이브")]
    [SerializeField] private YJ_StageManager stageManager;
    [SerializeField] private WBH_EnemySpawnManager authoredWaves;
    [SerializeField] private WBH_EnemySpawnArea authoredArea;
    [SerializeField] private NetworkEnemyAuthority[] authoredEnemyPrefabs;
    private List<(NetworkEnemyAuthority prefab, WBH_EnemyInfo info)>[] preparedWaves;

    [SyncVar] private int currentWave;
    [SyncVar] private uint totalSpawnCount;
    [SyncVar] private uint completedWaveCount;
    [SyncVar] private MirrorSessionPhase sessionPhase;
    [SyncVar] private uint sessionStateRevision;
    [SyncVar] private bool bossSession;

    private readonly List<NetworkEnemyAuthority> aliveEnemies = new();
    private Coroutine waveRoutine;
    private bool waveSpawnFinished;
    // SW 수정: 생성 직전 검사에 실패한 웨이브를 다시 시도할 시각. 0이면 대기 중인 재시도가 없다.
    private double spawnRetryAt;
    private const float SpawnRetrySeconds = 1f;
    [SyncVar] private int activeWaveCount;

    public int CurrentWave => currentWave;
    public int TotalWaveCount => activeWaveCount;
    public int AliveEnemyCount
    {
        get
        {
            int count = 0;
            foreach (NetworkEnemyAuthority enemy in aliveEnemies)
            {
                if (enemy != null && !enemy.IsDead)
                    count++;
            }

            return count;
        }
    }
    public uint TotalSpawnCount => totalSpawnCount;
    public uint CompletedWaveCount => completedWaveCount;
    public MirrorSessionPhase SessionPhase => sessionPhase;
    public uint SessionStateRevision => sessionStateRevision;
    public bool IsBossSession => bossSession;

    private void Awake()
    {
        RegisterEnemyPrefabsForClient();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        sessionPhase = MirrorSessionPhase.Waiting;
        sessionStateRevision = 0;
        activeWaveCount = 0;
        bossSession = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RegisterEnemyPrefabsForClient();
    }

    public override void OnStopServer()
    {
        if (waveRoutine != null)
            StopCoroutine(waveRoutine);
        spawnRetryAt = 0d;
        aliveEnemies.Clear();
        base.OnStopServer();
    }

    private void Update()
    {
        if (isServer && spawnRetryAt > 0d && waveRoutine == null && sessionPhase == MirrorSessionPhase.Playing &&
            Time.timeAsDouble >= spawnRetryAt)
        {
            spawnRetryAt = 0d;
            waveRoutine = StartCoroutine(SpawnAuthoredWave());
            return;
        }

        if (!isServer || !waveSpawnFinished || waveRoutine != null)
            return;

        RemoveFinishedEnemies();
        if (aliveEnemies.Count > 0)
            return;

        if (currentWave < activeWaveCount)
        {
            completedWaveCount++;
            waveSpawnFinished = false;
            waveRoutine = StartCoroutine(SpawnAuthoredWave());
            return;
        }

        if (bossSession)
        {
            MirrorNetworkManager manager = NetworkManager.singleton as MirrorNetworkManager;
            if (manager == null || !manager.ServerTryCompletePendingBossWithoutSceneChange())
            {
                Debug.LogError(
                    "[NetworkEnemyWaveSpawner] Final boss clear snapshot could not be published.",
                    this);
                return;
            }
        }

        completedWaveCount++;
        waveSpawnFinished = false;
        sessionPhase = MirrorSessionPhase.Completed;
        sessionStateRevision++;
        Debug.Log(
            $"[NetworkEnemyWaveSpawner] 전투 세션 완료 | 웨이브={currentWave}, 누적 생성={totalSpawnCount}");
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

        MirrorNetworkManager manager = NetworkManager.singleton as MirrorNetworkManager;
        if (manager == null || !manager.IsPartyGameplayReady) return false;
        if (!manager.TryGetPendingStageNode(out var pendingNode) ||
            !TryPrepareAuthoredWaves(manager, pendingNode))
            return false;
        bool startingBossSession = pendingNode.type == StageNodeType.Boss;
        if (startingBossSession && bossIntro != null && !bossIntro.ServerBegin(manager, initialDelay))
            return false;
        bossSession = startingBossSession;
        sessionPhase = MirrorSessionPhase.Playing;
        sessionStateRevision++;
        waveRoutine = StartCoroutine(SpawnAuthoredWave());
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
        if (sessionPhase == MirrorSessionPhase.Resetting)
            return;

        sessionPhase = MirrorSessionPhase.Resetting;
        sessionStateRevision++;
    }

    /// <summary>
    /// WJ 이우진 추가(2026-10-01): 노드의 층 번호는 액트마다 1부터라, Run Snapshot의 액트와 함께
    /// FloorStatScaleTable.ToContextFloor로 층 배율 표의 누적 번호로 바꾼다(싱글 YJ_StageManager와 같은 규칙).
    /// 이전에는 node.floor를 그대로 넘겨 Act2·3에서도 Act1 구간 배율을 받았다. 스냅샷이 없으면 Act1로 본다.
    /// </summary>
    private static int ResolveContextFloor(MirrorNetworkManager manager, StageNodeSaveData node)
    {
        int floor = Mathf.Max(1, node?.floor ?? 1);
        int act = manager != null && manager.TryGetRunSnapshot(out StageMapSaveData snapshot) && snapshot != null
            ? (int)snapshot.act
            : 1;
        return EnemySystem.FloorStatScaleTable.ToContextFloor(Mathf.Max(1, act), floor);
    }

    [Server]
    private bool TryPrepareAuthoredWaves(MirrorNetworkManager manager, StageNodeSaveData node)
    {
        if (manager == null || stageManager == null || authoredWaves == null || authoredArea == null ||
            enemyDataProvider == null || node == null)
        {
            Debug.LogError("[NetworkEnemyWaveSpawner] 정식 웨이브 설정이 누락되어 전투 출발을 거절합니다. " +
                "StageManager, SpawnManager, SpawnArea, EnemyDataProvider와 선택 노드를 확인하세요.", this);
            return false;
        }
        if (preparedWaves != null) return true;
        if (!manager.TryGetRunSnapshot(out StageMapSaveData snapshot) || !stageManager.TryPrepareSessionWaves(snapshot))
            return false;
        var byId = new Dictionary<string, NetworkEnemyAuthority>();
        foreach (var prefab in authoredEnemyPrefabs ?? System.Array.Empty<NetworkEnemyAuthority>())
        {
            if (prefab?.EnemyInfo == null || string.IsNullOrEmpty(prefab.EnemyInfo.enemyId) ||
                !byId.TryAdd(prefab.EnemyInfo.enemyId, prefab)) return false;
        }
        // 현재 Mirror 세션은 normal 난이도이며, 재접속 예약을 포함한 출발 인원으로 배율을 고정한다.
        var context = new WBH_EnemyStatContext(ResolveContextFloor(manager, node), "normal", Mathf.Max(1, manager.ServerRoster.Members.Count));
        if (authoredWaves.WaveCount <= 0)
        {
            Debug.LogError("[NetworkEnemyWaveSpawner] 정식 웨이브가 비어 있어 전투 출발을 거절합니다.", this);
            return false;
        }
        var waves = new List<(NetworkEnemyAuthority, WBH_EnemyInfo)>[authoredWaves.WaveCount];
        for (int wave = 0; wave < waves.Length; wave++)
        {
            if (!authoredArea.TryGetSpawnPoint(wave, out Transform point) ||
                !NavMesh.SamplePosition(point.position, out _, 2f, NavMesh.AllAreas)) return false;
            waves[wave] = new();
            foreach (var entry in authoredWaves.GetConfiguredWave(wave).enemies)
                for (int i = 0; i < entry.count; i++)
                {
                    var definition = authoredArea.ChooseEnemy(entry.grade);
                    if (definition == null || !byId.TryGetValue(definition.enemyId, out var prefab) ||
                        !enemyDataProvider.TryCreateEnemyInfo(definition.enemyId, context, out var info))
                    {
                        Debug.LogError($"정식 웨이브의 네트워크 적 연결 실패: {definition?.enemyId}", this);
                        return false;
                    }
                    waves[wave].Add((prefab, info));
                }
        }
        preparedWaves = waves;
        activeWaveCount = waves.Length;
        return activeWaveCount > 0;
    }

    [Server]
    public bool ServerPrepareConfiguration(MirrorNetworkManager manager)
    {
        return TryPrepareAuthoredWaves(manager,
            manager != null && manager.TryGetPendingStageNode(out var node) ? node : null);
    }

    [Server]
    private IEnumerator SpawnAuthoredWave()
    {
        if (currentWave == 0 && bossSession && bossIntro != null)
            while (!bossIntro.IsComplete) yield return null;
        // 정식 SpawnManager와 동일하게 웨이브별 한 포인트에서 확정 수량을 생성합니다.
        if (!authoredArea.TryGetSpawnPoint(currentWave, out Transform point) ||
            !NavMesh.SamplePosition(point.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            // SW 수정: 이 웨이브의 적은 아직 하나도 만들지 않았으므로 같은 웨이브만 일정 간격으로 다시 시도한다.
            // 웨이브 번호·완료 수를 올리지 않아 중복 생성이나 건너뛰기가 없다.
            // ponytail: 생성 지점이 끝내 복구되지 않으면 Playing에서 계속 재시도한다. 운영 이탈 처리가 필요해지면 실패 횟수 상한을 둔다.
            Debug.LogError($"정식 웨이브 {currentWave + 1} 생성 지점의 NavMesh가 없어 {SpawnRetrySeconds}초 뒤 다시 시도합니다.", this);
            spawnRetryAt = Time.timeAsDouble + SpawnRetrySeconds;
            waveRoutine = null;
            yield break;
        }
        int waveIndex = currentWave++;
        waveSpawnFinished = false;
        foreach (var entry in preparedWaves[waveIndex])
        {
            var enemy = Instantiate(entry.prefab, hit.position, point.rotation);
            enemy.ServerSetEnemyInfo(entry.info);
            if (enemy.TryGetComponent<WBH_BossMinionSpawner>(out var minions))
                minions.ExternalSpawn = ServerSpawnMinion;
            NetworkServer.Spawn(enemy.gameObject);
            aliveEnemies.Add(enemy);
            totalSpawnCount++;
        }
        yield return null;
        waveSpawnFinished = true;
        waveRoutine = null;
    }

    [Server]
    private bool ServerSpawnMinion(string enemyId, Vector3 position, Quaternion rotation, Transform target)
    {
        var manager = NetworkManager.singleton as MirrorNetworkManager;
        if (sessionPhase != MirrorSessionPhase.Playing || manager == null ||
            !manager.TryGetPendingStageNode(out var node)) return false;
        var prefab = System.Array.Find(authoredEnemyPrefabs, p => p != null && p.EnemyInfo?.enemyId == enemyId);
        // Act2 보스 소환 자폭병처럼 데이터(SO)만 분리된 적은 같은 등급·유형 계열의 네트워크 외형을 재사용합니다.
        // 전용 외형이 필요해지면 네트워크 프리팹을 추가하고 이 대체 조회를 제거합니다.
        if (prefab == null && enemyId != null && enemyId.LastIndexOf('.') > 0)
        {
            string family = enemyId.Substring(0, enemyId.LastIndexOf('.') + 1);
            prefab = System.Array.Find(authoredEnemyPrefabs, p => p != null && p.EnemyInfo?.enemyId != null && p.EnemyInfo.enemyId.StartsWith(family));
        }
        var context = new WBH_EnemyStatContext(ResolveContextFloor(manager, node), "normal", Mathf.Max(1, manager.ServerRoster.Members.Count));
        if (prefab == null || !enemyDataProvider.TryCreateEnemyInfo(enemyId, context, out var info)) return false;
        var enemy = Instantiate(prefab, position, rotation);
        enemy.ServerSetEnemyInfo(info);
        NetworkServer.Spawn(enemy.gameObject);
        aliveEnemies.Add(enemy);
        totalSpawnCount++;
        return true;
    }

    private void RemoveFinishedEnemies()
    {
        for (int index = aliveEnemies.Count - 1; index >= 0; index--)
        {
            NetworkEnemyAuthority enemy = aliveEnemies[index];
            if (enemy == null || enemy.IsDead)
                aliveEnemies.RemoveAt(index);
        }
    }

    private static bool CanStartSession(
        MirrorSessionPhase phase,
        bool hasRunningRoutine)
    {
        return phase == MirrorSessionPhase.Waiting && !hasRunningRoutine;
    }

    internal void RegisterEnemyPrefabsForClient()
    {
        if (NetworkClient.active && authoredEnemyPrefabs != null)
            foreach (var prefab in authoredEnemyPrefabs)
            {
                if (prefab == null || !prefab.TryGetComponent(out NetworkIdentity registered) || registered.assetId == 0) continue;
                if (!NetworkClient.GetPrefab(registered.assetId, out _)) NetworkClient.RegisterPrefab(prefab.gameObject);
                prefab.RegisterClientPrefabs();
            }
    }
}

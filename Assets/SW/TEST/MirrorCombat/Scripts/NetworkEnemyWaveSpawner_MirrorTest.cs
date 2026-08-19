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

    private readonly List<NetworkEnemyAuthority_MirrorTest> aliveEnemies = new();
    private Coroutine waveRoutine;
    private bool waveSpawnFinished;

    public int CurrentWave => currentWave;
    public int AliveEnemyCount => CalculateAliveEnemyCount();
    public uint TotalSpawnCount => totalSpawnCount;
    public uint CompletedWaveCount => completedWaveCount;
    public MirrorTestSessionPhase SessionPhase => sessionPhase;
    public uint SessionStateRevision => sessionStateRevision;

    public override void OnStartServer()
    {
        base.OnStartServer();
        sessionPhase = MirrorTestSessionPhase.Waiting;
        sessionStateRevision = 0;
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

        completedWaveCount++;
        waveSpawnFinished = false;
        if (currentWave < waveCount)
        {
            waveRoutine = StartCoroutine(SpawnWaveAfter(nextWaveDelay));
            return;
        }

        sessionPhase = MirrorTestSessionPhase.Completed;
        sessionStateRevision++;
        Debug.Log(
            $"[NetworkEnemyWaveSpawner_MirrorTest] 전투 세션 완료 | " +
            $"웨이브={currentWave}, 누적 생성={totalSpawnCount}");
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
    private IEnumerator SpawnWaveAfter(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        currentWave++;
        waveSpawnFinished = false;

        for (int index = 0; index < enemiesPerWave; index++)
        {
            NetworkEnemyAuthority_MirrorTest prefab = index % 2 == 0 ? meleePrefab : rangedPrefab;
            if (prefab == null)
            {
                Debug.LogError("[NetworkEnemyWaveSpawner_MirrorTest] 일반 적 Prefab이 비어 있습니다.", this);
                continue;
            }

            Vector3 position = ResolveSpawnPosition(index);
            NetworkEnemyAuthority_MirrorTest enemy = Instantiate(prefab, position, Quaternion.identity);
            NetworkServer.Spawn(enemy.gameObject);
            aliveEnemies.Add(enemy);
            totalSpawnCount++;

            if (spawnInterval > 0f && index < enemiesPerWave - 1)
                yield return new WaitForSeconds(spawnInterval);
        }

        waveSpawnFinished = true;
        waveRoutine = null;
    }

    private Vector3 ResolveSpawnPosition(int index)
    {
        float angle = enemiesPerWave > 0 ? 360f * index / enemiesPerWave : 0f;
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

    private int CalculateAliveEnemyCount()
    {
        int count = 0;
        foreach (NetworkEnemyAuthority_MirrorTest enemy in aliveEnemies)
        {
            if (enemy != null && !enemy.IsDead)
                count++;
        }

        return count;
    }

    private static bool CanStartSession(
        MirrorTestSessionPhase phase,
        bool hasRunningRoutine)
    {
        return phase == MirrorTestSessionPhase.Waiting && !hasRunningRoutine;
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
        Debug.Log("[NetworkEnemyWaveSpawner_MirrorTest] 세션 시작 규칙 검사 통과");
    }
#endif
}

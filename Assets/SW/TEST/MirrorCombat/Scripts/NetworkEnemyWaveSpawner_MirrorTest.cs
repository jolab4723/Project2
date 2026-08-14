using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Act1과 비슷한 소규모 일반 적 웨이브를 서버에서만 생성하는 Mirror 테스트 Spawner다.
/// <para>원본 SpawnManager의 임의 플레이어 <c>Find</c>와 로컬 풀을 실행하지 않고,
/// 생성·등록·제거를 <c>Instantiate → NetworkServer.Spawn → NetworkServer.Destroy</c>로 고정한다.</para>
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
    [SerializeField] private bool spawnAutomatically = true;

    [SyncVar] private int currentWave;
    [SyncVar] private uint totalSpawnCount;
    [SyncVar] private uint completedWaveCount;

    private readonly List<NetworkEnemyAuthority_MirrorTest> aliveEnemies = new();
    private Coroutine waveRoutine;
    private bool waveSpawnFinished;

    public int CurrentWave => currentWave;
    public int AliveEnemyCount => CountAliveEnemies();
    public uint TotalSpawnCount => totalSpawnCount;
    public uint CompletedWaveCount => completedWaveCount;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (spawnAutomatically)
            waveRoutine = StartCoroutine(SpawnWaveAfter(initialDelay));
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
            waveRoutine = StartCoroutine(SpawnWaveAfter(nextWaveDelay));
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

    private int CountAliveEnemies()
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

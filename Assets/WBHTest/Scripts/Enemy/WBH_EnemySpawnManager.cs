using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WBH_EnemyPoolManager))]
[RequireComponent(typeof(WBH_EnemyDataProvider))]
public class WBH_EnemySpawnManager : MonoBehaviour
{
    [System.Serializable]
    public class GradeCount
    {
        public EnemyGrade grade;
        [Min(0)] public int count;
    }


    [SerializeField] private WBH_WaveSetSO defaultWaveSet;

    [SerializeField] private WBH_EnemySpawnArea spawnArea; 
    [SerializeField] private Transform player;

    [SerializeField] private WBH_EnemyDataProvider enemyDataProvider;
    [SerializeField] private WBH_EnemyStatContext statContext = new WBH_EnemyStatContext(1, "normal", 1);

    [SerializeField] private WBH_HighEnemyHpbarView highEnemyView;
    [SerializeField] private WBH_EffectSpawner effectSpawner;
    [SerializeField] private WBH_ProjectileSpawner projectileSpawner;

    private WBH_EnemyPoolManager enemyPool;
    private WBH_FloatTextPoolManager damagePool;
    private PlayerWallet wallet;
    private WBH_WaveSetSO activeWaveSet;
    private WBH_WaveData[] activeWaves;
    private readonly Queue<EnemyGrade> pendingSpawns = new();

    private int currentWave = -1;
    private int aliveEnemyCount;
    private bool waveInProgress;
    private bool isSpawningWave;
    private bool spawnAreaInitialized;

    public event Action WaveCompleted;

    public WBH_WaveSetSO ActiveWaveSet => activeWaveSet;
    public int CurrentWaveIndex => currentWave;
    public bool HasUsableWaveSet => activeWaves != null && activeWaves.Length > 0;
    public int WaveCount => activeWaves?.Length ?? 0;
    public bool HasNextWave => currentWave + 1 < WaveCount;
    public bool AllwavesCompleted => HasUsableWaveSet && currentWave == WaveCount - 1 && !waveInProgress && !isSpawningWave && pendingSpawns.Count == 0 && aliveEnemyCount == 0;


    private void Awake()
    {
        enemyPool = GetComponent<WBH_EnemyPoolManager>();
        enemyDataProvider = GetComponent<WBH_EnemyDataProvider>();
        damagePool = FindFirstObjectByType<WBH_FloatTextPoolManager>();

        highEnemyView = FindFirstObjectByType<WBH_HighEnemyHpbarView>();
    }

    private void OnEnable()
    {
        WBH_EnemyController.OnEnemyDead += EnemyDead;
    }
    private void OnDisable()
    {
        WBH_EnemyController.OnEnemyDead -= EnemyDead;
    }

    public bool TrySetPlayer(T_PlayerController controller)
    {
        if (controller == null || !controller.isActiveAndEnabled)
        {
            Log.Error("적 스폰 초기화 실패: 활성 플레이어가 필요합니다.");
            return false;
        }
        if (spawnAreaInitialized || waveInProgress || isSpawningWave)
        {
            Log.Error("적 스폰 초기화 이후에는 플레이어 참조를 교체할 수 없습니다.");
            return false;
        }

        player = controller.transform;
        // 현재 지갑은 캐릭터가 아니라 씬의 PlayerManager에 배치되어 있습니다.
        wallet = FindFirstObjectByType<PlayerWallet>();
        return true;
    }

    private bool TryInitializeSpawnArea() 
    {
        if (spawnAreaInitialized)
            return true;

        // 스포너가 없는 기존 테스트 씬도 지원하되, 실제 생성 시점에 찾습니다.
        T_PlayerController controller = player != null && player.gameObject.activeInHierarchy
            ? player.GetComponent<T_PlayerController>()
            : FindFirstObjectByType<T_PlayerController>();
        if (!TrySetPlayer(controller))
            return false;

        if(spawnArea == null || !spawnArea.isActiveAndEnabled || enemyPool == null || enemyDataProvider == null || player == null || highEnemyView == null)
        {
            Log.Error($"{name} 데이터 참조를 확인하세요.");
            return false;
        }

        spawnArea.Initialize(this, enemyPool, enemyDataProvider, effectSpawner, projectileSpawner, player, FindClosePlayer, damagePool, highEnemyView, wallet);

        spawnAreaInitialized = true;
        return true;
    }

    public bool TrySetWaves(IReadOnlyList<WBH_WaveData> waves)
    {
        if (waveInProgress || isSpawningWave)
        {
            Log.Error("웨이브 진행 중에는 데이터를 교체할 수 없습니다.");
            return false;
        }
        if (waves == null || waves.Count == 0 ||
            spawnArea == null || !spawnArea.isActiveAndEnabled)
        {
            Log.Error("웨이브 데이터와 SpawnArea 참조를 확인하세요.");
            return false;
        }
        if (!spawnArea.ValidateSpawnPoints(waves.Count, out string error))
        {
            Log.Error(error);
            return false;
        }

        var copiedWaves = new WBH_WaveData[waves.Count];
        for(int i=0; i<waves.Count; i ++)
        {
            WBH_WaveGradeCount[] entries = waves[i]?.enemies;
            if(entries == null || entries.Length == 0)
            {
                Log.Error($"{i + 1}웨이브의 적 구성이 없습니다.");
                return false;
            }

            var copiedEntries = new WBH_WaveGradeCount[entries.Length];
            long total = 0;
            for(int j = 0; j < entries.Length; j++)
            {
                WBH_WaveGradeCount entry = entries[j];
                if(entry == null || entry.count < 0 || (entry.count > 0 && !spawnArea.CanSpawn(entry.grade)))
                {
                    Log.Error($"{i + 1}웨이브의 등급별 수량과 SpawnData 를 확인하세요.");
                    return false;
                }

                total += entry.count;
                copiedEntries[j] = new WBH_WaveGradeCount
                {
                    grade = entry.grade,
                    count = entry.count,
                };
            }
            if(total <= 0 || total > int.MaxValue)
            {
                Log.Error($"{i + 1}웨이브의 전체 수량이 잘못됐습니다.");
                return false;
            }
            copiedWaves[i] = new WBH_WaveData { enemies = copiedEntries };
        }

        activeWaves = copiedWaves;
        activeWaveSet = null;
        currentWave = -1;
        aliveEnemyCount = 0;
        waveInProgress = false;
        isSpawningWave = false;


        return true;
    }


    public bool TrySetWaveSet(WBH_WaveSetSO waveSet)
    {
        if (waveSet == null)
            return false;
        if (!TrySetWaves(waveSet.Waves))
            return false;
        activeWaveSet = waveSet;
        return true;
    }

    public bool TryUseDefaultWaveSet()
    {
        return TrySetWaveSet(defaultWaveSet);
    }

    public void SetStatContext(WBH_EnemyStatContext context)
    {
        if(!context.IsValid)
        {
            Log.Error($"잘못된 적 능력치 컨텍스트입니다. 층 = {context.floor}, 난이도 = {context.difficultyName}, 플레이어 수 = {context.playerCount}");
            return;
        }
        statContext = context;
    }

    public bool TrySpawnNextWave()
    {
        if (!isActiveAndEnabled || !HasUsableWaveSet || waveInProgress || !HasNextWave || !TryInitializeSpawnArea())
            return false;

        currentWave++;
        aliveEnemyCount = 0;
        waveInProgress = true;
        pendingSpawns.Clear();

        foreach(WBH_WaveGradeCount entry in activeWaves[currentWave].enemies)
        {
            for (int i = 0; i < entry.count; i++)
                pendingSpawns.Enqueue(entry.grade);
        }

        return TrySpawnPendingEnemies();
    }

    public bool TrySpawnPendingEnemies()
    {
        if (!isActiveAndEnabled || !waveInProgress || isSpawningWave)
            return false;
        if(!TryInitializeSpawnArea() || spawnArea == null || !spawnArea.isActiveAndEnabled || !spawnArea.TryGetSpawnPoint(currentWave, out _))
        {
            Log.Error($"{currentWave + 1} 웨이브의 스폰포인트를 사용할 수 없습니다.");
            return false;
        }

        bool succeeded = true;
        isSpawningWave = true;

        try
        {
            while (pendingSpawns.Count > 0)
            {
                EnemyGrade grade = pendingSpawns.Peek();
                // 같은 SpawnArea 안에서 웨이브 인덱스로 포인트를 선택.
                int spawned = spawnArea.Spawn(grade, 1, statContext, currentWave);
                if (spawned != 1)
                {
                    Log.Error($"{currentWave + 1}웨이브 생성 실패: {grade}, " +
                        $"미생성 {pendingSpawns.Count}마리. 데이터·풀·NavMesh를 확인하세요.");
                    succeeded = false;
                    break;
                }
                pendingSpawns.Dequeue();
                aliveEnemyCount++;
            }
        }
        finally
        {
            isSpawningWave = false;
        }
        TryCompleteCurrentWave();
        return succeeded;
    }

    [ContextMenu("남은 웨이브 적 생성 재시도")]
    private void RetryPendingSpawns()
    {
        if (Application.isPlaying)
            TrySpawnPendingEnemies();
    }

    private void EnemyDead()
    {
        if (!waveInProgress || aliveEnemyCount <= 0)
            return;

        aliveEnemyCount--;

        TryCompleteCurrentWave();
    }

    private void TryCompleteCurrentWave()
    {
        if (!waveInProgress || isSpawningWave || pendingSpawns.Count > 0 || aliveEnemyCount > 0)
            return;

        waveInProgress = false;
        WaveCompleted?.Invoke();
    }

    // 가까운 플레이어 찾기
    private Transform FindClosePlayer(Vector3 origin)
    {
        T_PlayerController[] players = FindObjectsByType<T_PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        Transform closePlayer = null;
        float closestSqrDistance = float.MaxValue;

        foreach(T_PlayerController player in players)
        {
            if (!player.isActiveAndEnabled)
                continue;

            float sqrDistance = (player.transform.position - origin).sqrMagnitude;

            if (sqrDistance >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistance;
            closePlayer = player.transform;
        }
        return closePlayer;
    }

    // 적 패턴 등으로 적을 추가 소환할 경우 aliveEnemyCount 를 증가
    public void RegisterAdditionalEnemies(int count)
    {
        if (!waveInProgress || count <= 0)
            return;

        aliveEnemyCount += count;
    }
}

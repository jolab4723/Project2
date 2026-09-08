using System;
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

    [System.Serializable]
    public class WaveData
    {
        public GradeCount[] enemies;
    }

    [SerializeField] private WBH_EnemySpawnArea[] spawnAreas; 
    [SerializeField] private Transform player;

    [SerializeField] private WBH_EnemyDataProvider enemyDataProvider;
    [SerializeField] private WBH_EnemyStatContext statContext = new WBH_EnemyStatContext(1, "normal", 1);
    [SerializeField] private WaveData[] waves;

    [SerializeField] private WBH_HighEnemyHpbarView highEnemyView;
    [SerializeField] private WBH_EffectSpawner effectSpawner;
    [SerializeField] private WBH_ProjectileSpawner projectileSpawner;

    private WBH_EnemyPoolManager enemyPool;
    private WBH_FloatTextPoolManager damagePool;
    private PlayerWallet wallet;

    private int currentWave = -1;
    private int aliveEnemyCount;
    private bool waveInProgress;
    private bool spawnAreasInitialized;

    public event Action WaveCompleted;

    public int CurrentWaveIndex => currentWave;
    public int WaveCount => waves != null ? waves.Length : 0;
    public bool HasNextWave => currentWave + 1 < WaveCount;

    private void Awake()
    {
        enemyPool = GetComponent<WBH_EnemyPoolManager>();
        enemyDataProvider = GetComponent<WBH_EnemyDataProvider>();
        damagePool = FindFirstObjectByType<WBH_FloatTextPoolManager>();

        spawnAreas = FindObjectsByType<WBH_EnemySpawnArea>(FindObjectsSortMode.None);
        player = FindAnyObjectByType<T_PlayerController>().transform;

        wallet = FindFirstObjectByType < PlayerWallet>();
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

    private void Start()
    {
        InitializeSpawnAreas();
    }

    private void InitializeSpawnAreas() //!@ 차후 어그로 시스템 제작 시 player 빼기, eliteview UI쪽과 통합 시 eliteView 빼기
    {
        if (spawnAreasInitialized)
            return;

        spawnAreasInitialized = true;

        foreach (WBH_EnemySpawnArea area in spawnAreas)
        {
            if (area == null)
                continue;

            area.Initialize(enemyPool, enemyDataProvider, effectSpawner, projectileSpawner, player, FindClosePlayer, damagePool, highEnemyView, wallet);
        }
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
        if (waveInProgress || !HasNextWave)
            return false;

        InitializeSpawnAreas();

        currentWave++;
        aliveEnemyCount = 0;
        waveInProgress = true;

        WaveData wave = waves[currentWave];

        if(wave != null && wave.enemies != null)
        {
            foreach(GradeCount entry in wave.enemies)
            {
                if (entry == null)
                    continue;

                aliveEnemyCount += Spawn(entry.grade, entry.count);
            }
        }

        if (aliveEnemyCount == 0)
            CompleteCurrentWave();

        return true;
    }

    private void EnemyDead()
    {
        if (!waveInProgress)
            return;

        aliveEnemyCount--;

        if (aliveEnemyCount <= 0)
            CompleteCurrentWave();
    }

    private void CompleteCurrentWave()
    {
        if (!waveInProgress)
            return;

        waveInProgress = false;
        aliveEnemyCount = 0;

        WaveCompleted?.Invoke();
    }

    private int Spawn(EnemyGrade grade, int count)
    {
        if (count <= 0 || spawnAreas.Length == 0)
            return 0;

        int spawnedCount = 0;

        for(int i = 0; i < count; i ++)
        {
            WBH_EnemySpawnArea area = GetRandomArea();
            spawnedCount += area.Spawn(grade, 1, statContext);
        }
        return spawnedCount;
    }

    private WBH_EnemySpawnArea GetRandomArea()
    {
        int index = UnityEngine.Random.Range(0, spawnAreas.Length);
        return spawnAreas[index];
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
}

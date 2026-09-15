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

    [SerializeField] private WBH_EnemySpawnArea[] spawnAreas; 
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

    private int currentWave = -1;
    private int aliveEnemyCount;
    private bool waveInProgress;
    private bool spawnAreasInitialized;

    public event Action WaveCompleted;

    private readonly List<WBH_EnemySpawnArea> compatibleAreas = new();

    public WBH_WaveSetSO ActiveWaveSet => activeWaveSet;
    public int CurrentWaveIndex => currentWave;
    public bool HasUsableWaveSet => activeWaveSet != null && activeWaveSet.WaveCount > 0;
    public int WaveCount => activeWaveSet != null ? activeWaveSet.WaveCount : 0;
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

        activeWaveSet = defaultWaveSet;
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

    private void InitializeSpawnAreas() 
    {
        if (spawnAreasInitialized)
            return;

        spawnAreasInitialized = true;

        foreach (WBH_EnemySpawnArea area in spawnAreas)
        {
            if (area == null)
                continue;

            area.Initialize(this, enemyPool, enemyDataProvider, effectSpawner, projectileSpawner, player, FindClosePlayer, damagePool, highEnemyView, wallet);
        }
    }

    public bool TrySetWaveSet(WBH_WaveSetSO waveSet)
    {
        if(waveInProgress)
        {
            Log.Error("웨이브 진행중에는 waveSet 교체가 불가능합니다.");
            return false;
        }
        if(waveSet == null)
        {
            Log.Error("적용할 waveSet 이 없습니다.");
            return false;
        }
        if(waveSet.WaveCount <= 0)
        {
            Log.Error($"{waveSet.name} : 웨이브 데이터가 없습니다.");
            return false;
        }

        activeWaveSet = waveSet;

        currentWave = -1;
        aliveEnemyCount = 0;
        waveInProgress = false;

        Log.Print($"WaveSet 적용 : {waveSet.WaveSetId}");

        return true;
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
        if (!HasUsableWaveSet || waveInProgress || !HasNextWave)
            return false;

        InitializeSpawnAreas();

        currentWave++;
        aliveEnemyCount = 0;
        waveInProgress = true;

        WBH_WaveData wave = activeWaveSet.GetWave(currentWave);

        if(wave != null && wave.enemies != null)
        {
            foreach(WBH_WaveGradeCount entry in wave.enemies)
            {
                if (entry == null || entry.count <= 0)
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
            WBH_EnemySpawnArea area = GetRandomArea(grade);
            
            if (area == null)
                break;

            spawnedCount += area.Spawn(grade, 1, statContext);
        }
        return spawnedCount;
    }

    private WBH_EnemySpawnArea GetRandomArea(EnemyGrade grade)
    {
        compatibleAreas.Clear();

        foreach(WBH_EnemySpawnArea area in spawnAreas)
        {
            if(area != null && area.CanSpawn(grade))
            {
                compatibleAreas.Add(area);
            }
        }

        if(compatibleAreas.Count == 0)
        {
            Log.Error($"{grade} 등급을 소환할 수 있는 SpawnArea 가 없습니다.");
            return null;
        }

        int index = UnityEngine.Random.Range(0, compatibleAreas.Count);
        return compatibleAreas[index];
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

    public bool TryUseDefaultWaveSet()
    {
        if(waveInProgress)
        {
            Log.Error("웨이브 진행중에는 기본 waveSet 으로 변경할 수 없습니다.");
            return false;
        }
        if(defaultWaveSet == null || defaultWaveSet.WaveCount <= 0)
        {
            Log.Error("EnemySpawnManager 의 defaultWaveSet 이 비어있습니다.");
            return false;
        }

        activeWaveSet = defaultWaveSet;

        currentWave = -1;
        aliveEnemyCount = 0;
        waveInProgress = false;

        return true;
    }
}

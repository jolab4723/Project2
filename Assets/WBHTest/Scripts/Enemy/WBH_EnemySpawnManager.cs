using UnityEngine;

[RequireComponent(typeof(WBH_EnemyPoolManager))]
public class WBH_EnemySpawnManager : MonoBehaviour
{
    [System.Serializable]
    public class WaveData
    {
        public int normalCount;
        public int eliteCount;
    }

    [SerializeField] private WBH_EnemySpawnArea[] spawnAreas; 
    [SerializeField] private Transform player;

    [SerializeField] private WaveData[] waves;

    private WBH_EnemyPoolManager enemyPool;
    private WBH_EffectPoolManager effectPool;
    private WBH_ProjectilePoolManager projectilePool;
    private WBH_DamageTextPoolManager damagePool;
    private YJ_PortalActive portalActive;

    private int currentWave = -1;
    private int aliveEnemyCount;
    private bool stageClear;

    private void Awake()
    {
        enemyPool = GetComponent<WBH_EnemyPoolManager>();

        effectPool = FindFirstObjectByType<WBH_EffectPoolManager>();
        projectilePool = FindFirstObjectByType<WBH_ProjectilePoolManager>();
        damagePool = FindFirstObjectByType<WBH_DamageTextPoolManager>();

        spawnAreas = FindObjectsByType<WBH_EnemySpawnArea>(FindObjectsSortMode.None);
        player = FindAnyObjectByType<T_PlayerController>().transform;

        portalActive = FindAnyObjectByType<YJ_PortalActive>();
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
        SpawnNextWave();
    }

    private void InitializeSpawnAreas() //!@
    {
        foreach (WBH_EnemySpawnArea area in spawnAreas)
        {
            area.Initialize(enemyPool, effectPool, projectilePool, player, damagePool);
        }
    }

    private void SpawnNextWave()
    {
        currentWave++;
        
        if(currentWave >= waves.Length)
        {
            stageClear = true;
            portalActive.Active(true);
            Debug.Log("Stage Clear");
            return;
        }

        WaveData wave = waves[currentWave];

        SpawnNormal(wave.normalCount);
        SpawnElite(wave.eliteCount);

        aliveEnemyCount = wave.normalCount + wave.eliteCount;
    }

    public void SpawnNormal(int count)
    {
        Spawn(count, false);
    }

    public void SpawnElite(int count)
    {
        Spawn(count, true);
    }

    private void Spawn(int count, bool isElite)
    {
        if (spawnAreas.Length == 0)
            return;

        for(int i = 0; i < count; i ++)
        {
            WBH_EnemySpawnArea area = GetRandomArea();

            if (isElite)
                area.SpawnElite(1);
            else
                area.SpawnNormal(1);
        }
    }

    private WBH_EnemySpawnArea GetRandomArea()
    {
        int index = Random.Range(0, spawnAreas.Length);
        return spawnAreas[index];
    }

    public void EnemyDead() //!@ 차후 게임 매니저 생기면 거기서 웨이브 감지 바꾸는 것 고려
    {
        if (stageClear)
            return;

        aliveEnemyCount--;

        if(aliveEnemyCount <= 0)
        {
            SpawnNextWave();
        }
    }
}

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

    private WBH_EnemyPoolManager poolManager;

    private int currentWave = -1;
    private int aliveEnemyCount;
    private bool stageClear;

    private void Awake()
    {
        poolManager = GetComponent<WBH_EnemyPoolManager>();
        spawnAreas = FindObjectsByType<WBH_EnemySpawnArea>(FindObjectsSortMode.None);
        player = FindAnyObjectByType<T_PlayerController>().transform;
    }

    private void Start()
    {
        InitializeSpawnAreas();
        SpawnNextWave();
    }

    private void InitializeSpawnAreas()
    {
        foreach (WBH_EnemySpawnArea area in spawnAreas)
        {
            area.Initialize(poolManager, player);
        }
    }

    private void SpawnNextWave()
    {
        currentWave++;
        
        if(currentWave >= waves.Length)
        {
            stageClear = true;
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

    public void OnEnemyDead()
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

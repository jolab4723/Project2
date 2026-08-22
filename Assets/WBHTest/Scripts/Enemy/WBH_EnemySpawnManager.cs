using UnityEngine;

[RequireComponent(typeof(WBH_EnemyPoolManager))]
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

    [SerializeField] private WaveData[] waves;

    [SerializeField] private WBH_HighEnemyHpbarView eliteView;

    private WBH_EnemyPoolManager enemyPool;
    private WBH_EffectPoolManager effectPool;
    private WBH_ProjectilePoolManager projectilePool;
    private WBH_FloatTextPoolManager damagePool;
    
    private YJ_PortalActive portalActive;

    private int currentWave = -1;
    private int aliveEnemyCount;
    private bool stageClear;

    private void Awake()
    {
        enemyPool = GetComponent<WBH_EnemyPoolManager>();

        effectPool = FindFirstObjectByType<WBH_EffectPoolManager>();
        projectilePool = FindFirstObjectByType<WBH_ProjectilePoolManager>();
        damagePool = FindFirstObjectByType<WBH_FloatTextPoolManager>();

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

    private void InitializeSpawnAreas() //!@ 차후 어그로 시스템 제작 시 player 빼기, eliteview UI쪽과 통합 시 eliteView 빼기
    {
        foreach (WBH_EnemySpawnArea area in spawnAreas)
        {
            area.Initialize(enemyPool, effectPool, projectilePool, player, FindClosePlayer, damagePool, eliteView);
        }
    }

    private void SpawnNextWave()
    {
        currentWave++;
        
        if(currentWave >= waves.Length)
        {
            stageClear = true;
            portalActive.Active(true);
            Log.Print("Stage Clear");
            return;
        }

        aliveEnemyCount = 0;

        foreach(GradeCount entry in waves[currentWave].enemies)
        {
            aliveEnemyCount += Spawn(entry.grade, entry.count);
        }

        if (aliveEnemyCount == 0)
        {
            SpawnNextWave();
        }
    }

    private int Spawn(EnemyGrade grade, int count)
    {
        if (count <= 0 || spawnAreas.Length == 0)
            return 0;

        int spawnedCount = 0;

        for(int i = 0; i < count; i ++)
        {
            WBH_EnemySpawnArea area = GetRandomArea();
            spawnedCount += area.Spawn(grade, 1);
        }
        return spawnedCount;
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

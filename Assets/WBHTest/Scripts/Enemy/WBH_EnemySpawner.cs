using UnityEngine;
using UnityEngine.AI;

public class WBH_EnemySpawner : MonoBehaviour
{
    [SerializeField] private WBH_EnemyInfo[] testInfos;

    private WBH_EnemyPoolManager enemyPool;
    private WBH_EffectPoolManager effectPool;
    private WBH_ProjectilePoolManager projectilePool;
    private WBH_FloatTextPoolManager floatTextPool;
    private WBH_HighEnemyHpbarView eliteView;
    private PlayerWallet wallet;

    private float spawnNavSearchRadius = 2f;


    //!@ 데이터 매니저 연결

    public void Initialize(WBH_EnemyPoolManager poolManager, 
                           WBH_EffectPoolManager effectPool, 
                           WBH_ProjectilePoolManager projectilePool, 
                           Transform localPlayer, // eliteView 에만 사용
                           WBH_FloatTextPoolManager floatTextPool,
                           WBH_HighEnemyHpbarView eliteView,
                           PlayerWallet wallet)
    {
        this.enemyPool = poolManager;
        this.effectPool = effectPool;
        this.projectilePool = projectilePool;
        this.floatTextPool = floatTextPool;
        this.eliteView = eliteView;
        this.eliteView.Initialize(localPlayer);
        this.wallet = wallet;
    }

    public WBH_EnemyController Spawn(int enemyID, Transform spawnPoint, Transform target)
    {
        if(enemyPool == null)
        {
            Log.Error("EnemyPoolManager가 초기화 되지 않았습니다.");
            return null;
        }

        WBH_EnemyInfo info = GetEnemyInfo(enemyID);
        // info = 데이터 매니저에서 enemyID 를 통해 info(스탯 등) 주입 !@

        if (info == null)
        {
            Log.Error($"EnemyInfo(ID : {enemyID}를 찾을 수 없습니다.)");
        }

        if(!NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, spawnNavSearchRadius, NavMesh.AllAreas))
        {
            Log.Error($"{spawnPoint.name} 주변에서 NavMesh 를 찾지 못했습니다.");
            return null;
        }

        WBH_EnemyController enemy = enemyPool.Get(enemyID);

        if (enemy == null)
            return null;

        enemy.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        enemy.gameObject.SetActive(true);

        enemy.GetComponent<WBH_EffectSpawner>().Initialize(effectPool);
        enemy.GetComponent<WBH_ProjectileSpawner>().Initialize(projectilePool);
        enemy.GetComponent<EnemyKillReward>()?.Initialize(wallet);
        enemy.GetComponent<WBH_EnemyView>().Initialize(floatTextPool, eliteView);

        enemy.Initialize(info, enemyPool);

        enemy.SetTarget(target);


        if(info.enemyGrade == EnemyGrade.Boss)
        {
            eliteView?.BindBoss(enemy);
        }

        return enemy;
    }

    private WBH_EnemyInfo GetEnemyInfo(int enemyID)
    {
        foreach (var info in testInfos)
        {
            if (info.id == enemyID)
                return info;
        }
        return null;
    }
}

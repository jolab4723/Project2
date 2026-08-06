using UnityEngine;

public class WBH_EnemySpawner : MonoBehaviour
{
    [SerializeField] private WBH_EnemyInfo[] testInfos;

    private WBH_EnemyPoolManager enemyPool;
    private WBH_EffectPoolManager effectPool;
    private WBH_ProjectilePoolManager projectilePool;
    private WBH_DamageTextPoolManager damageTextPool;
    private WBH_EliteHpbarView eliteView;


    //!@ 데이터 매니저 연결

    public void Initialize(WBH_EnemyPoolManager poolManager, 
                           WBH_EffectPoolManager effectPool, 
                           WBH_ProjectilePoolManager projectilePool, 
                           Transform localPlayer, // eliteView 에만 사용
                           WBH_DamageTextPoolManager damageTextPool,
                           WBH_EliteHpbarView eliteView)
    {
        this.enemyPool = poolManager;
        this.effectPool = effectPool;
        this.projectilePool = projectilePool;
        this.damageTextPool = damageTextPool;
        this.eliteView = eliteView;
        this.eliteView.Initialize(localPlayer);
    }

    public WBH_EnemyController Spawn(int enemyID, Transform spawnPoint, Transform target)
    {
        if(enemyPool == null)
        {
            Debug.LogError("EnemyPoolManager가 초기화 되지 않았습니다.");
            return null;
        }

        WBH_EnemyController enemy = enemyPool.Get(enemyID);

        if (enemy == null)
            return null;

        enemy.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        WBH_EnemyInfo info = GetEnemyInfo(enemyID);
        // info = 데이터 매니저에서 enemyID 를 통해 info(스탯 등) 주입 !@

        if(info == null)
        {
            Debug.LogError($"EnemyInfo(ID : {enemyID}를 찾을 수 없습니다.)");
        }

        enemy.GetComponent<WBH_EffectSpawner>().Initialize(effectPool);
        enemy.GetComponent<WBH_ProjectileSpawner>().Initialize(projectilePool);
        enemy.GetComponent<WBH_EnemyView>().Initialize(damageTextPool, eliteView);

        enemy.Initialize(info, enemyPool);

        enemy.SetTarget(target);

        enemy.gameObject.SetActive(true);

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

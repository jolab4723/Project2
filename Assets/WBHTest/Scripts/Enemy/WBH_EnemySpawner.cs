using PLAYERTWO.ARPGProject;
using UnityEngine;
using UnityEngine.AI;

public class WBH_EnemySpawner : MonoBehaviour
{
    private WBH_EnemySpawnManager spawnManager;
    private WBH_EnemyPoolManager enemyPool;
    private WBH_EnemyDataProvider enemyDataProvider;
    private WBH_EffectSpawner effectSpawner;
    private WBH_ProjectileSpawner projectileSpawner;
    private WBH_FloatTextPoolManager floatTextPool;
    private WBH_HighEnemyHpbarView eliteView;
    private PlayerWallet wallet;

    private float spawnNavSearchRadius = 2f;

    public event System.Action<WBH_EnemyController> BossSpawn;

    public void Initialize(WBH_EnemySpawnManager spawnManager,
                           WBH_EnemyPoolManager poolManager,
                           WBH_EnemyDataProvider enemyDataProvider,
                           WBH_EffectSpawner effectSpawner, 
                           WBH_ProjectileSpawner projectileSpawner, 
                           Transform localPlayer, // eliteView 에만 사용
                           WBH_FloatTextPoolManager floatTextPool,
                           WBH_HighEnemyHpbarView eliteView,
                           PlayerWallet wallet)
    {
        this.spawnManager = spawnManager;
        this.enemyPool = poolManager;
        this.enemyDataProvider = enemyDataProvider;
        this.effectSpawner = effectSpawner;
        this.projectileSpawner = projectileSpawner;
        this.floatTextPool = floatTextPool;
        this.eliteView = eliteView;
        this.eliteView.Initialize(localPlayer);
        this.wallet = wallet;

    }

    public WBH_EnemyController Spawn(string enemyId, Transform spawnPoint, Transform target, WBH_EnemyStatContext context)
    {
        if (spawnPoint == null)
            return null;

        return Spawn(enemyId, spawnPoint.position, spawnPoint.rotation, target, context);
    }

    public WBH_EnemyController Spawn(string enemyId, Vector3 spawnPosition, Quaternion spawnRotation, Transform target, WBH_EnemyStatContext context)
    {
        if (enemyPool == null)
        {
            Log.Error("EnemyPoolManager가 초기화 되지 않았습니다.");
            return null;
        }
        if (enemyDataProvider == null)
        {
            Log.Error("EnemyDataProvider 초기화 되지 않았습니다.");
            return null;
        }

        if (!enemyDataProvider.TryCreateEnemyInfo(enemyId, context, out WBH_EnemyInfo info))
        {
            Log.Error($"적 정보 생성에 실패했습니다. enemyId = {enemyId}");
            return null;
        }

        if (!NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, spawnNavSearchRadius, NavMesh.AllAreas))
        {
            Log.Error("소환위치 주변에서 NavMesh 를 찾지 못했습니다.");
            return null;
        }

        WBH_EnemyController enemy = enemyPool.Get(enemyId);

        if (enemy == null)
            return null;

        enemy.transform.SetPositionAndRotation(hit.position, spawnRotation);

        enemy.GetComponent<EnemyKillReward>()?.Initialize(wallet);
        enemy.GetComponent<WBH_EnemyView>()?.Initialize(floatTextPool, eliteView);

        enemy.Initialize(info, enemyPool, effectSpawner, projectileSpawner);

        if(enemy.TryGetComponent(out WBH_BossMinionSpawner bossMinionSpawner))
        {
            bossMinionSpawner.Initialize(spawnManager, this, context);
        }

        enemy.SetTarget(target);
        enemy.gameObject.SetActive(true);


        if (info.enemyGrade == EnemyGrade.Boss)
        {
            eliteView?.BindBoss(enemy);
            BossSpawn?.Invoke(enemy);
        }

        return enemy;
    }
}

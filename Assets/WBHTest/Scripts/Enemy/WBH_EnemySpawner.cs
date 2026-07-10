using UnityEngine;

public class WBH_EnemySpawner : MonoBehaviour
{
    [SerializeField] private WBH_EnemyInfo[] testInfos;

    private WBH_EnemyPoolManager poolManager;

    //!@ 데이터 매니저 연결

    private Transform target;

    public void Initialize(WBH_EnemyPoolManager poolManager, Transform target)
    {
        this.poolManager = poolManager;
        this.target = target;
    }

    public WBH_EnemyController Spawn(int enemyID, Transform spawnPoint)
    {
        if(poolManager == null)
        {
            Debug.LogError("EnemyPoolManager가 초기화 되지 않았습니다.");
            return null;
        }

        WBH_EnemyController enemy = poolManager.Get(enemyID);

        if (enemy == null)
            return null;

        enemy.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        WBH_EnemyInfo info = GetEnemyInfo(enemyID);
        // info = 데이터 매니저에서 enemyID 를 통해 info(스탯 등) 주입 !@

        if(info == null)
        {
            Debug.LogError($"EnemyInfo(ID : {enemyID}를 찾을 수 없습니다.)");
        }

        enemy.Initialize(info);

        enemy.SetTarget(target);

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

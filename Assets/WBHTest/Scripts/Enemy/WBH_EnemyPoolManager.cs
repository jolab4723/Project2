using System;
using System.Collections.Generic;
using UnityEngine;

public class WBH_EnemyPoolManager : MonoBehaviour
{
    [Serializable]
    private class EnemyPool
    {
        public int enemyID;
        public WBH_EnemyController prefab;
        public int poolSize = 10;
    }

    [SerializeField] private EnemyPool[] enemyPools;

    private Dictionary<int, Queue<WBH_EnemyController>> pools;
    private Dictionary<int, EnemyPool> poolDatas;

    private void Awake()
    {
        pools = new();
        poolDatas = new Dictionary<int, EnemyPool>();

        CreatePools();
    }

    private void CreatePools()
    {
        foreach(EnemyPool data in enemyPools)
        {
            Queue<WBH_EnemyController> pool = new ();

            for (int i = 0; i < data.poolSize; i++)
            {
                pool.Enqueue(CreateEnemy(data));
            }
            pools.Add(data.enemyID, pool);
            poolDatas.Add(data.enemyID, data);
        }
    }

    public WBH_EnemyController Get(int enemyID)
    {
        if(!pools.TryGetValue(enemyID, out Queue<WBH_EnemyController> pool))
        {
            Debug.LogWarning($"Enemy Pool 없음 : {enemyID}");
            return null;
        }

        WBH_EnemyController enemy;

        if (pool.Count == 0)
        {
            Debug.Log($"Enemy Pool 자동 확장 : {enemyID}");

            enemy = CreateEnemy(poolDatas[enemyID]);
        }
        else
        {
            enemy = pool.Dequeue();
        }
        enemy.gameObject.SetActive(true);

        return enemy;
    }

    public void Return(WBH_EnemyController enemy)
    {
        enemy.gameObject.SetActive(false);

        pools[enemy.Info.id].Enqueue(enemy);
    }

    private WBH_EnemyController CreateEnemy(EnemyPool data)
    {
        WBH_EnemyController enemy = Instantiate(data.prefab, transform);

        enemy.gameObject.SetActive(false);

        return enemy;
    }
}

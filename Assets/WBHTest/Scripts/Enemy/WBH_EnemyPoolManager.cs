using EnemySystem;
using System;
using System.Collections.Generic;
using UnityEngine;

public class WBH_EnemyPoolManager : MonoBehaviour
{
    [Serializable]
    private class EnemyPool
    {
        public EnemyDefinitionSO enemyDef;
        public WBH_EnemyController prefab;
        public int poolSize = 10;
    }

    [SerializeField] private EnemyPool[] enemyPools;

    private Dictionary<string, Queue<WBH_EnemyController>> pools;
    private Dictionary<string, EnemyPool> poolDatas;
    private Dictionary<WBH_EnemyController, string> poolKeys; // 풀에서 생성된 적이 어느 풀 소속인지 기억.

    private void Awake()
    {
        pools = new Dictionary<string, Queue<WBH_EnemyController>>(StringComparer.Ordinal);
        poolDatas = new Dictionary<string, EnemyPool>(StringComparer.Ordinal);
        poolKeys = new Dictionary<WBH_EnemyController, string>();

        CreatePools();
    }

    private void CreatePools()
    {
        if (enemyPools == null)
            return;

        foreach(EnemyPool data in enemyPools)
        {
            if (data == null)
                return;
            if (data.enemyDef == null)
            {
                Log.Error($"{name} : EnemyPool 의 EnemyDefSo 가 비어 있습니다.");
                continue;
            }
            if (data.prefab == null)
            {
                Log.Error($"{name} : {data.enemyDef.name} 의 적 프리팹이 비어 있습니다.");
                continue;
            }

            string enemyId = data.enemyDef.enemyId;

            if (string.IsNullOrWhiteSpace(enemyId))
            {
                Log.Error($"{name}: EnemyDefinitionSO의 enemyId가 비어 있습니다.");
                continue;
            }

            if (pools.ContainsKey(enemyId))
            {
                Log.Error( $"{name}: 중복된 EnemyPool enemyId입니다. enemyId = {enemyId}");
                continue;
            }

            var pool = new Queue<WBH_EnemyController>();

            for (int i = 0; i < data.poolSize; i++)
            {
                WBH_EnemyController enemy = CreateEnemy(data, enemyId);

                if(enemy != null)
                    pool.Enqueue(enemy);
            }
            pools.Add(enemyId, pool);
            poolDatas.Add(enemyId, data);
        }
    }

    public WBH_EnemyController Get(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            Log.Error("EnemyPool.Get()에 빈 enemyId가 전달됐습니다.");
            return null;
        }

        if (!pools.TryGetValue(enemyId, out Queue<WBH_EnemyController> pool))
        {
            Log.Warning($"EnemyPool이 없습니다. enemyId={enemyId}");
            return null;
        }

        WBH_EnemyController enemy;

        if (pool.Count > 0)
        {
            enemy = pool.Dequeue();
        }
        else
        {
            Log.Print($"Enemy Pool 자동 확장 : {enemyId}");

            enemy = CreateEnemy(poolDatas[enemyId], enemyId);
        }

        if (enemy == null)
            return null;

        enemy.ResetForPool();

        return enemy;
    }

    public void Return(WBH_EnemyController enemy)
    {
        if (enemy == null)
            return;

        if(!poolKeys.TryGetValue(enemy, out string enemyId))
        {
            Log.Error($"{enemy.name} : 풀 소속 정보를 찾지 못했습니다.");
            enemy.gameObject.SetActive(false);
            return;
        }

        if(!pools.TryGetValue(enemyId, out Queue<WBH_EnemyController> pool))
        {
            Log.Error($"{enemy.name}: 반환할 EnemyPool이 없습니다. enemyId={enemyId}");

            enemy.gameObject.SetActive(false);
            return;
        }

        enemy.ResetForPool();
        enemy.gameObject.SetActive(false);

        pool.Enqueue(enemy);
    }

    private WBH_EnemyController CreateEnemy(EnemyPool data, string enemyId)
    {
        WBH_EnemyController enemy = Instantiate(data.prefab, transform);

        enemy.gameObject.SetActive(false);
        poolKeys.Add(enemy, enemyId);

        return enemy;
    }
}

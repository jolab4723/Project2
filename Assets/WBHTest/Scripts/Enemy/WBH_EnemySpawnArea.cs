using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WBH_EnemySpawner))]
public class WBH_EnemySpawnArea : MonoBehaviour
{
    [Header("Spawn Setting")]
    [SerializeField] private Transform[] spawnPoints; //!@ find - inchildren 을 사용해서 자동처리
    [SerializeField] private int[] normalEnemyIDs;
    [SerializeField] private int[] eliteEnemyIDs;

    private WBH_EnemySpawner enemySpawner;
    private WBH_EnemyView view;

    private void Awake()
    {
        enemySpawner = GetComponent<WBH_EnemySpawner>();
    }

    public void Initialize(WBH_EnemyPoolManager enemyPool, 
                           WBH_EffectPoolManager effectPool, 
                           WBH_ProjectilePoolManager projectilePool, 
                           Transform target, 
                           WBH_DamageTextPoolManager damagePool,
                           WBH_EliteHpbarView eliteView)
    {
        enemySpawner.Initialize(enemyPool, effectPool, projectilePool, target, damagePool,eliteView);
        //view.Initialize(damagePool);
    }

    public void SpawnNormal(int count)
    {
        SpawnEnemies(normalEnemyIDs, count);
    }

    public void SpawnElite(int count)
    {
        SpawnEnemies(eliteEnemyIDs, count);
    }


    private void SpawnEnemies(int[] enemyIDs, int count)
    {
        if (spawnPoints.Length == 0 || enemyIDs.Length == 0)
            return;

        List<Transform> availablePoints = new List<Transform>(spawnPoints);

        for(int i = 0; i < count; i++)
        {
            // 스폰 지점 중복방지
            if(availablePoints.Count == 0)
            {
                availablePoints = new List<Transform>(spawnPoints);
            }

            int pointIndex = Random.Range(0, availablePoints.Count);
            Transform spawnPoint = availablePoints[pointIndex];
            availablePoints.RemoveAt(pointIndex);

            int enemyID = enemyIDs[Random.Range(0, enemyIDs.Length)];

            enemySpawner.Spawn(enemyID, spawnPoint);
        }
    }

}

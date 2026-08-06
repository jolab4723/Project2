using System;
using System.Collections.Generic;
using UnityEngine;



[RequireComponent(typeof(WBH_EnemySpawner))]
public class WBH_EnemySpawnArea : MonoBehaviour
{
    [System.Serializable]
    public class EnemyGradeSpawnData
    {
        public EnemyGrade grade;
        public int[] enemyIDs;
    }

    [Header("Spawn Setting")]
    [SerializeField] private Transform[] spawnPoints; //!@ find - inchildren 을 사용해서 자동처리
    [SerializeField] private EnemyGradeSpawnData[] spawnDatas; 

    private WBH_EnemySpawner enemySpawner;
    private Func<Vector3, Transform> findClosestPlayer;

    private void Awake()
    {
        enemySpawner = GetComponent<WBH_EnemySpawner>();
    }

    public void Initialize(WBH_EnemyPoolManager enemyPool, 
                           WBH_EffectPoolManager effectPool, 
                           WBH_ProjectilePoolManager projectilePool, 
                           Transform localPlayer, 
                           Func<Vector3, Transform> findClosestPlayer,
                           WBH_DamageTextPoolManager damagePool,
                           WBH_EliteHpbarView eliteView)
    {
        this.findClosestPlayer = findClosestPlayer;
        enemySpawner.Initialize(enemyPool, effectPool, projectilePool, localPlayer, damagePool,eliteView);
        //view.Initialize(damagePool);
    }

    public int Spawn(EnemyGrade grade, int count)
    {
        EnemyGradeSpawnData spawnData = System.Array.Find(spawnDatas, data => data.grade == grade);

        if(spawnData == null || spawnData.enemyIDs == null || spawnData.enemyIDs.Length == 0)
        {
            Log.Warning($"{name} : {grade} 등급의 스폰 ID가 설정되지 않았습니다.");
            return 0;
        }
        return SpawnEnemies(spawnData.enemyIDs, count);
    }

    private int SpawnEnemies(int[] enemyIDs, int count)
    {
        if (spawnPoints.Length == 0 || enemyIDs.Length == 0)
            return 0;

        int spawnCount = 0;
        List<Transform> availablePoints = new List<Transform>(spawnPoints);

        for(int i = 0; i < count; i++)
        {
            // 스폰 지점 중복방지
            if(availablePoints.Count == 0)
            {
                availablePoints = new List<Transform>(spawnPoints);
            }

            int pointIndex = UnityEngine.Random.Range(0, availablePoints.Count);
            Transform spawnPoint = availablePoints[pointIndex];
            availablePoints.RemoveAt(pointIndex);

            int enemyID = enemyIDs[UnityEngine.Random.Range(0, enemyIDs.Length)];

            Transform target = findClosestPlayer?.Invoke(spawnPoint.position); // 스폰포인트가 결정된 뒤 타겟 탐색

            if (enemySpawner.Spawn(enemyID, spawnPoint, target) != null)
                spawnCount++;
        }
        return spawnCount;
    }

}

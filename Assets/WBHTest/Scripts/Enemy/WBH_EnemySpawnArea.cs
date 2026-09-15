using EnemySystem;
using System;
using System.Collections.Generic;
using UnityEngine;



[RequireComponent(typeof(WBH_EnemySpawner))]
public class WBH_EnemySpawnArea : MonoBehaviour
{
    [Serializable]
    public class EnemyGradeSpawnData
    {
        public EnemyGrade grade;
        public EnemyDefinitionSO[] enemies;
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

    public void Initialize(WBH_EnemySpawnManager spawnManager,
                           WBH_EnemyPoolManager enemyPool, 
                           WBH_EnemyDataProvider enemyDataProvider,
                           WBH_EffectSpawner effectSpawner, 
                           WBH_ProjectileSpawner projectileSpawner, 
                           Transform localPlayer, 
                           Func<Vector3, Transform> findClosestPlayer,
                           WBH_FloatTextPoolManager damagePool,
                           WBH_HighEnemyHpbarView eliteView,
                           PlayerWallet playerWallet)
    {
        this.findClosestPlayer = findClosestPlayer;
        enemySpawner.Initialize(spawnManager, enemyPool, enemyDataProvider, effectSpawner, projectileSpawner, localPlayer, damagePool, eliteView, playerWallet);
    }

    public int Spawn(EnemyGrade grade, int count, WBH_EnemyStatContext context)
    {
        EnemyGradeSpawnData spawnData = System.Array.Find(spawnDatas, data => data.grade == grade);

        if(spawnData == null || spawnData.enemies == null || spawnData.enemies.Length == 0)
        {
            Log.Warning($"{name} : {grade} 등급의 스폰 ID가 설정되지 않았습니다.");
            return 0;
        }
        return SpawnEnemies(spawnData.enemies, count, context);
    }

    private int SpawnEnemies(EnemyDefinitionSO[] enemies, int count, WBH_EnemyStatContext context)
    {
        if (spawnPoints.Length == 0 || enemies.Length == 0)
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

            EnemyDefinitionSO def = enemies[UnityEngine.Random.Range(0, enemies.Length)];

            if(def == null)
            {
                Log.Error($"{name} : SpawnData 에 비어있는 defSO 가 있습니다.");
                continue;
            }

            Transform target = findClosestPlayer?.Invoke(spawnPoint.position); // 스폰포인트가 결정된 뒤 타겟 탐색

            if (enemySpawner.Spawn(def.enemyId, spawnPoint, target, context) != null)
                spawnCount++;
        }
        return spawnCount;
    }

    public bool CanSpawn(EnemyGrade grade)
    {
        if(spawnDatas == null)
            return false;

        EnemyGradeSpawnData spawnData = System.Array.Find(spawnDatas, data => data != null && data.grade == grade);

        if(spawnData == null || spawnData.enemies == null)
            return false;

        return System.Array.Exists(spawnData.enemies, enemy => enemy != null);
    }
}

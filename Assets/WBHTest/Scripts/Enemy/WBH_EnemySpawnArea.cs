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
                           PlayerWallet playerWallet,
                           YJ_SfxPlayer sfxPlayer)
    {
        this.findClosestPlayer = findClosestPlayer;
        enemySpawner.Initialize(spawnManager, enemyPool, enemyDataProvider, effectSpawner, projectileSpawner, localPlayer, damagePool, eliteView, playerWallet, sfxPlayer);
    }

    public bool TryGetSpawnPoint(int index, out Transform point)
    {
        point = null;
        if(spawnPoints == null || index < 0 || index >= spawnPoints.Length)
            return false;

        point = spawnPoints[index];
        return point != null && point != transform && point.IsChildOf(transform) && point.gameObject.activeInHierarchy;
    }

    public bool ValidateSpawnPoints(int waveCount, out string error)
    {
        error = null;
        if(waveCount <= 0 || spawnPoints == null || spawnPoints.Length < waveCount)
        {
            error = $"{name} : 스폰포인트가 최소 {waveCount} 개 필요합니다.";
            return false;
        }

        var usedPoints = new HashSet<Transform>();
        for(int i = 0; i < waveCount; i ++)
        {
            if(!TryGetSpawnPoint(i, out Transform point) || !usedPoints.Add(point))
            {
                error = $"{name} : SpawnPoints[{i}] 의 누락, 비활성, 중복, 자식 관계를 확인하세요.";
                return false;
            }
        }
        return true;
    }

    public bool CanSpawn(EnemyGrade grade)
    {
        if (spawnDatas == null)
            return false;

        EnemyGradeSpawnData selected = null;
        foreach(EnemyGradeSpawnData data in spawnDatas)
        {
            if(data == null || data.grade != grade)
                continue;
            if (selected != null)
                return false;
            selected = data;
        }

        if(selected?.enemies == null || selected.enemies.Length == 0)
            return false;

        foreach(EnemyDefinitionSO def in selected.enemies)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.enemyId) || def.enemyGrade != grade)
                return false;
        }
        return true;
    }

    public int Spawn(EnemyGrade grade, int count, WBH_EnemyStatContext context, int spawnPointIndex)
    {
        if(count <= 0 || enemySpawner == null || !TryGetSpawnPoint(spawnPointIndex, out Transform point) || !CanSpawn(grade))
            return 0;

        EnemyGradeSpawnData data = Array.Find(spawnDatas, entry => entry != null && entry.grade == grade);

        int spawnedCount = 0;

        for (int i = 0; i < count; i++)
        {
            EnemyDefinitionSO def = data.enemies[UnityEngine.Random.Range(0, data.enemies.Length)];
            Transform target = findClosestPlayer?.Invoke(point.position);

            WBH_EnemyController enemy = enemySpawner.Spawn(def.enemyId, point, target, context);

            if (enemy == null)
                break;
            spawnedCount++;
        }
        return spawnedCount;
    }
}

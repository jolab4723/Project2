using EnemySystem;
using UnityEngine;

public class WBH_BossMinionSpawner : MonoBehaviour
{
    [SerializeField] private EnemyDefinitionSO selfDestructEnemy;
    [Header("소환 범위")]
    [SerializeField] private float minSpawnRadius = 3f;
    [SerializeField] private float maxSpawnRadius = 6f;
    [SerializeField] private float navMeshSearchRadius = 2f;
    [SerializeField] private int positionSearchAttempts = 4;

    private WBH_EnemySpawnManager spawnManager;
    private WBH_EnemySpawner spawner;
    private WBH_EnemyStatContext statContext;

    /// <summary>SW 수정: 위치와 회전은 기존 규칙을 쓰며 멀티의 실제 생성·클리어 집계만 서버에 위임합니다.</summary>
    public System.Func<string, Vector3, Quaternion, Transform, bool> ExternalSpawn { get; set; }

    public void Initialize(WBH_EnemySpawnManager spawnManager, WBH_EnemySpawner spawner, WBH_EnemyStatContext statContext)
    {
        this.spawnManager = spawnManager;
        this.spawner = spawner;
        this.statContext = statContext;
    }

    public int Spawn(int count, Transform initialTarget)
    {
        if ((ExternalSpawn == null && (spawnManager == null || spawner == null)) || selfDestructEnemy == null || count <= 0)
        {
            Log.Error($"EnemySpawner 혹은 자폭병SO가 할당되지 않았습니다.");
            return 0;
        }

        float startAngle = Random.Range(0f, 360f);
        float angleStep = 360f / count;
        int spawnedCount = 0;

        for(int i = 0; i < count; i++)
        {
            if (!TryGetSpawnPosition(startAngle + angleStep * i, angleStep, out Vector3 spawnPosition))
                continue;

            Quaternion spawnRotation = GetSpawnRotation(spawnPosition, initialTarget);

            if (ExternalSpawn != null)
            {
                if (ExternalSpawn(selfDestructEnemy.enemyId, spawnPosition, spawnRotation, initialTarget)) spawnedCount++;
                continue;
            }

            WBH_EnemyController enemy = spawner.Spawn(selfDestructEnemy.enemyId, spawnPosition, spawnRotation, initialTarget, statContext);

            if (enemy != null)
                spawnedCount++;
        }
        if (ExternalSpawn == null) spawnManager.RegisterAdditionalEnemies(spawnedCount); // 소환된 적만큼 클리어조건 ++
        return spawnedCount;
    }

    private bool TryGetSpawnPosition(float baseAngle, float angleStep, out Vector3 spawnPosition)
    {
        for (int attempt = 0; attempt < positionSearchAttempts; attempt++)
        {
            float angleOffset = Random.Range(-angleStep * 0.35f, angleStep * 0.35f);
            float angle = baseAngle + angleOffset;
            float radius = Random.Range(minSpawnRadius, maxSpawnRadius);

            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 desiredPos = transform.position + dir * radius;

            if(UnityEngine.AI.NavMesh.SamplePosition(desiredPos, out UnityEngine.AI.NavMeshHit hit, navMeshSearchRadius, UnityEngine.AI.NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
                return true;
            }
        }
        spawnPosition = default;
        return false;
    }

    private Quaternion GetSpawnRotation(Vector3 spawnPosition, Transform initialTarget)
    {
        Vector3 lookDir;

        if(initialTarget != null && initialTarget.gameObject.activeInHierarchy)
        {
            lookDir = initialTarget.position - spawnPosition;
        }
        else
        {
            lookDir = spawnPosition - transform.position;
        }

        lookDir.y = 0f;

        if(lookDir.sqrMagnitude < 0.001f)
        {
            return transform.rotation;
        }

        return Quaternion.LookRotation(lookDir.normalized, Vector3.up);
    }
}

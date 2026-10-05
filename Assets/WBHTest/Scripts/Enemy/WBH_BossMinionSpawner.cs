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

    [Header("관문 소환")]
    [SerializeField, Min(0.1f)] private float gateSpawnRadius = 2f;

    // 기존 ExternalSpawn은 성공 여부만 반환한다.
    // 분신은 실제 객체와 체력 추적이 필요하므로 별도 콜백을 사용한다.
    public System.Func <string, Vector3, Quaternion, Transform, WBH_EnemyController> ExternalSpawnTracked { get; set; }

    // 네트워크 분신은 서버에서 역소환·웨이브 집계를 함께 처리해야 한다.
    public System.Action<WBH_EnemyController> ExternalDespawnTracked { get; set; }

    private bool IsNetworkSpawnSession =>
        Mirror.NetworkServer.active || Mirror.NetworkClient.active;

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

    // 분신 등 지정 위치 소환: 실제 생성된 객체를 반환.
    public WBH_EnemyController SpawnAt(EnemyDefinitionSO definition, Vector3 desiredPosition, Transform initialTarget)
    {
        if (definition == null)
        {
            Log.Error("지정 위치 소환용 EnemyDefinitionSO가 없습니다.");
            return null;
        }

        if (IsNetworkSpawnSession && !Mirror.NetworkServer.active)
            return null;

        bool useExternal = IsNetworkSpawnSession || ExternalSpawn != null || ExternalSpawnTracked != null;

        if (useExternal)
        {
            if (ExternalSpawnTracked == null || ExternalDespawnTracked == null)
            {
                Log.Error("분신 소환에는 객체를 반환하는 서버 소환 콜백과 서버 역소환 콜백이 필요합니다.");
                return null;
            }
        }
        else if (spawner == null || spawnManager == null)
        {
            Log.Error("EnemySpawner 또는 SpawnManager가 없습니다.");
            return null;
        }

        if (!UnityEngine.AI.NavMesh.SamplePosition(desiredPosition, out UnityEngine.AI.NavMeshHit hit, navMeshSearchRadius, UnityEngine.AI.NavMesh.AllAreas))
        {
            Log.Warning("지정된 소환 위치 인근에 NavMesh가 없습니다.");
            return null;
        }

        Quaternion rotation = GetSpawnRotation(hit.position, initialTarget);

        if (useExternal)
        {
            // 서버 콜백이 초기화와 네트워크 생성, 클리어 집계를 담당한다.
            return ExternalSpawnTracked( definition.enemyId, hit.position, rotation, initialTarget);
        }

        WBH_EnemyController enemy = spawner.Spawn(definition.enemyId, hit.position, rotation, initialTarget, statContext);

        if (enemy != null)
        {
            spawnManager.RegisterAdditionalEnemies(1);
        }

        return enemy;
    }

    // 분신 제한 시간 종료 또는 보스 패턴 정리 시 사용.
    public void DespawnTracked(WBH_EnemyController enemy)
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy)
            return;

        bool useExternal = IsNetworkSpawnSession || ExternalSpawn != null || ExternalSpawnTracked != null;

        if (useExternal)
        {
            if (IsNetworkSpawnSession && !Mirror.NetworkServer.active)
                return;

            if (ExternalDespawnTracked == null)
            {
                Log.Error("분신 서버 역소환 콜백이 없습니다.");
                return;
            }

            ExternalDespawnTracked(enemy);
            return;
        }

        // Despawn의 기존 OnEnemyDead 경로에서 웨이브 수가 감소.
        enemy.Despawn();
    }

    // 자폭병을 관문마다 순환 배분.
    // (현재 기획)참가자 수 × 6, 관문 3개라면 각 관문에 참가자 수 × 2가 배정.
    public int SpawnAtGates(Transform[] gates, int count, Transform initialTarget)
    {
        if (selfDestructEnemy == null || gates == null || gates.Length == 0 || count <= 0)
            return 0;

        foreach (Transform gate in gates)
        {
            if (gate == null)
                return 0;
        }

        if (IsNetworkSpawnSession && !Mirror.NetworkServer.active)
            return 0;

        bool useExternal = ExternalSpawn != null || ExternalSpawnTracked != null;

        if (IsNetworkSpawnSession && !useExternal)
        {
            Log.Error("관문 소환용 서버 소환 콜백이 없습니다.");
            return 0;
        }

        if (!useExternal && (spawner == null || spawnManager == null))
        {
            Log.Error("EnemySpawner 또는 SpawnManager가 없습니다.");
            return 0;
        }

        int spawnedCount = 0;

        for (int i = 0; i < count; i++)
        {
            Transform gate = gates[i % gates.Length];

            if (!TryGetGateSpawnPosition(gate, out Vector3 position))
                continue;

            Quaternion rotation = GetSpawnRotation(position, initialTarget);
            bool spawned;

            if (ExternalSpawn != null)
            {
                spawned = ExternalSpawn(selfDestructEnemy.enemyId, position, rotation, initialTarget);
            }
            else if (ExternalSpawnTracked != null)
            {
                spawned = ExternalSpawnTracked(selfDestructEnemy.enemyId, position, rotation, initialTarget) != null;
            }
            else
            {
                spawned = spawner.Spawn(selfDestructEnemy.enemyId, position, rotation, initialTarget, statContext) != null;
            }

            if (spawned)
                spawnedCount++;
        }

        // 외부 서버 소환은 서버 콜백에서 집계하므로 중복 등록하지 않는다.
        if (!useExternal && spawnedCount > 0)
            spawnManager.RegisterAdditionalEnemies(spawnedCount);

        return spawnedCount;
    }

    private bool TryGetGateSpawnPosition(Transform gate, out Vector3 position)
    {
        int attempts = Mathf.Max(1, positionSearchAttempts);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle * gateSpawnRadius;

            Vector3 desired = gate.position + new Vector3(offset.x, 0f, offset.y);

            if (UnityEngine.AI.NavMesh.SamplePosition(desired, out UnityEngine.AI.NavMeshHit hit, navMeshSearchRadius, UnityEngine.AI.NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }
        }

        position = default;
        return false;
    }
}

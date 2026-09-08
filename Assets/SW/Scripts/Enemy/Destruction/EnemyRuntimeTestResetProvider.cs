using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// WBH 전투 코드는 수정하지 않고, 테스트 패널의 리셋 요청만 기존 공개 API에 연결한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(ArtificerRuntimeTuningPanel))]
public sealed class EnemyRuntimeTestResetProvider : MonoBehaviour,
    IArtificerRuntimeResetProvider
{
    private const string MissingManagerWarning =
        "[EnemyRuntimeTestResetProvider] 현재 씬에서 WBH 적 풀, 스폰 매니저 또는 스폰 영역을 찾지 못했습니다.";

    [SerializeField, InspectorName("다시 생성할 적 등급과 수")]
    private WBH_EnemySpawnManager.GradeCount[] enemies =
    {
        new WBH_EnemySpawnManager.GradeCount
        {
            grade = EnemyGrade.Normal,
            count = 1
        }
    };

    [SerializeField, InspectorName("리셋 단축키")]
    private KeyCode resetKey = KeyCode.R;

    [Header("다시 생성할 적 배치")]
    [SerializeField, Min(1), InspectorName("한 줄에 배치할 적 수")]
    private int formationColumns = 5;

    [SerializeField, Min(0.5f), InspectorName("적 사이 간격")]
    private float formationSpacing = 2.5f;

    [SerializeField, Min(0.1f), InspectorName("NavMesh 탐색 반경")]
    private float navMeshSampleRadius = 1.25f;

    private System.Collections.IEnumerator Start()
    {
        // WBH SpawnManager의 첫 웨이브 생성은 그대로 실행한 뒤,
        // 이 테스트 씬에서는 사망 이벤트가 다음 웨이브나 Portal을 호출하지 않게 한다.
        yield return null;
        DisableWaveProgression();
    }

    private void Update()
    {
        if (Input.GetKeyDown(resetKey))
        {
            if (TryGetComponent(out ArtificerRuntimeTuningPanel panel))
                panel.RespawnAndApply();
            else
                ResetRuntimeArtificerTargets();
        }
    }

    public void ConfigureSpawnCounts(int normalCount, int eliteCount = 0)
    {
        normalCount = Mathf.Max(0, normalCount);
        eliteCount = Mathf.Max(0, eliteCount);
        enemies = eliteCount > 0
            ? new[]
            {
                new WBH_EnemySpawnManager.GradeCount
                {
                    grade = EnemyGrade.Normal,
                    count = normalCount
                },
                new WBH_EnemySpawnManager.GradeCount
                {
                    grade = EnemyGrade.Elite,
                    count = eliteCount
                }
            }
            : new[]
            {
                new WBH_EnemySpawnManager.GradeCount
                {
                    grade = EnemyGrade.Normal,
                    count = normalCount
                }
            };

        int totalCount = normalCount + eliteCount;
        if (totalCount > 0)
            formationColumns = Mathf.CeilToInt(Mathf.Sqrt(totalCount));
    }

    public void ResetRuntimeArtificerTargets()
    {
        EnemyDestructionService destructionService =
            GetComponent<EnemyDestructionService>();
        if (destructionService != null)
        {
            destructionService.ReturnAllActive();
        }

        WBH_EnemyPoolManager poolManager =
            FindFirstObjectByType<WBH_EnemyPoolManager>();
        WBH_EnemySpawnManager spawnManager =
            FindFirstObjectByType<WBH_EnemySpawnManager>();
        List<WBH_EnemySpawnArea> spawnAreas = FindSceneSpawnAreas();
        if (poolManager == null || spawnManager == null || spawnAreas.Count == 0)
        {
            Debug.LogWarning(
                MissingManagerWarning,
                this);
            return;
        }

        // 이 컴포넌트가 배치된 성능 테스트 씬에서는 적 사망을 웨이브
        // 진행으로 소비하지 않는다. 스폰 영역 API는 매니저가 비활성 상태여도
        // 호출할 수 있으므로 테스트용 재생성 흐름은 그대로 유지된다.
        DisableWaveProgression(spawnManager);

        WBH_EnemyController[] enemies =
            FindObjectsByType<WBH_EnemyController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            WBH_EnemyController enemy = enemies[i];
            if (enemy != null &&
                enemy.gameObject.scene.handle == gameObject.scene.handle &&
                enemy.Info != null)
            {
                poolManager.Return(enemy);
            }
        }

        SpawnConfiguredEnemies(spawnAreas);

        ArrangeAndSyncSpawnedEnemies();
    }

    private List<WBH_EnemySpawnArea> FindSceneSpawnAreas()
    {
        WBH_EnemySpawnArea[] found =
            FindObjectsByType<WBH_EnemySpawnArea>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        List<WBH_EnemySpawnArea> result = new List<WBH_EnemySpawnArea>();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null &&
                found[i].gameObject.scene.handle == gameObject.scene.handle)
            {
                result.Add(found[i]);
            }
        }
        return result;
    }

    private void SpawnConfiguredEnemies(List<WBH_EnemySpawnArea> spawnAreas)
    {
        if (enemies == null)
            return;

        for (int entryIndex = 0; entryIndex < enemies.Length; entryIndex++)
        {
            WBH_EnemySpawnManager.GradeCount entry = enemies[entryIndex];
            int count = Mathf.Max(0, entry.count);
            for (int i = 0; i < count; i++)
            {
                WBH_EnemySpawnArea area =
                    spawnAreas[UnityEngine.Random.Range(0, spawnAreas.Count)];
                area.Spawn(entry.grade, 1, new WBH_EnemyStatContext(1,"normal", 1));
            }
        }
    }

    private static void DisableWaveProgression(
        WBH_EnemySpawnManager spawnManager = null)
    {
        if (spawnManager == null)
            spawnManager = FindFirstObjectByType<WBH_EnemySpawnManager>();

        if (spawnManager != null)
            spawnManager.enabled = false;
    }

    private void ArrangeAndSyncSpawnedEnemies()
    {
        WBH_EnemyController[] enemies =
            FindObjectsByType<WBH_EnemyController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        if (enemies.Length == 0)
        {
            return;
        }

        Vector3 center = Vector3.zero;
        int sceneEnemyCount = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null ||
                enemies[i].gameObject.scene.handle != gameObject.scene.handle)
            {
                continue;
            }

            center += enemies[i].transform.position;
            sceneEnemyCount++;
        }

        if (sceneEnemyCount == 0)
        {
            return;
        }

        center /= sceneEnemyCount;
        int columns = Mathf.Clamp(formationColumns, 1, sceneEnemyCount);
        int rows = Mathf.CeilToInt(sceneEnemyCount / (float)columns);
        int arrangedIndex = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            WBH_EnemyController enemy = enemies[i];
            if (enemy == null ||
                enemy.gameObject.scene.handle != gameObject.scene.handle)
            {
                continue;
            }

            int row = arrangedIndex / columns;
            int column = arrangedIndex % columns;
            Vector3 candidate = center + new Vector3(
                (column - (columns - 1) * 0.5f) * formationSpacing,
                0f,
                (row - (rows - 1) * 0.5f) * formationSpacing);

            if (NavMesh.SamplePosition(
                    candidate,
                    out NavMeshHit hit,
                    navMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.Warp(hit.position);
                }
                else
                {
                    enemy.transform.position = hit.position;
                }
            }
            else
            {
                enemy.transform.position = candidate;
            }

            WBH_EnemyStatus status = enemy.GetComponent<WBH_EnemyStatus>();
            if (status != null)
            {
                status.Heal(0f);
            }

            arrangedIndex++;
        }
    }

    private void OnValidate()
    {
        if (enemies != null)
        {
            for (int i = 0; i < enemies.Length; i++)
                enemies[i].count = Mathf.Max(0, enemies[i].count);
        }
        formationColumns = Mathf.Max(1, formationColumns);
        formationSpacing = Mathf.Max(0.5f, formationSpacing);
        navMeshSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);
    }
}

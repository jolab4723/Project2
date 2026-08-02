using UnityEngine;

// WBH 전투 코드는 수정하지 않고, 테스트 패널의 리셋 요청만 기존 공개 API에 연결한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(ArtificerRuntimeTuningPanel))]
public sealed class EnemyRuntimeTestResetProvider : MonoBehaviour,
    IArtificerRuntimeResetProvider
{
    private const string MissingManagerWarning =
        "[EnemyRuntimeTestResetProvider] 현재 씬에서 WBH 적 풀 또는 스폰 매니저를 찾지 못했습니다.";

    [SerializeField, Min(0), InspectorName("다시 생성할 일반 적 수")]
    private int normalEnemyCount = 1;

    [SerializeField, Min(0), InspectorName("다시 생성할 정예 적 수")]
    private int eliteEnemyCount;

    [SerializeField, InspectorName("리셋 단축키")]
    private KeyCode resetKey = KeyCode.R;

    private void Update()
    {
        if (Input.GetKeyDown(resetKey))
        {
            ResetRuntimeArtificerTargets();
        }
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
        if (poolManager == null || spawnManager == null)
        {
            Debug.LogWarning(
                MissingManagerWarning,
                this);
            return;
        }

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

        if (normalEnemyCount > 0)
        {
            spawnManager.SpawnNormal(normalEnemyCount);
        }
        if (eliteEnemyCount > 0)
        {
            spawnManager.SpawnElite(eliteEnemyCount);
        }
    }

    private void OnValidate()
    {
        normalEnemyCount = Mathf.Max(0, normalEnemyCount);
        eliteEnemyCount = Mathf.Max(0, eliteEnemyCount);
    }
}

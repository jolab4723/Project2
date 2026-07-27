using System.Collections;
using System.Collections.Generic;
using Artifice;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyManualTestReset : MonoBehaviour,
    IArtificerRuntimeResetProvider
{
    private static readonly Rect ResetButtonRect =
        new Rect(28f, 112f, 260f, 32f);

    [Header("실제 적 본체 프리팹")]
    [SerializeField, InspectorName("Enemy 1 - 실제 적 본체")]
    private GameObject enemy1Prefab;
    [SerializeField, InspectorName("Enemy 2 - 실제 적 본체")]
    private GameObject enemy2Prefab;

    [Header("파괴 연출 프리팹")]
    [SerializeField, InspectorName("Enemy 1 - 파괴 연출")]
    private GameObject enemy1DestructionVisualPrefab;
    [SerializeField, InspectorName("Enemy 2 - 파괴 연출")]
    private GameObject enemy2DestructionVisualPrefab;

    [Header("기존 씬 참조 호환")]
    [SerializeField] private GameObject currentEnemy1;
    [SerializeField] private GameObject currentEnemy2;
    [SerializeField] private Vector3 enemy1Position =
        new Vector3(0f, 1f, 1.45f);
    [SerializeField] private Vector3 enemy1Rotation =
        new Vector3(0f, 180f, 0f);
    [SerializeField] private Vector3 enemy2Position =
        new Vector3(3.5f, 1f, 0f);
    [SerializeField] private Vector3 enemy2Rotation =
        new Vector3(0f, 90f, 0f);

    [Header("시연 배치")]
    [SerializeField, Min(0), InspectorName("Enemy 1 생성 수")]
    private int enemy1Count = 6;
    [SerializeField, Min(0), InspectorName("Enemy 2 생성 수")]
    private int enemy2Count = 6;
    [SerializeField, Min(1)] private int gridColumns = 4;
    [SerializeField, Min(0.5f)] private float horizontalSpacing = 3f;
    [SerializeField, Min(0.5f)] private float depthSpacing = 2.8f;
    [SerializeField] private Vector3 formationCenter =
        new Vector3(1.75f, 1f, 4.5f);
    [SerializeField] private bool alternateVariants = true;

    [Header("전투 테스트")]
    [SerializeField, Min(0.01f), InspectorName("적 체력")]
    private float enemyHealth = 1f;
    [SerializeField, Min(0f), InspectorName("파편 방향 힘")]
    private float directionalForce = 3f;
    [SerializeField, Range(0, 31), InspectorName("적 레이어")]
    private int enemyLayer = 10;
    [SerializeField, InspectorName("리셋 키")]
    private KeyCode resetKey = KeyCode.R;

    private readonly List<GameObject> currentEnemies =
        new List<GameObject>();

    // WBH 입력 시스템 연동부: 리셋 버튼 클릭이 플레이어 공격으로 전달되지 않도록 잠시 입력을 막는다.
    private WBH_PlayerInputHandler playerInput;
    private EnemyDestructionVisualPool destructionVisualPool;
    private DestructionDamageStrengthScaler damageStrengthScaler;
    private bool restorePlayerInput;
    private bool playerInputWasEnabled;
    private int restorePlayerInputAfterFrame;

    public int ResetCount { get; private set; }
    public int ConfiguredEnemyCount => enemy1Count + enemy2Count;
    public int ActiveEnemyCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < currentEnemies.Count; i++)
            {
                if (currentEnemies[i] != null &&
                    currentEnemies[i].activeInHierarchy)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public void ResetRuntimeArtificerTargets()
    {
        ResetEnemies();
    }

    public void ResetEnemies()
    {
        if (destructionVisualPool != null)
        {
            destructionVisualPool.ReturnAll();
        }
        RetireCurrentEnemies();

        int total = ConfiguredEnemyCount;
        int enemy1Spawned = 0;
        int enemy2Spawned = 0;

        for (int slot = 0; slot < total; slot++)
        {
            bool spawnEnemy1 = SelectEnemy1(
                slot,
                enemy1Spawned,
                enemy2Spawned);
            GameObject prefab = spawnEnemy1
                ? enemy1Prefab
                : enemy2Prefab;
            GameObject destructionVisualPrefab = spawnEnemy1
                ? enemy1DestructionVisualPrefab
                : enemy2DestructionVisualPrefab;
            int enemyNumber = spawnEnemy1
                ? ++enemy1Spawned
                : ++enemy2Spawned;
            string enemyLabel = spawnEnemy1 ? "Enemy 1" : "Enemy 2";
            Quaternion rotation = Quaternion.Euler(
                spawnEnemy1 ? enemy1Rotation : enemy2Rotation);

            GameObject enemy = SpawnEnemy(
                prefab,
                destructionVisualPrefab,
                $"{enemyLabel} {enemyNumber:00} - Manual Attack Target",
                GetFormationPosition(slot, total),
                rotation);
            if (enemy == null)
            {
                continue;
            }

            currentEnemies.Add(enemy);
            if (spawnEnemy1 && currentEnemy1 == null)
            {
                currentEnemy1 = enemy;
            }
            else if (!spawnEnemy1 && currentEnemy2 == null)
            {
                currentEnemy2 = enemy;
            }
        }

        ResetCount++;

        ArtificerRuntimeTuningPanel tuningPanel =
            GetComponent<ArtificerRuntimeTuningPanel>();
        if (tuningPanel != null && !tuningPanel.IsRespawning)
        {
            tuningPanel.RefreshTargetsAndApply();
        }
    }

    private IEnumerator Start()
    {
        // WBH 입력 시스템 연동부: 씬의 플레이어 입력 핸들러를 찾아 리셋 클릭을 보호한다.
        playerInput = FindFirstObjectByType<WBH_PlayerInputHandler>();
        destructionVisualPool = GetComponent<EnemyDestructionVisualPool>();
        if (destructionVisualPool == null)
        {
            destructionVisualPool =
                gameObject.AddComponent<EnemyDestructionVisualPool>();
        }
        damageStrengthScaler =
            GetComponent<DestructionDamageStrengthScaler>();
        if (damageStrengthScaler == null)
        {
            damageStrengthScaler =
                gameObject.AddComponent<DestructionDamageStrengthScaler>();
        }

        yield return PrewarmDestructionVisuals();
        ResetEnemies();
    }

    private IEnumerator PrewarmDestructionVisuals()
    {
        if (destructionVisualPool == null)
            yield break;

        bool usesSameVisual = enemy1DestructionVisualPrefab != null &&
            enemy1DestructionVisualPrefab == enemy2DestructionVisualPrefab;
        int enemy1WarmCount = Mathf.Max(0, enemy1Count) +
            (usesSameVisual ? Mathf.Max(0, enemy2Count) : 0);

        yield return destructionVisualPool.Prewarm(
            enemy1DestructionVisualPrefab,
            enemy1WarmCount);

        if (!usesSameVisual)
        {
            yield return destructionVisualPool.Prewarm(
                enemy2DestructionVisualPrefab,
                Mathf.Max(0, enemy2Count));
        }
    }

    private void Update()
    {
        RestorePlayerInputIfReady();

        if (Input.GetKeyDown(resetKey))
        {
            ResetEnemies();
        }

        Vector2 guiMousePosition = new Vector2(
            Input.mousePosition.x,
            Screen.height - Input.mousePosition.y);
        if (Input.GetMouseButtonDown(0) &&
            ResetButtonRect.Contains(guiMousePosition))
        {
            SuppressPlayerInputForCurrentClick();
            ResetEnemies();
        }
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUI.Box(new Rect(16f, 16f, 284f, 140f), "적 수동 파괴 테스트");
        GUI.Label(new Rect(28f, 42f, 260f, 22f), "좌클릭: 마우스 방향 공격");
        GUI.Label(new Rect(28f, 62f, 260f, 22f), "우클릭: 이동");
        GUI.Label(
            new Rect(28f, 82f, 260f, 22f),
            $"현재 {ActiveEnemyCount}마리 / 설정 {ConfiguredEnemyCount}마리");
        GUI.Button(
            ResetButtonRect,
            $"적 {ConfiguredEnemyCount}마리 다시 생성 (R)");
#endif
    }

    private bool SelectEnemy1(
        int slot,
        int enemy1Spawned,
        int enemy2Spawned)
    {
        if (enemy1Spawned >= enemy1Count)
        {
            return false;
        }
        if (enemy2Spawned >= enemy2Count)
        {
            return true;
        }

        return alternateVariants
            ? slot % 2 == 0
            : enemy1Spawned < enemy1Count;
    }

    private Vector3 GetFormationPosition(int slot, int total)
    {
        int columns = Mathf.Max(1, gridColumns);
        int rows = Mathf.Max(1, Mathf.CeilToInt(total / (float)columns));
        int row = slot / columns;
        int column = slot % columns;
        int itemsInRow = Mathf.Min(columns, total - row * columns);

        float xOffset =
            (column - (itemsInRow - 1) * 0.5f) * horizontalSpacing;
        float zOffset =
            (row - (rows - 1) * 0.5f) * depthSpacing;
        return formationCenter + new Vector3(xOffset, 0f, zOffset);
    }

    private GameObject SpawnEnemy(
        GameObject prefab,
        GameObject destructionVisualPrefab,
        string instanceName,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError(
                $"[Enemy Manual Test] {instanceName} 프리팹이 없습니다.",
                this);
            return null;
        }

        GameObject instance = Instantiate(
            prefab,
            position,
            rotation,
            transform);
        ConfigureEnemy(instance, destructionVisualPrefab, instanceName);
        return instance;
    }

    private void ConfigureEnemy(
        GameObject enemy,
        GameObject destructionVisualPrefab,
        string instanceName)
    {
        if (enemy == null)
        {
            return;
        }

        enemy.name = instanceName;
        enemy.layer = enemyLayer;
        EnsureRootHitCollider(enemy);

        Artificer artificer = enemy.GetComponent<Artificer>();
        CombatDroneVisualAnimator visualAnimator =
            enemy.GetComponent<CombatDroneVisualAnimator>();
        CombatDroneArtificerDestruction destruction =
            enemy.GetComponent<CombatDroneArtificerDestruction>();

        if (destruction != null)
        {
            destruction.Configure(
                artificer,
                visualAnimator,
                false,
                0f,
                false);
        }

        EnemyDestructionTarget target =
            enemy.GetComponent<EnemyDestructionTarget>();
        if (target == null)
        {
            target = enemy.AddComponent<EnemyDestructionTarget>();
        }
        target.Configure(
            enemyHealth,
            directionalForce,
            destructionVisualPool,
            destructionVisualPrefab,
            damageStrengthScaler);

        target.enemyGrade = EnemyGrade.Elite;
    }

    private static void EnsureRootHitCollider(GameObject enemy)
    {
        if (enemy == null || enemy.GetComponent<Collider>() != null)
        {
            return;
        }

        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>(true);
        Transform root = enemy.transform;
        bool hasBounds = false;
        Bounds localBounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer ||
                renderer is LineRenderer)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldCorner = center + new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z);
                Vector3 localCorner = root.InverseTransformPoint(worldCorner);
                if (!hasBounds)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        if (!hasBounds)
        {
            return;
        }

        BoxCollider hitCollider = enemy.AddComponent<BoxCollider>();
        hitCollider.center = localBounds.center;
        hitCollider.size = new Vector3(
            Mathf.Max(0.1f, localBounds.size.x),
            Mathf.Max(0.1f, localBounds.size.y),
            Mathf.Max(0.1f, localBounds.size.z));
    }

    private void RetireCurrentEnemies()
    {
        HashSet<int> retiredIds = new HashSet<int>();
        for (int i = 0; i < currentEnemies.Count; i++)
        {
            RetireEnemyOnce(currentEnemies[i], retiredIds);
        }
        currentEnemies.Clear();

        RetireEnemyOnce(currentEnemy1, retiredIds);
        RetireEnemyOnce(currentEnemy2, retiredIds);
        currentEnemy1 = null;
        currentEnemy2 = null;
    }

    private static void RetireEnemyOnce(
        GameObject enemy,
        HashSet<int> retiredIds)
    {
        if (enemy == null || !retiredIds.Add(enemy.GetInstanceID()))
        {
            return;
        }

        // 인스펙터에 프리팹 에셋이 실수로 들어와도 에셋 자체를 끄거나 제거하지 않는다.
        if (!enemy.scene.IsValid())
        {
            Debug.LogWarning(
                "[Enemy Manual Test] 씬 인스턴스가 아닌 프리팹 에셋은 " +
                "제거 대상에서 제외했습니다: " + enemy.name);
            return;
        }

        enemy.SetActive(false);
        Destroy(enemy);
    }

    // WBH 입력 시스템 연동부: 리셋 UI를 누른 같은 클릭이 공격으로 처리되지 않게 한다.
    private void SuppressPlayerInputForCurrentClick()
    {
        if (playerInput == null)
        {
            return;
        }

        playerInputWasEnabled = playerInput.enabled;
        playerInput.enabled = false;
        restorePlayerInput = true;
        restorePlayerInputAfterFrame = Time.frameCount;
    }

    // WBH 입력 시스템 연동부: 다음 프레임에 원래 입력 활성 상태를 복원한다.
    private void RestorePlayerInputIfReady()
    {
        if (!restorePlayerInput ||
            Time.frameCount <= restorePlayerInputAfterFrame)
        {
            return;
        }

        if (playerInput != null)
        {
            playerInput.enabled = playerInputWasEnabled;
        }
        restorePlayerInput = false;
    }

    private void OnDisable()
    {
        if (restorePlayerInput && playerInput != null)
        {
            playerInput.enabled = playerInputWasEnabled;
        }
        restorePlayerInput = false;
    }

    private void OnValidate()
    {
        enemy1Count = Mathf.Max(0, enemy1Count);
        enemy2Count = Mathf.Max(0, enemy2Count);
        gridColumns = Mathf.Max(1, gridColumns);
        horizontalSpacing = Mathf.Max(0.5f, horizontalSpacing);
        depthSpacing = Mathf.Max(0.5f, depthSpacing);
        enemyHealth = Mathf.Max(0.01f, enemyHealth);
        directionalForce = Mathf.Max(0f, directionalForce);
        enemyLayer = Mathf.Clamp(enemyLayer, 0, 31);
    }
}

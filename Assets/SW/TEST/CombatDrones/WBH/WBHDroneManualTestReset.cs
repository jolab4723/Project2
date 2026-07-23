using System.Collections.Generic;
using Artifice;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WBHDroneManualTestReset : MonoBehaviour,
    IArtificerRuntimeResetProvider
{
    private static readonly Rect ResetButtonRect =
        new Rect(28f, 112f, 260f, 32f);

    [Header("Drone Prefabs")]
    [SerializeField] private GameObject droneDPrefab;
    [SerializeField] private GameObject droneFPrefab;

    [Header("Legacy Scene References")]
    [SerializeField] private GameObject currentDroneD;
    [SerializeField] private GameObject currentDroneF;
    [SerializeField] private Vector3 droneDPosition =
        new Vector3(0f, 1f, 1.45f);
    [SerializeField] private Vector3 droneDRotation =
        new Vector3(0f, 180f, 0f);
    [SerializeField] private Vector3 droneFPosition =
        new Vector3(3.5f, 1f, 0f);
    [SerializeField] private Vector3 droneFRotation =
        new Vector3(0f, 90f, 0f);

    [Header("Demo Formation")]
    [SerializeField, Min(0)] private int droneDCount = 6;
    [SerializeField, Min(0)] private int droneFCount = 6;
    [SerializeField, Min(1)] private int gridColumns = 4;
    [SerializeField, Min(0.5f)] private float horizontalSpacing = 3f;
    [SerializeField, Min(0.5f)] private float depthSpacing = 2.8f;
    [SerializeField] private Vector3 formationCenter =
        new Vector3(1.75f, 1f, 4.5f);
    [SerializeField] private bool alternateVariants = true;

    [Header("Combat Test")]
    [SerializeField, Min(0.01f)] private float droneHealth = 1f;
    [SerializeField, Min(0f)] private float directionalForce = 4.5f;
    [SerializeField, Range(0, 31)] private int enemyLayer = 10;
    [SerializeField] private KeyCode resetKey = KeyCode.R;

    private readonly List<GameObject> currentDrones =
        new List<GameObject>();
    private WBH_PlayerInputHandler playerInput;
    private bool restorePlayerInput;
    private bool playerInputWasEnabled;
    private int restorePlayerInputAfterFrame;

    public int ResetCount { get; private set; }
    public int ConfiguredDroneCount => droneDCount + droneFCount;
    public int ActiveDroneCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < currentDrones.Count; i++)
            {
                if (currentDrones[i] != null &&
                    currentDrones[i].activeInHierarchy)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public void ResetRuntimeArtificerTargets()
    {
        ResetDrones();
    }

    public void ResetDrones()
    {
        RetireCurrentDrones();

        int total = ConfiguredDroneCount;
        int dSpawned = 0;
        int fSpawned = 0;

        for (int slot = 0; slot < total; slot++)
        {
            bool spawnD = SelectDroneD(slot, dSpawned, fSpawned);
            GameObject prefab = spawnD ? droneDPrefab : droneFPrefab;
            int variantNumber = spawnD ? ++dSpawned : ++fSpawned;
            string variant = spawnD ? "D" : "F";
            Quaternion rotation = Quaternion.Euler(
                spawnD ? droneDRotation : droneFRotation);

            GameObject drone = SpawnDrone(
                prefab,
                $"Drone {variant} {variantNumber:00} - Manual Attack Target",
                GetFormationPosition(slot, total),
                rotation);
            if (drone == null)
            {
                continue;
            }

            currentDrones.Add(drone);
            if (spawnD && currentDroneD == null)
            {
                currentDroneD = drone;
            }
            else if (!spawnD && currentDroneF == null)
            {
                currentDroneF = drone;
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

    private void Start()
    {
        playerInput = FindFirstObjectByType<WBH_PlayerInputHandler>();
        ResetDrones();
    }

    private void Update()
    {
        RestorePlayerInputIfReady();

        if (Input.GetKeyDown(resetKey))
        {
            ResetDrones();
        }

        Vector2 guiMousePosition = new Vector2(
            Input.mousePosition.x,
            Screen.height - Input.mousePosition.y);
        if (Input.GetMouseButtonDown(0) &&
            ResetButtonRect.Contains(guiMousePosition))
        {
            SuppressPlayerInputForCurrentClick();
            ResetDrones();
        }
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUI.Box(new Rect(16f, 16f, 284f, 140f), "드론 다중 파괴 테스트");
        GUI.Label(new Rect(28f, 42f, 260f, 22f), "좌클릭: 마우스 방향 공격");
        GUI.Label(new Rect(28f, 62f, 260f, 22f), "우클릭: 이동");
        GUI.Label(
            new Rect(28f, 82f, 260f, 22f),
            $"현재 {ActiveDroneCount}마리 / 설정 {ConfiguredDroneCount}마리");
        GUI.Button(
            ResetButtonRect,
            $"드론 {ConfiguredDroneCount}마리 다시 생성 (R)");
#endif
    }

    private bool SelectDroneD(int slot, int dSpawned, int fSpawned)
    {
        if (dSpawned >= droneDCount)
        {
            return false;
        }
        if (fSpawned >= droneFCount)
        {
            return true;
        }

        return alternateVariants
            ? slot % 2 == 0
            : dSpawned < droneDCount;
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

    private GameObject SpawnDrone(
        GameObject prefab,
        string instanceName,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError(
                $"[WBH Drone Manual Test] {instanceName} 프리팹이 없습니다.",
                this);
            return null;
        }

        GameObject instance = Instantiate(
            prefab,
            position,
            rotation,
            transform);
        ConfigureDrone(instance, instanceName);
        return instance;
    }

    private void ConfigureDrone(GameObject drone, string instanceName)
    {
        if (drone == null)
        {
            return;
        }

        drone.name = instanceName;
        drone.layer = enemyLayer;

        Artificer artificer = drone.GetComponent<Artificer>();
        CombatDroneVisualAnimator visualAnimator =
            drone.GetComponent<CombatDroneVisualAnimator>();
        CombatDroneArtificerDestruction destruction =
            drone.GetComponent<CombatDroneArtificerDestruction>();

        if (destruction != null)
        {
            destruction.Configure(
                artificer,
                visualAnimator,
                false,
                0f,
                false);
        }

        WBHCombatDroneDestructionTarget target =
            drone.GetComponent<WBHCombatDroneDestructionTarget>();
        if (target == null)
        {
            target = drone.AddComponent<WBHCombatDroneDestructionTarget>();
        }
        target.Configure(droneHealth, directionalForce);
    }

    private void RetireCurrentDrones()
    {
        HashSet<int> retiredIds = new HashSet<int>();
        for (int i = 0; i < currentDrones.Count; i++)
        {
            RetireDroneOnce(currentDrones[i], retiredIds);
        }
        currentDrones.Clear();

        RetireDroneOnce(currentDroneD, retiredIds);
        RetireDroneOnce(currentDroneF, retiredIds);
        currentDroneD = null;
        currentDroneF = null;
    }

    private static void RetireDroneOnce(
        GameObject drone,
        HashSet<int> retiredIds)
    {
        if (drone == null || !retiredIds.Add(drone.GetInstanceID()))
        {
            return;
        }

        drone.SetActive(false);
        Destroy(drone);
    }

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
        droneDCount = Mathf.Max(0, droneDCount);
        droneFCount = Mathf.Max(0, droneFCount);
        gridColumns = Mathf.Max(1, gridColumns);
        horizontalSpacing = Mathf.Max(0.5f, horizontalSpacing);
        depthSpacing = Mathf.Max(0.5f, depthSpacing);
        droneHealth = Mathf.Max(0.01f, droneHealth);
        directionalForce = Mathf.Max(0f, directionalForce);
        enemyLayer = Mathf.Clamp(enemyLayer, 0, 31);
    }
}

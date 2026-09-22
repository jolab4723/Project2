using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Act 규칙에 맞는 노드 맵 생성, 노드 선택 진행, 경로 활성화와 자동 스크롤을 총괄합니다.
/// </summary>
public class YJ_StageSelectManager : MonoBehaviour
{
    // 노드 프리팹을 Resources.Load로 불러올 때 사용하는 공통 폴더 경로입니다.
    private const string PrefabPath = "Prefabs/Map/StageNode/";
    // 분기 분리 조건을 만족하는 전체 맵 배치를 찾기 위해 허용할 최대 생성 횟수입니다.
    private const int MaximumMapGenerationAttempts = 32;
    // 미지 노드가 이동하여 실제 이벤트 씬을 다시 선택할 중간 씬 이름입니다.
    private const string UnknownMasterSceneName = "Unknown_Stage";
    // Unknown 이벤트 정의 데이터베이스의 Resources 경로입니다.
    private const string UnknownStageDatabaseResourcePath =
        "DataFiles/UnknownStageData/3. GeneratedAssets/AllUnknownStages";
    // 맵 Seed와 Unknown 이벤트 배정용 Seed를 분리하는 고정값입니다.
    private const int UnknownStageSeedSalt = 1851936439;
    // 확률에 따라 생성될 수 있는 일반 중간층 노드 종류 목록입니다.
    private static readonly StageNodeType[] MiddleNodeTypes =
    {
        StageNodeType.Battle,
        StageNodeType.Elite,
        StageNodeType.Camp,
        StageNodeType.Event
    };

    // (일반, 엘리트, 캠프, 미지) 스테이지 출현 확률
    // Act 1의 층 수, 고정 엘리트 층과 중간 노드 생성 가중치입니다.
    private static readonly ActRules Act1Rules =
        new(11, new[] { 6 }, 50f, 10f, 25f, 15f);
    // Act 2의 층 수, 고정 엘리트 층과 중간 노드 생성 가중치입니다.
    private static readonly ActRules Act2Rules =
        new(12, new[] { 5, 9 }, 45f, 15f, 20f, 20f);
    // Act 3의 층 수, 고정 엘리트 층과 중간 노드 생성 가중치입니다.
    private static readonly ActRules Act3Rules =
        new(13, new[] { 4, 7, 10 }, 30f, 10f, 35f, 25f);

    [Header("Act")]
    // 현재 생성하고 진행할 Act입니다.
    [SerializeField] private StageActType currentAct = StageActType.Act1;
    // 완료된 가장 높은 층 번호이며 다음 선택 가능 층 계산에 사용합니다.
    [SerializeField, Min(0)] private int clearedFloor;
    // 마지막으로 실제 선택하여 클리어한 노드의 ID입니다.
    [SerializeField] private string lastClearedNodeId;
    // 0이 아니면 동일한 노드 배치를 재현하는 고정 Seed로 사용합니다.
    [SerializeField] private int mapSeed;

    [Header("Progression Test")]
    // 테스트 중 클릭 즉시 노드를 완료 처리하고 다음 층을 활성화할지 결정합니다.
    [SerializeField] private bool completeNodeOnClick = true;
    // 테스트 모드에서 Reticle 애니메이션이 끝난 뒤 완료 및 스크롤까지 기다릴 시간입니다.
    [SerializeField, Min(0f)] private float testCompletionDelay = 1f;

    [Header("Scroll Focus")]
    // 노드 선택 후 다음 층을 ScrollRect 중앙으로 자동 이동할지 결정합니다.
    [FormerlySerializedAs("centerSelectedNode")]
    [SerializeField] private bool centerNextFloor = true;
    // 다음 층이 ScrollRect 중앙까지 이동하는 데 걸리는 시간입니다.
    [FormerlySerializedAs("nodeCenterDuration")]
    [SerializeField, Min(0f)] private float floorCenterDuration = 0.25f;

    [Header("Map References")]
    // 맵 Content의 세로 스크롤 위치를 제어할 ScrollRect입니다.
    [SerializeField] private ScrollRect mapScrollRect;
    // 층별 노드 개수와 위치를 계산하는 컨트롤러입니다.
    [SerializeField] private YJ_StageNodeLayoutController nodeLayoutController;
    // 노드 연결 그래프와 라인 UI를 생성하는 컨트롤러입니다.
    [SerializeField] private YJ_StageNodeLineController nodeLineController;
    // 맵 로드 후 마지막 클리어 노드의 위치를 표시할 HUD Reticle입니다.
    [SerializeField] private YJ_StageNodeReticle nodeReticle;

    [Header("Scene Transition")]
    // Reticle 애니메이션 종료 후 선택한 노드의 씬을 불러오는 테스트용 로더입니다.
    [SerializeField] private YJ_TestSceneLoader testSceneLoader;
    // Normal/Elite, Camp, Boss 노드에서 사용할 씬 이름을 Act별로 관리합니다.
    [FormerlySerializedAs("combatScenesByAct")]
    [SerializeField] private List<ActSceneList> stageScenesByAct =
        CreateDefaultStageSceneLists();
    // Event 노드에 맵 생성 시점부터 고정 이벤트를 배정할 데이터베이스입니다.
    [SerializeField] private YJ_UnknownStageDatabaseSO unknownStageDatabase;

    [Header("Local Progress")]
    // 스테이지 이동 전 맵을 저장하고 Stage Select 복귀 시 복원하는 서비스입니다.
    [SerializeField] private YJ_StageSaveService stageSaveService;
    // 로컬 JSON 저장 파일이 있으면 새 맵 생성보다 저장된 진행 상태 복원을 우선합니다.
    [SerializeField] private bool loadSavedMapOnStart = true;

    // 현재 맵에 생성된 모든 노드 UI 컴포넌트 목록입니다.
    private readonly List<YJ_StageNodeHover> generatedNodes = new();
    // 층 인덱스별로 생성된 노드 데이터를 묶어 보관합니다.
    private readonly List<List<YJ_StageNodeData>> generatedFloors = new();
    // 노드 종류별로 Resources에서 불러온 프리팹을 캐싱합니다.
    private readonly Dictionary<StageNodeType, GameObject> nodePrefabs = new();
    // 노드 ID로 데이터를 빠르게 찾기 위한 런타임 조회 사전입니다.
    private readonly Dictionary<string, YJ_StageNodeData> nodesById = new();
    // 마지막 클리어 노드에서 도달 가능한 노드 ID를 재사용해 저장합니다.
    private readonly HashSet<string> reachableNodeIds = new();
    // 도달 가능한 노드를 너비 우선 탐색할 때 재사용하는 큐입니다.
    private readonly Queue<YJ_StageNodeData> nodesToVisit = new();
    // 현재 맵 진행 중 일반/엘리트 노드에서 이미 사용한 전투 씬 이름입니다.
    private readonly HashSet<string> usedStageSceneNames = new();
    // 씬 선택 시 아직 사용하지 않은 후보를 모아 재사용하는 임시 목록입니다.
    private readonly List<string> availableStageSceneNames = new();
    // 유효한 Unknown 이벤트 정의를 배정할 때 재사용하는 임시 목록입니다.
    private readonly List<YJ_UnknownStageDefinitionSO> availableUnknownStages = new();

    // 현재 맵 생성에서 노드 종류와 연결을 결정할 난수 생성기입니다.
    private System.Random random;
    // 현재 생성 시도에서 만들어진 Event 노드 수입니다.
    private int generatedUnknownNodeCount;
    // 현재 Act에 대응하는 층 수와 노드 생성 규칙입니다.
    private ActRules currentRules;
    // 다음 층 중앙 이동을 실행 중인 코루틴입니다.
    private Coroutine floorCenterRoutine;
    // 테스트 모드에서 Reticle 종료 후 노드 완료를 지연하는 코루틴입니다.
    private Coroutine testCompletionRoutine;

    // 노드 UI가 현재 씬의 스테이지 선택 매니저를 찾을 때 사용하는 Singleton 참조입니다.
    public static YJ_StageSelectManager Instance { get; private set; }
    // 현재 사용자가 클릭하여 선택한 노드 UI입니다.
    public YJ_StageNodeHover SelectedNode { get; private set; }
    // 외부 시스템에서 현재 Act를 읽을 때 사용합니다.
    public StageActType CurrentAct => currentAct;
    // 현재 Act 규칙의 전체 층 수를 반환합니다.
    public int TotalFloors => currentRules.floorCount;
    // 현재까지 완료된 가장 높은 층을 반환합니다.
    public int ClearedFloor => clearedFloor;
    // 마지막으로 실제 클리어한 노드 ID를 저장 또는 조회할 때 사용합니다.
    public string LastClearedNodeId => lastClearedNodeId;
    // 현재 플레이어가 선택할 수 있는 다음 층 번호를 반환합니다.
    public int CurrentSelectableFloor => clearedFloor >= TotalFloors ? 0 : clearedFloor + 1;
    // 이번 맵 생성에 실제로 사용된 Seed를 반환합니다.
    public int GeneratedSeed { get; private set; }

    // 노드가 유효하게 선택된 직후 해당 노드 데이터를 외부 시스템에 전달합니다.
    public event Action<YJ_StageNodeData> NodeSelected;

    /// <summary>
    /// Singleton 중복을 방지하고 현재 Act 규칙 및 필수 컴포넌트 참조를 준비합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one YJ_StageSelectManager exists in the scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        currentRules = GetRules(currentAct);
        FindReferences();
    }

    /// <summary>
    /// 씬 시작 시 로컬 저장 맵을 우선 복원하고 저장 파일이 없으면 새 맵을 생성합니다.
    /// </summary>
    private void Start()
    {
        // 임시 런의 씬 간 진행 복원은 유지하되, 디스크 저장은 SaveService에서 차단한다.
        if ((YJ_StageSaveService.IsSessionOnly || loadSavedMapOnStart) &&
            stageSaveService != null &&
            stageSaveService.HasSaveFile)
        {
            if (stageSaveService.LoadCurrentMap())
                return;

            Log.Warning("저장된 스테이지 맵 복원에 실패하여 새 맵을 생성합니다.");
        }

        GenerateMap();
        YJ_BgmPlayer.Instance.Play(YJ_BgmPlayer.YJ_BgmType.StageSelectBgm);
    }

    /// <summary>
    /// 매니저가 파괴될 때 Singleton 참조를 안전하게 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Act 규칙에 따라 모든 층의 노드와 라인을 새로 만들고 진행 상태를 반영합니다.
    /// </summary>
    public void GenerateMap()
    {
        usedStageSceneNames.Clear();
        availableStageSceneNames.Clear();
        currentRules = GetRules(currentAct);
        FindReferences();

        RectTransform nodesLayer = nodeLayoutController != null
            ? nodeLayoutController.NodesLayer
            : null;
        if (nodesLayer == null)
        {
            Debug.LogError("NodesLayer was not found.", this);
            return;
        }

        LoadPrefabs();
        TryPrepareUnknownStageCandidates();

        int initialSeed = mapSeed != 0 ? mapSeed : Environment.TickCount;
        if (initialSeed == 0)
            initialSeed = 1;

        bool mapGenerated = false;
        for (int attempt = 0; attempt < MaximumMapGenerationAttempts; attempt++)
        {
            nodeLineController?.ClearLines();
            ClearGeneratedNodes();
            nodeLayoutController.PrepareMapRect(currentRules.floorCount);

            GeneratedSeed = GetGenerationAttemptSeed(initialSeed, attempt);
            random = new System.Random(GeneratedSeed);
            generatedUnknownNodeCount = 0;

            for (int floor = 1; floor <= currentRules.floorCount; floor++)
                GenerateFloor(floor, nodesLayer);

            mapGenerated = nodeLineController == null ||
                           nodeLineController.Rebuild(generatedFloors, random);
            if (!mapGenerated)
                continue;

            if (attempt > 0)
            {
                Debug.Log(
                    $"Stage map branch separation succeeded after {attempt + 1} attempts. " +
                    $"Seed: {GeneratedSeed}",
                    this);
            }

            break;
        }

        if (!mapGenerated)
        {
            bool fallbackGenerated = nodeLineController == null ||
                                     nodeLineController.Rebuild(
                                         generatedFloors,
                                         random,
                                         false);
            if (fallbackGenerated)
            {
                Debug.LogWarning(
                    $"Could not satisfy branch separation after " +
                    $"{MaximumMapGenerationAttempts} attempts. " +
                    $"A connected fallback map was generated with Seed {GeneratedSeed}.",
                    this);
            }
            else
            {
                Debug.LogError("Stage map line generation failed.", this);
            }
        }

        AssignUnknownStageIds();
        clearedFloor = Mathf.Clamp(clearedFloor, 0, currentRules.floorCount);
        RefreshNodeAvailability();

        Canvas.ForceUpdateCanvases();
        if (mapScrollRect != null)
            mapScrollRect.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// 이전 Act의 진행 상태를 제거하고 지정한 Act의 첫 층부터 시작하는 새 맵을 생성합니다.
    /// </summary>
    public bool GenerateNewActMap(StageActType act)
    {
        if (!Enum.IsDefined(typeof(StageActType), act))
        {
            Debug.LogError($"Unknown Act value {act}.", this);
            return false;
        }

        currentAct = act;
        mapSeed = 0;
        clearedFloor = 0;
        lastClearedNodeId = string.Empty;

        nodeReticle?.Hide();
        mapScrollRect?.StopMovement();
        GenerateMap();

        if (generatedFloors.Count != currentRules.floorCount)
            return false;

        foreach (List<YJ_StageNodeData> floorNodes in generatedFloors)
        {
            if (floorNodes == null || floorNodes.Count == 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 테스트 버튼에서 새 Seed와 초기 진행 상태로 전체 노드 배치 및 연결을 다시 생성합니다.
    /// 기존 JSON 저장 파일에는 영향을 주지 않습니다.
    /// </summary>
    public void RerollMapForTesting()
    {
        mapSeed = CreateRerollSeed();
        clearedFloor = 0;
        lastClearedNodeId = string.Empty;

        nodeReticle?.Hide();
        mapScrollRect?.StopMovement();
        GenerateMap();

        Debug.Log($"Stage map re-rolled for testing. Seed: {GeneratedSeed}", this);
    }

    /// <summary>
    /// 현재 생성 Seed와 중복되지 않는 0 이외의 새 테스트용 Seed를 만듭니다.
    /// </summary>
    private int CreateRerollSeed()
    {
        int newSeed;
        do
        {
            newSeed = Guid.NewGuid().GetHashCode();
        }
        while (newSeed == 0 || newSeed == GeneratedSeed);

        return newSeed;
    }

    /// <summary>
    /// 같은 최초 Seed에서 생성 재시도 순번별로 재현 가능한 파생 Seed를 반환합니다.
    /// </summary>
    private static int GetGenerationAttemptSeed(int initialSeed, int attempt)
    {
        if (attempt == 0)
            return initialSeed;

        unchecked
        {
            int derivedSeed = initialSeed ^ attempt * -1640531527;
            derivedSeed ^= derivedSeed >> 16;
            return derivedSeed != 0 ? derivedSeed : attempt;
        }
    }

    /// <summary>
    /// 현재 생성된 노드 배치, 연결 정보와 진행 상태를 JSON 저장용 순수 데이터로 변환합니다.
    /// </summary>
    public StageMapSaveData CaptureSaveData()
    {
        StageMapSaveData saveData = new()
        {
            act = currentAct,
            mapSeed = GeneratedSeed,
            clearedFloor = clearedFloor,
            lastClearedNodeId = lastClearedNodeId ?? string.Empty,
            pendingNodeId = SelectedNode != null && SelectedNode.NodeData != null
                ? SelectedNode.NodeData.id
                : string.Empty
        };

        saveData.usedStageSceneNames.AddRange(usedStageSceneNames);
        saveData.usedStageSceneNames.Sort(StringComparer.Ordinal);
        CollectConfiguredCombatSceneNames(currentAct, true, saveData.unknownCombatSceneNames);
        saveData.unknownCampSceneName = GetStageSceneList(currentAct)?.CampSceneName;

        foreach (List<YJ_StageNodeData> floorNodes in generatedFloors)
        {
            foreach (YJ_StageNodeData node in floorNodes)
            {
                if (node == null)
                    continue;

                StageNodeSaveData nodeSaveData = new()
                {
                    id = node.id,
                    floor = node.floor,
                    nodeIndex = node.nodeIndex,
                    type = node.type,
                    sceneName = node.sceneName,
                    unknownStageId = node.unknownStageId,
                    positionX = node.position.x,
                    positionY = node.position.y,
                    nextNodeIds = new List<string>(node.nextNodeIds)
                };

                saveData.nodes.Add(nodeSaveData);

                if (!node.cleared)
                    continue;

                saveData.clearedNodeIds.Add(node.id);
                saveData.visitedNodeIds.Add(node.id);
            }
        }

        return saveData;
    }

    /// <summary>
    /// JSON에서 읽은 스냅샷을 이용해 노드와 라인을 새로 만들고 진행 상태를 복원합니다.
    /// </summary>
    public bool RestoreMap(StageMapSaveData saveData)
    {
        if (!ValidateSaveData(saveData, out string validationError))
        {
            Debug.LogError($"Invalid stage map save data: {validationError}", this);
            return false;
        }

        currentAct = saveData.act;
        currentRules = GetRules(currentAct);
        FindReferences();

        RectTransform nodesLayer = nodeLayoutController != null
            ? nodeLayoutController.NodesLayer
            : null;
        if (nodesLayer == null)
        {
            Debug.LogError("NodesLayer was not found.", this);
            return false;
        }

        LoadPrefabs();
        foreach (StageNodeSaveData nodeSaveData in saveData.nodes)
        {
            if (GetPrefab(nodeSaveData.type) != null)
                continue;

            Debug.LogError($"No prefab is available for saved node type {nodeSaveData.type}.", this);
            return false;
        }

        nodeLineController?.ClearLines();
        ClearGeneratedNodes();
        nodeLayoutController.PrepareMapRect(currentRules.floorCount);

        GeneratedSeed = saveData.mapSeed;
        random = new System.Random(GeneratedSeed);

        for (int floor = 0; floor < currentRules.floorCount; floor++)
            generatedFloors.Add(new List<YJ_StageNodeData>());

        HashSet<string> clearedNodeIds = saveData.clearedNodeIds != null
            ? new HashSet<string>(saveData.clearedNodeIds)
            : new HashSet<string>();
        List<StageNodeSaveData> orderedNodes = new(saveData.nodes);
        orderedNodes.Sort((first, second) =>
        {
            int floorComparison = first.floor.CompareTo(second.floor);
            return floorComparison != 0
                ? floorComparison
                : first.nodeIndex.CompareTo(second.nodeIndex);
        });

        foreach (StageNodeSaveData nodeSaveData in orderedNodes)
            CreateNodeFromSaveData(nodeSaveData, clearedNodeIds, nodesLayer);

        AssignUnknownStageIds();
        RestoreUsedStageSceneNames(saveData, clearedNodeIds);

        foreach (StageNodeSaveData nodeSaveData in orderedNodes)
        {
            YJ_StageNodeData node = nodesById[nodeSaveData.id];
            node.nextNodeIds.Clear();

            if (nodeSaveData.nextNodeIds == null)
                continue;

            foreach (string nextNodeId in nodeSaveData.nextNodeIds)
            {
                if (!node.nextNodeIds.Contains(nextNodeId))
                    node.nextNodeIds.Add(nextNodeId);
            }
        }

        clearedFloor = Mathf.Clamp(saveData.clearedFloor, 0, currentRules.floorCount);
        lastClearedNodeId = saveData.lastClearedNodeId ?? string.Empty;

        nodeLineController?.RebuildFromSavedConnections(generatedFloors);
        RefreshNodeAvailability();
        RestorePendingSelection(saveData.pendingNodeId);

        Canvas.ForceUpdateCanvases();
        int focusFloor = SelectedNode != null && SelectedNode.NodeData != null
            ? SelectedNode.NodeData.floor + 1
            : CurrentSelectableFloor;
        CenterFloorVertically(focusFloor);
        RestoreReticleToLastClearedNode();

        return true;
    }

    /// <summary>
    /// 로드된 마지막 클리어 노드에 Reticle을 애니메이션 없이 즉시 배치합니다.
    /// </summary>
    private void RestoreReticleToLastClearedNode()
    {
        if (nodeReticle == null)
            return;

        YJ_StageNodeData lastClearedNode = FindGeneratedNodeData(lastClearedNodeId);
        if (lastClearedNode != null)
            nodeReticle.ShowAt(lastClearedNode);
        else
            nodeReticle.Hide();
    }

    // 노드를 선택했을때 호출되는 메서드
    /// <summary>
    /// 클릭된 노드가 현재 선택 가능한지 검증하고 단일 선택 상태 및 선택 이벤트를 처리합니다.
    /// </summary>
    public bool SelectNode(YJ_StageNodeHover node)
    {
        if (node == null || !node.IsInteractable)
            return false;

        YJ_StageNodeData data = node.NodeData;
        if (data == null || data.floor != CurrentSelectableFloor)
            return false;

        if (SelectedNode == node)
            return false;

        if (SelectedNode != null)
            SelectedNode.SetSelected(false);

        SelectedNode = node;
        SelectedNode.SetSelected(true);
        DisableAlternativeNodesOnFloor(data.floor, node);
        RefreshPathStateForSelectedNode(data);
        NodeSelected?.Invoke(data);

        return true;
    }

    /// <summary>
    /// 노드를 선택한 순간 같은 층의 나머지 노드를 경로에서 제외하고 Cleared Tint 상태로 전환합니다.
    /// </summary>
    private void DisableAlternativeNodesOnFloor(int floor, YJ_StageNodeHover selectedNode)
    {
        foreach (YJ_StageNodeHover node in generatedNodes)
        {
            if (node == null || node == selectedNode || node.NodeData == null ||
                node.NodeData.floor != floor)
                continue;

            YJ_StageNodeData data = node.NodeData;
            data.available = false;
            node.ApplyState(
                data.cleared,
                data.floor <= clearedFloor,
                true,
                false);
        }
    }

    /// <summary>
    /// 선택 노드에서 도달 가능한 미래 노드와 라인을 계산하고 나머지 경로를 비활성 상태로 표시합니다.
    /// </summary>
    private void RefreshPathStateForSelectedNode(YJ_StageNodeData selectedNode)
    {
        if (selectedNode == null)
            return;

        HashSet<string> reachableFromSelection = FindReachableNodeIds(selectedNode);
        reachableFromSelection.Add(selectedNode.id);

        foreach (YJ_StageNodeHover node in generatedNodes)
        {
            if (node == null || node.NodeData == null ||
                node.NodeData.floor <= selectedNode.floor)
            {
                continue;
            }

            YJ_StageNodeData data = node.NodeData;
            bool pathBlocked = !reachableFromSelection.Contains(data.id);
            data.available = false;
            node.ApplyState(data.cleared, false, pathBlocked, false);
        }

        nodeLineController?.RefreshReachability(reachableFromSelection, true);
    }

    /// <summary>
    /// 현재 선택 노드를 클리어 처리하고 다음 층 노드와 라인 도달 상태를 갱신합니다.
    /// </summary>
    public void CompleteSelectedNode()
    {
        if (testCompletionRoutine != null)
        {
            StopCoroutine(testCompletionRoutine);
            testCompletionRoutine = null;
        }

        if (SelectedNode == null || SelectedNode.NodeData == null)
            return;

        YJ_StageNodeData completedNode = SelectedNode.NodeData;
        completedNode.cleared = true;
        lastClearedNodeId = completedNode.id;
        clearedFloor = Mathf.Clamp(completedNode.floor, 0, TotalFloors);
        ClearSelection();
        RefreshNodeAvailability();

        if (centerNextFloor)
            CenterFloorVertically(completedNode.floor + 1);
    }

    /// <summary>
    /// Reticle 선택 애니메이션이 끝난 뒤 선택한 노드의 전투 또는 이벤트 씬으로 전환합니다.
    /// 생성 또는 저장 데이터에 기록된 sceneName을 테스트 씬 로더에 전달합니다.
    /// </summary>
    public void TransitionToSelectedNodeScene(YJ_StageNodeData nodeData)
    {
        if (nodeData == null)
        {
            Log.Warning("씬으로 전환할 노드 데이터가 없습니다.");
            return;
        }

        if (testSceneLoader == null)
            FindSceneLoader();

        if (testSceneLoader == null)
        {
            Log.Error("YJ_TestSceneLoader를 찾을 수 없습니다.");
            return;
        }

        if (stageSaveService == null)
            FindSaveService();

        if (stageSaveService == null)
        {
            Log.Error("YJ_StageSaveService를 찾을 수 없습니다.");
            return;
        }

        bool reservedCombatScene = ReserveSceneForNode(nodeData);
        if (string.IsNullOrWhiteSpace(nodeData.sceneName))
        {
            Log.Warning(
                $"노드에 이동할 씬 이름이 지정되지 않았습니다: {nodeData.id}");
            return;
        }

        if (!stageSaveService.SaveCurrentMap())
        {
            if (reservedCombatScene)
            {
                usedStageSceneNames.Remove(nodeData.sceneName);
                nodeData.sceneName = string.Empty;
            }

            Log.Error("스테이지 진행 상태 저장에 실패하여 씬 전환을 중단합니다.");
            return;
        }

        YJ_BgmPlayer.Instance.Stop();
        testSceneLoader.LoadScene(nodeData.sceneName);
    }

    /// <summary>
    /// Reticle 애니메이션 종료 후 테스트 모드에서는 지연 완료를, 실제 모드에서는 씬 전환을 시작합니다.
    /// </summary>
    public void NotifyReticleAnimationCompleted(YJ_StageNodeData nodeData)
    {
        if (nodeData == null)
            return;

        if (!completeNodeOnClick)
        {
            TransitionToSelectedNodeScene(nodeData);
            return;
        }

        if (testCompletionRoutine != null)
            StopCoroutine(testCompletionRoutine);

        testCompletionRoutine = StartCoroutine(
            CompleteNodeAfterReticleDelay(nodeData));
    }

    /// <summary>
    /// 테스트 모드에서 지정 시간만큼 기다린 뒤 선택 노드를 완료하고 다음 층으로 스크롤합니다.
    /// </summary>
    private IEnumerator CompleteNodeAfterReticleDelay(YJ_StageNodeData expectedNode)
    {
        if (testCompletionDelay > 0f)
            yield return new WaitForSecondsRealtime(testCompletionDelay);

        testCompletionRoutine = null;

        if (SelectedNode == null || SelectedNode.NodeData != expectedNode)
            yield break;

        CompleteSelectedNode();
    }

    /// <summary>
    /// 저장 데이터나 디버그 입력으로 클리어 층을 직접 변경하고 노드 상태를 다시 계산합니다.
    /// </summary>
    public void SetClearedFloor(int floor)
    {
        clearedFloor = Mathf.Clamp(floor, 0, TotalFloors);

        YJ_StageNodeData lastClearedNode = FindGeneratedNodeData(lastClearedNodeId);
        if (lastClearedNode == null || lastClearedNode.floor != clearedFloor)
            lastClearedNodeId = string.Empty;

        ClearSelection();
        RefreshNodeAvailability();
    }

    /// <summary>
    /// 현재 선택 노드의 선택 표현을 해제하고 선택 참조를 비웁니다.
    /// </summary>
    public void ClearSelection()
    {
        if (SelectedNode == null)
            return;

        SelectedNode.SetSelected(false);
        SelectedNode = null;
    }

    /// <summary>
    /// 선택 중인 노드가 비활성화될 때 남아 있는 선택 참조를 정리합니다.
    /// </summary>
    public void NotifyNodeDisabled(YJ_StageNodeHover node)
    {
        if (SelectedNode == node)
            SelectedNode = null;
    }

    /// <summary>
    /// 지정한 층이 유효하면 기존 이동을 교체하고 세로 중앙 스크롤을 시작합니다.
    /// </summary>
    private void CenterFloorVertically(int floor)
    {
        if (mapScrollRect == null || mapScrollRect.content == null ||
            floor < 1 || floor > generatedFloors.Count)
            return;

        if (floorCenterRoutine != null)
            StopCoroutine(floorCenterRoutine);

        floorCenterRoutine = StartCoroutine(CenterFloorVerticallyRoutine(floor));
    }

    /// <summary>
    /// 지정한 층 전체 노드의 세로 중심을 계산해 ScrollRect를 부드럽게 이동시킵니다.
    /// </summary>
    private IEnumerator CenterFloorVerticallyRoutine(int floor)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        RectTransform viewport = mapScrollRect.viewport != null
            ? mapScrollRect.viewport
            : mapScrollRect.transform as RectTransform;

        if (viewport == null || floor < 1 || floor > generatedFloors.Count)
        {
            floorCenterRoutine = null;
            yield break;
        }

        Bounds contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            viewport,
            mapScrollRect.content);
        float scrollableHeight = contentBounds.size.y - viewport.rect.height;

        if (scrollableHeight <= Mathf.Epsilon)
        {
            floorCenterRoutine = null;
            yield break;
        }

        bool hasFloorBounds = false;
        Bounds floorBounds = default;

        foreach (YJ_StageNodeData nodeData in generatedFloors[floor - 1])
        {
            RectTransform nodeRect = nodeData != null ? nodeData.transform as RectTransform : null;
            if (nodeRect == null)
                continue;

            Bounds nodeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, nodeRect);
            if (!hasFloorBounds)
            {
                floorBounds = nodeBounds;
                hasFloorBounds = true;
            }
            else
            {
                floorBounds.Encapsulate(nodeBounds);
            }
        }

        if (!hasFloorBounds)
        {
            floorCenterRoutine = null;
            yield break;
        }

        float offsetFromCenter = floorBounds.center.y - viewport.rect.center.y;
        float startPosition = mapScrollRect.verticalNormalizedPosition;
        float targetPosition = Mathf.Clamp01(startPosition + offsetFromCenter / scrollableHeight);

        mapScrollRect.StopMovement();

        if (floorCenterDuration <= Mathf.Epsilon)
        {
            mapScrollRect.verticalNormalizedPosition = targetPosition;
            floorCenterRoutine = null;
            yield break;
        }

        float elapsedTime = 0f;
        while (elapsedTime < floorCenterDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / floorCenterDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            mapScrollRect.verticalNormalizedPosition = Mathf.Lerp(startPosition, targetPosition, easedT);
            yield return null;
        }

        mapScrollRect.verticalNormalizedPosition = targetPosition;
        floorCenterRoutine = null;
    }

    /// <summary>
    /// 한 층의 노드 개수와 종류 및 위치를 결정하고 프리팹 인스턴스를 초기화합니다.
    /// </summary>
    private void GenerateFloor(int floor, RectTransform nodesLayer)
    {
        int nodeCount = nodeLayoutController.GetNodeCount(
            floor,
            currentRules.floorCount,
            generatedFloors,
            random);
        List<YJ_StageNodeData> floorNodes = new(nodeCount);
        generatedFloors.Add(floorNodes);

        for (int index = 0; index < nodeCount; index++)
        {
            StageNodeType type = GetNodeType(floor);
            GameObject prefab = GetPrefab(type);

            if (prefab == null)
            {
                Debug.LogError($"No prefab is available for node type {type}.", this);
                continue;
            }

            if (type == StageNodeType.Event)
                generatedUnknownNodeCount++;

            GameObject instance = Instantiate(prefab, nodesLayer, false);
            RectTransform nodeRect = instance.GetComponent<RectTransform>();
            if (nodeRect == null)
            {
                Debug.LogError($"{prefab.name} requires a RectTransform.", prefab);
                Destroy(instance);
                continue;
            }

            Vector2 position = nodeLayoutController.GetNodePosition(
                floor,
                index,
                nodeCount,
                currentRules.floorCount,
                random);

            nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
            nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
            nodeRect.anchoredPosition = position;

            string nodeId = $"A{(int)currentAct}_F{floor:00}_N{index + 1:00}";
            instance.name = $"{nodeId}_{type}";

            YJ_StageNodeData data = instance.GetComponent<YJ_StageNodeData>();
            if (data == null)
                data = instance.AddComponent<YJ_StageNodeData>();

            data.Initialize(nodeId, currentAct, floor, index, type, position);
            data.sceneName = ResolveInitialNodeSceneName(type);
            nodesById[nodeId] = data;

            YJ_StageNodeHover node = instance.GetComponent<YJ_StageNodeHover>();
            if (node != null)
            {
                node.Initialize(data, this);
                generatedNodes.Add(node);
            }

            floorNodes.Add(data);
        }
    }

    /// <summary>
    /// 저장된 노드 한 개를 대응하는 프리팹으로 생성하고 런타임 조회 컬렉션에 등록합니다.
    /// </summary>
    private void CreateNodeFromSaveData(
        StageNodeSaveData nodeSaveData,
        ISet<string> clearedNodeIds,
        RectTransform nodesLayer)
    {
        GameObject prefab = GetPrefab(nodeSaveData.type);
        GameObject instance = Instantiate(prefab, nodesLayer, false);
        instance.name = $"{nodeSaveData.id}_{nodeSaveData.type}";

        RectTransform nodeRect = instance.GetComponent<RectTransform>();
        Vector2 position = new(nodeSaveData.positionX, nodeSaveData.positionY);
        nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
        nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
        nodeRect.anchoredPosition = position;

        YJ_StageNodeData data = instance.GetComponent<YJ_StageNodeData>();
        if (data == null)
            data = instance.AddComponent<YJ_StageNodeData>();

        data.Initialize(
            nodeSaveData.id,
            currentAct,
            nodeSaveData.floor,
            nodeSaveData.nodeIndex,
            nodeSaveData.type,
            position);
        data.sceneName = string.IsNullOrWhiteSpace(nodeSaveData.sceneName)
            ? ResolveInitialNodeSceneName(nodeSaveData.type)
            : nodeSaveData.sceneName;
        data.unknownStageId = nodeSaveData.unknownStageId ?? string.Empty;
        data.cleared = clearedNodeIds.Contains(nodeSaveData.id);

        nodesById[data.id] = data;
        generatedFloors[data.floor - 1].Add(data);

        YJ_StageNodeHover node = instance.GetComponent<YJ_StageNodeHover>();
        if (node == null)
            return;

        node.Initialize(data, this);
        generatedNodes.Add(node);
    }

    /// <summary>
    /// 저장 당시 선택했지만 아직 완료하지 않은 노드를 선택 상태로 복구합니다.
    /// </summary>
    private void RestorePendingSelection(string pendingNodeId)
    {
        if (string.IsNullOrEmpty(pendingNodeId))
            return;

        foreach (YJ_StageNodeHover node in generatedNodes)
        {
            if (node == null || node.NodeData == null || node.NodeData.id != pendingNodeId)
                continue;

            if (!node.IsInteractable)
            {
                Debug.LogWarning($"Saved pending node is not currently selectable: {pendingNodeId}", this);
                return;
            }

            SelectedNode = node;
            SelectedNode.SetSelected(true);
            DisableAlternativeNodesOnFloor(node.NodeData.floor, node);
            RefreshPathStateForSelectedNode(node.NodeData);
            return;
        }
    }

    /// <summary>
    /// 저장 버전, 노드 ID, 층 범위와 모든 연결 대상이 복원 가능한지 검사합니다.
    /// </summary>
    private static bool ValidateSaveData(StageMapSaveData saveData, out string error)
    {
        if (saveData == null)
        {
            error = "Save data is null.";
            return false;
        }

        if (saveData.saveVersion != 1)
        {
            error = $"Unsupported save version {saveData.saveVersion}.";
            return false;
        }

        if (!Enum.IsDefined(typeof(StageActType), saveData.act))
        {
            error = $"Unknown Act value {saveData.act}.";
            return false;
        }

        if (saveData.nodes == null || saveData.nodes.Count == 0)
        {
            error = "No nodes were saved.";
            return false;
        }

        ActRules rules = GetRules(saveData.act);
        HashSet<string> nodeIds = new();
        bool[] floorsWithNodes = new bool[rules.floorCount];

        foreach (StageNodeSaveData node in saveData.nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id))
            {
                error = "A saved node has no ID.";
                return false;
            }

            if (!nodeIds.Add(node.id))
            {
                error = $"Duplicate node ID {node.id}.";
                return false;
            }

            if (node.floor < 1 || node.floor > rules.floorCount)
            {
                error = $"Node {node.id} has invalid floor {node.floor}.";
                return false;
            }

            if (!Enum.IsDefined(typeof(StageNodeType), node.type))
            {
                error = $"Node {node.id} has unknown type {node.type}.";
                return false;
            }

            floorsWithNodes[node.floor - 1] = true;
        }

        for (int floorIndex = 0; floorIndex < floorsWithNodes.Length; floorIndex++)
        {
            if (floorsWithNodes[floorIndex])
                continue;

            error = $"Floor {floorIndex + 1} has no saved nodes.";
            return false;
        }

        foreach (StageNodeSaveData node in saveData.nodes)
        {
            if (node.nextNodeIds == null)
                continue;

            foreach (string nextNodeId in node.nextNodeIds)
            {
                if (nodeIds.Contains(nextNodeId))
                    continue;

                error = $"Node {node.id} points to missing node {nextNodeId}.";
                return false;
            }
        }

        if (!string.IsNullOrEmpty(saveData.lastClearedNodeId) &&
            !nodeIds.Contains(saveData.lastClearedNodeId))
        {
            error = $"Last cleared node {saveData.lastClearedNodeId} does not exist.";
            return false;
        }

        if (!string.IsNullOrEmpty(saveData.pendingNodeId) &&
            !nodeIds.Contains(saveData.pendingNodeId))
        {
            error = $"Pending node {saveData.pendingNodeId} does not exist.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// 시작, 보스, 캠프, 고정 엘리트 규칙을 우선 적용하고 나머지는 확률 종류를 반환합니다.
    /// </summary>
    private StageNodeType GetNodeType(int floor)
    {
        if (floor == 1)
            return StageNodeType.Battle;

        if (floor == currentRules.floorCount)
            return StageNodeType.Boss;

        if (floor == currentRules.floorCount - 1)
            return StageNodeType.Camp;

        if (Array.IndexOf(currentRules.eliteFloors, floor) >= 0)
            return StageNodeType.Elite;

        return GetRandomMiddleNodeType();
    }

    /// <summary>
    /// 전투 외 노드에 맵 생성 시점부터 사용할 고정 씬 이름을 지정합니다.
    /// 일반 및 엘리트 노드는 실제 진입할 때 미사용 전투 씬을 선택하므로 빈 문자열을 반환합니다.
    /// </summary>
    private string ResolveInitialNodeSceneName(StageNodeType nodeType)
    {
        ActSceneList sceneList = GetStageSceneList(currentAct);

        return nodeType switch
        {
            StageNodeType.Camp => sceneList?.CampSceneName ?? string.Empty,
            StageNodeType.Boss => sceneList?.BossSceneName ?? string.Empty,
            StageNodeType.Event => UnknownMasterSceneName,
            _ => string.Empty
        };
    }

    /// <summary>
    /// 모든 Event 노드에 맵 Seed로 재현 가능한 Unknown 이벤트 ID를 고정 배정합니다.
    /// 같은 Act에서는 가능한 한 ID가 중복되지 않도록 남은 후보를 하나씩 사용합니다.
    /// 저장 데이터의 첫 번째 유효 ID는 유지하고, 이후 중복 ID는 새로운 후보로 교체합니다.
    /// </summary>
    private void AssignUnknownStageIds()
    {
        if (availableUnknownStages.Count == 0 &&
            !TryPrepareUnknownStageCandidates())
        {
            return;
        }

        int assignmentSeed = GeneratedSeed ^ UnknownStageSeedSalt;
        if (assignmentSeed == 0)
            assignmentSeed = UnknownStageSeedSalt;

        System.Random assignmentRandom = new(assignmentSeed);
        HashSet<string> assignedStageIds = new();
        List<YJ_StageNodeData> nodesRequiringAssignment = new();

        foreach (List<YJ_StageNodeData> floorNodes in generatedFloors)
        {
            foreach (YJ_StageNodeData node in floorNodes)
            {
                if (node == null || node.type != StageNodeType.Event)
                    continue;

                if (string.IsNullOrWhiteSpace(node.unknownStageId))
                {
                    nodesRequiringAssignment.Add(node);
                    continue;
                }

                if (unknownStageDatabase.GetById(node.unknownStageId) == null)
                {
                    Log.Warning(
                        $"Unknown 이벤트 ID가 유효하지 않아 다시 배정합니다: " +
                        $"{node.id} / {node.unknownStageId}");
                    node.unknownStageId = string.Empty;
                    nodesRequiringAssignment.Add(node);
                    continue;
                }

                if (assignedStageIds.Add(node.unknownStageId))
                    continue;

                Log.Warning(
                    $"같은 Act에 중복된 Unknown 이벤트 ID를 다시 배정합니다: " +
                    $"{node.id} / {node.unknownStageId}");
                node.unknownStageId = string.Empty;
                nodesRequiringAssignment.Add(node);
            }
        }

        List<YJ_UnknownStageDefinitionSO> remainingStages = new();
        foreach (YJ_UnknownStageDefinitionSO stage in availableUnknownStages)
        {
            if (!assignedStageIds.Contains(stage.StageId))
                remainingStages.Add(stage);
        }

        ShuffleUnknownStages(remainingStages, assignmentRandom);

        if (nodesRequiringAssignment.Count > remainingStages.Count)
        {
            Log.Warning(
                $"Event 노드 수가 고유한 Unknown 이벤트 수보다 많아 " +
                $"일부 ID가 중복될 수 있습니다. 노드: " +
                $"{nodesRequiringAssignment.Count + assignedStageIds.Count}, " +
                $"이벤트: {availableUnknownStages.Count}");
        }

        int remainingIndex = 0;
        foreach (YJ_StageNodeData node in nodesRequiringAssignment)
        {
            if (remainingIndex >= remainingStages.Count)
            {
                remainingStages.Clear();
                remainingStages.AddRange(availableUnknownStages);
                ShuffleUnknownStages(remainingStages, assignmentRandom);
                remainingIndex = 0;
            }

            YJ_UnknownStageDefinitionSO candidate =
                remainingStages[remainingIndex++];
            node.unknownStageId = candidate.StageId;
            assignedStageIds.Add(candidate.StageId);
        }
    }

    /// <summary>
    /// 맵 Seed 전용 난수로 Unknown 이벤트 후보 순서를 결정적으로 섞습니다.
    /// </summary>
    private static void ShuffleUnknownStages(
        List<YJ_UnknownStageDefinitionSO> stages,
        System.Random assignmentRandom)
    {
        for (int i = stages.Count - 1; i > 0; i--)
        {
            int swapIndex = assignmentRandom.Next(i + 1);
            (stages[i], stages[swapIndex]) =
                (stages[swapIndex], stages[i]);
        }
    }

    /// <summary>
    /// Resources에서 Unknown 이벤트 데이터베이스를 불러오고 유효한 정의만 후보로 준비합니다.
    /// </summary>
    private bool TryPrepareUnknownStageCandidates()
    {
        if (unknownStageDatabase == null)
        {
            unknownStageDatabase =
                Resources.Load<YJ_UnknownStageDatabaseSO>(
                    UnknownStageDatabaseResourcePath);
        }

        availableUnknownStages.Clear();
        HashSet<string> candidateStageIds = new();

        if (unknownStageDatabase != null)
        {
            foreach (YJ_UnknownStageDefinitionSO stage in
                     unknownStageDatabase.Stages)
            {
                if (stage != null &&
                    !string.IsNullOrWhiteSpace(stage.StageId) &&
                    candidateStageIds.Add(stage.StageId))
                {
                    availableUnknownStages.Add(stage);
                }
            }
        }

        if (availableUnknownStages.Count > 0)
            return true;

        Log.Error(
            "Unknown 이벤트 데이터베이스가 없거나 유효한 이벤트가 없습니다.");
        return false;
    }

    /// <summary>
    /// 노드 진입 직전에 이동할 씬을 확정합니다.
    /// 일반 및 엘리트 노드는 아직 씬이 없을 때만 미사용 전투 씬 하나를 예약합니다.
    /// </summary>
    private bool ReserveSceneForNode(YJ_StageNodeData nodeData)
    {
        if (!IsCombatNodeType(nodeData.type))
        {
            if (string.IsNullOrWhiteSpace(nodeData.sceneName))
                nodeData.sceneName = ResolveInitialNodeSceneName(nodeData.type);

            return false;
        }

        if (!string.IsNullOrWhiteSpace(nodeData.sceneName))
        {
            usedStageSceneNames.Add(nodeData.sceneName);
            return false;
        }

        nodeData.sceneName = GetUnusedCombatSceneName();
        return !string.IsNullOrWhiteSpace(nodeData.sceneName);
    }

    /// <summary>
    /// Inspector에 등록된 현재 Act의 전투 씬 중 아직 사용하지 않은 씬을 무작위로 선택합니다.
    /// 모든 등록 씬을 사용했다면 현재 Act 목록 중 하나를 다시 선택합니다.
    /// </summary>
    private string GetUnusedCombatSceneName()
    {
        if (random == null)
        {
            int seed = mapSeed != 0 ? mapSeed : Environment.TickCount;
            random = new System.Random(seed);
        }

        availableStageSceneNames.Clear();

        CollectConfiguredCombatSceneNames(
            currentAct,
            false,
            availableStageSceneNames);

        string selectedSceneName;
        if (availableStageSceneNames.Count > 0)
        {
            selectedSceneName =
                availableStageSceneNames[random.Next(availableStageSceneNames.Count)];
        }
        else
        {
            CollectConfiguredCombatSceneNames(
                currentAct,
                true,
                availableStageSceneNames);

            if (availableStageSceneNames.Count == 0)
            {
                Log.Error(
                    $"{currentAct}에 등록된 Normal/Elite 전투 씬이 없습니다.");
                return string.Empty;
            }

            selectedSceneName =
                availableStageSceneNames[random.Next(availableStageSceneNames.Count)];
            Log.Warning(
                $"{currentAct}의 미사용 전투 씬이 없어 {selectedSceneName} 씬을 다시 사용합니다.");
        }

        usedStageSceneNames.Add(selectedSceneName);
        return selectedSceneName;
    }

    /// <summary>
    /// JSON의 사용 이력을 복원하고, 구버전에서 미리 배정된 미방문 전투 씬은 초기화합니다.
    /// </summary>
    private void RestoreUsedStageSceneNames(
        StageMapSaveData saveData,
        HashSet<string> clearedNodeIds)
    {
        usedStageSceneNames.Clear();
        availableStageSceneNames.Clear();

        if (saveData.usedStageSceneNames != null)
        {
            foreach (string sceneName in saveData.usedStageSceneNames)
            {
                if (IsConfiguredCombatSceneName(sceneName, currentAct))
                    usedStageSceneNames.Add(sceneName);
            }
        }

        foreach (YJ_StageNodeHover node in generatedNodes)
        {
            YJ_StageNodeData nodeData = node != null ? node.NodeData : null;
            if (nodeData == null || !IsCombatNodeType(nodeData.type))
                continue;

            bool hasBeenUsed = clearedNodeIds.Contains(nodeData.id)
                || nodeData.id == saveData.pendingNodeId;

            if (hasBeenUsed &&
                IsConfiguredCombatSceneName(nodeData.sceneName, currentAct))
            {
                usedStageSceneNames.Add(nodeData.sceneName);
                continue;
            }

            nodeData.sceneName = string.Empty;
        }
    }

    /// <summary>
    /// 일반 전투 또는 엘리트 전투 노드인지 확인합니다.
    /// </summary>
    private static bool IsCombatNodeType(StageNodeType nodeType)
    {
        return nodeType == StageNodeType.Battle ||
               nodeType == StageNodeType.Elite;
    }

    /// <summary>
    /// 현재 Act에 등록된 전투 씬을 결과 목록에 중복 없이 추가합니다.
    /// 아직 사용하지 않은 씬만 필요하면 includeUsedScenes를 false로 전달합니다.
    /// </summary>
    private void CollectConfiguredCombatSceneNames(
        StageActType act,
        bool includeUsedScenes,
        List<string> result)
    {
        result.Clear();

        ActSceneList sceneList = GetStageSceneList(act);
        if (sceneList == null || sceneList.CombatSceneNames == null)
            return;

        foreach (string configuredSceneName in sceneList.CombatSceneNames)
        {
            if (string.IsNullOrWhiteSpace(configuredSceneName))
                continue;

            string sceneName = configuredSceneName.Trim();
            if ((!includeUsedScenes && usedStageSceneNames.Contains(sceneName)) ||
                result.Contains(sceneName))
            {
                continue;
            }

            result.Add(sceneName);
        }
    }

    /// <summary>
    /// 지정한 Act에 대응하는 Inspector 전투 씬 목록을 반환합니다.
    /// </summary>
    private ActSceneList GetStageSceneList(StageActType act)
    {
        if (stageScenesByAct == null)
            return null;

        foreach (ActSceneList sceneList in stageScenesByAct)
        {
            if (sceneList != null && sceneList.Act == act)
                return sceneList;
        }

        return null;
    }

    /// <summary>
    /// 씬 이름이 해당 Act의 Inspector 전투 씬 목록에 포함되는지 확인합니다.
    /// </summary>
    private bool IsConfiguredCombatSceneName(
        string sceneName,
        StageActType act)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        ActSceneList sceneList = GetStageSceneList(act);
        if (sceneList == null || sceneList.CombatSceneNames == null)
            return false;

        foreach (string configuredSceneName in sceneList.CombatSceneNames)
        {
            if (string.Equals(
                configuredSceneName?.Trim(),
                sceneName.Trim(),
                StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 현재 Act 가중치와 실제 존재하는 프리팹을 기준으로 중간층 노드 종류를 추첨합니다.
    /// </summary>
    private StageNodeType GetRandomMiddleNodeType()
    {
        float totalWeight = 0f;
        foreach (StageNodeType type in MiddleNodeTypes)
            totalWeight += GetAvailableNodeWeight(type);

        if (totalWeight <= 0f)
            return StageNodeType.Battle;

        double roll = random.NextDouble() * totalWeight;
        float accumulated = 0f;
        StageNodeType fallback = StageNodeType.Battle;

        foreach (StageNodeType type in MiddleNodeTypes)
        {
            float weight = GetAvailableNodeWeight(type);
            if (weight <= 0f)
                continue;

            fallback = type;
            accumulated += weight;
            if (roll <= accumulated)
                return type;
        }

        return fallback;
    }

    /// <summary>
    /// 해당 종류의 프리팹이 있을 때만 Act 규칙에 정의된 음수가 아닌 가중치를 반환합니다.
    /// </summary>
    private float GetAvailableNodeWeight(StageNodeType type)
    {
        if (GetPrefab(type) == null)
            return 0f;

        if (type == StageNodeType.Event &&
            generatedUnknownNodeCount >= availableUnknownStages.Count)
        {
            return 0f;
        }

        float weight = type switch
        {
            StageNodeType.Battle => currentRules.battleWeight,
            StageNodeType.Elite => currentRules.eliteWeight,
            StageNodeType.Camp => currentRules.campWeight,
            StageNodeType.Event => currentRules.eventWeight,
            _ => 0f
        };

        return Mathf.Max(0f, weight);
    }

    /// <summary>
    /// 클리어 층과 마지막 노드의 연결 그래프를 기준으로 모든 노드 및 라인 상태를 갱신합니다.
    /// </summary>
    private void RefreshNodeAvailability()
    {
        int selectableFloor = CurrentSelectableFloor;
        YJ_StageNodeData lastClearedNode = FindGeneratedNodeData(lastClearedNodeId);
        bool restrictToConnectedNodes = lastClearedNode != null &&
                                        lastClearedNode.floor == clearedFloor;
        HashSet<string> reachableIds = restrictToConnectedNodes
            ? FindReachableNodeIds(lastClearedNode)
            : null;

        foreach (YJ_StageNodeHover node in generatedNodes)
        {
            if (node == null || node.NodeData == null)
                continue;

            YJ_StageNodeData data = node.NodeData;
            bool onSelectableFloor = data.floor == selectableFloor;
            bool reachableFromLastNode = !restrictToConnectedNodes ||
                                         reachableIds.Contains(data.id);
            bool available = onSelectableFloor && reachableFromLastNode;
            bool isFutureNode = data.floor > clearedFloor;
            bool pathBlocked = restrictToConnectedNodes &&
                               isFutureNode &&
                               !reachableFromLastNode;
            bool floorCleared = data.floor <= clearedFloor;
            data.available = available;
            node.ApplyState(data.cleared, floorCleared, pathBlocked, available);
        }

        nodeLineController?.RefreshReachability(reachableIds, restrictToConnectedNodes);
    }

    /// <summary>
    /// 시작 노드의 nextNodeIds를 너비 우선 탐색하여 앞으로 도달 가능한 모든 노드를 찾습니다.
    /// </summary>
    private HashSet<string> FindReachableNodeIds(YJ_StageNodeData startNode)
    {
        reachableNodeIds.Clear();
        nodesToVisit.Clear();
        nodesToVisit.Enqueue(startNode);

        while (nodesToVisit.Count > 0)
        {
            YJ_StageNodeData currentNode = nodesToVisit.Dequeue();
            foreach (string nextNodeId in currentNode.nextNodeIds)
            {
                if (!reachableNodeIds.Add(nextNodeId))
                    continue;

                if (nodesById.TryGetValue(nextNodeId, out YJ_StageNodeData nextNode))
                    nodesToVisit.Enqueue(nextNode);
            }
        }

        return reachableNodeIds;
    }

    /// <summary>
    /// 생성된 노드 중 지정한 ID와 일치하는 데이터를 사전에서 조회합니다.
    /// </summary>
    private YJ_StageNodeData FindGeneratedNodeData(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && nodesById.TryGetValue(nodeId, out YJ_StageNodeData node)
            ? node
            : null;
    }

    /// <summary>
    /// 맵 재생성 전에 스크롤 효과, 선택 상태, 노드 캐시와 기존 노드 오브젝트를 정리합니다.
    /// </summary>
    private void ClearGeneratedNodes()
    {
        if (testCompletionRoutine != null)
        {
            StopCoroutine(testCompletionRoutine);
            testCompletionRoutine = null;
        }

        if (floorCenterRoutine != null)
        {
            StopCoroutine(floorCenterRoutine);
            floorCenterRoutine = null;
        }

        ClearSelection();
        generatedNodes.Clear();
        generatedFloors.Clear();
        nodesById.Clear();
        reachableNodeIds.Clear();
        nodesToVisit.Clear();
        nodeLayoutController?.ClearNodes();
    }

    /// <summary>
    /// 일반 노드 프리팹과 현재 Act 보스 프리팹을 Resources에서 불러옵니다.
    /// </summary>
    private void LoadPrefabs()
    {
        nodePrefabs.Clear();
        LoadPrefab(StageNodeType.Battle, "StageNode_Normal");
        LoadPrefab(StageNodeType.Elite, "StageNode_Elite");
        LoadPrefab(StageNodeType.Event, "StageNode_Unknown");
        LoadPrefab(StageNodeType.Camp, "StageNode_Camp");

        string bossName = $"StageNode_Act{(int)currentAct}Boss";
        LoadPrefab(StageNodeType.Boss, bossName, false);

        if (GetPrefab(StageNodeType.Boss) == null && currentAct != StageActType.Act1)
        {
            Debug.LogWarning($"{bossName} is missing. StageNode_Act1Boss will be used as a placeholder.", this);
            LoadPrefab(StageNodeType.Boss, "StageNode_Act1Boss");
        }

    }

    /// <summary>
    /// 지정한 Resources 이름의 프리팹을 노드 종류별 캐시에 등록합니다.
    /// </summary>
    private void LoadPrefab(StageNodeType type, string prefabName, bool logError = true)
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabPath + prefabName);
        if (prefab != null)
            nodePrefabs[type] = prefab;
        else if (logError)
            Debug.LogError($"Could not load {PrefabPath}{prefabName}.", this);
    }

    /// <summary>
    /// 노드 종류에 대응하는 캐시된 프리팹을 반환합니다.
    /// </summary>
    private GameObject GetPrefab(StageNodeType type)
    {
        nodePrefabs.TryGetValue(type, out GameObject prefab);
        return prefab;
    }

    /// <summary>
    /// 레이아웃, ScrollRect, 라인 컨트롤러 참조를 찾고 필요한 컴포넌트가 없으면 추가합니다.
    /// </summary>
    private void FindReferences()
    {
        if (nodeLayoutController == null)
            nodeLayoutController = GetComponent<YJ_StageNodeLayoutController>();

        if (nodeLayoutController == null)
            nodeLayoutController = gameObject.AddComponent<YJ_StageNodeLayoutController>();

        if (mapScrollRect == null && nodeLayoutController.NodesLayer != null)
            mapScrollRect = nodeLayoutController.NodesLayer.GetComponentInParent<ScrollRect>();

        if (nodeLineController == null)
            nodeLineController = GetComponent<YJ_StageNodeLineController>();

        if (nodeLineController == null)
            nodeLineController = gameObject.AddComponent<YJ_StageNodeLineController>();

        if (nodeReticle == null)
            nodeReticle = FindFirstObjectByType<YJ_StageNodeReticle>();

        FindSceneLoader();
        FindSaveService();
    }

    /// <summary>
    /// Inspector 참조, 같은 오브젝트, 현재 씬 순서로 테스트 씬 로더를 찾고 없으면 추가합니다.
    /// </summary>
    private void FindSceneLoader()
    {
        if (testSceneLoader != null)
            return;

        testSceneLoader = GetComponent<YJ_TestSceneLoader>();
        if (testSceneLoader == null)
            testSceneLoader = FindFirstObjectByType<YJ_TestSceneLoader>();

        if (testSceneLoader == null)
            testSceneLoader = gameObject.AddComponent<YJ_TestSceneLoader>();
    }

    /// <summary>
    /// Inspector 참조, 같은 오브젝트, 현재 씬 순서로 저장 서비스를 찾고 없으면 추가합니다.
    /// </summary>
    private void FindSaveService()
    {
        if (stageSaveService != null)
            return;

        stageSaveService = GetComponent<YJ_StageSaveService>();
        if (stageSaveService == null)
            stageSaveService = FindFirstObjectByType<YJ_StageSaveService>();

        if (stageSaveService == null)
            stageSaveService = gameObject.AddComponent<YJ_StageSaveService>();
    }

    /// <summary>
    /// 현재 프로젝트에 존재하는 Act별 전투 씬을 Inspector 기본값으로 제공합니다.
    /// 새 전투 씬은 Inspector의 해당 Act 목록에 추가할 수 있습니다.
    /// </summary>
    private static List<ActSceneList> CreateDefaultStageSceneLists()
    {
        return new List<ActSceneList>
        {
            new(
                StageActType.Act1,
                "Act1_Camp",
                "Act1_BossStage",
                "Act1_Stage1",
                "Act1_Stage2",
                "Act1_Stage3",
                "Act1_Stage4",
                "Act1_Stage5",
                "Act1_Stage6"),
            new(
                StageActType.Act2,
                "Act2_Camp",
                "Act2_BossStage",
                "Act2_Stage1",
                "Act2_Stage2",
                "Act2_Stage3",
                "Act2_Stage4",
                "Act2_Stage5",
                "Act2_Stage6"),
            new(
                StageActType.Act3,
                "Act3_Camp",
                "Act3_BossStage",
                "Act3_Stage1",
                "Act3_Stage2",
                "Act3_Stage3",
                "Act3_Stage4",
                "Act3_Stage5",
                "Act3_Stage6")
        };
    }

    /// <summary>
    /// 요청한 Act에 대응하는 고정 생성 규칙을 반환합니다.
    /// </summary>
    private static ActRules GetRules(StageActType act)
    {
        return act switch
        {
            StageActType.Act1 => Act1Rules,
            StageActType.Act2 => Act2Rules,
            StageActType.Act3 => Act3Rules,
            _ => Act1Rules
        };
    }

    /// <summary>
    /// 한 Act의 층 수, 고정 엘리트 층과 중간 노드 생성 가중치를 묶는 불변 규칙입니다.
    /// </summary>
    private readonly struct ActRules
    {
        // Act의 보스층을 포함한 전체 층 수입니다.
        public readonly int floorCount;
        // 무작위 종류 대신 엘리트로 고정할 층 번호 목록입니다.
        public readonly int[] eliteFloors;
        // 일반 전투 노드가 선택될 상대 가중치입니다.
        public readonly float battleWeight;
        // 엘리트 전투 노드가 선택될 상대 가중치입니다.
        public readonly float eliteWeight;
        // 캠프 노드가 선택될 상대 가중치입니다.
        public readonly float campWeight;
        // 이벤트 노드가 선택될 상대 가중치입니다.
        public readonly float eventWeight;

        /// <summary>
        /// Act의 전체 맵 생성 규칙 값을 초기화합니다.
        /// </summary>
        public ActRules(
            int floorCount,
            int[] eliteFloors,
            float battleWeight,
            float eliteWeight,
            float campWeight,
            float eventWeight)
        {
            this.floorCount = floorCount;
            this.eliteFloors = eliteFloors;
            this.battleWeight = battleWeight;
            this.eliteWeight = eliteWeight;
            this.campWeight = campWeight;
            this.eventWeight = eventWeight;
        }
    }

    /// <summary>
    /// 한 Act와 해당 Act의 Normal/Elite, Camp, Boss 씬을 묶는 Inspector 데이터입니다.
    /// </summary>
    [Serializable]
    private sealed class ActSceneList
    {
        [SerializeField] private StageActType act;
        [FormerlySerializedAs("sceneNames")]
        [SerializeField] private List<string> combatSceneNames = new();
        [SerializeField] private string campSceneName;
        [SerializeField] private string bossSceneName;

        public StageActType Act => act;
        public List<string> CombatSceneNames => combatSceneNames;
        public string CampSceneName => campSceneName?.Trim();
        public string BossSceneName => bossSceneName?.Trim();

        public ActSceneList(
            StageActType act,
            string campSceneName,
            string bossSceneName,
            params string[] combatSceneNames)
        {
            this.act = act;
            this.campSceneName = campSceneName;
            this.bossSceneName = bossSceneName;
            this.combatSceneNames = combatSceneNames != null
                ? new List<string>(combatSceneNames)
                : new List<string>();
        }
    }
}

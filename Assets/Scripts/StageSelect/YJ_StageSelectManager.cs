using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class YJ_StageSelectManager : MonoBehaviour
{
    private const string PrefabPath = "Prefabs/Map/StageNode/";
    private static readonly StageNodeType[] MiddleNodeTypes =
    {
        StageNodeType.Battle,
        StageNodeType.Elite,
        StageNodeType.Camp,
        StageNodeType.Event
    };
    private static readonly ActRules Act1Rules =
        new(11, new[] { 6 }, 40f, 10f, 35f, 15f);
    private static readonly ActRules Act2Rules =
        new(12, new[] { 5, 9 }, 40f, 10f, 30f, 20f);
    private static readonly ActRules Act3Rules =
        new(13, new[] { 4, 7, 10 }, 30f, 10f, 35f, 25f);

    [Header("Act")]
    [SerializeField] private StageActType currentAct = StageActType.Act1;
    [SerializeField, Min(0)] private int clearedFloor;
    [SerializeField] private string lastClearedNodeId;
    [SerializeField] private int mapSeed;

    [Header("Progression Test")]
    [SerializeField] private bool completeNodeOnClick = true;

    [Header("Scroll Focus")]
    [FormerlySerializedAs("centerSelectedNode")]
    [SerializeField] private bool centerNextFloor = true;
    [FormerlySerializedAs("nodeCenterDuration")]
    [SerializeField, Min(0f)] private float floorCenterDuration = 0.25f;

    [Header("Map References")]
    [SerializeField] private ScrollRect mapScrollRect;
    [SerializeField] private YJ_StageNodeLayoutController nodeLayoutController;
    [SerializeField] private YJ_StageNodeLineController nodeLineController;

    private readonly List<YJ_StageNodeHover> generatedNodes = new();
    private readonly List<List<YJ_StageNodeData>> generatedFloors = new();
    private readonly Dictionary<StageNodeType, GameObject> nodePrefabs = new();
    private readonly Dictionary<string, YJ_StageNodeData> nodesById = new();
    private readonly HashSet<string> reachableNodeIds = new();
    private readonly Queue<YJ_StageNodeData> nodesToVisit = new();

    private System.Random random;
    private ActRules currentRules;
    private Coroutine floorCenterRoutine;

    public static YJ_StageSelectManager Instance { get; private set; }
    public YJ_StageNodeHover SelectedNode { get; private set; }
    public StageActType CurrentAct => currentAct;
    public int TotalFloors => currentRules.floorCount;
    public int ClearedFloor => clearedFloor;
    public string LastClearedNodeId => lastClearedNodeId;
    public int CurrentSelectableFloor => clearedFloor >= TotalFloors ? 0 : clearedFloor + 1;
    public int GeneratedSeed { get; private set; }

    public event Action<YJ_StageNodeData> NodeSelected;

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

    private void Start()
    {
        GenerateMap();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void GenerateMap()
    {
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
        nodeLineController?.ClearLines();
        ClearGeneratedNodes();
        nodeLayoutController.PrepareMapRect(currentRules.floorCount);

        GeneratedSeed = mapSeed != 0 ? mapSeed : Environment.TickCount;
        random = new System.Random(GeneratedSeed);

        for (int floor = 1; floor <= currentRules.floorCount; floor++)
            GenerateFloor(floor, nodesLayer);

        nodeLineController?.Rebuild(generatedFloors, random);
        clearedFloor = Mathf.Clamp(clearedFloor, 0, currentRules.floorCount);
        RefreshNodeAvailability();

        Canvas.ForceUpdateCanvases();
        if (mapScrollRect != null)
            mapScrollRect.verticalNormalizedPosition = 0f;
    }

    public bool SelectNode(YJ_StageNodeHover node)
    {
        if (node == null || !node.IsInteractable)
            return false;

        YJ_StageNodeData data = node.NodeData;
        if (data == null || data.floor != CurrentSelectableFloor)
            return false;

        if (SelectedNode == node)
            return true;

        if (SelectedNode != null)
            SelectedNode.SetSelected(false);

        SelectedNode = node;
        SelectedNode.SetSelected(true);
        NodeSelected?.Invoke(data);

        if (centerNextFloor)
            CenterFloorVertically(data.floor + 1);

        if (completeNodeOnClick)
            CompleteSelectedNode();

        return true;
    }

    public void CompleteSelectedNode()
    {
        if (SelectedNode == null || SelectedNode.NodeData == null)
            return;

        YJ_StageNodeData completedNode = SelectedNode.NodeData;
        completedNode.cleared = true;
        lastClearedNodeId = completedNode.id;
        clearedFloor = Mathf.Clamp(completedNode.floor, 0, TotalFloors);
        ClearSelection();
        RefreshNodeAvailability();
    }

    public void SetClearedFloor(int floor)
    {
        clearedFloor = Mathf.Clamp(floor, 0, TotalFloors);

        YJ_StageNodeData lastClearedNode = FindGeneratedNodeData(lastClearedNodeId);
        if (lastClearedNode == null || lastClearedNode.floor != clearedFloor)
            lastClearedNodeId = string.Empty;

        ClearSelection();
        RefreshNodeAvailability();
    }

    public void ClearSelection()
    {
        if (SelectedNode == null)
            return;

        SelectedNode.SetSelected(false);
        SelectedNode = null;
    }

    public void NotifyNodeDisabled(YJ_StageNodeHover node)
    {
        if (SelectedNode == node)
            SelectedNode = null;
    }

    private void CenterFloorVertically(int floor)
    {
        if (mapScrollRect == null || mapScrollRect.content == null ||
            floor < 1 || floor > generatedFloors.Count)
            return;

        if (floorCenterRoutine != null)
            StopCoroutine(floorCenterRoutine);

        floorCenterRoutine = StartCoroutine(CenterFloorVerticallyRoutine(floor));
    }

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

    private float GetAvailableNodeWeight(StageNodeType type)
    {
        if (GetPrefab(type) == null)
            return 0f;

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
            bool isFutureNodeBeforeBoss = data.floor > clearedFloor &&
                                          data.floor < TotalFloors;
            bool pathBlocked = restrictToConnectedNodes &&
                               isFutureNodeBeforeBoss &&
                               !reachableFromLastNode;
            bool floorCleared = data.floor <= clearedFloor;
            data.available = available;
            node.ApplyState(floorCleared, pathBlocked, available);
        }

        nodeLineController?.RefreshReachability(reachableIds, restrictToConnectedNodes);
    }

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

    private YJ_StageNodeData FindGeneratedNodeData(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && nodesById.TryGetValue(nodeId, out YJ_StageNodeData node)
            ? node
            : null;
    }

    private void ClearGeneratedNodes()
    {
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

    private void LoadPrefab(StageNodeType type, string prefabName, bool logError = true)
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabPath + prefabName);
        if (prefab != null)
            nodePrefabs[type] = prefab;
        else if (logError)
            Debug.LogError($"Could not load {PrefabPath}{prefabName}.", this);
    }

    private GameObject GetPrefab(StageNodeType type)
    {
        nodePrefabs.TryGetValue(type, out GameObject prefab);
        return prefab;
    }

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
    }

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

    private readonly struct ActRules
    {
        public readonly int floorCount;
        public readonly int[] eliteFloors;
        public readonly float battleWeight;
        public readonly float eliteWeight;
        public readonly float campWeight;
        public readonly float eventWeight;

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
}

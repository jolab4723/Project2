using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class YJ_StageNodeLineController : MonoBehaviour
{
    private const string NodeLinePrefabPath = "Prefabs/Map/StageNode/NodeLine";

    [Header("References")]
    [SerializeField] private RectTransform linesLayer;
    [SerializeField] private GameObject nodeLinePrefab;

    [Header("Connections")]
    [SerializeField, Min(0f)] private float maximumConnectionDistance = 600f;

    [Header("Reachability")]
    [SerializeField, Range(0f, 1f)] private float unreachableLineAlpha = 0.15f;

    private readonly List<NodeConnection> generatedConnections = new();
    private readonly List<ConnectionLineVisual> generatedLineVisuals = new();
    private readonly List<Graphic> lineGraphics = new();

    private void Awake()
    {
        FindReferences();
        LoadPrefab();
    }

    private void OnValidate()
    {
        maximumConnectionDistance = Mathf.Max(0f, maximumConnectionDistance);
        unreachableLineAlpha = Mathf.Clamp01(unreachableLineAlpha);
    }

    public void Rebuild(
        IReadOnlyList<List<YJ_StageNodeData>> generatedFloors,
        System.Random random)
    {
        FindReferences();
        LoadPrefab();
        ClearLines();

        if (linesLayer == null)
        {
            Debug.LogError("LinesLayer was not found.", this);
            return;
        }

        if (nodeLinePrefab == null)
        {
            Debug.LogError($"Could not load {NodeLinePrefabPath}.", this);
            return;
        }

        PrepareLayer();
        BuildFloorConnections(generatedFloors, random ?? new System.Random());
        GenerateConnectionLines();
    }

    public void ClearLines()
    {
        generatedConnections.Clear();
        generatedLineVisuals.Clear();
        lineGraphics.Clear();

        if (linesLayer == null)
            return;

        for (int i = linesLayer.childCount - 1; i >= 0; i--)
        {
            GameObject child = linesLayer.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    public void RefreshReachability(ISet<string> reachableNodeIds, bool restrictToReachableNodes)
    {
        foreach (ConnectionLineVisual lineVisual in generatedLineVisuals)
        {
            if (lineVisual.canvasGroup == null)
                continue;

            bool connectionIsReachable = !restrictToReachableNodes ||
                                         IsOnActivePath(lineVisual.connection.startNode, reachableNodeIds) &&
                                         IsOnActivePath(lineVisual.connection.endNode, reachableNodeIds);
            lineVisual.canvasGroup.alpha = lineVisual.originalAlpha *
                                           (connectionIsReachable ? 1f : unreachableLineAlpha);
        }
    }

    private static bool IsOnActivePath(YJ_StageNodeData node, ISet<string> reachableNodeIds)
    {
        return node != null &&
               (node.cleared || reachableNodeIds != null && reachableNodeIds.Contains(node.id));
    }

    private void BuildFloorConnections(
        IReadOnlyList<List<YJ_StageNodeData>> generatedFloors,
        System.Random random)
    {
        foreach (List<YJ_StageNodeData> floorNodes in generatedFloors)
        {
            foreach (YJ_StageNodeData node in floorNodes)
                node.nextNodeIds.Clear();
        }

        List<NodeConnection> candidates = new();
        List<NodeConnection> acceptedConnections = new();
        Dictionary<YJ_StageNodeData, int> candidateCounts = new();
        Dictionary<YJ_StageNodeData, bool> allowOptionalConnections = new();
        List<YJ_StageNodeData> orderedCurrentNodes = new();
        List<YJ_StageNodeData> orderedNextNodes = new();
        float maximumDistanceSquared = maximumConnectionDistance * maximumConnectionDistance;

        for (int floorIndex = 0; floorIndex < generatedFloors.Count - 1; floorIndex++)
        {
            List<YJ_StageNodeData> currentFloorNodes = generatedFloors[floorIndex];
            List<YJ_StageNodeData> nextFloorNodes = generatedFloors[floorIndex + 1];

            candidates.Clear();
            acceptedConnections.Clear();
            candidateCounts.Clear();
            allowOptionalConnections.Clear();

            foreach (YJ_StageNodeData node in currentFloorNodes)
            {
                foreach (YJ_StageNodeData nextNode in nextFloorNodes)
                {
                    bool isFirstFloorConnection = node.floor == 1 && nextNode.floor == 2;
                    float distanceSquared = (node.position - nextNode.position).sqrMagnitude;

                    if (isFirstFloorConnection || distanceSquared <= maximumDistanceSquared)
                    {
                        candidates.Add(new NodeConnection(node, nextNode));
                        candidateCounts.TryGetValue(node, out int count);
                        candidateCounts[node] = count + 1;
                    }
                }
            }

            AddMandatoryConnections(
                currentFloorNodes,
                nextFloorNodes,
                acceptedConnections,
                orderedCurrentNodes,
                orderedNextNodes);

            foreach (KeyValuePair<YJ_StageNodeData, int> pair in candidateCounts)
            {
                allowOptionalConnections[pair.Key] = pair.Value < 2 || random.NextDouble() < 0.5;
            }

            Shuffle(candidates, random);

            foreach (NodeConnection candidate in candidates)
            {
                if (ContainsConnection(acceptedConnections, candidate) ||
                    !allowOptionalConnections.TryGetValue(candidate.startNode, out bool allowOptional) ||
                    !allowOptional ||
                    CrossesAnyLine(candidate, acceptedConnections))
                {
                    continue;
                }

                acceptedConnections.Add(candidate);
            }

            foreach (NodeConnection accepted in acceptedConnections)
            {
                accepted.startNode.nextNodeIds.Add(accepted.endNode.id);
                generatedConnections.Add(accepted);
            }
        }
    }

    private static void AddMandatoryConnections(
        List<YJ_StageNodeData> currentFloorNodes,
        List<YJ_StageNodeData> nextFloorNodes,
        List<NodeConnection> acceptedConnections,
        List<YJ_StageNodeData> orderedCurrentNodes,
        List<YJ_StageNodeData> orderedNextNodes)
    {
        orderedCurrentNodes.Clear();
        orderedNextNodes.Clear();
        orderedCurrentNodes.AddRange(currentFloorNodes);
        orderedNextNodes.AddRange(nextFloorNodes);
        orderedCurrentNodes.Sort((first, second) => first.position.x.CompareTo(second.position.x));
        orderedNextNodes.Sort((first, second) => first.position.x.CompareTo(second.position.x));

        if (orderedCurrentNodes.Count == 0 || orderedNextNodes.Count == 0)
            return;

        int currentIndex = 0;
        int nextIndex = 0;
        acceptedConnections.Add(new NodeConnection(
            orderedCurrentNodes[currentIndex],
            orderedNextNodes[nextIndex]));

        while (currentIndex < orderedCurrentNodes.Count - 1 ||
               nextIndex < orderedNextNodes.Count - 1)
        {
            bool canAdvanceCurrent = currentIndex < orderedCurrentNodes.Count - 1;
            bool canAdvanceNext = nextIndex < orderedNextNodes.Count - 1;

            if (!canAdvanceCurrent)
            {
                nextIndex++;
            }
            else if (!canAdvanceNext)
            {
                currentIndex++;
            }
            else
            {
                float nextCurrentProgress =
                    (currentIndex + 1f) / (orderedCurrentNodes.Count - 1f);
                float nextFloorProgress =
                    (nextIndex + 1f) / (orderedNextNodes.Count - 1f);

                if (Mathf.Approximately(nextCurrentProgress, nextFloorProgress))
                {
                    currentIndex++;
                    nextIndex++;
                }
                else if (nextCurrentProgress < nextFloorProgress)
                {
                    currentIndex++;
                }
                else
                {
                    nextIndex++;
                }
            }

            acceptedConnections.Add(new NodeConnection(
                orderedCurrentNodes[currentIndex],
                orderedNextNodes[nextIndex]));
        }
    }

    private static bool ContainsConnection(
        List<NodeConnection> connections,
        NodeConnection candidate)
    {
        foreach (NodeConnection connection in connections)
        {
            if (connection.startNode == candidate.startNode &&
                connection.endNode == candidate.endNode)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CrossesAnyLine(
        NodeConnection candidate,
        List<NodeConnection> acceptedConnections)
    {
        foreach (NodeConnection accepted in acceptedConnections)
        {
            if (LinesCross(candidate, accepted))
                return true;
        }

        return false;
    }

    private static void Shuffle(List<NodeConnection> connections, System.Random random)
    {
        for (int i = connections.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (connections[i], connections[swapIndex]) = (connections[swapIndex], connections[i]);
        }
    }

    private static bool LinesCross(NodeConnection first, NodeConnection second)
    {
        if (first.startNode == second.startNode ||
            first.startNode == second.endNode ||
            first.endNode == second.startNode ||
            first.endNode == second.endNode)
        {
            return false;
        }

        Vector2 firstStart = first.startNode.position;
        Vector2 firstEnd = first.endNode.position;
        Vector2 secondStart = second.startNode.position;
        Vector2 secondEnd = second.endNode.position;

        float secondStartSide = Cross(firstEnd - firstStart, secondStart - firstStart);
        float secondEndSide = Cross(firstEnd - firstStart, secondEnd - firstStart);
        float firstStartSide = Cross(secondEnd - secondStart, firstStart - secondStart);
        float firstEndSide = Cross(secondEnd - secondStart, firstEnd - secondStart);

        return HaveOppositeSigns(secondStartSide, secondEndSide) &&
               HaveOppositeSigns(firstStartSide, firstEndSide);
    }

    private static bool HaveOppositeSigns(float first, float second)
    {
        const float epsilon = 0.001f;
        return (first > epsilon && second < -epsilon) ||
               (first < -epsilon && second > epsilon);
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.x * second.y - first.y * second.x;
    }

    private void GenerateConnectionLines()
    {
        foreach (NodeConnection connection in generatedConnections)
            CreateConnectionLine(connection);
    }

    private void CreateConnectionLine(NodeConnection connection)
    {
        YJ_StageNodeData startNode = connection.startNode;
        YJ_StageNodeData endNode = connection.endNode;
        Vector2 startPosition = linesLayer.InverseTransformPoint(startNode.transform.position);
        Vector2 endPosition = linesLayer.InverseTransformPoint(endNode.transform.position);
        Vector2 direction = endPosition - startPosition;
        float distance = direction.magnitude;

        if (distance <= Mathf.Epsilon)
            return;

        GameObject line = Instantiate(nodeLinePrefab, linesLayer, false);
        line.name = $"Line_{startNode.id}_To_{endNode.id}";

        RectTransform lineRect = line.GetComponent<RectTransform>();
        if (lineRect == null)
        {
            Destroy(line);
            return;
        }

        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = (startPosition + endPosition) * 0.5f;
        lineRect.sizeDelta = new Vector2(lineRect.sizeDelta.x, distance);
        lineRect.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, direction));

        lineGraphics.Clear();
        line.GetComponentsInChildren(true, lineGraphics);
        foreach (Graphic graphic in lineGraphics)
            graphic.raycastTarget = false;

        CanvasGroup canvasGroup = line.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = line.AddComponent<CanvasGroup>();

        generatedLineVisuals.Add(new ConnectionLineVisual(
            connection,
            canvasGroup,
            canvasGroup.alpha));
    }

    private void PrepareLayer()
    {
        linesLayer.anchorMin = Vector2.zero;
        linesLayer.anchorMax = Vector2.one;
        linesLayer.anchoredPosition = Vector2.zero;
        linesLayer.sizeDelta = Vector2.zero;
        linesLayer.pivot = new Vector2(0.5f, 0.5f);
    }

    private void FindReferences()
    {
        if (linesLayer != null)
            return;

        GameObject linesLayerObject = GameObject.Find("LinesLayer");
        if (linesLayerObject != null)
            linesLayer = linesLayerObject.GetComponent<RectTransform>();
    }

    private void LoadPrefab()
    {
        if (nodeLinePrefab == null)
            nodeLinePrefab = Resources.Load<GameObject>(NodeLinePrefabPath);
    }

    private readonly struct NodeConnection
    {
        public readonly YJ_StageNodeData startNode;
        public readonly YJ_StageNodeData endNode;

        public NodeConnection(YJ_StageNodeData startNode, YJ_StageNodeData endNode)
        {
            this.startNode = startNode;
            this.endNode = endNode;
        }
    }

    private readonly struct ConnectionLineVisual
    {
        public readonly NodeConnection connection;
        public readonly CanvasGroup canvasGroup;
        public readonly float originalAlpha;

        public ConnectionLineVisual(
            NodeConnection connection,
            CanvasGroup canvasGroup,
            float originalAlpha)
        {
            this.connection = connection;
            this.canvasGroup = canvasGroup;
            this.originalAlpha = originalAlpha;
        }
    }
}

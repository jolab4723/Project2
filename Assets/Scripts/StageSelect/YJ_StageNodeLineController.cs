using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인접한 층의 노드 연결 규칙을 만들고 NodeLine UI를 생성하며 경로별 알파를 관리합니다.
/// </summary>
[DisallowMultipleComponent]
public class YJ_StageNodeLineController : MonoBehaviour
{
    // Inspector에 프리팹이 없을 때 NodeLine을 불러올 Resources 경로입니다.
    private const string NodeLinePrefabPath = "Prefabs/Map/StageNode/NodeLine";

    [Header("References")]
    // 생성된 모든 연결선을 배치할 UI 레이어입니다.
    [SerializeField] private RectTransform linesLayer;
    // 두 노드 사이의 선을 표현할 UI 프리팹입니다.
    [SerializeField] private GameObject nodeLinePrefab;

    [Header("Connections")]
    // 일반적인 인접 층 연결 후보로 허용할 최대 노드 거리입니다.
    [SerializeField, Min(0f)] private float maximumConnectionDistance = 600f;
    // 거리 내 다음 노드 후보가 2개 이상일 때 선택 연결을 허용할 확률입니다.
    [SerializeField, Range(0f, 1f)] private float optionalConnectionChance = 0.3f;
    // 한 노드로 들어오는 두 번째 이후 라인을 최종 연결 목록에 남겨둘 확률입니다.
    [SerializeField, Range(0f, 1f)] private float extraIncomingConnectionKeepChance = 0.5f;

    [Header("Reachability")]
    // 도달할 수 없는 노드와 연결된 선에 곱할 알파 비율입니다.
    [SerializeField, Range(0f, 1f)] private float unreachableLineAlpha = 0.15f;
    // 지나온 경로가 아니거나 현재 선택 노드에서 도달할 수 없는 라인에 적용할 색상입니다.
    [SerializeField] private Color unreachableLineTint = Color.gray;
    // 원본 색상과 비활성 Tint 사이를 부드럽게 전환하는 시간입니다.
    [SerializeField, Min(0f)] private float tintTransitionDuration = 0.5f;

    // 현재 맵에서 최종 채택된 시작 노드와 도착 노드 연결 목록입니다.
    private readonly List<NodeConnection> generatedConnections = new();
    // 생성된 선의 연결 정보와 CanvasGroup을 함께 보관하는 목록입니다.
    private readonly List<ConnectionLineVisual> generatedLineVisuals = new();
    // 라인 프리팹 하위 Graphic 검색 시 배열 할당을 줄이기 위해 재사용하는 목록입니다.
    private readonly List<Graphic> lineGraphics = new();
    // 모든 라인의 Tint 및 알파 전환을 함께 실행하는 코루틴입니다.
    private Coroutine tintTransitionRoutine;

    /// <summary>
    /// 런타임 시작 시 LinesLayer 참조와 NodeLine 프리팹을 준비합니다.
    /// </summary>
    private void Awake()
    {
        FindReferences();
        LoadPrefab();
    }

    /// <summary>
    /// Inspector 입력값이 유효한 거리 및 알파 범위를 벗어나지 않도록 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        maximumConnectionDistance = Mathf.Max(0f, maximumConnectionDistance);
        optionalConnectionChance = Mathf.Clamp01(optionalConnectionChance);
        extraIncomingConnectionKeepChance = Mathf.Clamp01(extraIncomingConnectionKeepChance);
        unreachableLineAlpha = Mathf.Clamp01(unreachableLineAlpha);
        tintTransitionDuration = Mathf.Max(0f, tintTransitionDuration);
    }

    /// <summary>
    /// 기존 라인을 제거한 뒤 층별 연결 그래프를 다시 만들고 실제 UI 라인을 생성합니다.
    /// </summary>
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

    /// <summary>
    /// JSON에서 복원된 nextNodeIds를 변경하지 않고 동일한 연결선 UI를 다시 생성합니다.
    /// </summary>
    public void RebuildFromSavedConnections(
        IReadOnlyList<List<YJ_StageNodeData>> generatedFloors)
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

        Dictionary<string, YJ_StageNodeData> nodesById = new();
        foreach (List<YJ_StageNodeData> floorNodes in generatedFloors)
        {
            foreach (YJ_StageNodeData node in floorNodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.id))
                    nodesById[node.id] = node;
            }
        }

        HashSet<string> connectionKeys = new();
        foreach (List<YJ_StageNodeData> floorNodes in generatedFloors)
        {
            foreach (YJ_StageNodeData startNode in floorNodes)
            {
                if (startNode == null)
                    continue;

                foreach (string nextNodeId in startNode.nextNodeIds)
                {
                    if (!nodesById.TryGetValue(nextNodeId, out YJ_StageNodeData endNode))
                    {
                        Debug.LogWarning(
                            $"Saved connection target was not found: {startNode.id} -> {nextNodeId}",
                            this);
                        continue;
                    }

                    string connectionKey = $"{startNode.id}>{endNode.id}";
                    if (connectionKeys.Add(connectionKey))
                        generatedConnections.Add(new NodeConnection(startNode, endNode));
                }
            }
        }

        GenerateConnectionLines();
    }

    /// <summary>
    /// 저장된 연결 및 시각 정보와 LinesLayer 아래의 기존 라인 오브젝트를 제거합니다.
    /// </summary>
    public void ClearLines()
    {
        StopTintTransition();
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

    /// <summary>
    /// 현재 선택 경로에 포함되지 않는 노드와 연결된 라인의 알파를 낮춥니다.
    /// </summary>
    public void RefreshReachability(ISet<string> reachableNodeIds, bool restrictToReachableNodes)
    {
        StopTintTransition();

        bool requiresTransition = false;
        foreach (ConnectionLineVisual lineVisual in generatedLineVisuals)
        {
            if (lineVisual.canvasGroup == null)
                continue;

            bool connectionIsReachable = !restrictToReachableNodes ||
                                         IsOnActivePath(lineVisual.connection.startNode, reachableNodeIds) &&
                                         IsOnActivePath(lineVisual.connection.endNode, reachableNodeIds);
            requiresTransition |= lineVisual.PrepareTransition(
                connectionIsReachable,
                unreachableLineAlpha,
                unreachableLineTint);
        }

        if (!requiresTransition)
            return;

        if (!isActiveAndEnabled || tintTransitionDuration <= Mathf.Epsilon)
        {
            ApplyTintTransition(1f);
            return;
        }

        tintTransitionRoutine = StartCoroutine(TintTransitionRoutine());
    }

    /// <summary>
    /// 모든 라인의 현재 색상과 알파를 목표 상태까지 지정된 시간 동안 보간합니다.
    /// </summary>
    private IEnumerator TintTransitionRoutine()
    {
        float elapsedTime = 0f;

        while (elapsedTime < tintTransitionDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / tintTransitionDuration);
            ApplyTintTransition(Mathf.SmoothStep(0f, 1f, normalizedTime));
            yield return null;
        }

        ApplyTintTransition(1f);
        tintTransitionRoutine = null;
    }

    /// <summary>
    /// 준비된 모든 라인 전환에 동일한 보간 진행률을 적용합니다.
    /// </summary>
    private void ApplyTintTransition(float normalizedTime)
    {
        foreach (ConnectionLineVisual lineVisual in generatedLineVisuals)
        {
            lineVisual.ApplyTransition(
                normalizedTime,
                unreachableLineAlpha,
                unreachableLineTint);
        }
    }

    /// <summary>
    /// 진행 중인 라인 전환을 중지하며 현재 화면 색상은 그대로 유지합니다.
    /// </summary>
    private void StopTintTransition()
    {
        if (tintTransitionRoutine == null)
            return;

        StopCoroutine(tintTransitionRoutine);
        tintTransitionRoutine = null;
    }

    /// <summary>
    /// 노드가 이미 클리어되었거나 현재 노드에서 앞으로 도달 가능한 경로인지 확인합니다.
    /// </summary>
    private static bool IsOnActivePath(YJ_StageNodeData node, ISet<string> reachableNodeIds)
    {
        return node != null &&
               (node.cleared || reachableNodeIds != null && reachableNodeIds.Contains(node.id));
    }

    /// <summary>
    /// 모든 인접 층에 대해 필수 연결과 확률 기반 선택 연결을 만들고 nextNodeIds를 기록합니다.
    /// </summary>
    private void BuildFloorConnections(
        IReadOnlyList<List<YJ_StageNodeData>> generatedFloors,
        System.Random random)
    {
        foreach (List<YJ_StageNodeData> floorNodes in generatedFloors)
        {
            foreach (YJ_StageNodeData node in floorNodes)
                node.nextNodeIds.Clear();
        }

        // 아래 컬렉션은 층마다 Clear하여 재사용하므로 층 수만큼 반복 할당되지 않습니다.
        List<NodeConnection> candidates = new();
        List<NodeConnection> acceptedConnections = new();
        Dictionary<YJ_StageNodeData, int> candidateCounts = new();
        Dictionary<YJ_StageNodeData, bool> allowOptionalConnections = new();
        List<YJ_StageNodeData> orderedCurrentNodes = new();
        List<YJ_StageNodeData> orderedNextNodes = new();
        List<NodeConnection> extraIncomingCandidates = new();
        Dictionary<YJ_StageNodeData, int> incomingConnectionCounts = new();
        Dictionary<YJ_StageNodeData, int> outgoingConnectionCounts = new();
        HashSet<YJ_StageNodeData> nodesWithProtectedIncoming = new();
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
                orderedNextNodes,
                random);

            foreach (KeyValuePair<YJ_StageNodeData, int> pair in candidateCounts)
            {
                allowOptionalConnections[pair.Key] =
                    pair.Value < 2 || random.NextDouble() < optionalConnectionChance;
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

            PruneExtraIncomingConnections(
                acceptedConnections,
                extraIncomingCandidates,
                incomingConnectionCounts,
                outgoingConnectionCounts,
                nodesWithProtectedIncoming,
                random);

            foreach (NodeConnection accepted in acceptedConnections)
            {
                accepted.startNode.nextNodeIds.Add(accepted.endNode.id);
                generatedConnections.Add(accepted);
            }
        }
    }

    /// <summary>
    /// 각 도착 노드의 첫 유입 라인은 보존하고 두 번째 이후 라인은 확률적으로 제거합니다.
    /// 출발 노드의 마지막 출구와 도착 노드의 마지막 입구는 삭제하지 않습니다.
    /// </summary>
    private void PruneExtraIncomingConnections(
        List<NodeConnection> acceptedConnections,
        List<NodeConnection> extraIncomingCandidates,
        Dictionary<YJ_StageNodeData, int> incomingConnectionCounts,
        Dictionary<YJ_StageNodeData, int> outgoingConnectionCounts,
        HashSet<YJ_StageNodeData> nodesWithProtectedIncoming,
        System.Random random)
    {
        extraIncomingCandidates.Clear();
        incomingConnectionCounts.Clear();
        outgoingConnectionCounts.Clear();
        nodesWithProtectedIncoming.Clear();

        foreach (NodeConnection connection in acceptedConnections)
        {
            incomingConnectionCounts.TryGetValue(connection.endNode, out int incomingCount);
            incomingConnectionCounts[connection.endNode] = incomingCount + 1;

            outgoingConnectionCounts.TryGetValue(connection.startNode, out int outgoingCount);
            outgoingConnectionCounts[connection.startNode] = outgoingCount + 1;

            if (!nodesWithProtectedIncoming.Add(connection.endNode))
                extraIncomingCandidates.Add(connection);
        }

        Shuffle(extraIncomingCandidates, random);

        foreach (NodeConnection candidate in extraIncomingCandidates)
        {
            if (random.NextDouble() < extraIncomingConnectionKeepChance ||
                incomingConnectionCounts[candidate.endNode] <= 1 ||
                outgoingConnectionCounts[candidate.startNode] <= 1)
            {
                continue;
            }

            if (!acceptedConnections.Remove(candidate))
                continue;

            incomingConnectionCounts[candidate.endNode]--;
            outgoingConnectionCounts[candidate.startNode]--;
        }
    }

    /// <summary>
    /// 모든 현재 층 노드에 출구가 있고 모든 다음 층 노드에 입구가 생기도록
    /// X 좌표 순서를 유지하는 교차 없는 필수 연결을 추가합니다.
    /// </summary>
    private static void AddMandatoryConnections(
        List<YJ_StageNodeData> currentFloorNodes,
        List<YJ_StageNodeData> nextFloorNodes,
        List<NodeConnection> acceptedConnections,
        List<YJ_StageNodeData> orderedCurrentNodes,
        List<YJ_StageNodeData> orderedNextNodes,
        System.Random random)
    {
        orderedCurrentNodes.Clear();
        orderedNextNodes.Clear();
        orderedCurrentNodes.AddRange(currentFloorNodes);
        orderedNextNodes.AddRange(nextFloorNodes);
        orderedCurrentNodes.Sort((first, second) => first.position.x.CompareTo(second.position.x));
        orderedNextNodes.Sort((first, second) => first.position.x.CompareTo(second.position.x));

        // 층마다 진행 방향을 반전해 노드 수가 다른 층에서 한쪽 끝에 연결이 치우치는 현상을 줄입니다.
        if (random.NextDouble() < 0.5)
        {
            orderedCurrentNodes.Reverse();
            orderedNextNodes.Reverse();
        }

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

    /// <summary>
    /// 동일한 시작 및 도착 노드 연결이 이미 목록에 있는지 확인합니다.
    /// </summary>
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

    /// <summary>
    /// 후보 연결이 현재까지 채택된 연결 중 하나와 교차하는지 확인합니다.
    /// </summary>
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

    /// <summary>
    /// 선택 연결의 검사 순서가 편향되지 않도록 Fisher-Yates 방식으로 후보를 섞습니다.
    /// </summary>
    private static void Shuffle(List<NodeConnection> connections, System.Random random)
    {
        for (int i = connections.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (connections[i], connections[swapIndex]) = (connections[swapIndex], connections[i]);
        }
    }

    /// <summary>
    /// 끝점을 공유하지 않는 두 선분이 2차원 좌표상에서 서로 교차하는지 계산합니다.
    /// </summary>
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

    /// <summary>
    /// 부동소수점 오차 허용 범위를 적용해 두 외적값의 부호가 반대인지 확인합니다.
    /// </summary>
    private static bool HaveOppositeSigns(float first, float second)
    {
        const float epsilon = 0.001f;
        return (first > epsilon && second < -epsilon) ||
               (first < -epsilon && second > epsilon);
    }

    /// <summary>
    /// 두 2차원 벡터의 외적 스칼라값을 반환합니다.
    /// </summary>
    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.x * second.y - first.y * second.x;
    }

    /// <summary>
    /// 채택된 모든 연결 정보를 실제 NodeLine 프리팹 인스턴스로 변환합니다.
    /// </summary>
    private void GenerateConnectionLines()
    {
        foreach (NodeConnection connection in generatedConnections)
            CreateConnectionLine(connection);
    }

    /// <summary>
    /// 한 연결의 중점, 길이와 각도를 계산해 두 노드 사이에 UI 라인을 배치합니다.
    /// </summary>
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

        Graphic[] graphics = lineGraphics.ToArray();
        Color[] originalColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++)
            originalColors[i] = graphics[i] != null ? graphics[i].color : Color.white;

        CanvasGroup canvasGroup = line.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = line.AddComponent<CanvasGroup>();

        generatedLineVisuals.Add(new ConnectionLineVisual(
            connection,
            canvasGroup,
            canvasGroup.alpha,
            graphics,
            originalColors));
    }

    /// <summary>
    /// LinesLayer가 Content 전체를 덮도록 Stretch 기준값을 설정합니다.
    /// </summary>
    private void PrepareLayer()
    {
        linesLayer.anchorMin = Vector2.zero;
        linesLayer.anchorMax = Vector2.one;
        linesLayer.anchoredPosition = Vector2.zero;
        linesLayer.sizeDelta = Vector2.zero;
        linesLayer.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// Inspector 참조가 없을 때 씬에서 LinesLayer를 찾아 캐싱합니다.
    /// </summary>
    private void FindReferences()
    {
        if (linesLayer != null)
            return;

        GameObject linesLayerObject = GameObject.Find("LinesLayer");
        if (linesLayerObject != null)
            linesLayer = linesLayerObject.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Inspector에 NodeLine 프리팹이 없을 때 Resources에서 불러옵니다.
    /// </summary>
    private void LoadPrefab()
    {
        if (nodeLinePrefab == null)
            nodeLinePrefab = Resources.Load<GameObject>(NodeLinePrefabPath);
    }

    /// <summary>
    /// 하나의 방향성 연결을 구성하는 시작 노드와 다음 층 도착 노드를 묶습니다.
    /// </summary>
    private readonly struct NodeConnection
    {
        // 라인이 출발하는 현재 층 노드입니다.
        public readonly YJ_StageNodeData startNode;
        // 라인이 도착하는 다음 층 노드입니다.
        public readonly YJ_StageNodeData endNode;

        /// <summary>
        /// 시작 노드와 도착 노드로 연결 데이터를 생성합니다.
        /// </summary>
        public NodeConnection(YJ_StageNodeData startNode, YJ_StageNodeData endNode)
        {
            this.startNode = startNode;
            this.endNode = endNode;
        }
    }

    /// <summary>
    /// 생성된 라인의 경로 정보와 알파 제어 정보를 함께 보관합니다.
    /// </summary>
    private sealed class ConnectionLineVisual
    {
        // 이 UI 라인이 표현하는 시작 및 도착 노드 연결입니다.
        public readonly NodeConnection connection;
        // 경로 도달 가능 여부에 따라 전체 라인 알파를 변경할 CanvasGroup입니다.
        public readonly CanvasGroup canvasGroup;
        // 도달 가능한 상태로 돌아갈 때 복원할 프리팹 원본 알파입니다.
        public readonly float originalAlpha;
        // 라인을 구성하는 모든 UI Graphic입니다.
        private readonly Graphic[] graphics;
        // 활성 상태로 복원할 각 Graphic의 프리팹 원본 색상입니다.
        private readonly Color[] originalColors;
        // 새 전환이 시작될 때 각 Graphic이 가지고 있던 색상입니다.
        private readonly Color[] transitionStartColors;
        // 새 전환이 시작될 때 CanvasGroup이 가지고 있던 알파입니다.
        private float transitionStartAlpha;
        // 이번 전환이 도달 가능한 원본 상태를 목표로 하는지 나타냅니다.
        private bool targetActive;
        // 현재 라인이 실제로 전환할 색상 또는 알파 차이를 가지고 있는지 나타냅니다.
        private bool requiresTransition;

        /// <summary>
        /// 연결 데이터와 해당 라인의 CanvasGroup 및 기준 알파를 묶어 저장합니다.
        /// </summary>
        public ConnectionLineVisual(
            NodeConnection connection,
            CanvasGroup canvasGroup,
            float originalAlpha,
            Graphic[] graphics,
            Color[] originalColors)
        {
            this.connection = connection;
            this.canvasGroup = canvasGroup;
            this.originalAlpha = originalAlpha;
            this.graphics = graphics;
            this.originalColors = originalColors;
            transitionStartColors = new Color[graphics.Length];
        }

        /// <summary>
        /// 현재 표시값을 시작점으로 저장하고 목표 상태와 차이가 있는지 확인합니다.
        /// </summary>
        public bool PrepareTransition(bool active, float inactiveAlpha, Color inactiveTint)
        {
            targetActive = active;
            transitionStartAlpha = canvasGroup.alpha;
            requiresTransition = !Mathf.Approximately(
                transitionStartAlpha,
                GetTargetAlpha(inactiveAlpha));

            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                    continue;

                transitionStartColors[i] = graphic.color;
                requiresTransition |= graphic.color != GetTargetColor(i, inactiveTint);
            }

            return requiresTransition;
        }

        /// <summary>
        /// 저장된 시작값에서 현재 목표 알파와 Tint까지 지정된 진행률만큼 보간합니다.
        /// </summary>
        public void ApplyTransition(float normalizedTime, float inactiveAlpha, Color inactiveTint)
        {
            if (!requiresTransition)
                return;

            canvasGroup.alpha = Mathf.LerpUnclamped(
                transitionStartAlpha,
                GetTargetAlpha(inactiveAlpha),
                normalizedTime);

            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                    continue;

                graphic.color = Color.LerpUnclamped(
                    transitionStartColors[i],
                    GetTargetColor(i, inactiveTint),
                    normalizedTime);
            }

            if (normalizedTime >= 1f)
                requiresTransition = false;
        }

        /// <summary>
        /// 현재 목표 상태에 맞는 CanvasGroup 알파를 반환합니다.
        /// </summary>
        private float GetTargetAlpha(float inactiveAlpha)
        {
            return originalAlpha * (targetActive ? 1f : inactiveAlpha);
        }

        /// <summary>
        /// 현재 목표 상태에 맞는 Graphic 색상을 반환합니다.
        /// </summary>
        private Color GetTargetColor(int index, Color inactiveTint)
        {
            Color originalColor = originalColors[index];

            return targetActive
                ? originalColor
                : new Color(
                    inactiveTint.r,
                    inactiveTint.g,
                    inactiveTint.b,
                    originalColor.a);
        }
    }
}

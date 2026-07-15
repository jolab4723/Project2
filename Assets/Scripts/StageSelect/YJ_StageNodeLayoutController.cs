using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Act의 층별 노드 개수, UI 맵 높이와 각 노드의 좌표를 계산합니다.
/// </summary>
[DisallowMultipleComponent]
public class YJ_StageNodeLayoutController : MonoBehaviour
{
    [Header("Reference")]
    // 런타임에 생성된 모든 노드가 배치될 UI 레이어입니다.
    [SerializeField] private RectTransform nodesLayer;

    [Header("Node Count")]
    // 일반 층에 무작위로 생성할 최소 노드 개수입니다.
    [SerializeField, Range(1, 5)] private int minimumNodesPerFloor = 3;
    // 일반 층에 무작위로 생성할 최대 노드 개수입니다.
    [SerializeField, Range(1, 5)] private int maximumNodesPerFloor = 5;

    [Header("Layout")]
    // 서로 인접한 층 사이의 세로 간격입니다.
    [SerializeField, Min(100f)] private float floorSpacing = 360f;
    // 맵 최하단과 최상단 노드 바깥에 확보할 기본 여백입니다.
    [SerializeField, Min(0f)] private float verticalPadding = 250f;
    // 같은 층의 노드 사이에 적용할 가로 간격입니다.
    [SerializeField, Min(100f)] private float columnSpacing = 440f;
    // 같은 층 노드의 X 좌표에 무작위로 더할 최대 흔들림 값입니다.
    [SerializeField, Min(0f)] private float horizontalJitter = 45f;
    // 일반 층의 Y 좌표에 무작위로 더할 최대 흔들림 값입니다.
    [SerializeField, Min(0f)] private float verticalJitter = 20f;

    // 첫 층과 마지막 층도 Viewport 중앙에 올 수 있도록 보정된 실제 세로 여백입니다.
    private float preparedVerticalPadding;
    // NodesLayer와 LinesLayer를 감싸며 ScrollRect가 이동시키는 Content입니다.
    private RectTransform mapContent;

    /// <summary>
    /// 매니저가 노드를 생성할 부모 RectTransform을 안전하게 조회할 때 사용합니다.
    /// </summary>
    public RectTransform NodesLayer
    {
        get
        {
            FindReferences();
            return nodesLayer;
        }
    }

    /// <summary>
    /// 런타임 시작 시 NodesLayer와 Content 참조를 준비합니다.
    /// </summary>
    private void Awake()
    {
        FindReferences();
    }

    /// <summary>
    /// Inspector에서 최소 노드 수가 최대 노드 수를 넘지 않도록 값을 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        minimumNodesPerFloor = Mathf.Clamp(minimumNodesPerFloor, 1, 5);
        maximumNodesPerFloor = Mathf.Clamp(maximumNodesPerFloor, minimumNodesPerFloor, 5);
    }

    /// <summary>
    /// 전체 층이 들어갈 Content 높이와 NodesLayer의 Stretch 설정을 준비합니다.
    /// </summary>
    public void PrepareMapRect(int totalFloors)
    {
        FindReferences();
        if (nodesLayer == null)
            return;

        if (mapContent == null)
            return;

        RectTransform viewport = mapContent.parent as RectTransform;
        float viewportHalfHeight = viewport != null ? viewport.rect.height * 0.5f : 0f;
        preparedVerticalPadding = Mathf.Max(verticalPadding, viewportHalfHeight);

        float requiredHeight = preparedVerticalPadding * 2f + (totalFloors - 1) * floorSpacing;
        mapContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, requiredHeight);

        nodesLayer.anchorMin = Vector2.zero;
        nodesLayer.anchorMax = Vector2.one;
        nodesLayer.anchoredPosition = Vector2.zero;
        nodesLayer.sizeDelta = Vector2.zero;
        nodesLayer.pivot = new Vector2(0.5f, 0.5f);

        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 층 규칙에 따라 해당 층에 생성할 노드 개수를 반환합니다.
    /// 시작과 보스층은 1개, 보스 전 캠프층은 이전 층과 같은 개수를 사용합니다.
    /// </summary>
    public int GetNodeCount(
        int floor,
        int totalFloors,
        IReadOnlyList<List<YJ_StageNodeData>> generatedFloors,
        System.Random random)
    {
        if (floor == 1 || floor == totalFloors)
            return 1;

        if (floor == totalFloors - 1)
        {
            int previousFloorIndex = floor - 2;
            if (previousFloorIndex >= 0 && previousFloorIndex < generatedFloors.Count)
                return generatedFloors[previousFloorIndex].Count;
        }

        return random.Next(minimumNodesPerFloor, maximumNodesPerFloor + 1);
    }

    /// <summary>
    /// 층 번호와 같은 층의 인덱스를 이용해 노드의 anchoredPosition을 계산합니다.
    /// </summary>
    public Vector2 GetNodePosition(
        int floor,
        int index,
        int nodeCount,
        int totalFloors,
        System.Random random)
    {
        float contentHeight = mapContent != null ? mapContent.rect.height : 3000f;
        float effectiveVerticalPadding = preparedVerticalPadding > 0f
            ? preparedVerticalPadding
            : verticalPadding;
        float firstFloorY = -contentHeight * 0.5f + effectiveVerticalPadding;
        float y = firstFloorY + (floor - 1) * floorSpacing;

        if (floor != 1 && floor != totalFloors)
            y += RandomRange(-verticalJitter, verticalJitter, random);

        float centeredIndex = index - (nodeCount - 1) * 0.5f;
        float x = centeredIndex * columnSpacing;

        if (nodeCount > 1)
            x += RandomRange(-horizontalJitter, horizontalJitter, random);

        return new Vector2(x, y);
    }

    /// <summary>
    /// 맵 재생성 전에 NodesLayer 아래의 기존 런타임 노드를 모두 제거합니다.
    /// </summary>
    public void ClearNodes()
    {
        FindReferences();
        if (nodesLayer == null)
            return;

        for (int i = nodesLayer.childCount - 1; i >= 0; i--)
        {
            GameObject child = nodesLayer.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    /// <summary>
    /// System.Random을 사용해 지정된 최소값과 최대값 사이의 실수를 반환합니다.
    /// </summary>
    private static float RandomRange(float min, float max, System.Random random)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    /// <summary>
    /// Inspector 참조가 없을 때 씬에서 NodesLayer를 찾고 Content를 캐싱합니다.
    /// </summary>
    private void FindReferences()
    {
        if (nodesLayer == null)
        {
            GameObject nodesLayerObject = GameObject.Find("NodesLayer");
            if (nodesLayerObject != null)
                nodesLayer = nodesLayerObject.GetComponent<RectTransform>();
        }

        if (mapContent == null && nodesLayer != null)
            mapContent = nodesLayer.parent as RectTransform;
    }
}

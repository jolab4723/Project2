using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class YJ_StageNodeLayoutController : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private RectTransform nodesLayer;

    [Header("Node Count")]
    [SerializeField, Range(1, 5)] private int minimumNodesPerFloor = 3;
    [SerializeField, Range(1, 5)] private int maximumNodesPerFloor = 5;

    [Header("Layout")]
    [SerializeField, Min(100f)] private float floorSpacing = 360f;
    [SerializeField, Min(0f)] private float verticalPadding = 250f;
    [SerializeField, Min(100f)] private float columnSpacing = 440f;
    [SerializeField, Min(0f)] private float horizontalJitter = 45f;
    [SerializeField, Min(0f)] private float verticalJitter = 20f;

    private float preparedVerticalPadding;
    private RectTransform mapContent;

    public RectTransform NodesLayer
    {
        get
        {
            FindReferences();
            return nodesLayer;
        }
    }

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        minimumNodesPerFloor = Mathf.Clamp(minimumNodesPerFloor, 1, 5);
        maximumNodesPerFloor = Mathf.Clamp(maximumNodesPerFloor, minimumNodesPerFloor, 5);
    }

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

    private static float RandomRange(float min, float max, System.Random random)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

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

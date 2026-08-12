using UnityEngine;

public class InventoryGrid : MonoBehaviour
{
    private InventoryItem[,] grid;
    [SerializeField] private RectTransform gridRect;
    [SerializeField] private RectTransform itemsContainer;
    [SerializeField] private int gridWidth = 8;
    [SerializeField] private int gridHeight = 6;
    [SerializeField] private float cellSize = 70f;
    [SerializeField] private float cellSpacing = 3f;
    [SerializeField] private GridHighlightUI highlightUI;

    public GridHighlightUI Highlight => highlightUI;
    public RectTransform GridRect => gridRect;
    public RectTransform ItemsContainer => itemsContainer;
    public float CellSize => cellSize;
    public float CellSpacing => cellSpacing;
    public float Step => cellSize + cellSpacing;

    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public bool HasView => gridRect != null && itemsContainer != null;

    public void BindView(
        RectTransform boundGridRect,
        RectTransform boundItemsContainer,
        GridHighlightUI boundHighlight)
    {
        gridRect = boundGridRect;
        itemsContainer = boundItemsContainer;
        highlightUI = boundHighlight;
    }

    public void UnbindView(RectTransform boundItemsContainer)
    {
        if (itemsContainer != boundItemsContainer)
            return;

        gridRect = null;
        itemsContainer = null;
        highlightUI = null;
    }

    private void Awake()
    {
        grid = new InventoryItem[gridWidth, gridHeight];
    }

    private bool IsAreaInsideGrid(
        int startX,
        int startY,
        int width,
        int height)
    {
        if (grid == null || width <= 0 || height <= 0)
            return false;

        return startX >= 0 &&
               startY >= 0 &&
               startX <= gridWidth - width &&
               startY <= gridHeight - height;
    }

    public bool CanPlaceItem(int startX, int startY, int width, int height)
    {
        if (!IsAreaInsideGrid(startX, startY, width, height))
            return false;

        for ( int x = startX; x < startX + width; x++)
        {
            for(int y = startY; y < startY + height; y++)
            {
                if (grid[x, y] != null)
                {
                    return false;
                }
            }
        }
        return true;
    }


    public bool TryPlaceItem(
        InventoryItem item,
        int startX,
        int startY)
    {
        if (item?.itemData?.definition == null || grid == null)
            return false;

        if (!CanPlaceItem(
                startX,
                startY,
                item.CurrentWidth,
                item.CurrentHeight))
        {
            return false;
        }

        PlaceItemUnchecked(item, startX, startY);
        return true;
    }
    private void PlaceItemUnchecked(
        InventoryItem item,
        int startX,
        int startY)
    {
        for (int x = startX; x < startX + item.CurrentWidth; x++)
        {
            for (int y = startY; y < startY + item.CurrentHeight; y++)
            {
                grid[x, y] = item;
            }
        }

        item.x = startX;
        item.y = startY;
        item.isEquipped = false;
    }

    public bool FindEmptySpace(int width, int height, out int foundX, out int foundY)
    {
        for (int y = 0; y <= gridHeight - height; y++)
        {
            for (int x = 0; x <= gridWidth - width; x++)
            {
                if (CanPlaceItem(x, y, width, height))
                {
                    foundX = x;
                    foundY = y;
                    return true;
                }
            }
        }
        foundX = -1; foundY = -1;
        return false;
    }

    /// <summary>
    /// 아이템을 인벤토리에 자동 배치할 빈 공간을 찾는다.
    /// 지정한 회전 상태를 먼저 확인하고, 자리가 없으면 반대 회전 상태까지 확인한다.
    /// 장비 해제와 장비 교체가 같은 자동 회전 배치 규칙을 사용하도록 제공하는 공용 진입점이다.
    /// </summary>
    public bool TryFindEmptySpaceForItem(
        InventoryItem item,
        bool preferredRotation,
        out InventoryPlacementSnapshot placement)
    {
        placement = default;

        if (item?.itemData?.definition == null)
            return false;

        if (TryFindEmptySpaceForRotation(
                item,
                preferredRotation,
                out placement))
        {
            return true;
        }

        ItemSystem.ItemDefinitionSO definition =
            item.itemData.definition;

        // 정사각형 아이템은 회전해도 점유 크기가 같으므로 같은 탐색을 반복하지 않는다.
        if (definition.itemWidth == definition.itemHeight)
            return false;

        return TryFindEmptySpaceForRotation(
            item,
            !preferredRotation,
            out placement);
    }

    /// <summary>
    /// 아이템 상태를 변경하지 않고 지정한 회전 방향의 빈 공간과 배치 정보를 계산한다.
    /// 실제 회전 상태 반영은 배치를 확정하는 트랜잭션에서만 수행한다.
    /// </summary>
    private bool TryFindEmptySpaceForRotation(
        InventoryItem item,
        bool isRotated,
        out InventoryPlacementSnapshot placement)
    {
        placement = default;

        ItemSystem.ItemDefinitionSO definition =
            item.itemData.definition;

        int width = isRotated
            ? definition.itemHeight
            : definition.itemWidth;
        int height = isRotated
            ? definition.itemWidth
            : definition.itemHeight;

        if (!FindEmptySpace(
                width,
                height,
                out int foundX,
                out int foundY))
        {
            return false;
        }

        placement = InventoryPlacementSnapshot.FromOriginalState(
            this,
            item,
            foundX,
            foundY,
            isRotated);

        return placement.IsValid;
    }
    public InventoryItem GetItemAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
            return null;

        return grid[x, y];
    }

    /// <summary>
    /// 그리드에 배치된 모든 아이템을 중복 없이 반환한다 (세이브용). 여러 칸을 차지하는 아이템은 grid 배열에 여러 번 들어있어서 HashSet으로 거른다.
    /// </summary>
    public System.Collections.Generic.List<InventoryItem> GetAllItems()
    {
        var seen = new System.Collections.Generic.HashSet<InventoryItem>();
        var result = new System.Collections.Generic.List<InventoryItem>();

        if (grid == null)
            return result;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                var item = grid[x, y];
                if (item != null && seen.Add(item))
                    result.Add(item);
            }
        }

        return result;
    }

    public bool TryGetItemInArea(
    int startX,
    int startY,
    int width,
    int height,
    out InventoryItem foundItem)
    {
        foundItem = null;

        if (startX < 0 || startY < 0 || startX + width > gridWidth || startY + height > gridHeight)
            return false;

        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                InventoryItem item = grid[x, y];

                if (item == null)
                    continue;

                if (foundItem == null)
                {
                    foundItem = item;
                }
                else if (foundItem != item)
                {
                    return false;
                }
            }
        }

        return foundItem != null;
    }

    public bool ContainsItem(InventoryItem item)
    {
        if (item == null || grid == null)
            return false;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == item)
                    return true;
            }
        }

        return false;
    }
    public bool TryRemoveItem(InventoryItem item)
    {
        if (item?.itemData?.definition == null || grid == null)
            return false;

        int startX = item.x;
        int startY = item.y;
        int width = item.CurrentWidth;
        int height = item.CurrentHeight;

        if (!IsAreaInsideGrid(startX, startY, width, height))
        {
            Debug.LogError(
                $"[InventoryGrid] 제거할 아이템의 위치가 Grid 범위를 벗어났습니다. " +
                $"item={item.itemData.definition.itemName}, " +
                $"position=({startX},{startY}), size={width}x{height}");

            return false;
        }

        // 예상 사각형 전체가 정확히 item으로 채워져 있고,
        // 사각형 밖에는 같은 item 참조가 없는지 검사한다.
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                bool shouldContainItem =
                    x >= startX &&
                    x < startX + width &&
                    y >= startY &&
                    y < startY + height;

                bool actuallyContainsItem = grid[x, y] == item;

                if (shouldContainItem != actuallyContainsItem)
                {
                    Debug.LogError(
                        $"[InventoryGrid] 아이템 점유 상태가 올바르지 않습니다. " +
                        $"item={item.itemData.definition.itemName}, " +
                        $"cell=({x},{y}), " +
                        $"expected={shouldContainItem}, " +
                        $"actual={actuallyContainsItem}");

                    return false;
                }
            }
        }

        // 검사가 모두 성공한 후에만 변경한다.
        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                grid[x, y] = null;
            }
        }

        return true;
    }
}

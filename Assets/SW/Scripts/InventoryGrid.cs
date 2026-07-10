using TMPro;
using UnityEngine;

public class InventoryGrid : MonoBehaviour
{
    //public static InventoryGrid Instance;
    private InventoryItem[,] grid;
    [SerializeField] private RectTransform gridRect;
    [SerializeField] private TextMeshProUGUI text;
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


    private void Awake()
    {
        // Instance = this;
        grid = new InventoryItem[gridWidth, gridHeight];
    }
    
    public bool CanPlaceItem(int startX, int startY, int width, int height)
    {
        if (startX < 0 || startY < 0 || startX + width > gridWidth || startY + height > gridHeight)
            return false;

        for( int x = startX; x < startX + width; x++)
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

    public bool TryPlaceItem(InventoryItem item, int startX, int startY)
    {
        if (!CanPlaceItem(startX, startY, item.CurrentWidth, item.CurrentHeight))
            return false;

        PlaceItem(item, startX, startY);
        return true;
    }
    public void PlaceItem(InventoryItem item, int startX, int startY)
    {
        for (int x = startX; x < startX + item.CurrentWidth; x++)
        {
            for (int y = startY; y < startY + item.CurrentHeight; y++)
            {
                if (grid[x, y] != null && grid[x, y] != item)
                {
                    Debug.LogError(
                        $"[InventoryGrid] PlaceItem 겹침 발생. " +
                        $"place={item.itemData.definition.itemName}, " +
                        $"cellOwner={grid[x, y].itemData.definition.itemName}, " +
                        $"cell=({x},{y})"
                    );
                }

                grid[x, y] = item;
            }
        }
        item.x = startX;
        item.y = startY;
        item.isEquipped = false;
    }

    public void RemoveItem(InventoryItem item)
    {
        for (int x = item.x; x < item.x + item.CurrentWidth; x++)
        {
            for (int y = item.y; y < item.y + item.CurrentHeight; y++)
            {
                if (grid[x, y] == item)
                {
                    grid[x, y] = null;
                }
                else if (grid[x, y] != null)
                {
                    Debug.LogError(
                    $"[InventoryGrid] RemoveItem이 다른 아이템 칸을 지우려고 함. " +
                    $"remove={item.itemData.definition.itemName}, " +
                    $"cellOwner={grid[x, y].itemData.definition.itemName}, " +
                    $"cell=({x},{y})");
                }
            }
        }
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
    public InventoryItem GetItemAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
            return null;

        return grid[x, y];
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
}

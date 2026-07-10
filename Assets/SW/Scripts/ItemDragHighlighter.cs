using UnityEngine;
public class ItemDragHighlighter : MonoBehaviour
{
    [SerializeField] private ItemUI itemUI;
    private InventoryGrid activeHighlightGrid;

    private void Awake()
    {
        if (itemUI == null)
            itemUI = GetComponent<ItemUI>();
    }

    public void RefreshHighlight()
    {
        InventoryGrid targetGrid = GetHighlightTargetGrid();

        if (targetGrid == null || !IsItemOverGrid(targetGrid))
        {
            HideActiveHighlight();
            return;
        }

        InventoryItem item = itemUI.Item;
        Vector2Int cell = itemUI.GetCellFromItemRect(targetGrid);

        bool canPlace = targetGrid.CanPlaceItem(
            cell.x,
            cell.y,
            item.CurrentWidth,
            item.CurrentHeight
        );

        ShowHighlightOnGrid(targetGrid, cell, canPlace);
    }

    public void HideActiveHighlight()
    {
        if (activeHighlightGrid != null && activeHighlightGrid.Highlight != null)
            activeHighlightGrid.Highlight.HideHighlight();

        activeHighlightGrid = null;
    }

    private InventoryGrid GetHighlightTargetGrid()
    {
        InventoryGrid originalGrid = itemUI.OriginalGrid;
        InventoryGrid currentGrid = itemUI.CurrentGrid;

        if (ShopController.Instance != null)
        {
            InventoryGrid playerGrid = ShopController.Instance.PlayerGrid;
            InventoryGrid shopGrid = ShopController.Instance.ShopGrid;

            if (originalGrid == playerGrid && IsItemOverGrid(shopGrid))
                return shopGrid;

            if (originalGrid == shopGrid && IsItemOverGrid(playerGrid))
                return playerGrid;
        }

        return currentGrid;
    }

    private bool IsItemOverGrid(InventoryGrid grid)
    {
        if (grid == null)
            return false;

        InventoryItem item = itemUI.Item;
        Vector2Int cell = itemUI.GetCellFromItemRect(grid);

        return cell.x + item.CurrentWidth > 0 &&
               cell.y + item.CurrentHeight > 0 &&
               cell.x < grid.GridWidth &&
               cell.y < grid.GridHeight;
    }

    private void ShowHighlightOnGrid(InventoryGrid grid, Vector2Int cell, bool canPlace)
    {
        if (grid == null || grid.Highlight == null)
            return;

        if (activeHighlightGrid != null &&
            activeHighlightGrid != grid &&
            activeHighlightGrid.Highlight != null)
        {
            activeHighlightGrid.Highlight.HideHighlight();
        }

        activeHighlightGrid = grid;

        InventoryItem item = itemUI.Item;

        grid.Highlight.ShowHighlight(
            item.CurrentWidth,
            item.CurrentHeight,
            grid.CellSize,
            grid.CellSpacing
        );

        grid.Highlight.MoveHighlight(
            cell.x,
            cell.y,
            canPlace,
            grid.CellSize,
            grid.CellSpacing
        );
    }
}
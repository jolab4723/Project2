using UnityEngine;

public class ItemDragHighlighter : MonoBehaviour
{
    [SerializeField] private ItemUI itemUI;

    private InventoryGrid activeHighlightGrid;
    private bool hasPointerContext;
    private Vector2 lastScreenPosition;
    private Camera lastEventCamera;

    public InventorySwapPlan CurrentSwapPlan { get; private set; }

    private void Awake()
    {
        if (itemUI == null)
            itemUI = GetComponent<ItemUI>();
    }

    public void RefreshHighlight(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        hasPointerContext = true;
        lastScreenPosition = screenPosition;
        lastEventCamera = eventCamera;
        RefreshHighlight();
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
        Vector2Int requestedCell = itemUI.GetCellFromItemRect(targetGrid);
        InventoryCellRect requestedRect = new InventoryCellRect(
            requestedCell.x,
            requestedCell.y,
            item.CurrentWidth,
            item.CurrentHeight);

        if (targetGrid.CanPlaceItem(
                requestedCell.x,
                requestedCell.y,
                item.CurrentWidth,
                item.CurrentHeight))
        {
            CurrentSwapPlan = default;
            ShowMovePreview(targetGrid, requestedRect);
            return;
        }

        ShopController shop = ShopController.Instance;

        bool isSellingToShop =
            shop != null &&
            targetGrid == shop.ShopGrid &&
            shop.IsTradingToShop(itemUI.OriginalGrid, itemUI);

        if (isSellingToShop)
        {
            if (shop.TryResolveSellPosition(
                    item,
                    requestedCell.x,
                    requestedCell.y,
                    out InventoryPlacementSnapshot placement))
            {
                CurrentSwapPlan = default;
                ShowMovePreview(targetGrid, placement.Rect);
            }
            else
            {
                CurrentSwapPlan = default;
                ShowInvalidPreview(targetGrid, requestedRect);
            }

            return;
        }
        if (targetGrid != itemUI.OriginalGrid || itemUI.OriginalWasEquipped)
        {
            CurrentSwapPlan = default;
            ShowInvalidPreview(targetGrid, requestedRect);
            return;
        }

        Vector2Int? pointerCell = hasPointerContext
            ? itemUI.GetCellFromScreenPoint(
                targetGrid,
                lastScreenPosition,
                lastEventCamera)
            : null;

        if (!InventorySwapTargetResolver.TryResolveTarget(
                targetGrid,
                item,
                requestedCell,
                pointerCell,
                out InventoryItem otherItem))
        {
            CurrentSwapPlan = InventorySwapPlan.Invalid();
            ShowInvalidPreview(targetGrid, requestedRect);
            return;
        }

        CurrentSwapPlan = InventorySwapPlanner.BuildPlan(
            targetGrid,
            item,
            itemUI.OriginalPlacement,
            otherItem);

        if (CurrentSwapPlan.IsValid)
        {
            ShowSwapPreview(targetGrid, CurrentSwapPlan);
        }
        else
        {
            ShowInvalidPreview(targetGrid, requestedRect);
        }
    }

    public void HideActiveHighlight()
    {
        if (activeHighlightGrid != null && activeHighlightGrid.Highlight != null)
            activeHighlightGrid.Highlight.HideHighlight();

        activeHighlightGrid = null;
        CurrentSwapPlan = default;
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

    private void ShowMovePreview(
        InventoryGrid grid,
        InventoryCellRect rect)
    {
        if (!TrySetActiveGrid(grid))
            return;

        grid.Highlight.ShowMovePreview(
            rect,
            grid.CellSize,
            grid.CellSpacing);
    }

    private void ShowInvalidPreview(
        InventoryGrid grid,
        InventoryCellRect rect)
    {
        if (!TrySetActiveGrid(grid))
            return;

        grid.Highlight.ShowInvalidPreview(
            rect,
            grid.CellSize,
            grid.CellSpacing);
    }

    private void ShowSwapPreview(
        InventoryGrid grid,
        InventorySwapPlan plan)
    {
        if (!TrySetActiveGrid(grid))
            return;

        grid.Highlight.ShowSwapPreview(
            plan,
            grid.CellSize,
            grid.CellSpacing);
    }

    private bool TrySetActiveGrid(InventoryGrid grid)
    {
        if (grid == null || grid.Highlight == null)
            return false;

        if (activeHighlightGrid != null &&
            activeHighlightGrid != grid &&
            activeHighlightGrid.Highlight != null)
        {
            activeHighlightGrid.Highlight.HideHighlight();
        }

        activeHighlightGrid = grid;
        return true;
    }
}

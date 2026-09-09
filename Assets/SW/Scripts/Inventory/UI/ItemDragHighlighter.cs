using UnityEngine;
using UnityEngine.UI;

public class ItemDragHighlighter : MonoBehaviour
{
    private static readonly Color EquipMoveColor = new Color(0.34f, 0.84f, 0.64f, 0.68f);
    private static readonly Color EquipInvalidColor = new Color(1f, 0.42f, 0.42f, 0.68f);
    private static readonly Color EquipSwapColor = new Color(1f, 0.82f, 0.40f, 0.68f);

    [SerializeField] private ItemUI itemUI;

    private InventoryGrid activeHighlightGrid;
    private Image activeEquipSlotImage;
    private Color activeEquipSlotColor;
    private bool hasPointerContext;
    private Vector2 lastScreenPosition;
    private Camera lastEventCamera;
    private GameObject lastPointerTarget;

    public InventorySwapPlan CurrentSwapPlan { get; private set; }

    private void Awake()
    {
        if (itemUI == null)
            itemUI = GetComponent<ItemUI>();
    }

    public void RefreshHighlight(
        Vector2 screenPosition,
        Camera eventCamera,
        GameObject pointerTarget)
    {
        hasPointerContext = true;
        lastScreenPosition = screenPosition;
        lastEventCamera = eventCamera;
        lastPointerTarget = pointerTarget;
        RefreshHighlight();
    }

    public void RefreshHighlight()
    {
        EquipSlotUI targetEquipSlot = lastPointerTarget != null
            ? lastPointerTarget.GetComponentInParent<EquipSlotUI>()
            : null;

        if (targetEquipSlot != null)
        {
            HideActiveGridHighlight();
            ShowEquipSlotPreview(targetEquipSlot);
            return;
        }

        HideActiveEquipSlotPreview();

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

        if (InventoryGridMath.CanPlaceRectIgnoring(
                targetGrid,
                requestedRect,
                itemUI.DragSourceItem))
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
                out InventoryItem otherItem,
                itemUI.DragSourceItem))
        {
            CurrentSwapPlan = InventorySwapPlan.Invalid();
            ShowInvalidPreview(targetGrid, requestedRect);
            return;
        }

        CurrentSwapPlan = InventorySwapPlanner.BuildPlan(
            targetGrid,
            item,
            itemUI.OriginalPlacement,
            otherItem,
            itemUI.DragSourceItem);

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
        HideActiveGridHighlight();
        HideActiveEquipSlotPreview();
        CurrentSwapPlan = default;
    }

    private void ShowEquipSlotPreview(EquipSlotUI slot)
    {
        Image slotImage = slot.GetComponent<Image>();
        if (slotImage == null)
        {
            HideActiveEquipSlotPreview();
            return;
        }

        if (activeEquipSlotImage != slotImage)
        {
            HideActiveEquipSlotPreview();
            activeEquipSlotImage = slotImage;
            activeEquipSlotColor = slotImage.color;
        }

        bool isShopItem =
            itemUI.OriginalGrid == ShopController.Instance?.ShopGrid;

        bool canAccept = itemUI.OriginalWasEquipped
            ? slot == itemUI.OriginalEquipSlot
            : !isShopItem && slot.CanAcceptType(itemUI.Item?.itemData);

        activeEquipSlotImage.color = !canAccept
            ? EquipInvalidColor
            : slot.IsEmpty
                ? EquipMoveColor
                : EquipSwapColor;

        CurrentSwapPlan = default;
    }

    private void HideActiveGridHighlight()
    {
        if (activeHighlightGrid != null && activeHighlightGrid.Highlight != null)
            activeHighlightGrid.Highlight.HideHighlight();

        activeHighlightGrid = null;
    }

    private void HideActiveEquipSlotPreview()
    {
        if (activeEquipSlotImage != null)
            activeEquipSlotImage.color = activeEquipSlotColor;

        activeEquipSlotImage = null;
        activeEquipSlotColor = default;
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

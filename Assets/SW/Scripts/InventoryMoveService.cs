using UnityEngine;

public static class InventoryMoveService
{
    public static bool TrySwapItem(
        InventoryGrid grid,
        InventoryItem movingItem,
        ItemUI movingUI,
        int targetX,
        int targetY,
        int originalX,
        int originalY)
    {
        if (!grid.TryGetItemInArea(
                targetX,
                targetY,
                movingItem.CurrentWidth,
                movingItem.CurrentHeight,
                out InventoryItem otherItem))
        {
            return false;
        }

        if (targetX != otherItem.x || targetY != otherItem.y) // 아이템이 한 칸만 겹쳤을때 스왑되는 것을 방지
            return false;

        grid.RemoveItem(otherItem);

        bool canPlaceMoving = grid.CanPlaceItem(
            targetX,
            targetY,
            movingItem.CurrentWidth,
            movingItem.CurrentHeight
        );

        bool canPlaceOther = grid.CanPlaceItem(
            originalX,
            originalY,
            otherItem.CurrentWidth,
            otherItem.CurrentHeight
        );

        if (!canPlaceMoving || !canPlaceOther)
        {
            grid.PlaceItem(otherItem, otherItem.x, otherItem.y);
            return false;
        }

        ItemUI otherUI = FindItemUI(grid, otherItem);

        if (otherUI == null)
        {
            grid.PlaceItem(otherItem, otherItem.x, otherItem.y);
            return false;
        }

        grid.PlaceItem(movingItem, targetX, targetY);
        movingUI.SetGridPosition(grid, targetX, targetY);

        grid.PlaceItem(otherItem, originalX, originalY);
        otherUI.SetGridPosition(grid, originalX, originalY);

        return true;
    }

    private static ItemUI FindItemUI(InventoryGrid grid, InventoryItem item)
    {
        foreach (Transform child in grid.ItemsContainer)
        {
            ItemUI ui = child.GetComponent<ItemUI>();

            if (ui != null && ui.Item == item)
                return ui;
        }

        return null;
    }

    public static bool HandleGridDrop(ItemUI itemUI, int targetX, int targetY)
    {
        InventoryGrid grid = itemUI.CurrentGrid;
        InventoryItem item = itemUI.Item;

        if (grid.CanPlaceItem(targetX, targetY, item.CurrentWidth, item.CurrentHeight))
        {
            grid.PlaceItem(item, targetX, targetY);
            itemUI.SetGridPosition(grid, targetX, targetY);
            return true;
        }

        if (TrySwapItem(
            grid,
            item,
            itemUI,
            targetX,
            targetY,
            itemUI.OriginalX,
            itemUI.OriginalY))
        {
            return true;
        }

        itemUI.RestoreRotationToOriginal();

        if (grid.CanPlaceItem(itemUI.OriginalX, itemUI.OriginalY, item.CurrentWidth, item.CurrentHeight))
        {
            grid.PlaceItem(item, itemUI.OriginalX, itemUI.OriginalY);
            itemUI.SetGridPosition(grid, itemUI.OriginalX, itemUI.OriginalY);
            return false;
        }

        if (grid.FindEmptySpace(item.CurrentWidth, item.CurrentHeight, out int foundX, out int foundY))
        {
            grid.PlaceItem(item, foundX, foundY);
            itemUI.SetGridPosition(grid, foundX, foundY);
            return false;
        }

        InventoryController.Instance.PrintLog("드래그 도중 인벤토리가 가득 차 넣을 수 없어서 파괴되었습니다.");
        Object.Destroy(itemUI.gameObject);
        return false;
    }
}
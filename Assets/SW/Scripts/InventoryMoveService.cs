public static class InventoryMoveService
{
    public static InventoryMoveResultData TrySwapItem(
        InventoryGrid grid,
        InventoryItem movingItem,
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
            return InventoryMoveResultData.Failed();
        }

        if (targetX != otherItem.x || targetY != otherItem.y) // 아이템이 한 칸만 겹쳤을때 스왑되는 것을 방지
            return InventoryMoveResultData.Failed();

        int otherOriginalX = otherItem.x;
        int otherOriginalY = otherItem.y;

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
            grid.PlaceItem(otherItem, otherOriginalX, otherOriginalY);
            return InventoryMoveResultData.Failed();
        }

        grid.PlaceItem(movingItem, targetX, targetY);
        grid.PlaceItem(otherItem, originalX, originalY);

        return InventoryMoveResultData.Swapped(
        movingItem,
        targetX,
        targetY,
        otherItem,
        originalX,
        originalY );
    }

    public static InventoryMoveResultData HandleGridDrop(InventoryGrid grid, InventoryItem item,
        int targetX, int targetY,
        int originalX, int originalY,
        bool originalRotated)
    {
        if (grid.CanPlaceItem(targetX, targetY, item.CurrentWidth, item.CurrentHeight))
        {
            grid.PlaceItem(item, targetX, targetY);
            return InventoryMoveResultData.Success(item, targetX, targetY);
        }

        InventoryMoveResultData swapResult = TrySwapItem(
        grid,
        item,
        targetX,
        targetY,
        originalX,
        originalY);

        if (swapResult.Result == InventoryMoveResult.Swapped)
            return swapResult;

        item.isRotated = originalRotated;

        if (grid.CanPlaceItem(originalX, originalY, item.CurrentWidth, item.CurrentHeight))
        {
            grid.PlaceItem(item, originalX, originalY);
            return InventoryMoveResultData.ReturnedToOriginal(
                item,
                originalX,
                originalY);
        }

        if (grid.FindEmptySpace(item.CurrentWidth, item.CurrentHeight, out int foundX, out int foundY))
        {
            grid.PlaceItem(item, foundX, foundY);
            return InventoryMoveResultData.MovedToEmptySpace(
                item,
                foundX,
                foundY
                );
        }
        return InventoryMoveResultData.Failed();
    }
}
public static class InventoryMoveService
{
    public static InventoryMoveResultData TrySwapOnGrid(
        InventoryGrid grid,
        InventoryItem movingItem,
        int targetX,
        int targetY,
        int originalX,
        int originalY)
    {
        return InventorySwapService.TrySwapOnGrid(
            grid,
            movingItem,
            targetX,
            targetY,
            originalX,
            originalY);
    }

    public static InventoryMoveResultData TryMoveOnGrid(
        InventoryGrid grid,
        InventoryItem item,
        int targetX,
        int targetY,
        int originalX,
        int originalY,
        bool originalRotated)
    {
        if (grid.CanPlaceItem(targetX, targetY, item.CurrentWidth, item.CurrentHeight))
        {
            grid.TryPlaceItem(item, targetX, targetY);
            return InventoryMoveResultData.Success(item, targetX, targetY);
        }

        InventoryMoveResultData swapResult = InventorySwapService.TrySwapOnGrid(
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
            grid.TryPlaceItem(item, originalX, originalY);
            return InventoryMoveResultData.ReturnedToOriginal(
                item,
                originalX,
                originalY);
        }

        if (grid.FindEmptySpace(item.CurrentWidth, item.CurrentHeight, out int foundX, out int foundY))
        {
            grid.TryPlaceItem(item, foundX, foundY);
            return InventoryMoveResultData.MovedToEmptySpace(
                item,
                foundX,
                foundY);
        }

        return InventoryMoveResultData.Failed();
    }
}
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
        InventoryPlacementSnapshot movingOriginal =
            InventoryPlacementSnapshot.FromOriginalState(
                grid,
                item,
                originalX,
                originalY,
                originalRotated);

        return TryMoveOnGrid(
            grid,
            item,
            targetX,
            targetY,
            movingOriginal,
            default);
    }

    public static InventoryMoveResultData TryMoveOnGrid(
        InventoryGrid grid,
        InventoryItem item,
        int targetX,
        int targetY,
        InventoryPlacementSnapshot movingOriginal,
        InventorySwapPlan previewPlan)
    {
        if (grid.CanPlaceItem(targetX, targetY, item.CurrentWidth, item.CurrentHeight))
        {
            grid.TryPlaceItem(item, targetX, targetY);
            return InventoryMoveResultData.Success(item, targetX, targetY);
        }

        InventoryMoveResultData swapResult;

        if (previewPlan.IsEvaluated)
        {
            swapResult = previewPlan.IsValid
                ? InventorySwapService.TryCommitPlan(previewPlan)
                : InventoryMoveResultData.Failed();
        }
        else
        {
            swapResult = InventorySwapService.TrySwapOnGrid(
                grid,
                item,
                new UnityEngine.Vector2Int(targetX, targetY),
                movingOriginal);
        }

        if (swapResult.Result == InventoryMoveResult.Swapped)
            return swapResult;

        item.isRotated = movingOriginal.IsRotated;

        if (grid.CanPlaceItem(
                movingOriginal.Rect.X,
                movingOriginal.Rect.Y,
                item.CurrentWidth,
                item.CurrentHeight))
        {
            grid.TryPlaceItem(
                item,
                movingOriginal.Rect.X,
                movingOriginal.Rect.Y);
            return InventoryMoveResultData.ReturnedToOriginal(
                item,
                movingOriginal.Rect.X,
                movingOriginal.Rect.Y);
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

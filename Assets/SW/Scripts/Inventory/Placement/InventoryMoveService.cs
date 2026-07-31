public static class InventoryMoveService
{
    /// <summary>
    /// 드래그가 중단되었을 때 교환 없이 원래 위치부터 복구한다.
    /// 원래 자리가 점유된 경우에만 같은 Grid의 다른 빈자리를 사용한다.
    /// </summary>
    public static InventoryMoveResultData TryRestoreToGrid(
        InventoryGrid grid,
        InventoryItem item,
        InventoryPlacementSnapshot originalPlacement)
    {
        if (grid == null ||
            item?.itemData?.definition == null ||
            !originalPlacement.IsValid)
        {
            return InventoryMoveResultData.Failed();
        }

        item.isRotated = originalPlacement.IsRotated;

        if (grid.TryPlaceItem(
                item,
                originalPlacement.Rect.X,
                originalPlacement.Rect.Y))
        {
            return InventoryMoveResultData.ReturnedToOriginal(
                originalPlacement.Rect.X,
                originalPlacement.Rect.Y);
        }

        if (!grid.TryFindEmptySpaceForItem(
                item,
                originalPlacement.IsRotated,
                out InventoryPlacementSnapshot fallbackPlacement))
        {
            return InventoryMoveResultData.Failed();
        }

        item.isRotated = fallbackPlacement.IsRotated;

        if (!grid.TryPlaceItem(
                item,
                fallbackPlacement.Rect.X,
                fallbackPlacement.Rect.Y))
        {
            return InventoryMoveResultData.Failed();
        }

        return InventoryMoveResultData.MovedToEmptySpace(
            fallbackPlacement.Rect.X,
            fallbackPlacement.Rect.Y);
    }

    public static InventoryMoveResultData TryMoveOnGrid(
        InventoryGrid grid,
        InventoryItem item,
        int targetX,
        int targetY,
        InventoryPlacementSnapshot movingOriginal,
        InventorySwapPlan previewPlan)
    {
        if (grid.CanPlaceItem(targetX, targetY,
        item.CurrentWidth, item.CurrentHeight) &&
        grid.TryPlaceItem(item,targetX,targetY))
        {
            return InventoryMoveResultData.Success(targetX, targetY);
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

        if (grid.CanPlaceItem(movingOriginal.Rect.X, movingOriginal.Rect.Y,
            item.CurrentWidth, item.CurrentHeight) &&
            grid.TryPlaceItem(item, movingOriginal.Rect.X, movingOriginal.Rect.Y))
        {
            return InventoryMoveResultData.ReturnedToOriginal(
                movingOriginal.Rect.X,
                movingOriginal.Rect.Y);
        }

        if (grid.FindEmptySpace(
            item.CurrentWidth,item.CurrentHeight,
            out int foundX, out int foundY) &&
            grid.TryPlaceItem(item, foundX, foundY))
        {
            return InventoryMoveResultData.MovedToEmptySpace(
                foundX,
                foundY);
        }

        return InventoryMoveResultData.Failed();
    }
}

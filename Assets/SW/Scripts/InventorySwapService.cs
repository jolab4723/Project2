using UnityEngine;

public static class InventorySwapService
{
    public static InventorySwapPlan BuildPlan(
        InventoryGrid grid,
        InventoryItem movingItem,
        InventoryPlacementSnapshot movingOriginal,
        Vector2Int requestedCell,
        InventoryItem otherItem)
    {
        return InventorySwapPlanner.BuildPlan(
            grid,
            movingItem,
            movingOriginal,
            requestedCell,
            otherItem);
    }

    public static InventoryMoveResultData TryCommitPlan(InventorySwapPlan plan)
    {
        if (!plan.IsValid || !plan.MatchesCurrentState())
            return InventoryMoveResultData.Failed();

        InventoryGrid grid = plan.Grid;
        InventoryItem movingItem = plan.MovingItem;
        InventoryItem otherItem = plan.OtherItem;

        if (!InventoryGridMath.CanPlacePairIgnoring(
                grid,
                plan.MovingTo,
                plan.OtherTo,
                movingItem,
                otherItem))
        {
            return InventoryMoveResultData.Failed();
        }

        grid.RemoveItem(otherItem);

        if (!grid.TryPlaceItem(
                movingItem,
                plan.MovingTo.X,
                plan.MovingTo.Y))
        {
            RestoreOtherItem(plan);
            return InventoryMoveResultData.Failed();
        }

        if (!grid.TryPlaceItem(
                otherItem,
                plan.OtherTo.X,
                plan.OtherTo.Y))
        {
            grid.RemoveItem(movingItem);
            RestoreOtherItem(plan);
            return InventoryMoveResultData.Failed();
        }

        return InventoryMoveResultData.Swapped(
            movingItem,
            plan.MovingTo.X,
            plan.MovingTo.Y,
            otherItem,
            plan.OtherTo.X,
            plan.OtherTo.Y,
            plan.Mode);
    }

    public static InventoryMoveResultData TrySwapOnGrid(
        InventoryGrid grid,
        InventoryItem movingItem,
        int targetX,
        int targetY,
        int originalX,
        int originalY)
    {
        InventoryPlacementSnapshot movingOriginal =
            InventoryPlacementSnapshot.FromOriginalState(
                grid,
                movingItem,
                originalX,
                originalY,
                movingItem != null && movingItem.isRotated);

        return TrySwapOnGrid(
            grid,
            movingItem,
            new Vector2Int(targetX, targetY),
            movingOriginal);
    }

    public static InventoryMoveResultData TrySwapOnGrid(
        InventoryGrid grid,
        InventoryItem movingItem,
        Vector2Int requestedCell,
        InventoryPlacementSnapshot movingOriginal)
    {
        if (!InventorySwapTargetResolver.TryResolveTargetFromFootprint(
                grid,
                movingItem,
                requestedCell,
                out InventoryItem otherItem))
        {
            return InventoryMoveResultData.Failed();
        }

        InventorySwapPlan plan = BuildPlan(
            grid,
            movingItem,
            movingOriginal,
            requestedCell,
            otherItem);

        return TryCommitPlan(plan);
    }

    private static void RestoreOtherItem(InventorySwapPlan plan)
    {
        if (plan.Grid.TryPlaceItem(
                plan.OtherItem,
                plan.OtherOriginal.Rect.X,
                plan.OtherOriginal.Rect.Y))
        {
            return;
        }

        Debug.LogError(
            "[InventorySwapService] 스왑 롤백 중 대상 아이템을 원래 위치에 복구하지 못했습니다.");
    }
}

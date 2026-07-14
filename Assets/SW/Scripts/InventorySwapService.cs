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

        if (!grid.TryRemoveItem(otherItem))
        {
            Debug.LogError(
                "[InventorySwapService] 교환 대상 아이템을 Grid에서 제거하지 못했습니다.");

            return InventoryMoveResultData.Failed();
        }

        if (!grid.TryPlaceItem(
                movingItem,
                plan.MovingTo.X,
                plan.MovingTo.Y))
        {
            if (!RestoreOtherItem(plan))
            {
                Debug.LogError(
                    "[InventorySwapService] 이동 아이템 배치 실패 후 교환 대상 아이템을 복구하지 못했습니다.");
            }

            return InventoryMoveResultData.Failed();
        }

        if (!grid.TryPlaceItem(
        otherItem,
        plan.OtherTo.X,
        plan.OtherTo.Y))
        {
            bool movingItemRemoved =
                grid.TryRemoveItem(movingItem);

            bool otherItemRestored =
                movingItemRemoved &&
                RestoreOtherItem(plan);

            if (!movingItemRemoved || !otherItemRestored)
            {
                Debug.LogError(
                    "[InventorySwapService] 교환 실패 후 Grid 상태를 복구하지 못했습니다.");
            }

            return InventoryMoveResultData.Failed();
        }

        // 두 배치가 모두 성공했을 때만 여기까지 온다.
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

    private static bool RestoreOtherItem(InventorySwapPlan plan)
    {
        InventoryItem otherItem = plan.OtherItem;

        if (otherItem == null || plan.Grid == null)
            return false;

        otherItem.isRotated =
            plan.OtherOriginal.IsRotated;

        return plan.Grid.TryPlaceItem(
            otherItem,
            plan.OtherOriginal.Rect.X,
            plan.OtherOriginal.Rect.Y);
    }
}

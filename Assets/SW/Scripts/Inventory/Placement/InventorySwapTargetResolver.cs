using UnityEngine;

public static class InventorySwapTargetResolver
{
    private const float MinimumExclusiveOverlapRatio = 0.5f;

    public static bool TryResolveTarget(
        InventoryGrid grid,
        InventoryItem movingItem,
        Vector2Int requestedCell,
        Vector2Int? pointerCell,
        out InventoryItem targetItem,
        InventoryItem occupiedMovingItem = null)
    {
        targetItem = null;

        if (grid == null || movingItem == null)
            return false;

        if (pointerCell.HasValue && IsCellInsideGrid(grid, pointerCell.Value))
        {
            InventoryItem pointerTarget = grid.GetItemAt(
                pointerCell.Value.x,
                pointerCell.Value.y);

            if (pointerTarget != null && pointerTarget != movingItem && pointerTarget != occupiedMovingItem)
            {
                targetItem = pointerTarget;
                return true;
            }
        }

        return TryResolveSingleDominantOverlap(
            grid,
            movingItem,
            requestedCell,
            out targetItem,
            occupiedMovingItem);
    }

    public static bool TryResolveTargetFromFootprint(
        InventoryGrid grid,
        InventoryItem movingItem,
        Vector2Int requestedCell,
        out InventoryItem targetItem)
    {
        return TryResolveSingleDominantOverlap(
            grid,
            movingItem,
            requestedCell,
            out targetItem);
    }

    private static bool TryResolveSingleDominantOverlap(
        InventoryGrid grid,
        InventoryItem movingItem,
        Vector2Int requestedCell,
        out InventoryItem targetItem,
        InventoryItem occupiedMovingItem = null)
    {
        targetItem = null;

        InventoryCellRect requestedRect = new InventoryCellRect(
            requestedCell.x,
            requestedCell.y,
            movingItem.CurrentWidth,
            movingItem.CurrentHeight);

        int minX = Mathf.Max(0, requestedRect.X);
        int minY = Mathf.Max(0, requestedRect.Y);
        int maxX = Mathf.Min(grid.GridWidth, requestedRect.Right);
        int maxY = Mathf.Min(grid.GridHeight, requestedRect.Bottom);

        if (minX >= maxX || minY >= maxY)
            return false;

        int overlapCells = 0;

        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                InventoryItem occupant = grid.GetItemAt(x, y);
                if (occupant == null || occupant == movingItem || occupant == occupiedMovingItem)
                    continue;

                if (targetItem == null)
                {
                    targetItem = occupant;
                }
                else if (targetItem != occupant)
                {
                    targetItem = null;
                    return false;
                }

                overlapCells++;
            }
        }

        if (targetItem == null)
            return false;

        int targetArea = targetItem.CurrentWidth * targetItem.CurrentHeight;
        int comparisonArea = Mathf.Min(requestedRect.Area, targetArea);

        if (comparisonArea <= 0)
        {
            targetItem = null;
            return false;
        }

        float overlapRatio = overlapCells / (float)comparisonArea;
        if (overlapRatio <= MinimumExclusiveOverlapRatio)
        {
            targetItem = null;
            return false;
        }

        return true;
    }

    private static bool IsCellInsideGrid(InventoryGrid grid, Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < grid.GridWidth &&
               cell.y < grid.GridHeight;
    }
}

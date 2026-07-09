using UnityEngine;

public static class InventoryGridMath
{
    public static bool IsOutsideGrid(
        InventoryGrid grid,
        InventoryItem item,
        int x,
        int y)
    {
        return x < 0 ||
               y < 0 ||
               x + item.CurrentWidth > grid.GridWidth ||
               y + item.CurrentHeight > grid.GridHeight;
    }

    public static bool IsOverlappingGrid(
        InventoryGrid grid,
        InventoryItem item,
        int x,
        int y)
    {
        return IsAreaOverlapping(
            x,
            y,
            item.CurrentWidth,
            item.CurrentHeight,
            0,
            0,
            grid.GridWidth,
            grid.GridHeight);
    }

    public static bool TryClampCell(
        InventoryGrid grid,
        InventoryItem item,
        int preferredX,
        int preferredY,
        out int clampedX,
        out int clampedY)
    {
        clampedX = clampedY = -1;

        if (item.CurrentWidth > grid.GridWidth || item.CurrentHeight > grid.GridHeight)
            return false;

        clampedX = Mathf.Clamp(preferredX, 0, grid.GridWidth - item.CurrentWidth);
        clampedY = Mathf.Clamp(preferredY, 0, grid.GridHeight - item.CurrentHeight);
        return true;
    }

    public static bool TryClampStackStart(
        int desiredStart,
        int gridSize,
        int firstSize,
        int offset,
        int secondSize,
        out int start)
    {
        start = -1;

        int minLocal = Mathf.Min(0, offset);
        int maxLocal = Mathf.Max(firstSize, offset + secondSize);

        if (maxLocal - minLocal > gridSize)
            return false;

        int minStart = -minLocal;
        int maxStart = gridSize - maxLocal;

        start = Mathf.Clamp(desiredStart, minStart, maxStart);
        return true;
    }

    public static bool CanPlaceBoth(
        InventoryGrid grid,
        InventoryItem firstItem,
        int firstX,
        int firstY,
        InventoryItem secondItem,
        int secondX,
        int secondY)
    {
        if (IsAreaOverlapping(
                firstX,
                firstY,
                firstItem.CurrentWidth,
                firstItem.CurrentHeight,
                secondX,
                secondY,
                secondItem.CurrentWidth,
                secondItem.CurrentHeight))
        {
            return false;
        }

        return grid.CanPlaceItem(firstX, firstY, firstItem.CurrentWidth, firstItem.CurrentHeight) &&
               grid.CanPlaceItem(secondX, secondY, secondItem.CurrentWidth, secondItem.CurrentHeight);
    }

    public static bool IsRangeOverlapping(int aStart, int aEnd, int bStart, int bEnd)
    {
        return aStart < bEnd && aEnd > bStart;
    }

    public static bool IsAreaOverlapping(
        int ax, int ay, int aw, int ah,
        int bx, int by, int bw, int bh)
    {
        return ax < bx + bw &&
               ax + aw > bx &&
               ay < by + bh &&
               ay + ah > by;
    }
}

public static class InventoryGridMath
{
    public static bool CanPlacePairIgnoring(
        InventoryGrid grid,
        InventoryCellRect firstRect,
        InventoryCellRect secondRect,
        InventoryItem ignoredFirst,
        InventoryItem ignoredSecond)
    {
        if (grid == null ||
            !firstRect.IsInside(grid) ||
            !secondRect.IsInside(grid) ||
            firstRect.Overlaps(secondRect))
        {
            return false;
        }

        return CanPlaceRectIgnoring(
                   grid,
                   firstRect,
                   ignoredFirst,
                   ignoredSecond) &&
               CanPlaceRectIgnoring(
                   grid,
                   secondRect,
                   ignoredFirst,
                   ignoredSecond);
    }

    private static bool CanPlaceRectIgnoring(
        InventoryGrid grid,
        InventoryCellRect rect,
        InventoryItem ignoredFirst,
        InventoryItem ignoredSecond)
    {
        for (int x = rect.X; x < rect.Right; x++)
        {
            for (int y = rect.Y; y < rect.Bottom; y++)
            {
                InventoryItem occupant = grid.GetItemAt(x, y);
                if (occupant != null &&
                    occupant != ignoredFirst &&
                    occupant != ignoredSecond)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static bool IsRangeOverlapping(int aStart, int aEnd, int bStart, int bEnd)
    {
        return aStart < bEnd && aEnd > bStart;
    }

}

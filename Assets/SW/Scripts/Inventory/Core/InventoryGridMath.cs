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

    /// <summary>모델을 변경하지 않고 지정한 아이템의 기존 점유를 제외해 미리보기 영역을 검사한다.</summary>
    public static bool CanPlaceRectIgnoring(
        InventoryGrid grid,
        InventoryCellRect rect,
        InventoryItem ignoredFirst,
        InventoryItem ignoredSecond = null)
    {
        if (grid == null || !rect.IsInside(grid))
            return false;

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

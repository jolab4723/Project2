using UnityEngine;

public static class InventorySwapService
{
    public static InventoryMoveResultData TrySwapOnGrid(
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
            if (!InventoryGridMath.IsOutsideGrid(grid, movingItem, targetX, targetY) ||
                !InventoryGridMath.IsOverlappingGrid(grid, movingItem, targetX, targetY) ||
                !InventoryGridMath.TryClampCell(grid, movingItem, targetX, targetY, out int clampedTargetX, out int clampedTargetY) ||
                !grid.TryGetItemInArea(
                    clampedTargetX,
                    clampedTargetY,
                    movingItem.CurrentWidth,
                    movingItem.CurrentHeight,
                    out otherItem))
            {
                return InventoryMoveResultData.Failed();
            }
        }

        int otherOriginalX = otherItem.x;
        int otherOriginalY = otherItem.y;

        grid.RemoveItem(otherItem);

        if (!TryFindSwapCells(
                grid,
                movingItem,
                otherItem,
                otherOriginalX,
                otherOriginalY,
                originalX,
                originalY,
                out int movingX,
                out int movingY,
                out int otherX,
                out int otherY))
        {
            grid.PlaceItem(otherItem, otherOriginalX, otherOriginalY);
            return InventoryMoveResultData.Failed();
        }

        if (!grid.TryPlaceItem(movingItem, movingX, movingY))
        {
            grid.PlaceItem(otherItem, otherOriginalX, otherOriginalY);
            return InventoryMoveResultData.Failed();
        }

        if (!grid.TryPlaceItem(otherItem, otherX, otherY))
        {
            grid.RemoveItem(movingItem);
            grid.PlaceItem(otherItem, otherOriginalX, otherOriginalY);
            return InventoryMoveResultData.Failed();
        }

        return InventoryMoveResultData.Swapped(
            movingItem,
            movingX,
            movingY,
            otherItem,
            otherX,
            otherY);
    }

    private static bool TryFindSwapCells(
        InventoryGrid grid,
        InventoryItem movingItem,
        InventoryItem otherItem,
        int itemToX,
        int itemToY,
        int otherToX,
        int otherToY,
        out int movingX,
        out int movingY,
        out int otherX,
        out int otherY)
    {
        movingX = movingY = otherX = otherY = -1;

        if (TryStackVertical(
                grid,
                movingItem,
                otherItem,
                itemToX,
                itemToY,
                otherToX,
                otherToY,
                out movingX,
                out movingY,
                out otherX,
                out otherY))
        {
            return true;
        }

        if (TryStackHorizontal(
                grid,
                movingItem,
                otherItem,
                itemToX,
                itemToY,
                otherToX,
                otherToY,
                out movingX,
                out movingY,
                out otherX,
                out otherY))
        {
            return true;
        }

        if (TryEdgeSwap(
                grid,
                movingItem,
                otherItem,
                itemToX,
                itemToY,
                otherToX,
                otherToY,
                out movingX,
                out movingY,
                out otherX,
                out otherY))
        {
            return true;
        }

        if (InventoryGridMath.CanPlaceBoth(
                grid,
                movingItem,
                itemToX,
                itemToY,
                otherItem,
                otherToX,
                otherToY))
        {
            movingX = itemToX;
            movingY = itemToY;
            otherX = otherToX;
            otherY = otherToY;
            return true;
        }

        if (TryClampSwap(
                grid,
                movingItem,
                otherItem,
                itemToX,
                itemToY,
                otherToX,
                otherToY,
                out movingX,
                out movingY,
                out otherX,
                out otherY))
        {
            return true;
        }

        return false;
    }

    private static bool TryStackVertical(
        InventoryGrid grid,
        InventoryItem movingItem,
        InventoryItem otherItem,
        int itemToX,
        int itemToY,
        int otherToX,
        int otherToY,
        out int movingX,
        out int movingY,
        out int otherX,
        out int otherY)
    {
        movingX = movingY = otherX = otherY = -1;

        int movingOriginalX = otherToX;
        int movingOriginalY = otherToY;

        int otherOriginalX = itemToX;
        int otherOriginalY = itemToY;

        if (!InventoryGridMath.IsRangeOverlapping(
                movingOriginalX,
                movingOriginalX + movingItem.CurrentWidth,
                otherOriginalX,
                otherOriginalX + otherItem.CurrentWidth))
        {
            return false;
        }

        bool isTouchingVertically =
            movingOriginalY + movingItem.CurrentHeight == otherOriginalY ||
            otherOriginalY + otherItem.CurrentHeight == movingOriginalY;

        if (!isTouchingVertically)
            return false;

        bool movingWasTop = movingOriginalY < otherOriginalY;

        InventoryItem newTopItem = movingWasTop ? otherItem : movingItem;
        InventoryItem newBottomItem = movingWasTop ? movingItem : otherItem;

        int topOriginalX = movingWasTop ? movingOriginalX : otherOriginalX;
        int topOriginalY = movingWasTop ? movingOriginalY : otherOriginalY;
        int bottomOriginalX = movingWasTop ? otherOriginalX : movingOriginalX;

        int xOffset = bottomOriginalX - topOriginalX;
        int totalHeight = newTopItem.CurrentHeight + newBottomItem.CurrentHeight;

        if (totalHeight > grid.GridHeight)
            return false;

        int topY = Mathf.Clamp(topOriginalY, 0, grid.GridHeight - totalHeight);

        if (!InventoryGridMath.TryClampStackStart(
                topOriginalX,
                grid.GridWidth,
                newTopItem.CurrentWidth,
                xOffset,
                newBottomItem.CurrentWidth,
                out int topX))
        {
            return false;
        }

        int bottomX = topX + xOffset;
        int bottomY = topY + newTopItem.CurrentHeight;

        if (movingWasTop)
        {
            otherX = topX;
            otherY = topY;
            movingX = bottomX;
            movingY = bottomY;
        }
        else
        {
            movingX = topX;
            movingY = topY;
            otherX = bottomX;
            otherY = bottomY;
        }

        if (InventoryGridMath.CanPlaceBoth(grid, movingItem, movingX, movingY, otherItem, otherX, otherY))
            return true;

        movingX = movingY = otherX = otherY = -1;
        return false;
    }

    private static bool TryStackHorizontal(
        InventoryGrid grid,
        InventoryItem movingItem,
        InventoryItem otherItem,
        int itemToX,
        int itemToY,
        int otherToX,
        int otherToY,
        out int movingX,
        out int movingY,
        out int otherX,
        out int otherY)
    {
        movingX = movingY = otherX = otherY = -1;

        int movingOriginalX = otherToX;
        int movingOriginalY = otherToY;

        int otherOriginalX = itemToX;
        int otherOriginalY = itemToY;

        if (!InventoryGridMath.IsRangeOverlapping(
                movingOriginalY,
                movingOriginalY + movingItem.CurrentHeight,
                otherOriginalY,
                otherOriginalY + otherItem.CurrentHeight))
        {
            return false;
        }

        bool isTouchingHorizontally =
            movingOriginalX + movingItem.CurrentWidth == otherOriginalX ||
            otherOriginalX + otherItem.CurrentWidth == movingOriginalX;

        if (!isTouchingHorizontally)
            return false;

        bool movingWasLeft = movingOriginalX < otherOriginalX;

        InventoryItem newLeftItem = movingWasLeft ? otherItem : movingItem;
        InventoryItem newRightItem = movingWasLeft ? movingItem : otherItem;

        int leftOriginalX = movingWasLeft ? movingOriginalX : otherOriginalX;
        int leftOriginalY = movingWasLeft ? movingOriginalY : otherOriginalY;
        int rightOriginalY = movingWasLeft ? otherOriginalY : movingOriginalY;

        int yOffset = rightOriginalY - leftOriginalY;
        int totalWidth = newLeftItem.CurrentWidth + newRightItem.CurrentWidth;

        if (totalWidth > grid.GridWidth)
            return false;

        int leftX = Mathf.Clamp(leftOriginalX, 0, grid.GridWidth - totalWidth);

        if (!InventoryGridMath.TryClampStackStart(
                leftOriginalY,
                grid.GridHeight,
                newLeftItem.CurrentHeight,
                yOffset,
                newRightItem.CurrentHeight,
                out int leftY))
        {
            return false;
        }

        int rightX = leftX + newLeftItem.CurrentWidth;
        int rightY = leftY + yOffset;

        if (movingWasLeft)
        {
            otherX = leftX;
            otherY = leftY;
            movingX = rightX;
            movingY = rightY;
        }
        else
        {
            movingX = leftX;
            movingY = leftY;
            otherX = rightX;
            otherY = rightY;
        }

        if (InventoryGridMath.CanPlaceBoth(grid, movingItem, movingX, movingY, otherItem, otherX, otherY))
            return true;

        movingX = movingY = otherX = otherY = -1;
        return false;
    }

    private static bool TryEdgeSwap(
        InventoryGrid grid,
        InventoryItem movingItem,
        InventoryItem otherItem,
        int itemToX,
        int itemToY,
        int otherToX,
        int otherToY,
        out int movingX,
        out int movingY,
        out int otherX,
        out int otherY)
    {
        movingX = movingY = otherX = otherY = -1;

        if (!TrySnapToTargetEdges(grid, movingItem, otherItem, itemToX, itemToY, out movingX, out movingY))
            return false;

        if (!TrySnapToTargetEdges(grid, otherItem, movingItem, otherToX, otherToY, out otherX, out otherY))
            return false;

        bool changed = movingX != itemToX ||
                       movingY != itemToY ||
                       otherX != otherToX ||
                       otherY != otherToY;

        if (!changed)
        {
            movingX = movingY = otherX = otherY = -1;
            return false;
        }

        if (InventoryGridMath.CanPlaceBoth(grid, movingItem, movingX, movingY, otherItem, otherX, otherY))
            return true;

        movingX = movingY = otherX = otherY = -1;
        return false;
    }

    private static bool TrySnapToTargetEdges(
        InventoryGrid grid,
        InventoryItem item,
        InventoryItem targetItem,
        int targetX,
        int targetY,
        out int x,
        out int y)
    {
        x = targetX;
        y = targetY;

        if (item.CurrentWidth > grid.GridWidth || item.CurrentHeight > grid.GridHeight)
            return false;

        if (targetX == 0)
            x = 0;
        else if (targetX + targetItem.CurrentWidth == grid.GridWidth)
            x = grid.GridWidth - item.CurrentWidth;

        if (targetY == 0)
            y = 0;
        else if (targetY + targetItem.CurrentHeight == grid.GridHeight)
            y = grid.GridHeight - item.CurrentHeight;

        return true;
    }

    private static bool TryClampSwap(
        InventoryGrid grid,
        InventoryItem movingItem,
        InventoryItem otherItem,
        int itemToX,
        int itemToY,
        int otherToX,
        int otherToY,
        out int movingX,
        out int movingY,
        out int otherX,
        out int otherY)
    {
        movingX = movingY = otherX = otherY = -1;

        bool movingNeedsClamp = InventoryGridMath.IsOutsideGrid(
            grid,
            movingItem,
            itemToX,
            itemToY);

        bool otherNeedsClamp = InventoryGridMath.IsOutsideGrid(
            grid,
            otherItem,
            otherToX,
            otherToY);

        if (!movingNeedsClamp && !otherNeedsClamp)
            return false;

        if (!InventoryGridMath.TryClampCell(grid, movingItem, itemToX, itemToY, out movingX, out movingY))
            return false;

        if (!InventoryGridMath.TryClampCell(grid, otherItem, otherToX, otherToY, out otherX, out otherY))
            return false;

        bool movingStillTargetsOther = InventoryGridMath.IsAreaOverlapping(
            movingX,
            movingY,
            movingItem.CurrentWidth,
            movingItem.CurrentHeight,
            itemToX,
            itemToY,
            otherItem.CurrentWidth,
            otherItem.CurrentHeight);

        bool otherStillTargetsMoving = InventoryGridMath.IsAreaOverlapping(
            otherX,
            otherY,
            otherItem.CurrentWidth,
            otherItem.CurrentHeight,
            otherToX,
            otherToY,
            movingItem.CurrentWidth,
            movingItem.CurrentHeight);

        if (!movingStillTargetsOther || !otherStillTargetsMoving)
        {
            movingX = movingY = otherX = otherY = -1;
            return false;
        }

        if (InventoryGridMath.CanPlaceBoth(grid, movingItem, movingX, movingY, otherItem, otherX, otherY))
            return true;

        movingX = movingY = otherX = otherY = -1;
        return false;
    }
}

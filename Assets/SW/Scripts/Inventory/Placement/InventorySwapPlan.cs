using System;
using UnityEngine;

[Flags]
public enum InventoryGridEdgeFlags
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Top = 1 << 2,
    Bottom = 1 << 3
}

public readonly struct InventoryCellRect
{
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    public int Right => X + Width;
    public int Bottom => Y + Height;
    public int Area => Width > 0 && Height > 0 ? Width * Height : 0;
    public bool IsValid => Width > 0 && Height > 0;

    public InventoryCellRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public bool IsInside(InventoryGrid grid)
    {
        return grid != null &&
               X >= 0 &&
               Y >= 0 &&
               Right <= grid.GridWidth &&
               Bottom <= grid.GridHeight;
    }

    public bool Overlaps(InventoryCellRect other)
    {
        return X < other.Right &&
               Right > other.X &&
               Y < other.Bottom &&
               Bottom > other.Y;
    }

    public int IntersectionArea(InventoryCellRect other)
    {
        int width = Mathf.Max(0, Mathf.Min(Right, other.Right) - Mathf.Max(X, other.X));
        int height = Mathf.Max(0, Mathf.Min(Bottom, other.Bottom) - Mathf.Max(Y, other.Y));
        return width * height;
    }

}

/// <summary>
/// 드래그 실패나 장비 해제 시 아이템을 되돌릴 수 있도록 그리드 배치를 보관한다.
/// </summary>
public readonly struct InventoryPlacementSnapshot
{
    public InventoryCellRect Rect { get; }
    public bool IsRotated { get; }
    public InventoryGridEdgeFlags Edges { get; }

    public bool IsValid => Rect.IsValid;

    public InventoryPlacementSnapshot(
        InventoryCellRect rect,
        bool isRotated,
        InventoryGridEdgeFlags edges)
    {
        Rect = rect;
        IsRotated = isRotated;
        Edges = edges;
    }

    public static InventoryPlacementSnapshot Capture(InventoryGrid grid, InventoryItem item)
    {
        if (grid == null || item == null)
            return default;

        return Create(
            grid,
            item.x,
            item.y,
            item.CurrentWidth,
            item.CurrentHeight,
            item.isRotated);
    }

    public static InventoryPlacementSnapshot FromOriginalState(
        InventoryGrid grid,
        InventoryItem item,
        int x,
        int y,
        bool isRotated)
    {
        if (grid == null || item?.itemData?.definition == null)
            return default;

        int width = isRotated
            ? item.itemData.definition.itemHeight
            : item.itemData.definition.itemWidth;
        int height = isRotated
            ? item.itemData.definition.itemWidth
            : item.itemData.definition.itemHeight;

        return Create(grid, x, y, width, height, isRotated);
    }

    private static InventoryPlacementSnapshot Create(
        InventoryGrid grid,
        int x,
        int y,
        int width,
        int height,
        bool isRotated)
    {
        InventoryGridEdgeFlags edges = InventoryGridEdgeFlags.None;

        if (x == 0)
            edges |= InventoryGridEdgeFlags.Left;
        if (x + width == grid.GridWidth)
            edges |= InventoryGridEdgeFlags.Right;
        if (y == 0)
            edges |= InventoryGridEdgeFlags.Top;
        if (y + height == grid.GridHeight)
            edges |= InventoryGridEdgeFlags.Bottom;

        return new InventoryPlacementSnapshot(
            new InventoryCellRect(x, y, width, height),
            isRotated,
            edges);
    }
}

public readonly struct InventorySwapPlan
{
    public bool IsEvaluated { get; }
    public bool IsValid { get; }

    public InventoryGrid Grid { get; }
    public InventoryItem MovingItem { get; }
    public InventoryItem OtherItem { get; }

    public InventoryPlacementSnapshot OtherOriginal { get; }
    public InventoryCellRect MovingTo { get; }
    public InventoryCellRect OtherTo { get; }

    private InventorySwapPlan(
        bool isEvaluated,
        bool isValid,
        InventoryGrid grid,
        InventoryItem movingItem,
        InventoryItem otherItem,
        InventoryPlacementSnapshot otherOriginal,
        InventoryCellRect movingTo,
        InventoryCellRect otherTo)
    {
        IsEvaluated = isEvaluated;
        IsValid = isValid;
        Grid = grid;
        MovingItem = movingItem;
        OtherItem = otherItem;
        OtherOriginal = otherOriginal;
        MovingTo = movingTo;
        OtherTo = otherTo;
    }

    public static InventorySwapPlan Valid(
        InventoryGrid grid,
        InventoryItem movingItem,
        InventoryItem otherItem,
        InventoryPlacementSnapshot otherOriginal,
        InventoryCellRect movingTo,
        InventoryCellRect otherTo)
    {
        return new InventorySwapPlan(
            true,
            true,
            grid,
            movingItem,
            otherItem,
            otherOriginal,
            movingTo,
            otherTo);
    }

    public static InventorySwapPlan Invalid()
    {
        return new InventorySwapPlan(
            true,
            false,
            null,
            null,
            null,
            default,
            default,
            default);
    }

    public bool MatchesCurrentState()
    {
        if (!IsValid || Grid == null || MovingItem == null || OtherItem == null)
            return false;

        if (MovingItem.CurrentWidth != MovingTo.Width ||
            MovingItem.CurrentHeight != MovingTo.Height)
        {
            return false;
        }

        return OtherItem.x == OtherOriginal.Rect.X &&
               OtherItem.y == OtherOriginal.Rect.Y &&
               OtherItem.CurrentWidth == OtherOriginal.Rect.Width &&
               OtherItem.CurrentHeight == OtherOriginal.Rect.Height &&
               OtherItem.isRotated == OtherOriginal.IsRotated;
    }
}

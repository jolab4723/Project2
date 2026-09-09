using System.Collections.Generic;
using UnityEngine;

public static class InventorySwapPlanner
{
    private enum SwapRelationship
    {
        Separate,
        Horizontal,
        Vertical
    }

    private readonly struct SwapCandidateScore
    {
        public int IntentPenalty { get; }
        public int StabilityPenalty { get; }
        public int EdgePenalty { get; }
        public int CoveragePenalty { get; }
        public int IdealDistance { get; }
        public int BoundsPenalty { get; }
        public int MovementDistance { get; }

        public SwapCandidateScore(
            int intentPenalty,
            int stabilityPenalty,
            int edgePenalty,
            int coveragePenalty,
            int idealDistance,
            int boundsPenalty,
            int movementDistance)
        {
            IntentPenalty = intentPenalty;
            StabilityPenalty = stabilityPenalty;
            EdgePenalty = edgePenalty;
            CoveragePenalty = coveragePenalty;
            IdealDistance = idealDistance;
            BoundsPenalty = boundsPenalty;
            MovementDistance = movementDistance;
        }

        public bool IsBetterThan(SwapCandidateScore other)
        {
            if (IntentPenalty != other.IntentPenalty)
                return IntentPenalty < other.IntentPenalty;
            if (StabilityPenalty != other.StabilityPenalty)
                return StabilityPenalty < other.StabilityPenalty;
            if (EdgePenalty != other.EdgePenalty)
                return EdgePenalty < other.EdgePenalty;
            if (CoveragePenalty != other.CoveragePenalty)
                return CoveragePenalty < other.CoveragePenalty;
            if (IdealDistance != other.IdealDistance)
                return IdealDistance < other.IdealDistance;
            if (BoundsPenalty != other.BoundsPenalty)
                return BoundsPenalty < other.BoundsPenalty;

            return MovementDistance < other.MovementDistance;
        }

        public bool EqualsScore(SwapCandidateScore other)
        {
            return IntentPenalty == other.IntentPenalty &&
                   StabilityPenalty == other.StabilityPenalty &&
                   EdgePenalty == other.EdgePenalty &&
                   CoveragePenalty == other.CoveragePenalty &&
                   IdealDistance == other.IdealDistance &&
                   BoundsPenalty == other.BoundsPenalty &&
                   MovementDistance == other.MovementDistance;
        }
    }

    private readonly struct SwapCandidate
    {
        public InventoryCellRect MovingTo { get; }
        public InventoryCellRect OtherTo { get; }
        public SwapCandidateScore Score { get; }

        public SwapCandidate(
            InventoryCellRect movingTo,
            InventoryCellRect otherTo,
            SwapCandidateScore score)
        {
            MovingTo = movingTo;
            OtherTo = otherTo;
            Score = score;
        }

        public bool IsBetterThan(SwapCandidate other)
        {
            if (!Score.EqualsScore(other.Score))
                return Score.IsBetterThan(other.Score);

            GetCanonicalOrder(
                MovingTo,
                OtherTo,
                out InventoryCellRect first,
                out InventoryCellRect second);
            GetCanonicalOrder(
                other.MovingTo,
                other.OtherTo,
                out InventoryCellRect otherFirst,
                out InventoryCellRect otherSecond);

            int firstComparison = CompareRects(first, otherFirst);
            if (firstComparison != 0)
                return firstComparison < 0;

            return CompareRects(second, otherSecond) < 0;
        }

        private static void GetCanonicalOrder(
            InventoryCellRect first,
            InventoryCellRect second,
            out InventoryCellRect orderedFirst,
            out InventoryCellRect orderedSecond)
        {
            if (CompareRects(first, second) <= 0)
            {
                orderedFirst = first;
                orderedSecond = second;
            }
            else
            {
                orderedFirst = second;
                orderedSecond = first;
            }
        }

        private static int CompareRects(
            InventoryCellRect first,
            InventoryCellRect second)
        {
            if (first.Width != second.Width)
                return first.Width.CompareTo(second.Width);
            if (first.Height != second.Height)
                return first.Height.CompareTo(second.Height);
            if (first.Y != second.Y)
                return first.Y.CompareTo(second.Y);

            return first.X.CompareTo(second.X);
        }
    }

    /// <summary>
    /// 점유를 바꾸지 않고 교환 위치를 계산한다. 별도 표시용 아이템을 쓸 때는
    /// occupiedMovingItem으로 Grid에 남은 원본을 전달하며 그 계획은 표시용으로만 사용한다.
    /// </summary>
    public static InventorySwapPlan BuildPlan(
        InventoryGrid grid,
        InventoryItem movingItem,
        InventoryPlacementSnapshot movingOriginal,
        InventoryItem otherItem,
        InventoryItem occupiedMovingItem = null)
    {
        if (grid == null ||
            movingItem == null ||
            otherItem == null ||
            movingItem == otherItem ||
            occupiedMovingItem == otherItem ||
            !movingOriginal.IsValid)
        {
            return InventorySwapPlan.Invalid();
        }

        InventoryPlacementSnapshot otherOriginal =
            InventoryPlacementSnapshot.Capture(grid, otherItem);

        if (!otherOriginal.IsValid)
        {
            return InventorySwapPlan.Invalid();
        }

        SwapRelationship relationship = GetRelationship(
            movingOriginal.Rect,
            otherOriginal.Rect);

        InventoryCellRect idealMoving = ApplyTargetEdges(
            grid,
            movingItem.CurrentWidth,
            movingItem.CurrentHeight,
            otherOriginal);
        InventoryCellRect idealOther = ApplyTargetEdges(
            grid,
            otherItem.CurrentWidth,
            otherItem.CurrentHeight,
            movingOriginal);

        bool footprintCompatible =
            movingItem.CurrentWidth == otherOriginal.Rect.Width &&
            movingItem.CurrentHeight == otherOriginal.Rect.Height &&
            otherItem.CurrentWidth == movingOriginal.Rect.Width &&
            otherItem.CurrentHeight == movingOriginal.Rect.Height;

        bool idealHasPriority =
            relationship == SwapRelationship.Separate ||
            footprintCompatible;

        if (idealHasPriority &&
            InventoryGridMath.CanPlacePairIgnoring(
                grid,
                idealMoving,
                idealOther,
                occupiedMovingItem ?? movingItem,
                otherItem))
        {
            return InventorySwapPlan.Valid(
                grid,
                movingItem,
                otherItem,
                otherOriginal,
                idealMoving,
                idealOther);
        }

        List<InventoryCellRect> movingPlacements =
            GetPlacementsOverlappingTarget(
                grid,
                movingItem.CurrentWidth,
                movingItem.CurrentHeight,
                otherOriginal.Rect);
        List<InventoryCellRect> otherPlacements =
            GetPlacementsOverlappingTarget(
                grid,
                otherItem.CurrentWidth,
                otherItem.CurrentHeight,
                movingOriginal.Rect);

        bool foundCandidate = false;
        SwapCandidate bestCandidate = default;

        foreach (InventoryCellRect movingTo in movingPlacements)
        {
            foreach (InventoryCellRect otherTo in otherPlacements)
            {
                if (!InventoryGridMath.CanPlacePairIgnoring(
                        grid,
                        movingTo,
                        otherTo,
                        occupiedMovingItem ?? movingItem,
                        otherItem))
                {
                    continue;
                }

                SwapCandidate candidate = CreateCandidate(
                    grid,
                    relationship,
                    movingOriginal,
                    otherOriginal,
                    idealMoving,
                    idealOther,
                    movingTo,
                    otherTo);

                if (!foundCandidate || candidate.IsBetterThan(bestCandidate))
                {
                    bestCandidate = candidate;
                    foundCandidate = true;
                }
            }
        }

        if (!foundCandidate)
        {
            return InventorySwapPlan.Invalid();
        }

        return InventorySwapPlan.Valid(
            grid,
            movingItem,
            otherItem,
            otherOriginal,
            bestCandidate.MovingTo,
            bestCandidate.OtherTo);
    }

    private static SwapCandidate CreateCandidate(
        InventoryGrid grid,
        SwapRelationship relationship,
        InventoryPlacementSnapshot movingOriginal,
        InventoryPlacementSnapshot otherOriginal,
        InventoryCellRect idealMoving,
        InventoryCellRect idealOther,
        InventoryCellRect movingTo,
        InventoryCellRect otherTo)
    {
        int intentPenalty = GetIntentPenalty(
            relationship,
            movingTo,
            otherTo);
        int stabilityPenalty = GetStabilityPenalty(
            relationship,
            movingOriginal,
            otherOriginal,
            movingTo,
            otherTo);
        int edgePenalty = GetEdgePenalty(
            grid,
            movingTo,
            otherOriginal.Edges) +
            GetEdgePenalty(
                grid,
                otherTo,
                movingOriginal.Edges);
        int coveragePenalty = GetCoveragePenalty(
            movingTo,
            otherOriginal.Rect) +
            GetCoveragePenalty(
                otherTo,
                movingOriginal.Rect);
        int idealDistance = GetDistance(movingTo, idealMoving) +
                            GetDistance(otherTo, idealOther);
        int boundsPenalty = GetBoundsPenalty(
            movingOriginal.Rect,
            otherOriginal.Rect,
            movingTo,
            otherTo);
        int movementDistance = GetDistance(
                                   movingTo,
                                   movingOriginal.Rect) +
                               GetDistance(
                                   otherTo,
                                   otherOriginal.Rect);

        return new SwapCandidate(
            movingTo,
            otherTo,
            new SwapCandidateScore(
                intentPenalty,
                stabilityPenalty,
                edgePenalty,
                coveragePenalty,
                idealDistance,
                boundsPenalty,
                movementDistance));
    }

    private static List<InventoryCellRect> GetPlacementsOverlappingTarget(
        InventoryGrid grid,
        int width,
        int height,
        InventoryCellRect target)
    {
        List<InventoryCellRect> placements =
            new List<InventoryCellRect>();

        if (width <= 0 ||
            height <= 0 ||
            width > grid.GridWidth ||
            height > grid.GridHeight)
        {
            return placements;
        }

        int minX = Mathf.Max(0, target.X - width + 1);
        int maxX = Mathf.Min(
            grid.GridWidth - width,
            target.Right - 1);
        int minY = Mathf.Max(0, target.Y - height + 1);
        int maxY = Mathf.Min(
            grid.GridHeight - height,
            target.Bottom - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                placements.Add(
                    new InventoryCellRect(x, y, width, height));
            }
        }

        return placements;
    }

    private static SwapRelationship GetRelationship(
        InventoryCellRect movingOriginal,
        InventoryCellRect otherOriginal)
    {
        if (AreHorizontallyTouching(movingOriginal, otherOriginal))
            return SwapRelationship.Horizontal;
        if (AreVerticallyTouching(movingOriginal, otherOriginal))
            return SwapRelationship.Vertical;

        return SwapRelationship.Separate;
    }

    private static int GetIntentPenalty(
        SwapRelationship relationship,
        InventoryCellRect movingTo,
        InventoryCellRect otherTo)
    {
        switch (relationship)
        {
            case SwapRelationship.Horizontal:
                return AreHorizontallyTouching(movingTo, otherTo) ? 0 : 1;
            case SwapRelationship.Vertical:
                return AreVerticallyTouching(movingTo, otherTo) ? 0 : 1;
            default:
                return 0;
        }
    }

    private static int GetStabilityPenalty(
        SwapRelationship relationship,
        InventoryPlacementSnapshot movingOriginal,
        InventoryPlacementSnapshot otherOriginal,
        InventoryCellRect movingTo,
        InventoryCellRect otherTo)
    {
        switch (relationship)
        {
            case SwapRelationship.Horizontal:
                return Mathf.Abs(movingTo.Y - movingOriginal.Rect.Y) +
                       Mathf.Abs(otherTo.Y - otherOriginal.Rect.Y);
            case SwapRelationship.Vertical:
                return Mathf.Abs(movingTo.X - movingOriginal.Rect.X) +
                       Mathf.Abs(otherTo.X - otherOriginal.Rect.X);
            default:
                return 0;
        }
    }

    private static int GetEdgePenalty(
        InventoryGrid grid,
        InventoryCellRect rect,
        InventoryGridEdgeFlags targetEdges)
    {
        int penalty = 0;

        if ((targetEdges & InventoryGridEdgeFlags.Left) != 0 && rect.X != 0)
            penalty++;
        if ((targetEdges & InventoryGridEdgeFlags.Right) != 0 &&
            rect.Right != grid.GridWidth)
        {
            penalty++;
        }
        if ((targetEdges & InventoryGridEdgeFlags.Top) != 0 && rect.Y != 0)
            penalty++;
        if ((targetEdges & InventoryGridEdgeFlags.Bottom) != 0 &&
            rect.Bottom != grid.GridHeight)
        {
            penalty++;
        }

        return penalty;
    }

    private static int GetCoveragePenalty(
        InventoryCellRect destination,
        InventoryCellRect target)
    {
        int maximumCoverage = Mathf.Min(destination.Area, target.Area);
        return maximumCoverage - destination.IntersectionArea(target);
    }

    private static int GetBoundsPenalty(
        InventoryCellRect firstOriginal,
        InventoryCellRect secondOriginal,
        InventoryCellRect firstDestination,
        InventoryCellRect secondDestination)
    {
        int originalLeft = Mathf.Min(firstOriginal.X, secondOriginal.X);
        int originalTop = Mathf.Min(firstOriginal.Y, secondOriginal.Y);
        int originalRight = Mathf.Max(firstOriginal.Right, secondOriginal.Right);
        int originalBottom = Mathf.Max(firstOriginal.Bottom, secondOriginal.Bottom);

        int destinationLeft = Mathf.Min(
            firstDestination.X,
            secondDestination.X);
        int destinationTop = Mathf.Min(
            firstDestination.Y,
            secondDestination.Y);
        int destinationRight = Mathf.Max(
            firstDestination.Right,
            secondDestination.Right);
        int destinationBottom = Mathf.Max(
            firstDestination.Bottom,
            secondDestination.Bottom);

        return Mathf.Abs(destinationLeft - originalLeft) +
               Mathf.Abs(destinationTop - originalTop) +
               Mathf.Abs(destinationRight - originalRight) +
               Mathf.Abs(destinationBottom - originalBottom);
    }

    private static int GetDistance(
        InventoryCellRect first,
        InventoryCellRect second)
    {
        return Mathf.Abs(first.X - second.X) +
               Mathf.Abs(first.Y - second.Y);
    }

    private static bool AreVerticallyTouching(
        InventoryCellRect first,
        InventoryCellRect second)
    {
        return InventoryGridMath.IsRangeOverlapping(
                   first.X,
                   first.Right,
                   second.X,
                   second.Right) &&
               (first.Bottom == second.Y ||
                second.Bottom == first.Y);
    }

    private static bool AreHorizontallyTouching(
        InventoryCellRect first,
        InventoryCellRect second)
    {
        return InventoryGridMath.IsRangeOverlapping(
                   first.Y,
                   first.Bottom,
                   second.Y,
                   second.Bottom) &&
               (first.Right == second.X ||
                second.Right == first.X);
    }

    private static InventoryCellRect ApplyTargetEdges(
        InventoryGrid grid,
        int itemWidth,
        int itemHeight,
        InventoryPlacementSnapshot targetSlot)
    {
        int x = targetSlot.Rect.X;
        int y = targetSlot.Rect.Y;

        if ((targetSlot.Edges & InventoryGridEdgeFlags.Left) != 0)
            x = 0;
        else if ((targetSlot.Edges & InventoryGridEdgeFlags.Right) != 0)
            x = grid.GridWidth - itemWidth;

        if ((targetSlot.Edges & InventoryGridEdgeFlags.Top) != 0)
            y = 0;
        else if ((targetSlot.Edges & InventoryGridEdgeFlags.Bottom) != 0)
            y = grid.GridHeight - itemHeight;

        return new InventoryCellRect(x, y, itemWidth, itemHeight);
    }
}

#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using ItemSystem;
using NUnit.Framework;
using UnityEngine;

public class InventorySwapPlannerTests
{
    private GameObject gridObject;
    private InventoryGrid grid;
    private readonly List<UnityEngine.Object> createdObjects =
        new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        gridObject = new GameObject("InventorySwapPlannerTests_Grid");
        grid = gridObject.AddComponent<InventoryGrid>();

        MethodInfo awake = typeof(InventoryGrid).GetMethod(
            "Awake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        awake?.Invoke(grid, null);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (UnityEngine.Object createdObject in createdObjects)
        {
            if (createdObject != null)
                UnityEngine.Object.DestroyImmediate(createdObject);
        }

        createdObjects.Clear();

        if (gridObject != null)
            UnityEngine.Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void HorizontalDifferentWidths_PreservesPackedOuterBounds()
    {
        InventoryItem moving = CreateItem(2, 3);
        InventoryItem other = CreateItem(3, 3);

        Place(moving, 0, 0);
        Place(other, 2, 0);

        InventoryPlacementSnapshot original =
            InventoryPlacementSnapshot.Capture(grid, moving);
        grid.RemoveItem(moving);

        InventorySwapPlan plan = InventorySwapPlanner.BuildPlan(
            grid,
            moving,
            original,
            new Vector2Int(2, 0),
            other);

        Assert.That(plan.IsValid, Is.True);
        Assert.That(plan.Mode, Is.EqualTo(InventorySwapMode.StackHorizontal));
        AssertRect(plan.MovingTo, 3, 0, 2, 3);
        AssertRect(plan.OtherTo, 0, 0, 3, 3);

        InventoryMoveResultData result =
            InventorySwapService.TryCommitPlan(plan);

        Assert.That(result.Result, Is.EqualTo(InventoryMoveResult.Swapped));
        Assert.That(moving.x, Is.EqualTo(3));
        Assert.That(other.x, Is.EqualTo(0));
        AssertGridIntegrity(moving, other);
    }

    [Test]
    public void VerticalDifferentHeights_PreservesPackedOuterBounds()
    {
        InventoryItem moving = CreateItem(2, 2);
        InventoryItem other = CreateItem(2, 3);

        Place(moving, 1, 0);
        Place(other, 1, 2);

        InventoryPlacementSnapshot original =
            InventoryPlacementSnapshot.Capture(grid, moving);
        grid.RemoveItem(moving);

        InventorySwapPlan plan = InventorySwapPlanner.BuildPlan(
            grid,
            moving,
            original,
            new Vector2Int(1, 2),
            other);

        Assert.That(plan.IsValid, Is.True);
        Assert.That(plan.Mode, Is.EqualTo(InventorySwapMode.StackVertical));
        AssertRect(plan.MovingTo, 1, 3, 2, 2);
        AssertRect(plan.OtherTo, 1, 0, 2, 3);
    }

    [Test]
    public void RotatedMovingItem_UsesOriginalFootprintForAdjacency()
    {
        InventoryItem moving = CreateItem(2, 3);
        InventoryItem other = CreateItem(2, 2);

        Place(moving, 0, 0);
        Place(other, 2, 0);

        InventoryPlacementSnapshot original =
            InventoryPlacementSnapshot.Capture(grid, moving);
        grid.RemoveItem(moving);
        moving.isRotated = true;

        InventorySwapPlan plan = InventorySwapPlanner.BuildPlan(
            grid,
            moving,
            original,
            new Vector2Int(2, 0),
            other);

        Assert.That(plan.IsValid, Is.True);
        Assert.That(plan.Mode, Is.EqualTo(InventorySwapMode.StackHorizontal));
        AssertRect(plan.MovingTo, 2, 0, 3, 2);
        AssertRect(plan.OtherTo, 0, 0, 2, 2);
    }

    [Test]
    public void StaggeredHorizontalItems_PreserveOwnYInEitherDirection()
    {
        InventoryItem sword = CreateItem(1, 3);
        InventoryItem boots = CreateItem(2, 2);
        InventoryItem blocker = CreateItem(2, 3);

        Place(sword, 2, 0);
        Place(boots, 3, 1);
        Place(blocker, 3, 3);

        InventoryPlacementSnapshot swordOriginal =
            InventoryPlacementSnapshot.Capture(grid, sword);
        InventoryPlacementSnapshot bootsOriginal =
            InventoryPlacementSnapshot.Capture(grid, boots);

        InventorySwapPlan swordPlan = InventorySwapPlanner.BuildPlan(
            grid,
            sword,
            swordOriginal,
            new Vector2Int(boots.x, boots.y),
            boots);
        InventorySwapPlan bootsPlan = InventorySwapPlanner.BuildPlan(
            grid,
            boots,
            bootsOriginal,
            new Vector2Int(sword.x, sword.y),
            sword);

        Assert.That(swordPlan.IsValid, Is.True);
        Assert.That(swordPlan.Mode, Is.EqualTo(InventorySwapMode.StackHorizontal));
        AssertRect(swordPlan.MovingTo, 4, 0, 1, 3);
        AssertRect(swordPlan.OtherTo, 2, 1, 2, 2);

        Assert.That(bootsPlan.IsValid, Is.True);
        Assert.That(bootsPlan.Mode, Is.EqualTo(InventorySwapMode.StackHorizontal));
        AssertRect(bootsPlan.MovingTo, 2, 1, 2, 2);
        AssertRect(bootsPlan.OtherTo, 4, 0, 1, 3);

        grid.RemoveItem(sword);
        InventoryMoveResultData result =
            InventorySwapService.TryCommitPlan(swordPlan);

        Assert.That(result.Result, Is.EqualTo(InventoryMoveResult.Swapped));
        AssertGridIntegrity(sword, boots, blocker);
    }

    [Test]
    public void StaggeredVerticalItems_PreserveOwnXBeforeAlignmentFallbacks()
    {
        InventoryItem topItem = CreateItem(3, 1);
        InventoryItem bottomItem = CreateItem(2, 2);
        InventoryItem blocker = CreateItem(1, 1);

        Place(topItem, 0, 2);
        Place(bottomItem, 1, 3);
        Place(blocker, 3, 4);

        InventoryPlacementSnapshot original =
            InventoryPlacementSnapshot.Capture(grid, topItem);
        grid.RemoveItem(topItem);

        InventorySwapPlan plan = InventorySwapPlanner.BuildPlan(
            grid,
            topItem,
            original,
            new Vector2Int(bottomItem.x, bottomItem.y),
            bottomItem);

        Assert.That(plan.IsValid, Is.True);
        Assert.That(plan.Mode, Is.EqualTo(InventorySwapMode.StackVertical));
        AssertRect(plan.MovingTo, 0, 4, 3, 1);
        AssertRect(plan.OtherTo, 1, 2, 2, 2);

        InventoryMoveResultData result =
            InventorySwapService.TryCommitPlan(plan);

        Assert.That(result.Result, Is.EqualTo(InventoryMoveResult.Swapped));
        AssertGridIntegrity(topItem, bottomItem, blocker);
    }

    [Test]
    public void ExactSwapWinsBeforeAdjacentAdjustmentCandidates()
    {
        InventoryItem boots = CreateItem(2, 2);
        InventoryItem helmet = CreateItem(2, 2);
        InventoryItem branch = CreateItem(1, 4);

        Place(branch, 7, 0);
        Place(boots, 4, 3);
        Place(helmet, 6, 4);

        InventoryPlacementSnapshot bootsOriginal =
            InventoryPlacementSnapshot.Capture(grid, boots);
        InventoryPlacementSnapshot helmetOriginal =
            InventoryPlacementSnapshot.Capture(grid, helmet);

        InventorySwapPlan bootsPlan = InventorySwapPlanner.BuildPlan(
            grid,
            boots,
            bootsOriginal,
            new Vector2Int(helmet.x, helmet.y),
            helmet);
        InventorySwapPlan helmetPlan = InventorySwapPlanner.BuildPlan(
            grid,
            helmet,
            helmetOriginal,
            new Vector2Int(boots.x, boots.y),
            boots);

        Assert.That(bootsPlan.IsValid, Is.True);
        Assert.That(bootsPlan.Mode, Is.EqualTo(InventorySwapMode.Direct));
        AssertRect(bootsPlan.MovingTo, 6, 4, 2, 2);
        AssertRect(bootsPlan.OtherTo, 4, 3, 2, 2);

        Assert.That(helmetPlan.IsValid, Is.True);
        Assert.That(helmetPlan.Mode, Is.EqualTo(InventorySwapMode.Direct));
        AssertRect(helmetPlan.MovingTo, 4, 3, 2, 2);
        AssertRect(helmetPlan.OtherTo, 6, 4, 2, 2);
    }

    [Test]
    public void OccupiedIdealSlot_UsesNearestIntentPreservingAdjustment()
    {
        InventoryItem claw = CreateItem(3, 2);
        InventoryItem boots = CreateItem(2, 2);
        InventoryItem helmet = CreateItem(2, 2);

        Place(claw, 0, 3);
        Place(boots, 4, 3);
        Place(helmet, 6, 4);

        InventoryPlacementSnapshot clawOriginal =
            InventoryPlacementSnapshot.Capture(grid, claw);
        InventoryPlacementSnapshot bootsOriginal =
            InventoryPlacementSnapshot.Capture(grid, boots);

        InventorySwapPlan clawPlan = InventorySwapPlanner.BuildPlan(
            grid,
            claw,
            clawOriginal,
            new Vector2Int(boots.x, boots.y),
            boots);
        InventorySwapPlan bootsPlan = InventorySwapPlanner.BuildPlan(
            grid,
            boots,
            bootsOriginal,
            new Vector2Int(claw.x, claw.y),
            claw);

        Assert.That(clawPlan.IsValid, Is.True);
        Assert.That(clawPlan.Mode, Is.EqualTo(InventorySwapMode.Adjusted));
        AssertRect(clawPlan.MovingTo, 3, 3, 3, 2);
        AssertRect(clawPlan.OtherTo, 0, 3, 2, 2);

        Assert.That(bootsPlan.IsValid, Is.True);
        Assert.That(bootsPlan.Mode, Is.EqualTo(InventorySwapMode.Adjusted));
        AssertRect(bootsPlan.MovingTo, 0, 3, 2, 2);
        AssertRect(bootsPlan.OtherTo, 3, 3, 3, 2);
    }

    [Test]
    public void AdjacentSizeCombinations_AreValidAndDirectionSymmetric()
    {
        for (int movingWidth = 1; movingWidth <= 3; movingWidth++)
        {
            for (int movingHeight = 1; movingHeight <= 3; movingHeight++)
            {
                for (int otherWidth = 1; otherWidth <= 3; otherWidth++)
                {
                    for (int otherHeight = 1; otherHeight <= 3; otherHeight++)
                    {
                        InventoryItem moving = CreateItem(
                            movingWidth,
                            movingHeight);
                        InventoryItem other = CreateItem(
                            otherWidth,
                            otherHeight);

                        Place(moving, 0, 0);
                        Place(other, movingWidth, 0);

                        InventoryPlacementSnapshot movingOriginal =
                            InventoryPlacementSnapshot.Capture(grid, moving);
                        InventoryPlacementSnapshot otherOriginal =
                            InventoryPlacementSnapshot.Capture(grid, other);

                        InventorySwapPlan movingPlan =
                            InventorySwapPlanner.BuildPlan(
                                grid,
                                moving,
                                movingOriginal,
                                new Vector2Int(other.x, other.y),
                                other);
                        InventorySwapPlan otherPlan =
                            InventorySwapPlanner.BuildPlan(
                                grid,
                                other,
                                otherOriginal,
                                new Vector2Int(moving.x, moving.y),
                                moving);

                        string context =
                            $"moving={movingWidth}x{movingHeight}, " +
                            $"other={otherWidth}x{otherHeight}";

                        Assert.That(
                            movingPlan.IsValid,
                            Is.True,
                            context);
                        Assert.That(
                            otherPlan.IsValid,
                            Is.True,
                            context);
                        AssertRectEqual(
                            movingPlan.MovingTo,
                            otherPlan.OtherTo,
                            context);
                        AssertRectEqual(
                            movingPlan.OtherTo,
                            otherPlan.MovingTo,
                            $"horizontal {context}");

                        grid.RemoveItem(moving);
                        grid.RemoveItem(other);

                        Place(moving, 0, 0);
                        Place(other, 0, movingHeight);

                        movingOriginal =
                            InventoryPlacementSnapshot.Capture(grid, moving);
                        otherOriginal =
                            InventoryPlacementSnapshot.Capture(grid, other);

                        movingPlan = InventorySwapPlanner.BuildPlan(
                            grid,
                            moving,
                            movingOriginal,
                            new Vector2Int(other.x, other.y),
                            other);
                        otherPlan = InventorySwapPlanner.BuildPlan(
                            grid,
                            other,
                            otherOriginal,
                            new Vector2Int(moving.x, moving.y),
                            moving);

                        Assert.That(
                            movingPlan.IsValid,
                            Is.True,
                            $"vertical {context}");
                        Assert.That(
                            otherPlan.IsValid,
                            Is.True,
                            $"vertical {context}");
                        AssertRectEqual(
                            movingPlan.MovingTo,
                            otherPlan.OtherTo,
                            $"vertical {context}");
                        AssertRectEqual(
                            movingPlan.OtherTo,
                            otherPlan.MovingTo,
                            $"vertical {context}");

                        grid.RemoveItem(moving);
                        grid.RemoveItem(other);
                    }
                }
            }
        }
    }

    [Test]
    public void OppositeHorizontalEdges_InheritTargetEdgeAnchors()
    {
        InventoryItem moving = CreateItem(1, 2);
        InventoryItem other = CreateItem(3, 2);

        Place(moving, 0, 2);
        Place(other, 5, 2);

        InventoryPlacementSnapshot original =
            InventoryPlacementSnapshot.Capture(grid, moving);
        grid.RemoveItem(moving);

        InventorySwapPlan plan = InventorySwapPlanner.BuildPlan(
            grid,
            moving,
            original,
            new Vector2Int(5, 2),
            other);

        Assert.That(plan.IsValid, Is.True);
        Assert.That(plan.Mode, Is.EqualTo(InventorySwapMode.EdgeAnchored));
        Assert.That(plan.MovingTo.Right, Is.EqualTo(grid.GridWidth));
        Assert.That(plan.OtherTo.X, Is.EqualTo(0));
    }

    [Test]
    public void HalfOverlapWithoutPointerTarget_DoesNotSelectSwapTarget()
    {
        InventoryItem moving = CreateItem(2, 3);
        InventoryItem other = CreateItem(3, 3);

        Place(moving, 0, 0);
        Place(other, 2, 0);
        grid.RemoveItem(moving);

        bool resolvedFromEmptyPointer =
            InventorySwapTargetResolver.TryResolveTarget(
                grid,
                moving,
                new Vector2Int(1, 0),
                new Vector2Int(1, 0),
                out InventoryItem emptyPointerTarget);

        bool resolvedFromTargetPointer =
            InventorySwapTargetResolver.TryResolveTarget(
                grid,
                moving,
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                out InventoryItem pointerTarget);

        Assert.That(resolvedFromEmptyPointer, Is.False);
        Assert.That(emptyPointerTarget, Is.Null);
        Assert.That(resolvedFromTargetPointer, Is.True);
        Assert.That(pointerTarget, Is.SameAs(other));
    }

    [Test]
    public void StalePlan_FailsWithoutMutatingExistingGridState()
    {
        InventoryItem moving = CreateItem(2, 3);
        InventoryItem other = CreateItem(3, 3);
        InventoryItem blocker = CreateItem(1, 1);

        Place(moving, 0, 0);
        Place(other, 2, 0);

        InventoryPlacementSnapshot original =
            InventoryPlacementSnapshot.Capture(grid, moving);
        grid.RemoveItem(moving);

        InventorySwapPlan plan = InventorySwapPlanner.BuildPlan(
            grid,
            moving,
            original,
            new Vector2Int(2, 0),
            other);

        Assert.That(plan.IsValid, Is.True);
        Place(blocker, 0, 0);

        InventoryMoveResultData result =
            InventorySwapService.TryCommitPlan(plan);

        Assert.That(result.Result, Is.EqualTo(InventoryMoveResult.Failed));
        Assert.That(grid.GetItemAt(0, 0), Is.SameAs(blocker));
        Assert.That(grid.GetItemAt(2, 0), Is.SameAs(other));
        Assert.That(FindItemCell(moving), Is.EqualTo(new Vector2Int(-1, -1)));
    }

    [Test]
    public void RepeatedUnequalSwaps_KeepGridIntegrity()
    {
        InventoryItem moving = CreateItem(2, 3);
        InventoryItem other = CreateItem(3, 3);

        Place(moving, 0, 0);
        Place(other, 2, 0);

        for (int iteration = 0; iteration < 20; iteration++)
        {
            InventoryPlacementSnapshot original =
                InventoryPlacementSnapshot.Capture(grid, moving);
            Vector2Int requestedCell = new Vector2Int(other.x, other.y);

            grid.RemoveItem(moving);

            InventorySwapPlan plan = InventorySwapPlanner.BuildPlan(
                grid,
                moving,
                original,
                requestedCell,
                other);

            Assert.That(plan.IsValid, Is.True, $"iteration={iteration}");

            InventoryMoveResultData result =
                InventorySwapService.TryCommitPlan(plan);

            Assert.That(
                result.Result,
                Is.EqualTo(InventoryMoveResult.Swapped),
                $"iteration={iteration}");
            AssertGridIntegrity(moving, other);
        }

        Assert.That(moving.x, Is.EqualTo(0));
        Assert.That(other.x, Is.EqualTo(2));
    }

    private InventoryItem CreateItem(int width, int height, bool isRotated = false)
    {
        ItemDefinitionSO definition =
            ScriptableObject.CreateInstance<ItemDefinitionSO>();
        definition.itemId = Guid.NewGuid().ToString("N");
        definition.itemName = $"Test_{width}x{height}";
        definition.itemWidth = width;
        definition.itemHeight = height;
        createdObjects.Add(definition);

        ItemInstance instance = new ItemInstance
        {
            instanceId = Guid.NewGuid().ToString("N"),
            definition = definition
        };

        return new InventoryItem(instance)
        {
            isRotated = isRotated
        };
    }

    private void Place(InventoryItem item, int x, int y)
    {
        Assert.That(
            grid.TryPlaceItem(item, x, y),
            Is.True,
            $"Failed to place {item.itemData.definition.itemName} at ({x},{y})");
    }

    private void AssertGridIntegrity(params InventoryItem[] items)
    {
        foreach (InventoryItem item in items)
        {
            InventoryCellRect rect = new InventoryCellRect(
                item.x,
                item.y,
                item.CurrentWidth,
                item.CurrentHeight);

            Assert.That(rect.IsInside(grid), Is.True, item.itemData.definition.itemName);

            for (int x = rect.X; x < rect.Right; x++)
            {
                for (int y = rect.Y; y < rect.Bottom; y++)
                {
                    Assert.That(
                        grid.GetItemAt(x, y),
                        Is.SameAs(item),
                        $"cell=({x},{y}) item={item.itemData.definition.itemName}");
                }
            }
        }

        for (int x = 0; x < grid.GridWidth; x++)
        {
            for (int y = 0; y < grid.GridHeight; y++)
            {
                InventoryItem occupant = grid.GetItemAt(x, y);
                if (occupant == null)
                    continue;

                InventoryCellRect ownerRect = new InventoryCellRect(
                    occupant.x,
                    occupant.y,
                    occupant.CurrentWidth,
                    occupant.CurrentHeight);
                Assert.That(ownerRect.ContainsCell(x, y), Is.True);
            }
        }
    }

    private Vector2Int FindItemCell(InventoryItem item)
    {
        for (int x = 0; x < grid.GridWidth; x++)
        {
            for (int y = 0; y < grid.GridHeight; y++)
            {
                if (grid.GetItemAt(x, y) == item)
                    return new Vector2Int(x, y);
            }
        }

        return new Vector2Int(-1, -1);
    }

    private static void AssertRect(
        InventoryCellRect rect,
        int x,
        int y,
        int width,
        int height)
    {
        Assert.That(rect.X, Is.EqualTo(x));
        Assert.That(rect.Y, Is.EqualTo(y));
        Assert.That(rect.Width, Is.EqualTo(width));
        Assert.That(rect.Height, Is.EqualTo(height));
    }

    private static void AssertRectEqual(
        InventoryCellRect actual,
        InventoryCellRect expected,
        string context)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X), context);
        Assert.That(actual.Y, Is.EqualTo(expected.Y), context);
        Assert.That(actual.Width, Is.EqualTo(expected.Width), context);
        Assert.That(actual.Height, Is.EqualTo(expected.Height), context);
    }
}
#endif

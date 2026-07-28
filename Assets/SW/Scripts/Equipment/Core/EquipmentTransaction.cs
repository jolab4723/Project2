using ItemSystem;
using UnityEngine;
public readonly struct EquipmentTransactionResult
{
    public EquipResultData EquipmentResult { get; }
    public InventoryItem EquippedItem { get; }
    public InventoryItem ReturnedItem { get; }

    public InventoryGrid ResultGrid { get; }
    public InventoryPlacementSnapshot ResultPlacement { get; }

    public bool RollbackSucceeded { get; }
    public bool IsSuccess =>
        EquipmentResult != null &&
        EquipmentResult.IsSuccess;

    public EquipmentTransactionResult(
    EquipResultData equipmentResult,
    InventoryItem equippedItem,
    InventoryItem returnedItem,
    InventoryGrid resultGrid,
    InventoryPlacementSnapshot resultPlacement,
    bool rollbackSucceeded)
    {
        EquipmentResult = equipmentResult;
        EquippedItem = equippedItem;
        ReturnedItem = returnedItem;
        ResultGrid = resultGrid;
        ResultPlacement = resultPlacement;
        RollbackSucceeded = rollbackSucceeded;
    }

    public static EquipmentTransactionResult Failed(
    EquipResultData equipmentResult,
    bool rollbackSucceeded = true)
    {
        return new EquipmentTransactionResult(
            equipmentResult,
            null,
            null,
            null,
            default,
            rollbackSucceeded);
    }
}
public class EquipmentTransaction
{
    private readonly EquipmentSystem equipmentSystem;

    public EquipmentTransaction(EquipmentSystem equipmentSystem)
    {
        this.equipmentSystem = equipmentSystem;
    }
    
    public EquipmentTransactionResult TryEquip(
    InventoryGrid sourceGrid,
    InventoryItem incomingItem,
    InventoryPlacementSnapshot originalPlacement,
    EquipSlotType targetSlot)
    {
        if (equipmentSystem == null ||
        sourceGrid == null ||
        incomingItem == null ||
        !originalPlacement.IsValid)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.InvalidItem,
                    targetSlot,
                    incomingItem));
        }

        // 우클릭 장착이면 아직 그리드에 있고,
        // 드래그 장착이면 이미 그리드에서 빠져 있을 수 있다.
        if (sourceGrid.ContainsItem(incomingItem) &&
            !sourceGrid.TryRemoveItem(incomingItem))
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.Failed,
                    targetSlot,
                    incomingItem));
        }

        // 장비 상태에서는 회전하지 않은 상태로 통일한다.
        incomingItem.isRotated = false;

        EquipResultData equipmentResult =
            equipmentSystem.TryEquipState(
                incomingItem,
                targetSlot);

        if (!equipmentResult.IsSuccess)
        {
            bool restored = RestoreToGrid(
                sourceGrid,
                incomingItem,
                originalPlacement);

            if (!restored)
            {
                Debug.LogError(
                    "[EquipmentTransaction] 장착 실패 후 " +
                    "아이템을 원래 그리드 위치에 복구하지 못했습니다.");
            }

            return EquipmentTransactionResult.Failed(
                equipmentResult,
                restored);
        }

        // 장비와 인벤토리 상태가 모두 확정된 뒤 호출한다.
        equipmentSystem.PublishChanged();
        NotifyEquipped(incomingItem);

        return new EquipmentTransactionResult(
            equipmentResult,
            incomingItem,
            null,
            null,
            default,
            true);
    }

    public EquipmentTransactionResult TryUnequip(
        EquipSlotType sourceSlot,
        InventoryGrid targetGrid,
        InventoryPlacementSnapshot targetPlacement)
    {
        if (equipmentSystem == null ||
        targetGrid == null ||
        !targetPlacement.IsValid)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.NoReturnSpace,
                    sourceSlot));
        }

        if (!equipmentSystem.TryGetEquippedItem(
                sourceSlot,
                out InventoryItem item) ||
            item == null)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.NotEquipped,
                    sourceSlot));
        }

        // 장비 슬롯 안에서는 회전하지 않은 상태가 기준이다.
        item.isRotated = targetPlacement.IsRotated;

        // 전달받은 위치 정보가 현재 아이템 크기와 맞는지 확인한다.
        if (item.CurrentWidth != targetPlacement.Rect.Width ||
            item.CurrentHeight != targetPlacement.Rect.Height)
        {
            item.isRotated = false;

            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.Failed,
                    sourceSlot,
                    item));
        }

        if (!targetGrid.CanPlaceItem(
                targetPlacement.Rect.X,
                targetPlacement.Rect.Y,
                item.CurrentWidth,
                item.CurrentHeight))
        {
            item.isRotated = false;

            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.NoReturnSpace,
                    sourceSlot,
                    item));
        }

        EquipResultData equipmentResult =
            equipmentSystem.TryUnequipState(sourceSlot);

        if (!equipmentResult.IsSuccess)
        {
            item.isRotated = false;

            return EquipmentTransactionResult.Failed(
                equipmentResult);
        }

        bool placed = targetGrid.TryPlaceItem(
            item,
            targetPlacement.Rect.X,
            targetPlacement.Rect.Y);

        if (!placed)
        {
            // 인벤토리 배치 실패: 원래 장비 상태로 무이벤트 복구한다.
            item.isRotated = false;

            EquipResultData rollbackResult =
                equipmentSystem.TryEquipState(
                    item,
                    sourceSlot);

            bool rollbackSucceeded =
                rollbackResult.IsSuccess;

            if (!rollbackSucceeded)
            {
                Debug.LogError(
                    "[EquipmentTransaction] 장착 해제 실패 후 " +
                    "원래 장비 슬롯으로 복구하지 못했습니다.");
            }

            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.Failed,
                    sourceSlot,
                    item),
                rollbackSucceeded);
        }

        // 장비와 인벤토리가 모두 확정된 뒤 한 번만 알린다.
        equipmentSystem.PublishChanged();
        NotifyUnequipped(item);

        return new EquipmentTransactionResult(
            equipmentResult,
            null,
            item,
            targetGrid,
            targetPlacement,
            true);
    }

    public EquipmentTransactionResult TryUnequipForTransfer(
    EquipSlotType sourceSlot,
    InventoryItem expectedItem)
    {
        if (equipmentSystem == null || expectedItem?.itemData?.definition == null)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.InvalidItem,
                    sourceSlot,
                    expectedItem));
        }

        if (!equipmentSystem.TryGetEquippedItem(sourceSlot, out InventoryItem equippedItem) ||
            equippedItem == null ||
            !ReferenceEquals(equippedItem, expectedItem))
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.NotEquipped,
                    sourceSlot,
                    expectedItem));
        }

        EquipResultData equipmentResult =
            equipmentSystem.TryUnequipState(sourceSlot);

        if (!equipmentResult.IsSuccess)
        {
            return EquipmentTransactionResult.Failed(
                equipmentResult);
        }

        expectedItem.isRotated = false;

        equipmentSystem.PublishChanged();
        NotifyUnequipped(expectedItem);

        return new EquipmentTransactionResult(
            equipmentResult,
            null,
            expectedItem,
            null,
            default,
            true);
    }

    public EquipmentTransactionResult TrySwap(
    EquipSlotType targetSlot,
    InventoryItem incomingItem,
    InventoryGrid incomingGrid,
    InventoryPlacementSnapshot incomingOriginal,
    InventoryGrid outgoingGrid,
    InventoryPlacementSnapshot outgoingPlacement,
    bool allowAlternativeSpace)
    {
        if (equipmentSystem == null ||
            incomingItem == null ||
            incomingGrid == null ||
            outgoingGrid == null ||
            !incomingOriginal.IsValid ||
            !outgoingPlacement.IsValid)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.InvalidItem,
                    targetSlot,
                    incomingItem));
        }

        if (!equipmentSystem.TryGetEquippedItem(
                targetSlot,
                out InventoryItem outgoingItem) ||
            outgoingItem == null)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.NotEquipped,
                    targetSlot,
                    incomingItem));
        }

        if (incomingItem == outgoingItem)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.Failed,
                    targetSlot,
                    incomingItem));
        }

        // 우클릭이면 아직 그리드에 있고,
        // 드래그 중이면 이미 그리드에서 제거되어 있을 수 있다.
        if (incomingGrid.ContainsItem(incomingItem) &&
            !incomingGrid.TryRemoveItem(incomingItem))
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.Failed,
                    targetSlot,
                    incomingItem));
        }

        // 먼저 교체 대상 아이템이 있던 위치를 확인한다. 이때 아이템 상태를 미리 바꾸지 않고
        // 전달받은 배치 정보와 정의상의 크기가 일치하는지도 함께 검증한다.
        InventoryPlacementSnapshot resolvedOutgoingPlacement =
            InventoryPlacementSnapshot.FromOriginalState(
                outgoingGrid,
                outgoingItem,
                outgoingPlacement.Rect.X,
                outgoingPlacement.Rect.Y,
                outgoingPlacement.IsRotated);

        bool requestedPlacementMatches =
            resolvedOutgoingPlacement.IsValid &&
            resolvedOutgoingPlacement.Rect.Width ==
                outgoingPlacement.Rect.Width &&
            resolvedOutgoingPlacement.Rect.Height ==
                outgoingPlacement.Rect.Height;

        bool hasOutgoingSpace =
            requestedPlacementMatches &&
            outgoingGrid.CanPlaceItem(
                resolvedOutgoingPlacement.Rect.X,
                resolvedOutgoingPlacement.Rect.Y,
                resolvedOutgoingPlacement.Rect.Width,
                resolvedOutgoingPlacement.Rect.Height);

        // 원래 위치에 들어가지 않으면 장비 직접 해제와 같은 규칙으로 다른 위치를 찾고,
        // 현재 방향의 공간도 없을 때는 회전 방향까지 확인한다.
        if (!hasOutgoingSpace && allowAlternativeSpace)
        {
            hasOutgoingSpace =
                outgoingGrid.TryFindEmptySpaceForItem(
                    outgoingItem,
                    outgoingPlacement.IsRotated,
                    out resolvedOutgoingPlacement);
        }

        if (!hasOutgoingSpace)
        {
            // 실패 후에도 장비 슬롯의 회전 상태는 정방향으로 유지한다.
            outgoingItem.isRotated = false;

            bool restored = RestoreToGrid(
                incomingGrid,
                incomingItem,
                incomingOriginal);

            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.NoReturnSpace,
                    targetSlot,
                    incomingItem),
                restored);
        }

        // 배치가 확정된 뒤에만 인벤토리로 나갈 장비의 실제 회전 상태를 반영한다.
        outgoingItem.isRotated =
            resolvedOutgoingPlacement.IsRotated;

        int outgoingX =
            resolvedOutgoingPlacement.Rect.X;
        int outgoingY =
            resolvedOutgoingPlacement.Rect.Y;

        // 장착되는 아이템은 회전하지 않은 상태로 통일한다.
        incomingItem.isRotated = false;

        EquipResultData equipmentResult =
            equipmentSystem.TrySwapState(
                targetSlot,
                incomingItem);

        if (!equipmentResult.IsSuccess)
        {
            outgoingItem.isRotated = false;

            bool restored = RestoreToGrid(
                incomingGrid,
                incomingItem,
                incomingOriginal);

            return EquipmentTransactionResult.Failed(
                equipmentResult,
                restored);
        }

        bool outgoingPlaced =
            outgoingGrid.TryPlaceItem(
                outgoingItem,
                outgoingX,
                outgoingY);

        if (!outgoingPlaced)
        {
            // 장비 상태부터 원래 장비로 되돌린다.
            outgoingItem.isRotated = false;

            EquipResultData equipmentRollback =
                equipmentSystem.TrySwapState(
                    targetSlot,
                    outgoingItem);

            // 그다음 들어오던 아이템을 원래 그리드로 복구한다.
            bool incomingRestored = RestoreToGrid(
                incomingGrid,
                incomingItem,
                incomingOriginal);

            bool rollbackSucceeded =
                equipmentRollback.IsSuccess &&
                incomingRestored;

            if (!rollbackSucceeded)
            {
                Debug.LogError(
                    "[EquipmentTransaction] 장비 교환 실패 후 " +
                    "원래 상태로 복구하지 못했습니다.");
            }

            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.Failed,
                    targetSlot,
                    incomingItem),
                rollbackSucceeded);
        }

        InventoryPlacementSnapshot actualPlacement =
            InventoryPlacementSnapshot.Capture(
                outgoingGrid,
                outgoingItem);

        equipmentSystem.PublishChanged();

        NotifyUnequipped(outgoingItem);
        NotifyEquipped(incomingItem);

        return new EquipmentTransactionResult(
            equipmentResult,
            incomingItem,
            outgoingItem,
            outgoingGrid,
            actualPlacement,
            true);
    }

    private bool RestoreToGrid(
    InventoryGrid grid,
    InventoryItem item,
    InventoryPlacementSnapshot placement)
    {
        if (grid == null ||
            item == null ||
            !placement.IsValid)
        {
            return false;
        }
        item.isRotated = placement.IsRotated;
        if (grid.ContainsItem(item))
            return true;

        return grid.TryPlaceItem(
            item,
            placement.Rect.X,
            placement.Rect.Y);
    }

    public EquipmentTransactionResult TryRestoreEquippedItem(
    InventoryItem item,
    EquipSlotType targetSlot)
    {
        if (equipmentSystem == null ||
            item?.itemData?.definition == null)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.InvalidItem,
                    targetSlot,
                    item));
        }

        EquipResultData equipmentResult = equipmentSystem.TryEquipState(item, targetSlot);

        if (!equipmentResult.IsSuccess)
        {
            return EquipmentTransactionResult.Failed(
                equipmentResult);
        }

        // 장비 슬롯에서는 회전하지 않은 상태로 통일한다.
        item.isRotated = false;

        equipmentSystem.PublishChanged();
        NotifyEquipped(item);

        return new EquipmentTransactionResult(
            equipmentResult,
            item,
            null,
            null,
            default,
            true);
    }

    /// <summary>장착 성공 시 고유효과 OnEquip 호출</summary>
    /// 스탯 갱신은 EquipmentSystem의 변경 이벤트 구독자가 담당한다.
    private static void NotifyEquipped(InventoryItem item)
    {
        item?.itemData?.definition?.uniqueEffect?
            .OnEquip(item.itemData);
    }
    /// <summary>해제 성공 시 고유효과 OnUnequip 호출</summary>
    /// 스탯 갱신은 EquipmentSystem의 변경 이벤트 구독자가 담당한다.
    private static void NotifyUnequipped(InventoryItem item)
    {
        item?.itemData?.definition?.uniqueEffect?
            .OnUnequip(item.itemData);
    }
}

using ItemSystem;
using UnityEngine;
public readonly struct EquipmentTransactionResult
{
    private readonly EquipResultData equipmentResult;
    private readonly bool rollbackSucceeded;

    /// <summary>
    /// 장비 처리 결과다. 초기화되지 않은 기본 구조체는 Failed로 취급한다.
    /// </summary>
    public EquipResult Result => equipmentResult?.Result ?? EquipResult.Failed;
    public InventoryGrid ResultGrid { get; }
    public InventoryPlacementSnapshot ResultPlacement { get; }

    public bool IsSuccess =>
        equipmentResult != null &&
        equipmentResult.IsSuccess;

    /// <summary>
    /// 실패 후 장비·인벤토리 모델을 원래 상태로 되돌리지 못했을 때만 true다.
    /// UI 표시 복구 여부는 포함하지 않는다.
    /// </summary>
    public bool HasRecoveryFailure =>
        equipmentResult != null &&
        !rollbackSucceeded;

    internal EquipmentTransactionResult(
    EquipResultData equipmentResult,
    InventoryGrid resultGrid,
    InventoryPlacementSnapshot resultPlacement,
    bool rollbackSucceeded)
    {
        this.equipmentResult = equipmentResult;
        ResultGrid = resultGrid;
        ResultPlacement = resultPlacement;
        this.rollbackSucceeded = rollbackSucceeded;
    }

    internal static EquipmentTransactionResult Failed(
    EquipResultData equipmentResult,
    bool rollbackSucceeded = true)
    {
        return new EquipmentTransactionResult(
            equipmentResult,
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
    
    /// <summary>
    /// 인벤토리 아이템을 장착한다. 드래그 경로에서는 아이템이 이미 Grid에서
    /// 빠져 있을 수 있으며, 실패하면 전달받은 원래 배치로 복구한다.
    /// </summary>
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
                    EquipResult.InvalidItem));
        }

        // 우클릭 장착이면 아직 그리드에 있고,
        // 드래그 장착이면 이미 그리드에서 빠져 있을 수 있다.
        if (sourceGrid.ContainsItem(incomingItem) &&
            !sourceGrid.TryRemoveItem(incomingItem))
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.Failed));
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
            null,
            default,
            true);
    }

    /// <summary>
    /// 장착 아이템을 검증된 인벤토리 위치로 옮긴다.
    /// 배치 실패 시 장비 모델을 원래 슬롯으로 자동 복구한다.
    /// </summary>
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
                    EquipResult.NoReturnSpace));
        }

        if (!equipmentSystem.TryGetEquippedItem(
                sourceSlot,
                out InventoryItem item) ||
            item == null)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.NotEquipped));
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
                    EquipResult.Failed));
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
                    EquipResult.NoReturnSpace));
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
                    EquipResult.Failed),
                rollbackSucceeded);
        }

        // 장비와 인벤토리가 모두 확정된 뒤 한 번만 알린다.
        equipmentSystem.PublishChanged();
        NotifyUnequipped(item);

        return new EquipmentTransactionResult(
            equipmentResult,
            targetGrid,
            targetPlacement,
            true);
    }

    /// <summary>
    /// 판매 같은 외부 이동을 위해 장비 모델에서 아이템을 분리한다.
    /// 성공 이후의 이동 또는 실패 복구는 호출자가 이어서 책임진다.
    /// </summary>
    public EquipmentTransactionResult TryUnequipForTransfer(
    EquipSlotType sourceSlot,
    InventoryItem expectedItem)
    {
        if (equipmentSystem == null || expectedItem?.itemData?.definition == null)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.InvalidItem));
        }

        if (!equipmentSystem.TryGetEquippedItem(sourceSlot, out InventoryItem equippedItem) ||
            equippedItem == null ||
            !ReferenceEquals(equippedItem, expectedItem))
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.NotEquipped));
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
            default,
            true);
    }

    /// <summary>
    /// 인벤토리 아이템과 장착 아이템을 하나의 복구 가능한 흐름으로 교환한다.
    /// allowAlternativeSpace가 true면 기존 위치가 찼을 때 다른 빈 공간도 찾는다.
    /// </summary>
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
                    EquipResult.InvalidItem));
        }

        if (!equipmentSystem.TryGetEquippedItem(
                targetSlot,
                out InventoryItem outgoingItem) ||
            outgoingItem == null)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.NotEquipped));
        }

        if (incomingItem == outgoingItem)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.Failed));
        }

        // 우클릭이면 아직 그리드에 있고,
        // 드래그 중이면 이미 그리드에서 제거되어 있을 수 있다.
        if (incomingGrid.ContainsItem(incomingItem) &&
            !incomingGrid.TryRemoveItem(incomingItem))
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.Failed));
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
                    EquipResult.NoReturnSpace),
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
                    EquipResult.Failed),
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

    /// <summary>
    /// 로드 또는 실패 복구 경로에서 아이템을 장비 모델에 다시 넣는다.
    /// 이 메서드는 인벤토리 Grid에서 아이템을 제거하지 않는다.
    /// </summary>
    public EquipmentTransactionResult TryRestoreEquippedItem(
    InventoryItem item,
    EquipSlotType targetSlot)
    {
        if (equipmentSystem == null ||
            item?.itemData?.definition == null)
        {
            return EquipmentTransactionResult.Failed(
                EquipResultData.Failed(
                    EquipResult.InvalidItem));
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
            null,
            default,
            true);
    }

    /// <summary>
    /// 장착 성공 시 고유 효과를 적용한다.
    /// 스탯 갱신은 EquipmentSystem 변경 이벤트 구독자가 담당한다.
    /// </summary>
    private static void NotifyEquipped(InventoryItem item)
    {
        item?.itemData?.definition?.uniqueEffect?
            .OnEquip(item.itemData);
    }
    /// <summary>
    /// 해제 성공 시 고유 효과를 제거한다.
    /// 스탯 갱신은 EquipmentSystem 변경 이벤트 구독자가 담당한다.
    /// </summary>
    private static void NotifyUnequipped(InventoryItem item)
    {
        item?.itemData?.definition?.uniqueEffect?
            .OnUnequip(item.itemData);
    }
}

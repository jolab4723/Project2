using UnityEngine;

public class ItemEquipHandler : MonoBehaviour
{
    [SerializeField] private ItemUI itemUI;
    private InventoryController inventoryController;
    private EquipmentSystem equipmentSystem;
    private EquipSlotUI[] equipmentSlots;
    private EquipmentTransaction equipmentTransaction;
    private bool bindWarningLogged;

    private void Awake()
    {
        if (itemUI == null)
            itemUI = GetComponent<ItemUI>();
    }

    /// <summary>
    /// 생성한 아이템 UI가 어느 플레이어의 인벤토리와 장비를 변경할지 지정한다.
    /// 입력이 시작되기 전에 Spawner가 호출해야 하며 전역 Instance를 대신한다.
    /// </summary>
    public void Bind(
        InventoryController owner,
        EquipSlotUI[] ownerEquipmentSlots)
    {
        inventoryController = owner;
        equipmentSystem = owner != null ? owner.EquipmentSystem : null;
        equipmentSlots = ownerEquipmentSlots;
        equipmentTransaction = equipmentSystem != null
            ? new EquipmentTransaction(equipmentSystem)
            : null;
        bindWarningLogged = false;
    }

    /// <summary>
    /// 현재 아이템의 장착 상태에 따라 우클릭 장착 또는 해제를 요청한다.
    /// 반환값은 실제 장비 변경 성공 여부다.
    /// </summary>
    public bool TryHandleRightClick()
    {
        if (!EnsureReady())
            return false;

        if (itemUI.IsEquipped)
            return TryUnequipByRightClick();

        return TryEquipByRightClick();
    }

    private bool TryEquipByRightClick()
    {
        EquipSlotUI swapSlot = null;

        InventoryGrid sourceGrid = itemUI.CurrentGrid;

        InventoryPlacementSnapshot originalPlacement =
            InventoryPlacementSnapshot.Capture(
                sourceGrid,
                itemUI.Item);

        foreach (EquipSlotUI slot in equipmentSlots)
        {
            if (slot == null || !slot.CanAcceptType(itemUI.Item.itemData))
                continue;

            EquipmentTransactionResult result =
                equipmentTransaction.TryEquip(
                    sourceGrid,
                    itemUI.Item,
                    originalPlacement,
                    slot.SlotType);

            if (result.IsSuccess)
            {
                SetEquipSlotVisual(slot);
                return true;
            }

            if (result.HasRecoveryFailure)
            {
                Debug.LogError(
                    "[ItemEquipHandler] 장착 실패 후 " +
                    "인벤토리 상태 복구에 실패했습니다.");
                return false;
            }

            if (result.Result ==
                EquipResult.SlotOccupied &&
                swapSlot == null)
            {
                swapSlot = slot;
            }
        }

        // 호환 슬롯이 이미 차 있었다면 교체 메서드가 성공 여부와 구체적인 실패 메시지를 담당한다.
        // 교체 실패 뒤 일반 메시지로 다시 덮어쓰지 않도록 결과를 그대로 반환한다.
        if (swapSlot != null)
            return TryRightClickSwapWithEquipSlot(swapSlot);

        inventoryController.PrintLog("장착할 수 있는 슬롯이 없거나 꽉 찼습니다!");
        return false;
    }

    private bool TryUnequipByRightClick()
    {
        EquipSlotUI equippedSlot = itemUI.CurrentEquipSlot;
        if (equippedSlot == null) return false;

        InventoryGrid grid = itemUI.CurrentGrid;
        bool rotationBeforeRequest = itemUI.Item.isRotated;

        if (!itemUI.TryFindUnequipSpace(
                out int foundX,
                out int foundY,
                out bool targetRotated))
        {
            inventoryController.PrintLog(
                EquipMessageMapper.GetMessage(EquipResult.NoReturnSpace));
            return false;
        }

        InventoryPlacementSnapshot targetPlacement =
            InventoryPlacementSnapshot.FromOriginalState(
                grid,
                itemUI.Item,
                foundX,
                foundY,
                targetRotated);

        EquipmentTransactionResult result =
            equipmentTransaction.TryUnequip(
                equippedSlot.SlotType,
                grid,
                targetPlacement);

        if (!result.IsSuccess)
        {
            LogTransactionFailure(
                result,
                "[ItemEquipHandler] 장착 해제 실패 후 장비 상태 복구에 실패했습니다.");
            return false;
        }

        equippedSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        ApplyResultGridPlacement(itemUI, result);

        if (rotationBeforeRequest != targetRotated)
        {
            inventoryController.PrintLog(
                "공간 확보를 위해 아이템을 회전하여 보관했습니다.");
        }

        return true;
    }

    /// <summary>
    /// 장비 슬롯으로 놓은 드래그를 처리한다.
    /// true면 장착·교환 또는 원래 위치 복구까지 이 메서드가 마친 상태다.
    /// </summary>
    public bool TryHandleDropToEquipSlot(EquipSlotUI targetSlot)
    {
        if (!EnsureReady() || targetSlot == null)
            return false;

        if (itemUI.OriginalWasEquipped)
        {
            EquipSlotUI originalSlot = itemUI.CurrentEquipSlot;
            SetEquipSlotVisual(originalSlot);
            return true;
        }

        if (itemUI.OriginalGrid == ShopController.Instance?.ShopGrid)
        {
            return itemUI.TryReturnToOriginalPosition();
        }
        if (!targetSlot.IsEmpty)
        {
            if (TrySwapWithEquipSlot(targetSlot))
                return true;

            return itemUI.TryReturnToOriginalPosition();
        }

        EquipmentTransactionResult result =
            equipmentTransaction.TryEquip(
                itemUI.OriginalGrid,
                itemUI.Item,
                itemUI.OriginalPlacement,
                targetSlot.SlotType);

        if (result.IsSuccess)
        {
            SetEquipSlotVisual(targetSlot);
            return true;
        }
        LogTransactionFailure(
            result,
            "[ItemEquipHandler] 드래그 장착 실패 후 아이템 모델을 복구하지 못했습니다.");

        if (result.HasRecoveryFailure)
            return true;

        RestoreOriginalGridVisual();

        return true;
    }

    /// <summary>
    /// 드래그 중 사라진 장비 슬롯 표시를 실제 EquipmentSystem 상태에 맞춰 복구한다.
    /// 모델이 이미 바뀐 경우에만 정식 EquipmentTransaction 복구 경로를 사용한다.
    /// 반환값은 모델과 슬롯 표시가 모두 복구되었는지 여부다.
    /// </summary>
    public bool TryRestoreOriginalEquipment(EquipSlotUI originalSlot)
    {
        if (!EnsureReady() || originalSlot == null || itemUI?.Item == null)
            return false;

        if (equipmentSystem.TryGetEquippedItem(
                originalSlot.SlotType,
                out InventoryItem equippedItem))
        {
            if (!ReferenceEquals(equippedItem, itemUI.Item))
                return false;

            SetEquipSlotVisual(originalSlot);
            return true;
        }

        EquipmentTransactionResult result =
            equipmentTransaction.TryRestoreEquippedItem(
                itemUI.Item,
                originalSlot.SlotType);

        if (!result.IsSuccess)
            return false;

        SetEquipSlotVisual(originalSlot);
        return true;
    }

    /// <summary>
    /// 확정되거나 검증된 장비 모델 상태를 슬롯 UI에 반영한다.
    /// EquipmentSystem 상태나 고유 효과는 변경하지 않는다.
    /// </summary>
    public void SetEquipSlotVisual(EquipSlotUI slot)
    {
        if (slot == null)
            return;

        slot.SetItemUI(itemUI);
        itemUI.SetCurrentEquipSlot(slot);

        itemUI.ResetRotationForEquipSlot();

        RectTransform itemRect = itemUI.Rect;
        RectTransform slotRect = slot.transform as RectTransform;
        RectTransform iconRect = itemUI.ItemTransform as RectTransform;

        transform.SetParent(slot.transform);
        transform.position = slot.transform.position;

        itemRect.sizeDelta = slotRect.sizeDelta;
        iconRect.sizeDelta = slotRect.sizeDelta;
    }

    private bool TrySwapWithEquipSlot(EquipSlotUI slot)
    {
        if (slot == null)
            return false;

        ItemUI equippedUI = slot.EquippedItemUI;
        if (equippedUI == null)
            return false;

        if (!slot.CanAcceptType(itemUI.Item.itemData))
            return false;

        InventoryGrid returnGrid = itemUI.OriginalGrid;
        InventoryItem outgoingItem = equippedUI.Item;

        InventoryPlacementSnapshot outgoingPlacement =
            InventoryPlacementSnapshot.FromOriginalState(
                returnGrid,
                outgoingItem,
                itemUI.OriginalPlacement.Rect.X,
                itemUI.OriginalPlacement.Rect.Y,
                outgoingItem.isRotated);

        // B가 A의 원래 위치에 들어갈 수 있는지 확인
        EquipmentTransactionResult result =
            equipmentTransaction.TrySwap(
                slot.SlotType,
                itemUI.Item,
                itemUI.OriginalGrid,
                itemUI.OriginalPlacement,
                returnGrid,
                outgoingPlacement,
                true);

        if (!result.IsSuccess)
        {
            LogTransactionFailure(
                result,
                "[ItemEquipHandler] 장비 교환 실패 후 상태 복구에 실패했습니다.");

            if (!result.HasRecoveryFailure)
            {
                // 트랜잭션이 모델을 복구했으므로 UI만 복구한다.
                RestoreOriginalGridVisual();
            }

            // 교환 실패까지 처리했으므로 외부에서 다시
            // ReturnToOriginalPosition을 호출하지 않게 true를 반환한다.
            return true;
        }

        ApplySuccessfulEquipmentSwapVisuals(slot, equippedUI, result);

        return true;
    }

    private bool TryRightClickSwapWithEquipSlot(EquipSlotUI slot)
    {
        InventoryGrid grid = itemUI.CurrentGrid;
        if (slot == null || slot.IsEmpty)
            return false;

        if (!slot.CanAcceptType(itemUI.Item.itemData))
            return false;

        if (grid == null)
            return false;

        ItemUI equippedUI = slot.EquippedItemUI;
        if (equippedUI == null || equippedUI.Item == null)
            return false;

        InventoryItem equippedItem = equippedUI.Item;

        InventoryPlacementSnapshot incomingOriginal =
            InventoryPlacementSnapshot.Capture(
                grid,
                itemUI.Item);

        InventoryPlacementSnapshot outgoingPlacement =
            InventoryPlacementSnapshot.FromOriginalState(
                grid,
                equippedItem,
                itemUI.Item.x,
                itemUI.Item.y,
                equippedItem.isRotated);

        EquipmentTransactionResult result =
            equipmentTransaction.TrySwap(
                slot.SlotType,
                itemUI.Item,
                grid,
                incomingOriginal,
                grid,
                outgoingPlacement,
                true);

        if (!result.IsSuccess)
        {
            LogTransactionFailure(
                result,
                "[ItemEquipHandler] 우클릭 장비 교환 실패 후 상태 복구에 실패했습니다.");
            return false;
        }

        ApplySuccessfulEquipmentSwapVisuals(slot, equippedUI, result);

        return true;
    }

    /// <summary>
    /// 장착 아이템을 상점으로 판매하고, 실패하면 원래 장비 상태를 복구한다.
    /// true면 판매 성공 여부와 관계없이 이 전용 경로가 요청 처리를 마친 상태다.
    /// </summary>
    public bool TryHandleSellEquippedItem(ShopController shop)
    {
        if (!EnsureReady() ||
            shop == null ||
            itemUI == null ||
            !itemUI.OriginalWasEquipped)
        {
            return false;
        }

        EquipSlotUI previousSlot = itemUI.CurrentEquipSlot;

        if (previousSlot == null)
        {
            Debug.LogError(
                "[ItemEquipHandler] 판매할 장착 아이템의 기존 장비 슬롯을 찾지 못했습니다.");

            return true;
        }

        if (!shop.IsTradingToShop(itemUI.OriginalGrid, itemUI))
            return false;

        Vector2Int targetCell = itemUI.GetCellFromItemRect(shop.ShopGrid);

        EquipmentTransactionResult unequipResult =
            equipmentTransaction.TryUnequipForTransfer(
                previousSlot.SlotType,
                itemUI.Item);

        if (!unequipResult.IsSuccess)
        {
            inventoryController.PrintLog(
                EquipMessageMapper.GetMessage(
                    unequipResult.Result));

            // 드래그 시작 시 비워진 장비 슬롯 UI 복구
            SetEquipSlotVisual(previousSlot);

            return true;
        }

        // 실제 EquipmentSystem 해제가 성공했으므로 UI 상태도 해제한다.
        previousSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        bool sold = shop.TrySell(
                    itemUI,
                    targetCell.x,
                    targetCell.y);

        if (sold)
            return true;

        // 판매 실패 후 혹시 상점 Grid에 남은 아이템이 있으면 제거한다.
        if (shop.ShopGrid.ContainsItem(itemUI.Item) && !shop.ShopGrid.TryRemoveItem(itemUI.Item))
        {
            Debug.LogError(
                "[ItemEquipHandler] 판매 실패 후 상점 Grid에서 아이템을 제거하지 못했습니다.");

            // 같은 아이템을 장비와 상점 Grid에 동시에 넣지 않도록
            // 이 경우에는 장비 복구를 진행하지 않는다.
            return true;
        }

        EquipmentTransactionResult restoreResult =
            equipmentTransaction.TryRestoreEquippedItem(
                itemUI.Item,
                previousSlot.SlotType);

        if (!restoreResult.IsSuccess)
        {
            Debug.LogError(
                "[ItemEquipHandler] 판매 실패 후 원래 장비 슬롯으로 복구하지 못했습니다.");

            return true;
        }

        SetEquipSlotVisual(previousSlot);
        return true;
    }

    /// <summary>
    /// 장착 아이템을 인벤토리로 놓는 드래그를 처리한다.
    /// true면 해제·교환 또는 장비 슬롯 표시 복구까지 이 메서드가 마친 상태다.
    /// </summary>
    public bool TryHandleDropFromEquipSlotToGrid(int targetX, int targetY)
    {
        if (!EnsureReady() || !itemUI.IsEquipped)
            return false;

        EquipSlotUI previousSlot = itemUI.CurrentEquipSlot;
        if (previousSlot == null)
            return false;

        InventoryGrid grid = itemUI.CurrentGrid;

        if (!grid.CanPlaceItem(
            targetX,
            targetY,
            itemUI.Item.CurrentWidth,
            itemUI.Item.CurrentHeight))
        {
            if (TrySwapEquippedItemWithGridItem(previousSlot, grid, targetX, targetY))
                return true;

            SetEquipSlotVisual(previousSlot);
            return true;
        }
        InventoryPlacementSnapshot targetPlacement =
            InventoryPlacementSnapshot.FromOriginalState(
                grid,
                itemUI.Item,
                targetX,
                targetY,
                itemUI.Item.isRotated);

        EquipmentTransactionResult result =
            equipmentTransaction.TryUnequip(
                previousSlot.SlotType,
                grid,
                targetPlacement);

        if (!result.IsSuccess)
        {
            LogTransactionFailure(
                result,
                "[ItemEquipHandler] 드래그 해제 실패 후 장비 상태 복구에 실패했습니다.");

            if (!result.HasRecoveryFailure)
            {
                // 드래그 시작 때 사라진 슬롯 UI만 복구한다.
                SetEquipSlotVisual(previousSlot);
            }

            return true;
        }

        previousSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        ApplyResultGridPlacement(itemUI, result);

        return true;
    }

    private bool TrySwapEquippedItemWithGridItem(
        EquipSlotUI previousSlot,
        InventoryGrid grid,
        int targetX,
        int targetY)
    {
        if (previousSlot == null || grid == null)
            return false;

        InventoryItem equippedItem = itemUI.Item;

        if (!grid.TryGetItemInArea(
            targetX,
            targetY,
            equippedItem.CurrentWidth,
            equippedItem.CurrentHeight,
            out InventoryItem gridItem))
        {
            return false;
        }

        if (gridItem == null)
            return false;

        // 아이템 일부에 걸쳤을 때 스왑되는 것 방지
        if (targetX != gridItem.x || targetY != gridItem.y)
            return false;

        ItemUI gridItemUI = ItemUIFinder.FindInGrid(grid, gridItem);
        if (gridItemUI == null)
            return false;

        ItemEquipHandler gridItemEquipHandler = gridItemUI.GetComponent<ItemEquipHandler>();
        if (gridItemEquipHandler == null)
            return false;

        InventoryPlacementSnapshot incomingOriginal =
            InventoryPlacementSnapshot.Capture(
                grid,
                gridItem);

        InventoryPlacementSnapshot outgoingPlacement =
            InventoryPlacementSnapshot.FromOriginalState(
                grid,
                equippedItem,
                targetX,
                targetY,
                equippedItem.isRotated);

        EquipmentTransactionResult result =
            equipmentTransaction.TrySwap(
                previousSlot.SlotType,
                gridItem,
                grid,
                incomingOriginal,
                grid,
                outgoingPlacement,
                false);

        if (!result.IsSuccess)
        {
            LogTransactionFailure(
                result,
                "[ItemEquipHandler] 장비와 인벤토리 아이템 교환 실패 후 " +
                "상태 복구에 실패했습니다.");

            if (result.HasRecoveryFailure)
            {
                // 외부에서 장비 UI만 잘못 복구하지 않도록 처리 완료로 반환
                return true;
            }

            // 호출한 쪽에서 기존 장비 슬롯 UI를 복구하게 한다.
            return false;
        }

        gridItemEquipHandler.ApplySuccessfulEquipmentSwapVisuals(
            previousSlot,
            itemUI,
            result);

        return true;
    }

    private void ApplySuccessfulEquipmentSwapVisuals(
        EquipSlotUI slot,
        ItemUI previouslyEquippedUI,
        EquipmentTransactionResult result)
    {
        slot.ClearItemUI();
        previouslyEquippedUI.ClearCurrentEquipSlot();
        ApplyResultGridPlacement(previouslyEquippedUI, result);
        SetEquipSlotVisual(slot);
    }

    private void RestoreOriginalGridVisual()
    {
        itemUI.SetGridPosition(
            itemUI.OriginalGrid,
            itemUI.OriginalPlacement.Rect.X,
            itemUI.OriginalPlacement.Rect.Y);
    }

    private void LogTransactionFailure(
        EquipmentTransactionResult result,
        string recoveryFailureMessage)
    {
        inventoryController.PrintLog(EquipMessageMapper.GetMessage(result.Result));

        if (result.HasRecoveryFailure)
            Debug.LogError(recoveryFailureMessage);
    }

    private static void ApplyResultGridPlacement(
        ItemUI targetItemUI,
        EquipmentTransactionResult result)
    {
        targetItemUI.SetGridPosition(
            result.ResultGrid,
            result.ResultPlacement.Rect.X,
            result.ResultPlacement.Rect.Y);
    }

    private bool EnsureReady()
    {
        if (itemUI != null &&
            inventoryController != null &&
            equipmentSystem != null &&
            equipmentTransaction != null &&
            equipmentSlots != null)
        {
            return true;
        }

        if (!bindWarningLogged)
        {
            bindWarningLogged = true;
            Debug.LogError(
                "[ItemEquipHandler] InventoryController가 Bind되기 전에 장비 동작이 요청되었습니다.",
                this);
        }

        return false;
    }
}

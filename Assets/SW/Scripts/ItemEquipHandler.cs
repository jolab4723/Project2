using Unity.VisualScripting;
using UnityEngine;
public class ItemEquipHandler : MonoBehaviour
{
    [SerializeField] private ItemUI itemUI;
    private EquipmentSystem equipmentSystem;
    private EquipmentTransaction equipmentTransaction;
    private void Awake()
    {
        if (itemUI == null)
            itemUI = GetComponent<ItemUI>();

        equipmentSystem = InventoryController.Instance.EquipmentSystem;
        equipmentTransaction = new EquipmentTransaction(equipmentSystem);
    }

    public bool TryHandleRightClick()
    {
        if (itemUI.IsEquipped)
            return TryUnequip();

        return TryEquip();
    }

    private bool TryEquip()
    {
        EquipSlotUI swapSlot = null;

        InventoryGrid sourceGrid = itemUI.CurrentGrid;

        InventoryPlacementSnapshot originalPlacement =
            InventoryPlacementSnapshot.Capture(
                sourceGrid,
                itemUI.Item);

        foreach (EquipSlotUI slot in InventoryController.Instance.allEquipSlots)
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

            if (!result.RollbackSucceeded)
            {
                Debug.LogError(
                    "[ItemEquipHandler] 장착 실패 후 " +
                    "인벤토리 상태 복구에 실패했습니다.");
                return false;
            }

            if (result.EquipmentResult.Result ==
                EquipResult.SlotOccupied &&
                swapSlot == null)
            {
                swapSlot = slot;
            }
        }

        if (TryRightClickSwapWithEquipSlot(swapSlot))
            return true;

        InventoryController.Instance.PrintLog("장착할 수 있는 슬롯이 없거나 꽉 찼습니다!");
        return false;
    }

    private bool TryUnequip()
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
            InventoryController.Instance.PrintLog(
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
            InventoryController.Instance.PrintLog(
                EquipMessageMapper.GetMessage(result.EquipmentResult.Result));

            if (!result.RollbackSucceeded)
            {
                Debug.LogError(
                    "[ItemEquipHandler] 장착 해제 실패 후 " +
                    "장비 상태 복구에 실패했습니다.");
            }

            return false;
        }

        equippedSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        itemUI.SetGridPosition(
            result.ResultGrid,
            result.ResultPlacement.Rect.X,
            result.ResultPlacement.Rect.Y);

        if (rotationBeforeRequest != targetRotated)
        {
            InventoryController.Instance.PrintLog(
                "공간 확보를 위해 아이템을 회전하여 보관했습니다.");
        }

        return true;
    }
    
    public bool TryHandleDropToEquipSlot(EquipSlotUI targetSlot)
    {
        if (targetSlot == null)
            return false;

        if (itemUI.OriginalWasEquipped)
        {
            EquipSlotUI originalSlot = itemUI.CurrentEquipSlot;

            if (targetSlot == originalSlot)
            {
                SetEquipSlotVisual(originalSlot);
                return true;
            }
            SetEquipSlotVisual(originalSlot);
            return true;
        }

        if (itemUI.OriginalGrid == ShopController.Instance?.ShopGrid)
        {
            itemUI.ReturnToOriginalPosition();
            return true;
        }
        if (!targetSlot.IsEmpty)
        {
            if (TrySwapWithEquipSlot(targetSlot))
                return true;

            itemUI.ReturnToOriginalPosition();
            return true;
        }
        EquipmentSystem equipmentSystem = InventoryController.Instance.EquipmentSystem;

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
        InventoryController.Instance.PrintLog(EquipMessageMapper.GetMessage(result.EquipmentResult.Result));

        if (!result.RollbackSucceeded)
        {
            Debug.LogError(
                "[ItemEquipHandler] 드래그 장착 실패 후 " +
                "아이템 모델을 복구하지 못했습니다.");

            return true;
        }

        itemUI.SetGridPosition(
            itemUI.OriginalGrid,
            itemUI.OriginalX,
            itemUI.OriginalY);

        return true;
    }

    public void SetEquipSlotVisual(EquipSlotUI slot)
    {
        // 기존 EquipDirectly 내용

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

        if (slot.IsEmpty)
            return false;

        if (!slot.CanAcceptType(itemUI.Item.itemData))
            return false;

        InventoryGrid returnGrid = itemUI.OriginalGrid;
        InventoryItem outgoingItem = equippedUI.Item;

        InventoryPlacementSnapshot outgoingPlacement =
            InventoryPlacementSnapshot.FromOriginalState(
                returnGrid,
                outgoingItem,
                itemUI.OriginalX,
                itemUI.OriginalY,
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
            InventoryController.Instance.PrintLog(
                EquipMessageMapper.GetMessage(result.EquipmentResult.Result));

            if (result.RollbackSucceeded)
            {
                // 트랜잭션이 모델을 복구했으므로 UI만 복구한다.
                itemUI.SetGridPosition(
                    itemUI.OriginalGrid,
                    itemUI.OriginalX,
                    itemUI.OriginalY);
            }
            else
            {
                Debug.LogError(
                    "[ItemEquipHandler] 장비 교환 실패 후 " +
                    "상태 복구에 실패했습니다.");
            }

            // 교환 실패까지 처리했으므로 외부에서 다시
            // ReturnToOriginalPosition을 호출하지 않게 true를 반환한다.
            return true;
        }

        slot.ClearItemUI();
        equippedUI.ClearCurrentEquipSlot();

        equippedUI.SetGridPosition(
            result.ResultGrid,
            result.ResultPlacement.Rect.X,
            result.ResultPlacement.Rect.Y);

        SetEquipSlotVisual(slot);

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
            InventoryController.Instance.PrintLog(
                EquipMessageMapper.GetMessage(result.EquipmentResult.Result));

            if (!result.RollbackSucceeded)
            {
                Debug.LogError(
                    "[ItemEquipHandler] 우클릭 장비 교환 실패 후 " +
                    "상태 복구에 실패했습니다.");
            }

            return false;
        }

        slot.ClearItemUI();
        equippedUI.ClearCurrentEquipSlot();

        equippedUI.SetGridPosition(
            result.ResultGrid,
            result.ResultPlacement.Rect.X,
            result.ResultPlacement.Rect.Y);

        SetEquipSlotVisual(slot);

        return true;
    }

    public bool TryHandleDropFromEquipSlotToGrid(int targetX, int targetY)
    {
        if (!itemUI.IsEquipped)
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
            InventoryController.Instance.PrintLog(
                EquipMessageMapper.GetMessage(result.EquipmentResult.Result));

            if (result.RollbackSucceeded)
            {
                // 드래그 시작 때 사라진 슬롯 UI만 복구한다.
                SetEquipSlotVisual(previousSlot);
            }
            else
            {
                Debug.LogError(
                    "[ItemEquipHandler] 드래그 해제 실패 후 " +
                    "장비 상태 복구에 실패했습니다.");
            }

            return true;
        }

        previousSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        itemUI.SetGridPosition(
            result.ResultGrid,
            result.ResultPlacement.Rect.X,
            result.ResultPlacement.Rect.Y);

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
            InventoryController.Instance.PrintLog(
                EquipMessageMapper.GetMessage(result.EquipmentResult.Result));

            if (!result.RollbackSucceeded)
            {
                Debug.LogError(
                    "[ItemEquipHandler] 장비와 인벤토리 아이템 " +
                    "교환 실패 후 상태 복구에 실패했습니다.");

                // 외부에서 장비 UI만 잘못 복구하지 않도록 처리 완료로 반환
                return true;
            }

            // 호출한 쪽에서 기존 장비 슬롯 UI를 복구하게 한다.
            return false;
        }

        previousSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        itemUI.SetGridPosition(
            result.ResultGrid,
            result.ResultPlacement.Rect.X,
            result.ResultPlacement.Rect.Y);

        gridItemEquipHandler.SetEquipSlotVisual(
            previousSlot);

        return true;
    }
}
using Unity.VisualScripting;
using UnityEngine;
public class ItemEquipHandler : MonoBehaviour
{
    [SerializeField] private ItemUI itemUI;
    private EquipmentSystem equipmentSystem;

    private void Awake()
    {
        if (itemUI == null)
            itemUI = GetComponent<ItemUI>();

        equipmentSystem = InventoryController.Instance.EquipmentSystem;
    }

    public bool TryHandleRightClick()
    {
        if (itemUI.IsEquipped)
            return TryUnequip();

        return TryEquip();
    }

    private bool TryEquip()
    {
        // 기존 ItemUI.Equip() 내용 이동 예정
        EquipSlotUI swapSlot = null;

        foreach (EquipSlotUI slot in InventoryController.Instance.allEquipSlots)
        {
            if (slot == null || !slot.CanAcceptType(itemUI.Item.itemData))
                continue;

            EquipResultData result = equipmentSystem.TryEquip(itemUI.Item, slot.SlotType);
            if (result.Result == EquipResult.Success)
            {
                itemUI.CurrentGrid.RemoveItem(itemUI.Item);
                SetEquipSlotVisual(slot);
                NotifyEquipped(itemUI.Item);
                return true;
            }

            if (result.Result == EquipResult.SlotOccupied && swapSlot == null)
                swapSlot = slot;
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

        if (!itemUI.TryFindUnequipSpace(out int foundX, out int foundY))
        {
            InventoryController.Instance.PrintLog("인벤토리가 꽉 찼습니다!");
            return false;
        }

        EquipResultData result = equipmentSystem.TryUnequip(equippedSlot.SlotType);
        if (!result.IsSuccess)
        {
            InventoryController.Instance.PrintLog(GetEquipMessage(result.Result));
            return false;
        }
            

        equippedSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        InventoryGrid grid = itemUI.CurrentGrid;
        if (!grid.TryPlaceItem(itemUI.Item, foundX, foundY))
        {
            InventoryController.Instance.PrintLog(GetEquipMessage(EquipResult.Failed));
            return false;
        }
        itemUI.SetGridPosition(grid, foundX, foundY);

        NotifyUnequipped(itemUI.Item);

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

        EquipmentSystem equipmentSystem = InventoryController.Instance.EquipmentSystem;

        EquipResultData result = equipmentSystem.TryEquip(itemUI.Item, targetSlot.SlotType);
        if (result.Result == EquipResult.Success)
        {
            SetEquipSlotVisual(targetSlot);
            NotifyEquipped(itemUI.Item);
            return true;
        }
        if (result.Result != EquipResult.SlotOccupied)
        {
            InventoryController.Instance.PrintLog(GetEquipMessage(result.Result));
            itemUI.ReturnToOriginalPosition();
            return true;
        }
        if (TrySwapWithEquipSlot(targetSlot))
            return true;

        itemUI.ReturnToOriginalPosition();
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

    /// <summary>장착 성공 시 고유효과 OnEquip 호출 + 스탯 재계산.</summary>
    private void NotifyEquipped(InventoryItem item)
    {
        if (item?.itemData?.definition?.uniqueEffect != null)
            item.itemData.definition.uniqueEffect.OnEquip(item.itemData);
    }

    /// <summary>해제 성공 시 고유효과 OnUnequip 호출 + 스탯 재계산.</summary>
    private void NotifyUnequipped(InventoryItem item)
    {
        if (item?.itemData?.definition?.uniqueEffect != null)
            item.itemData.definition.uniqueEffect.OnUnequip(item.itemData);
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
        int preferredX = itemUI.OriginalX;
        int preferredY = itemUI.OriginalY;

        if (!TryFindReturnSpaceForEquippedItem(
            returnGrid,
            equippedUI.Item,
            preferredX,
            preferredY,
            out int returnX,
            out int returnY))
        {
            InventoryController.Instance.PrintLog(GetEquipMessage(EquipResult.NoReturnSpace));
            return false;
        }
        // B가 A의 원래 위치에 들어갈 수 있는지 확인
        EquipResultData result = equipmentSystem.TrySwapEquip(
        slot.SlotType,
        itemUI.Item,
        returnGrid,
        returnX,
        returnY
);

        if (result.Result != EquipResult.Swapped)
        {
            InventoryController.Instance.PrintLog(GetEquipMessage(result.Result));
            return false;
        }

        InventoryItem outgoingItem = result.PreviousItem;

        // B를 인벤토리로 내림
        slot.ClearItemUI();
        equippedUI.ClearCurrentEquipSlot();

        returnGrid.TryPlaceItem(outgoingItem, returnX, returnY);
        equippedUI.SetGridPosition(returnGrid, returnX, returnY);
        NotifyUnequipped(outgoingItem);

        // A를 장비칸에 장착
        SetEquipSlotVisual(slot);
        NotifyEquipped(itemUI.Item);
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
        int targetX = itemUI.Item.x;
        int targetY = itemUI.Item.y;


        grid.RemoveItem(itemUI.Item);

        if (!TryFindReturnSpaceForEquippedItem(
            grid,
            equippedItem,
            targetX,
            targetY,
            out int returnX,
            out int returnY))
        {
            grid.TryPlaceItem(itemUI.Item, targetX, targetY);
            InventoryController.Instance.PrintLog(GetEquipMessage(EquipResult.NoReturnSpace));
            return false;
        }
        EquipResultData result = equipmentSystem.TrySwapEquip(
            slot.SlotType,
            itemUI.Item,
            grid,
            returnX,
            returnY
        );

        if (result.Result != EquipResult.Swapped)
        {
            grid.TryPlaceItem(itemUI.Item, targetX, targetY);
            InventoryController.Instance.PrintLog(GetEquipMessage(result.Result));
            return false;
        }

        InventoryItem outgoingItem = result.PreviousItem;

        slot.ClearItemUI();
        equippedUI.ClearCurrentEquipSlot();

        grid.TryPlaceItem(outgoingItem, returnX, returnY);
        equippedUI.SetGridPosition(grid, returnX, returnY);
        NotifyUnequipped(outgoingItem);

        SetEquipSlotVisual(slot);
        NotifyEquipped(itemUI.Item);
        return true;
    }

    private bool TryFindReturnSpaceForEquippedItem(
    InventoryGrid grid,
    InventoryItem outgoingItem,
    int preferredX,
    int preferredY,
    out int returnX,
    out int returnY)
    {
        returnX = preferredX;
        returnY = preferredY;

        if (grid.CanPlaceItem(
            preferredX,
            preferredY,
            outgoingItem.CurrentWidth,
            outgoingItem.CurrentHeight))
        {
            return true;
        }

        if (grid.FindEmptySpace(
            outgoingItem.CurrentWidth,
            outgoingItem.CurrentHeight,
            out returnX,
            out returnY))
        {
            return true;
        }

        return false;
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
        EquipResultData result = equipmentSystem.TryUnequip(previousSlot.SlotType);
        if (!result.IsSuccess)
        {
            InventoryController.Instance.PrintLog(GetEquipMessage(result.Result));
            SetEquipSlotVisual(previousSlot);
            return true;
        }

        previousSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        if (!grid.TryPlaceItem(itemUI.Item, targetX, targetY))
        {
            InventoryController.Instance.PrintLog(GetEquipMessage(EquipResult.Failed));
            return false;
        }
        itemUI.SetGridPosition(grid, targetX, targetY);
        NotifyUnequipped(itemUI.Item);

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

        int gridItemOriginalX = gridItem.x;
        int gridItemOriginalY = gridItem.y;

        // 먼저 인벤토리 아이템을 비워야 기존 장비가 그 자리에 들어갈 수 있는지 검사 가능
        grid.RemoveItem(gridItem);

        EquipResultData result = equipmentSystem.TrySwapEquip(
            previousSlot.SlotType,
            gridItem,
            grid,
            targetX,
            targetY
        );

        if (result.Result != EquipResult.Swapped)
        {
            grid.TryPlaceItem(gridItem, gridItemOriginalX, gridItemOriginalY);
            InventoryController.Instance.PrintLog(GetEquipMessage(result.Result));
            return false;
        }

        InventoryItem outgoingItem = result.PreviousItem;

        previousSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        grid.TryPlaceItem(outgoingItem, targetX, targetY);
        itemUI.SetGridPosition(grid, targetX, targetY);
        NotifyUnequipped(outgoingItem);

        gridItemEquipHandler.SetEquipSlotVisual(previousSlot);
        gridItemEquipHandler.NotifyEquipped(gridItem);

        return true;
    }
    private string GetEquipMessage(EquipResult result)
    {
        switch (result)
        {
            case EquipResult.InvalidItem:
                return "장착할 수 없는 아이템입니다.";
            case EquipResult.InvalidSlot:
                return "해당 슬롯에 장착할 수 없습니다.";
            case EquipResult.SlotOccupied:
                return "이미 장비가 장착되어 있습니다.";
            case EquipResult.NotEquipped:
                return "해제할 장비가 없습니다.";
            case EquipResult.NoReturnSpace:
                return "기존 장비를 인벤토리에 내려놓을 공간이 없습니다.";
            default:
                return "장비 처리가 실패했습니다.";
        }
    }
}
using UnityEngine;
using static PLAYERTWO.ARPGProject.InventorySerializer;

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

            if (InventoryController.Instance.EquipmentSystem.TryEquip(slot.SlotType, itemUI.Item))
            {
                itemUI.CurrentGrid.RemoveItem(itemUI.Item);
                SetEquipSlotVisual(slot);
                return true;
            }

            if (swapSlot == null)
                swapSlot = slot;
        }

        if (TryRightClickSwapWithEquipSlot(swapSlot))
            return true;

        InventoryController.Instance.PrintLog("장착할 수 있는 슬롯이 없거나 꽉 찼습니다!");
        return false;
    }

    private bool TryUnequip()
    {
        // 기존 ItemUI.UnEquip() 내용 이동 예정
        EquipSlotUI equippedSlot = itemUI.CurrentEquipSlot;
        if (equippedSlot == null) return false;

        if (!itemUI.TryFindUnequipSpace(out int foundX, out int foundY))
        {
            InventoryController.Instance.PrintLog("인벤토리가 꽉 찼습니다!");
            return false;
        }

        if (!equipmentSystem.TryUnequip(equippedSlot.SlotType, itemUI.Item))
            return false;

        equippedSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        InventoryGrid grid = itemUI.CurrentGrid;
        grid.PlaceItem(itemUI.Item, foundX, foundY);
        itemUI.SetGridPosition(grid, foundX, foundY);

        NotifyUnequipped(itemUI.Item);

        return true;
    }
    
    public bool TryHandleDropToEquipSlot(EquipSlotUI targetSlot)
    {
        if (targetSlot == null)
            return false;

        if (itemUI.OriginalGrid == ShopController.Instance?.ShopGrid)
        {
            itemUI.ReturnToOriginalPosition();
            return true;
        }

        EquipmentSystem equipmentSystem = InventoryController.Instance.EquipmentSystem;

        if (equipmentSystem.TryEquip(targetSlot.SlotType, itemUI.Item))
        {
            SetEquipSlotVisual(targetSlot);
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

        NotifyEquipped(itemUI.Item);
    }

    /// <summary>장착 성공 시 고유효과 OnEquip 호출 + 스탯 재계산.</summary>
    private void NotifyEquipped(InventoryItem item)
    {
        if (item?.itemData?.definition?.uniqueEffect != null)
            item.itemData.definition.uniqueEffect.OnEquip(item.itemData);

        if (PlayerStatManager.Instance != null)
            PlayerStatManager.Instance.Recalculate();
    }

    /// <summary>해제 성공 시 고유효과 OnUnequip 호출 + 스탯 재계산.</summary>
    private void NotifyUnequipped(InventoryItem item)
    {
        if (item?.itemData?.definition?.uniqueEffect != null)
            item.itemData.definition.uniqueEffect.OnUnequip(item.itemData);

        if (PlayerStatManager.Instance != null)
            PlayerStatManager.Instance.Recalculate();
    }

    private bool TrySwapWithEquipSlot(EquipSlotUI slot)
    {
        // 기존 TrySwapWithEquipSlot 내용
        // 단 itemUI.Item, itemUI.OriginalGrid, itemUI.OriginalX 같은 식으로 접근
        if (slot == null)
            return false;

        ItemUI equippedUI = slot.EquippedItemUI;
        if (equippedUI == null)
            return false;

        if (slot.IsEmpty)
            return false;

        if (!slot.CanAcceptType(itemUI.Item.itemData))
            return false;

        // B가 A의 원래 위치에 들어갈 수 있는지 확인
        if (!equipmentSystem.TrySwapEquip(
            slot.SlotType,
            itemUI.Item,
            itemUI.OriginalGrid,
            itemUI.OriginalX,
            itemUI.OriginalY,
            out InventoryItem outgoingItem))
        {
            return false;
        }

        // B를 인벤토리로 내림
        slot.ClearItemUI();
        equippedUI.ClearCurrentEquipSlot();

        itemUI.OriginalGrid.PlaceItem(outgoingItem, itemUI.OriginalX, itemUI.OriginalY);
        equippedUI.SetGridPosition(itemUI.OriginalGrid, itemUI.OriginalX, itemUI.OriginalY);
        NotifyUnequipped(outgoingItem);

        // A를 장비칸에 장착
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
        int targetX = itemUI.Item.x;
        int targetY = itemUI.Item.y;


        grid.RemoveItem(itemUI.Item);
        if (!equipmentSystem.TrySwapEquip(
            slot.SlotType,
            itemUI.Item,
            grid,
            targetX,
            targetY,
            out InventoryItem outgoingItem))
        {
            grid.PlaceItem(itemUI.Item, targetX, targetY);
            return false;
        }

        slot.ClearItemUI();
        equippedUI.ClearCurrentEquipSlot();

        grid.PlaceItem(outgoingItem, targetX, targetY);
        equippedUI.SetGridPosition(grid, targetX, targetY);
        NotifyUnequipped(outgoingItem);

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
            SetEquipSlotVisual(previousSlot);
            return true;
        }

        if (!equipmentSystem.TryUnequip(previousSlot.SlotType, itemUI.Item))
        {
            SetEquipSlotVisual(previousSlot);
            return true;
        }

        previousSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        grid.PlaceItem(itemUI.Item, targetX, targetY);
        itemUI.SetGridPosition(grid, targetX, targetY);
        NotifyUnequipped(itemUI.Item);

        return true;
    }
}
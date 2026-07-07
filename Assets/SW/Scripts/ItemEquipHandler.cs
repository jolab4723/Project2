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
        grid.PlaceItem(itemUI.Item, foundX, foundY);
        itemUI.SetGridPosition(grid, foundX, foundY);

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

        EquipResultData result = equipmentSystem.TryEquip(itemUI.Item, targetSlot.SlotType);
        if (result.Result == EquipResult.Success)
        {
            SetEquipSlotVisual(targetSlot);
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
        EquipResultData result = equipmentSystem.TrySwapEquip(
        slot.SlotType,
        itemUI.Item,
        itemUI.OriginalGrid,
        itemUI.OriginalX,
        itemUI.OriginalY
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

        itemUI.OriginalGrid.PlaceItem(outgoingItem, itemUI.OriginalX, itemUI.OriginalY);
        equippedUI.SetGridPosition(itemUI.OriginalGrid, itemUI.OriginalX, itemUI.OriginalY);

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

        EquipResultData result = equipmentSystem.TrySwapEquip(
            slot.SlotType,
            itemUI.Item,
            grid,
            targetX,
            targetY
        );

        if (result.Result != EquipResult.Swapped)
        {
            grid.PlaceItem(itemUI.Item, targetX, targetY);
            InventoryController.Instance.PrintLog(GetEquipMessage(result.Result));
            return false;
        }

        InventoryItem outgoingItem = result.PreviousItem;

        slot.ClearItemUI();
        equippedUI.ClearCurrentEquipSlot();

        grid.PlaceItem(outgoingItem, targetX, targetY);
        equippedUI.SetGridPosition(grid, targetX, targetY);

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
        EquipResultData result = equipmentSystem.TryUnequip(previousSlot.SlotType);
        if (!result.IsSuccess)
        {
            InventoryController.Instance.PrintLog(GetEquipMessage(result.Result));
            SetEquipSlotVisual(previousSlot);
            return true;
        }

        previousSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        grid.PlaceItem(itemUI.Item, targetX, targetY);
        itemUI.SetGridPosition(grid, targetX, targetY);

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
            grid.PlaceItem(gridItem, gridItemOriginalX, gridItemOriginalY);
            InventoryController.Instance.PrintLog(GetEquipMessage(result.Result));
            return false;
        }

        InventoryItem outgoingItem = result.PreviousItem;

        previousSlot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();

        grid.PlaceItem(outgoingItem, targetX, targetY);
        itemUI.SetGridPosition(grid, targetX, targetY);

        gridItemEquipHandler.SetEquipSlotVisual(previousSlot);

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
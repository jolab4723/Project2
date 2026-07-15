using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public class EquipSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    [SerializeField] private EquipSlotType slotType;
    public EquipSlotType SlotType => slotType;

    [HideInInspector]
    private ItemUI equipItemUI;
    public ItemUI EquippedItemUI => equipItemUI;
    public bool IsEmpty => equipItemUI == null;


    public bool CanAccept(ItemInstance data)
    {
        return IsEmpty && CanAcceptType(data);
    }

    public bool CanAcceptType(ItemInstance data)
    {
        if (data == null || data.definition == null)
            return false;

        return EquipSlotRules.CanEquipTo(data.definition, slotType);
    }

    public bool CanSwap(ItemUI incomingItem)
    {
        return !IsEmpty && CanAcceptType(incomingItem.Item.itemData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        InventoryController.Instance.hoveredEquipSlot = this;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (InventoryController.Instance.hoveredEquipSlot == this)
            InventoryController.Instance.hoveredEquipSlot = null;
    }

    public void SetItemUI(ItemUI itemUI)
    {
        equipItemUI = itemUI;
    }

    public void ClearItemUI()
    {
        equipItemUI = null;
    }
}

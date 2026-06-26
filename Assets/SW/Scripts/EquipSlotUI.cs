using UnityEngine;
using UnityEngine.EventSystems;

public class EquipSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public EquipItem allowedType;

    [HideInInspector]
    public ItemUI equipItemUI;

    public bool IsEmpty => equipItemUI == null;

    public bool CanAccept(ItemData data)
    {
        return IsEmpty && data.equipItem == allowedType;
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
}

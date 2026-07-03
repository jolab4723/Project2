using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public class EquipSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ArmorType allowedType;

    [HideInInspector]
    public ItemUI equipItemUI;

    public bool IsEmpty => equipItemUI == null;

    public bool CanAccept(ItemInstance data)
    {
        // 무기 부분 별도 관리
        return IsEmpty && data.definition.armorType == allowedType;
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

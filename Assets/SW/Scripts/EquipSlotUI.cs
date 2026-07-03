using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public class EquipSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    [SerializeField] private EquipSlotType slotType;
    public EquipSlotType SlotType => slotType;
    [Tooltip("이 슬롯이 받는 아이템 카테고리. Armor면 아래 allowedType으로 부위까지 추가 검증한다.")]
    public ItemCategory requiredCategory = ItemCategory.Armor;

    [Tooltip("requiredCategory가 Armor일 때만 사용 (투구/갑옷/부츠 구분)")]
    public ArmorType allowedType;

    [HideInInspector]
    public ItemUI equipItemUI;

    public bool IsEmpty => equipItemUI == null;


    public bool CanAccept(ItemInstance data)
    {
        return IsEmpty && CanAcceptType(data);
    }

    public bool CanAcceptType(ItemInstance data)
    {
        if (data == null || data.definition == null)
            return false;

        return data.definition.allowedEquipSlots.Contains(slotType);

        //if (data.definition.category != requiredCategory)
        //    return false;

        //if (requiredCategory == ItemCategory.Armor)
        //    return data.definition.armorType == allowedType;

        //return true;
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
}

using ItemSystem;
using UnityEngine;

public class EquipSlotUI : MonoBehaviour
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

    public void SetItemUI(ItemUI itemUI)
    {
        equipItemUI = itemUI;
    }

    public void ClearItemUI()
    {
        equipItemUI = null;
    }
}

using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

public class EquipmentSystem : MonoBehaviour
{
    public event System.Action<EquippedItemInfo[]> OnEquipmentChanged;

    private Dictionary<EquipSlotType, InventoryItem> equippedItems = new();

    public void Equip(EquipSlotType slotType, InventoryItem item)
    {
        equippedItems[slotType] = item;
        EquipmentChanged();
    }

    public void Unequip(EquipSlotType slotType, InventoryItem item)
    {
        equippedItems.Remove(slotType);
        EquipmentChanged();
    }
    public bool TryGetEquippedItem(EquipSlotType slotType, out InventoryItem item)
    {
        return equippedItems.TryGetValue(slotType, out item);
    }
    private void EquipmentChanged()
    {
        EquippedItemInfo[] infos = new EquippedItemInfo[equippedItems.Count];

        int index = 0;
        foreach (var item in equippedItems)
        {
            infos[index] = new EquippedItemInfo(item.Key, item.Value);
            index++;
        }


        OnEquipmentChanged?.Invoke(infos);
    }
}
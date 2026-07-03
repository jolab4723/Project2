using System.Collections.Generic;
using UnityEngine;

public class EquipmentSystem : MonoBehaviour
{
    public event System.Action<EquippedItemInfo[]> OnEquipmentChanged;

    private List<InventoryItem> equippedItems = new List<InventoryItem>();

    public void Equip(InventoryItem item)
    {
        equippedItems.Add(item);
        EquipmentChanged();
    }

    public void Unequip(InventoryItem item)
    {
        equippedItems.Remove(item);
        EquipmentChanged();
    }

    private void EquipmentChanged()
    {
        EquippedItemInfo[] infos = new EquippedItemInfo[equippedItems.Count];

        for (int i = 0; i < equippedItems.Count; i++)
        {
            InventoryItem item = equippedItems[i];

            //infos[i] = new EquippedItemInfo(
            //    item.itemData.itemID,
            //    item.upgradeLevel
            //);
        }

        OnEquipmentChanged?.Invoke(infos);
    }
}
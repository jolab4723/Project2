using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

public class EquipmentSystem : MonoBehaviour
{
    public event System.Action<EquippedItemInfo[]> OnEquipmentChanged;

    private Dictionary<EquipSlotType, InventoryItem> equippedItems = new();

    public IEnumerable<KeyValuePair<EquipSlotType, InventoryItem>> GetEquippedItems()
    {
        return equippedItems;
    }

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

    public bool IsSlotEmpty(EquipSlotType slotType)
    {
        return !equippedItems.ContainsKey(slotType);
    }

    public bool CanEquip(InventoryItem item, EquipSlotType slotType)
    {
        if (item == null || item.itemData == null || item.itemData.definition == null)
            return false;

        if (!IsSlotEmpty(slotType))
            return false;

        return EquipSlotRules.CanEquipTo(item.itemData.definition, slotType);
    }

    public bool TryEquip(EquipSlotType slotType, InventoryItem item)
    {
        if (!CanEquip(item, slotType))
            return false;

        equippedItems[slotType] = item;
        EquipmentChanged();
        return true;
    }

    public bool CanSwapEquip(
    EquipSlotType slotType,
    InventoryItem incomingItem,
    InventoryGrid returnGrid,
    int returnX,
    int returnY)
    {
        if (incomingItem == null || incomingItem.itemData == null)
            return false;

        if (!equippedItems.TryGetValue(slotType, out InventoryItem equippedItem))
            return false;

        if (!EquipSlotRules.CanEquipTo(incomingItem.itemData.definition, slotType))
            return false;

        if (returnGrid == null)
            return false;

        return returnGrid.CanPlaceItem(
            returnX,
            returnY,
            equippedItem.CurrentWidth,
            equippedItem.CurrentHeight
        );
    }

    public bool TrySwapEquip(
    EquipSlotType slotType,
    InventoryItem incomingItem,
    InventoryGrid returnGrid,
    int returnX,
    int returnY,
    out InventoryItem outgoingItem)
    {
        outgoingItem = null;

        if (!CanSwapEquip(slotType, incomingItem, returnGrid, returnX, returnY))
            return false;

        outgoingItem = equippedItems[slotType];
        equippedItems[slotType] = incomingItem;

        EquipmentChanged();
        return true;
    }

    public bool TryUnequip(EquipSlotType slotType, InventoryItem item)
    {
        if (item == null)
            return false;

        if (!equippedItems.TryGetValue(slotType, out InventoryItem equippedItem))
            return false;

        if (equippedItem != item)
            return false;

        equippedItems.Remove(slotType);
        EquipmentChanged();
        return true;
    }
}
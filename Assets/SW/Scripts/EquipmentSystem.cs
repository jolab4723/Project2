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

    public EquipResultData TryEquip(InventoryItem item, EquipSlotType slotType)
    {
        EquipResult validation = ValidateEquip(item, slotType);
        if (validation != EquipResult.Success)
            return EquipResultData.Failed(validation, slotType, item);

        if (equippedItems.TryGetValue(slotType, out InventoryItem currentItem) && currentItem != null)
            return EquipResultData.Failed(EquipResult.SlotOccupied, slotType, item);

        equippedItems[slotType] = item;
        item.isEquipped = true;

        EquipmentChanged();

        return EquipResultData.Success(slotType, item);
    }

    private EquipResult ValidateEquip(InventoryItem item, EquipSlotType slotType)
    {
        if (item == null || item.itemData == null || item.itemData.definition == null)
            return EquipResult.InvalidItem;

        if (!EquipSlotRules.CanEquipTo(item.itemData.definition, slotType))
            return EquipResult.InvalidSlot;

        return EquipResult.Success;
    }

    private EquipResult ValidateSwapEquip(
    EquipSlotType slotType,
    InventoryItem incomingItem,
    InventoryGrid returnGrid,
    int returnX,
    int returnY)
    {
        EquipResult validation = ValidateEquip(incomingItem, slotType);
        if (validation != EquipResult.Success)
            return validation;

        if (!equippedItems.TryGetValue(slotType, out InventoryItem outgoingItem) || outgoingItem == null)
            return EquipResult.NotEquipped;

        if (returnGrid == null)
            return EquipResult.NoReturnSpace;

        if (!returnGrid.CanPlaceItem(
            returnX,
            returnY,
            outgoingItem.CurrentWidth,
            outgoingItem.CurrentHeight))
        {
            return EquipResult.NoReturnSpace;
        }

        return EquipResult.Success;
    }

    public EquipResultData TrySwapEquip(
        EquipSlotType slotType,
        InventoryItem incomingItem,
        InventoryGrid returnGrid,
        int returnX,
        int returnY)
    {
        EquipResult validation = ValidateSwapEquip(
            slotType,
            incomingItem,
            returnGrid,
            returnX,
            returnY);

        if (validation != EquipResult.Success)
            return EquipResultData.Failed(validation, slotType, incomingItem);

        InventoryItem outgoingItem = equippedItems[slotType];

        equippedItems[slotType] = incomingItem;

        incomingItem.isEquipped = true;
        outgoingItem.isEquipped = false;

        EquipmentChanged();

        return EquipResultData.Swapped(slotType, incomingItem, outgoingItem);
    }

    public EquipResultData TryUnequip(EquipSlotType slotType)
    {
        if (!equippedItems.TryGetValue(slotType, out InventoryItem item) || item == null)
            return EquipResultData.Failed(EquipResult.NotEquipped, slotType);

        equippedItems.Remove(slotType);
        item.isEquipped = false;

        EquipmentChanged();

        return EquipResultData.Success(slotType, item);
    }
}
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

    public EquipResultData TryReplaceEquip(InventoryItem newItem, EquipSlotType slotType)
    {
        EquipResult validation = ValidateEquip(newItem, slotType);
        if (validation != EquipResult.Success)
            return EquipResultData.Failed(validation, slotType, newItem);

        equippedItems.TryGetValue(slotType, out InventoryItem previousItem);

        equippedItems[slotType] = newItem;
        newItem.isEquipped = true;

        if (previousItem != null)
            previousItem.isEquipped = false;

        EquipmentChanged();

        if (previousItem != null)
            return EquipResultData.Swapped(slotType, newItem, previousItem);

        return EquipResultData.Success(slotType, newItem);
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
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

public class EquipmentSystem : MonoBehaviour
{
    public event System.Action<EquippedItemInfo[]> OnEquipmentChanged;

    private Dictionary<EquipSlotType, InventoryItem> equippedItems = new();

    internal EquipResultData TryEquipState(
    InventoryItem item,
    EquipSlotType slotType)
    {
        EquipResult validation = ValidateEquip(item, slotType);

        if (validation != EquipResult.Success)
            return EquipResultData.Failed(validation, slotType, item);

        if (equippedItems.TryGetValue(
                slotType,
                out InventoryItem currentItem) &&
            currentItem != null)
        {
            return EquipResultData.Failed(
                EquipResult.SlotOccupied,
                slotType,
                item);
        }

        // 인벤토리에서 회전된 아이템도 장비 슬롯 안에서는 항상 정방향 상태를 가진다.
        item.isRotated = false;
        equippedItems[slotType] = item;
        item.isEquipped = true;

        // 여기서는 EquipmentChanged를 호출하지 않는다.
        return EquipResultData.Success(slotType, item);
    }
    

    internal EquipResultData TryUnequipState(
        EquipSlotType slotType)
    {

        if (!equippedItems.TryGetValue(
            slotType,
            out InventoryItem item) ||
        item == null)
        {
            return EquipResultData.Failed(
                EquipResult.NotEquipped,
                slotType);
        }

        equippedItems.Remove(slotType);
        item.isEquipped = false;

        // 여기서는 EquipmentChanged를 호출하지 않는다.
        return EquipResultData.Success(slotType, item);
    }

    internal EquipResultData TrySwapState(
        EquipSlotType slotType,
        InventoryItem incomingItem)
    {
        EquipResult validation =
        ValidateEquip(incomingItem, slotType);

        if (validation != EquipResult.Success)
        {
            return EquipResultData.Failed(
                validation,
                slotType,
                incomingItem);
        }

        if (!equippedItems.TryGetValue(
                slotType,
                out InventoryItem outgoingItem) ||
            outgoingItem == null)
        {
            return EquipResultData.Failed(
                EquipResult.NotEquipped,
                slotType,
                incomingItem);
        }

        if (outgoingItem == incomingItem)
        {
            return EquipResultData.Failed(
                EquipResult.Failed,
                slotType,
                incomingItem);
        }

        // 교체로 들어오는 아이템 역시 장비 상태에서는 회전값을 유지하지 않는다.
        incomingItem.isRotated = false;
        equippedItems[slotType] = incomingItem;

        outgoingItem.isEquipped = false;
        incomingItem.isEquipped = true;

        // 여기서는 EquipmentChanged를 호출하지 않는다.
        return EquipResultData.Swapped(
            slotType,
            incomingItem,
            outgoingItem);

    }

    internal void PublishChanged()
    {
        EquipmentChanged();
    }
    public IEnumerable<KeyValuePair<EquipSlotType, InventoryItem>> GetEquippedItems()
    {
        return equippedItems;
    }

    public bool TryGetEquippedItem(EquipSlotType slotType, out InventoryItem item)
    {
        return equippedItems.TryGetValue(slotType, out item);
    }

    public bool TryGetEquippedItemInstance(
    EquipSlotType slotType,
    out ItemInstance itemInstance)
    {
        itemInstance = null;

        if (!equippedItems.TryGetValue(slotType, out InventoryItem inventoryItem))
            return false;

        if (inventoryItem?.itemData?.definition == null)
            return false;

        itemInstance = inventoryItem.itemData;
        return true;
    }

    public bool TryGetEquippedWeaponType(out WeaponType weaponType)
    {
        weaponType = default;

        if (!TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance weapon))
            return false;

        weaponType = weapon.definition.weaponType;
        return true;
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

    public bool NotifyEquippedItemChanged(ItemInstance changedItem)
    {
        if (changedItem == null)
            return false;

        foreach (InventoryItem equippedItem
                 in equippedItems.Values)
        {
            if (ReferenceEquals(
                    equippedItem?.itemData,
                    changedItem))
            {
                PublishChanged();
                return true;
            }
        }
        return false;
    }

    private EquipResult ValidateEquip(InventoryItem item, EquipSlotType slotType)
    {
        if (item == null || item.itemData == null || item.itemData.definition == null)
            return EquipResult.InvalidItem;

        if (!EquipSlotRules.CanEquipTo(item.itemData.definition, slotType))
            return EquipResult.InvalidSlot;

        return EquipResult.Success;
    }
}

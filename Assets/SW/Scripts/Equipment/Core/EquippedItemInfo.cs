using ItemSystem;

[System.Serializable]
public struct EquippedItemInfo
{
    public EquipSlotType slotType;
    public string instanceId;
    public string itemId;
    public int upgradeLevel;

    public EquippedItemInfo(EquipSlotType slotType, InventoryItem item)
    {
        this.slotType = slotType;
        instanceId = item.itemData.instanceId;
        itemId = item.itemData.definition.itemId;
        upgradeLevel = item.itemData.upgradeLevel;
    }
}
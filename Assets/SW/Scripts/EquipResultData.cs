using ItemSystem;

public class EquipResultData
{
    public EquipResult Result { get; private set; }
    public EquipSlotType SlotType { get; private set; }

    public InventoryItem EquippedItem { get; private set; }
    public InventoryItem PreviousItem { get; private set; }

    public bool IsSuccess =>
        Result == EquipResult.Success ||
        Result == EquipResult.Swapped;

    private EquipResultData(
        EquipResult result,
        EquipSlotType slotType,
        InventoryItem equippedItem,
        InventoryItem previousItem)
    {
        Result = result;
        SlotType = slotType;
        EquippedItem = equippedItem;
        PreviousItem = previousItem;
    }

    public static EquipResultData Success(EquipSlotType slotType, InventoryItem item)
    {
        return new EquipResultData(EquipResult.Success, slotType, item, null);
    }

    public static EquipResultData Swapped(EquipSlotType slotType, InventoryItem newItem, InventoryItem previousItem)
    {
        return new EquipResultData(EquipResult.Swapped, slotType, newItem, previousItem);
    }

    public static EquipResultData Failed(EquipResult result, EquipSlotType slotType, InventoryItem item = null)
    {
        return new EquipResultData(result, slotType, item, null);
    }
}
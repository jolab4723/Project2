using ItemSystem;

public static class EquipSlotRules
{
    public static bool CanEquipTo(ItemDefinitionSO def, EquipSlotType slotType)
    {
        if (def == null)
            return false;

        switch (def.category)
        {
            case ItemCategory.Weapon:
                return slotType == EquipSlotType.Weapon;

            case ItemCategory.Armor:
                return ArmorToSlot(def.armorType) == slotType;

            case ItemCategory.Potion:
                return slotType == EquipSlotType.Potion;

            default:
                return false;
        }
    }

    public static EquipSlotType ArmorToSlot(ArmorType armorType)
    {
        switch (armorType)
        {
            case ArmorType.Helmet:
                return EquipSlotType.Helmet;

            case ArmorType.Armor:
                return EquipSlotType.Chest;

            case ArmorType.Boots:
                return EquipSlotType.Boots;

            default:
                return EquipSlotType.None;
        }
    }
}
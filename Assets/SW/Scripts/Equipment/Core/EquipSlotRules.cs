using ItemSystem;

public static class EquipSlotRules
{
    /// <summary>
    /// 아이템을 지정한 장비 슬롯에 장착할 수 있는지 확인한다.
    /// </summary>
    public static bool CanEquipTo(
        ItemDefinitionSO definition,
        EquipSlotType targetSlot)
    {
        if (!TryGetEquipSlot(
                definition,
                out EquipSlotType itemSlot))
        {
            return false;
        }

        return itemSlot == targetSlot;
    }

    /// <summary>
    /// 아이템이 장착되는 실제 슬롯을 반환한다.
    ///
    /// 무기  -> Weapon
    /// 헬멧  -> Helmet
    /// 상의  -> Chest
    /// 부츠  -> Boots
    /// 포션  -> Potion
    /// 유물  -> 장착 슬롯 없음
    /// </summary>
    public static bool TryGetEquipSlot(
        ItemDefinitionSO definition,
        out EquipSlotType slotType)
    {
        slotType = EquipSlotType.None;

        if (definition == null)
            return false;

        switch (definition.category)
        {
            case ItemCategory.Weapon:
                slotType = EquipSlotType.Weapon;
                return true;

            case ItemCategory.Armor:
                slotType = ArmorToSlot(definition.armorType);
                return slotType != EquipSlotType.None;

            case ItemCategory.Potion:
                slotType = EquipSlotType.Potion;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// 스탯 비교 기능에서 사용할 슬롯을 반환한다.
    ///
    /// 현재 비교 범위는 무기와 방어구뿐이다.
    /// 포션과 유물은 기존 단일 툴팁만 표시한다.
    /// </summary>
    public static bool TryGetComparisonSlot(
        ItemDefinitionSO definition,
        out EquipSlotType slotType)
    {
        slotType = EquipSlotType.None;

        if (definition == null)
            return false;

        if (definition.category != ItemCategory.Weapon &&
            definition.category != ItemCategory.Armor)
        {
            return false;
        }

        return TryGetEquipSlot(
            definition,
            out slotType);
    }

    public static EquipSlotType ArmorToSlot(
        ArmorType armorType)
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
using System.Collections.Generic;

namespace ItemSystem
{
    /// <summary>
    /// UI 표시용 한글 이름/색상 매핑. 인덱스 기반 배열 대신 Dictionary를 써서
    /// enum 순서가 바뀌거나 항목이 늘어나도 매핑이 깨지지 않게 함.
    /// 새 UI 스크립트에서 한글 이름이 필요하면 배열을 새로 만들지 말고 여기를 참조할 것.
    /// </summary>
    public static class ItemDisplayNames
    {
        public static readonly Dictionary<ItemRarity, string> GradeNames = new Dictionary<ItemRarity, string>
        {
            { ItemRarity.Common, "일반" },
            { ItemRarity.Advanced, "고급" },
            { ItemRarity.Rare, "희귀" },
            { ItemRarity.Unique, "유일" },
            { ItemRarity.Legendary, "전설" },
        };

        public static readonly Dictionary<ItemRarity, string> GradeColorHex = new Dictionary<ItemRarity, string>
        {
            { ItemRarity.Common, "#FFFFFF" },
            { ItemRarity.Advanced, "#82D17D" },
            { ItemRarity.Rare, "#4D9CE4" },
            { ItemRarity.Unique, "#C27BA0" },
            { ItemRarity.Legendary, "#FFD175" },
        };

        public static readonly Dictionary<ElementType, string> ElementNames = new Dictionary<ElementType, string>
        {
            { ElementType.None, "무속성" },
            { ElementType.Fire, "불" },
            { ElementType.Ice, "얼음" },
            { ElementType.Electric, "번개" },
        };

        public static readonly Dictionary<ElementType, string> ElementColorHex = new Dictionary<ElementType, string>
        {
            { ElementType.None, "#A8A8A8" },
            { ElementType.Fire, "#E06666" },
            { ElementType.Ice, "#6D9EEB" },
            { ElementType.Electric, "#FFD966" },
        };

        public static readonly Dictionary<ItemCategory, string> CategoryNames = new Dictionary<ItemCategory, string>
        {
            { ItemCategory.Weapon, "무기" },
            { ItemCategory.Armor, "방어구" },
            { ItemCategory.Relic, "유물" },
            { ItemCategory.Potion, "포션" },
        };

        public static readonly Dictionary<CharacterClass, string> ClassNames = new Dictionary<CharacterClass, string>
        {
            { CharacterClass.Fighter, "파이터" },
            { CharacterClass.Gunner, "거너" },
        };

        public static readonly Dictionary<WeaponType, string> WeaponNames = new Dictionary<WeaponType, string>
        {
            { WeaponType.Greatsword, "대검" },
            { WeaponType.Blunt, "둔기" },
            { WeaponType.Axe, "도끼" },
            { WeaponType.GrenadeLauncher, "유탄발사기" },
            { WeaponType.Shotgun, "샷건" },
            { WeaponType.Rifle, "소총" },
        };

        public static readonly Dictionary<ArmorType, string> ArmorNames = new Dictionary<ArmorType, string>
        {
            { ArmorType.Helmet, "헬멧" },
            { ArmorType.Armor, "갑옷" },
            { ArmorType.Boots, "신발" },
        };

        // 마스터 변수 시트 기준 한글명
        public static readonly Dictionary<StatType, string> StatNames = new Dictionary<StatType, string>
        {
            { StatType.healthFlat, "체력" },
            { StatType.healthPercent, "체력%" },
            { StatType.attackPowerFlat, "공격력" },
            { StatType.attackPowerPercent, "공격력%" },
            { StatType.defensePowerFlat, "방어력" },
            { StatType.defensePowerPercent, "방어력%" },
            { StatType.moveSpeedFlat, "이동속도" },
            { StatType.moveSpeedPercent, "이동속도%" },
            { StatType.attackSpeedFlat, "공격속도" },
            { StatType.attackSpeedPercent, "공격속도%" },
            { StatType.critRateFlat, "크리티컬 확률" },
            { StatType.critMultFlat, "크리티컬 배율" },
            { StatType.cdrFlat, "스킬 쿨타임 감소" },
            { StatType.mpRegenFlat, "MP 재생력" },
            { StatType.mpRegenPercent, "MP 재생력%" },
            { StatType.penetrationFlat, "관통력" },
            { StatType.skillRangeFlat, "스킬 범위" },
            { StatType.fireBonusFlat, "불 속성 보너스" },
            { StatType.iceBonusFlat, "얼음 속성 보너스" },
            { StatType.electricBonusFlat, "전기 속성 보너스" },
        };
    }
}

using System.Collections.Generic;

namespace ItemSystem
{
    public enum ItemRarity { Common, Advanced, Rare, Unique, Legendary }

    // 유물/포션 분기는 아직 미확정이라 우선 무기/방어구만 다룸
    public enum ItemCategory { Weapon, Armor, Relic, Potion }

    // category가 Weapon일 때만 사용
    public enum CharacterClass { Fighter, Gunner }

    // 클래스별 무기 종류. 새 클래스/무기가 추가되면 여기에 값만 늘리면 됨
    public enum WeaponType
    {
        // Fighter
        Greatsword,
        Blunt,
        Axe,

        // Gunner
        GrenadeLauncher,
        Shotgun,
        Rifle,
    }
    
    /// <summary>
    /// 클래스별로 어떤 WeaponType이 유효한지 정의하는 테이블.
    /// 새 클래스가 추가되면 여기에 한 줄만 추가하면 인스펙터 드롭다운에도 자동 반영됨.
    /// </summary>
    public static class ClassWeaponTable
    {
        public static readonly Dictionary<CharacterClass, List<WeaponType>> WeaponsByClass =
            new Dictionary<CharacterClass, List<WeaponType>>
            {
                { CharacterClass.Fighter, new List<WeaponType> { WeaponType.Greatsword, WeaponType.Blunt, WeaponType.Axe } },
                { CharacterClass.Gunner, new List<WeaponType> { WeaponType.GrenadeLauncher, WeaponType.Shotgun, WeaponType.Rifle } },
            };
    }

    // category가 Armor일 때만 사용
    public enum ArmorType { Helmet, Armor, Boots, None }

    public enum SubStatSlotType { Combat, Utility, Either }

    public enum ElementType { None, Fire, Ice, Electric }

    /// <summary>
    /// 등급별 서브 옵션 슬롯 구성표.
    /// Rare에서 생긴 Utility 슬롯은 그대로 유지되고,
    /// Unique의 Either 슬롯은 Legendary에서 Combat으로 고정되며 새 Either 슬롯이 추가된다.
    /// (아이템_스트럭쳐 시트 기준 — Legendary = Combat, Utility, Combat, Either)
    /// </summary>
    public static class ItemGradeSlotTable
    {
        public static readonly Dictionary<ItemRarity, List<SubStatSlotType>> SlotsByGrade =
            new Dictionary<ItemRarity, List<SubStatSlotType>>
            {
                { ItemRarity.Common, new List<SubStatSlotType>() },
                { ItemRarity.Advanced, new List<SubStatSlotType> { SubStatSlotType.Combat } },
                { ItemRarity.Rare, new List<SubStatSlotType> { SubStatSlotType.Combat, SubStatSlotType.Utility } },
                { ItemRarity.Unique, new List<SubStatSlotType> { SubStatSlotType.Combat, SubStatSlotType.Either, SubStatSlotType.Utility } },
                { ItemRarity.Legendary, new List<SubStatSlotType> { SubStatSlotType.Combat, SubStatSlotType.Combat, SubStatSlotType.Either, SubStatSlotType.Utility } },
            };

        // 속성 보너스/공격력% 슬롯은 Rare 이상부터 활성화
        public static bool HasElementalBonusSlot(ItemRarity grade) => grade >= ItemRarity.Rare;
    }
}

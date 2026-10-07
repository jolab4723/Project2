using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// UI 표시용 이름/색상 매핑. 인덱스 기반 배열 대신 Dictionary를 써서
    /// enum 순서가 바뀌거나 항목이 늘어나도 매핑이 깨지지 않게 함.
    /// 새 UI 스크립트에서 표시 이름이 필요하면 배열을 새로 만들지 말고 여기를 참조할 것.
    ///
    /// !! 이름 쪽(Grade/Element/Category/Class/Weapon/Armor/StatNames)은 전부 다국어다 - 예전엔
    ///    한국어 문자열이 직접 박힌 static readonly Dictionary였는데, 언어를 바꿔도 절대 안 바뀌는
    ///    문제가 있었다(아이템 툴팁/강화 팝업 전체에 영향). 지금은 매번 새 Dictionary를 만들어
    ///    ItemDisplayNameDatabaseSO(Resources에서 로드, TooltipUI.GetItemName 등과 같은 패턴)를 조회한다.
    ///    호출부(TooltipUI.cs 등)는 기존처럼 `ItemDisplayNames.GradeNames[rarity]` 형태로 그대로 쓰면 된다 -
    ///    프로퍼티라 매번 최신 언어로 다시 만들어질 뿐, 반환 타입/사용법은 바뀌지 않는다.
    ///    DB에 key가 없으면 원래 하드코딩돼 있던 한국어 문구로 폴백한다.
    ///    색상 쪽(GradeColorHex/ElementColorHex)은 언어와 무관해서 그대로 고정 Dictionary로 둔다.
    ///
    /// key 규칙: "grade.*", "element.*", "category.*", "class.*", "weapon.*", "armor.*", "stat.*"
    /// (ItemDisplayNameDatabaseSO.GetLabel 참고, 자세한 값은 ItemDisplayName.xlsx 참고)
    /// </summary>
    public static class ItemDisplayNames
    {
        private const string DatabaseResourcePath = "DataFiles/ItemData/3. GeneratedAssets/LabelData/ItemDisplayNameDatabase";

        private static ItemDisplayNameDatabaseSO labelsCache;

        private static ItemDisplayNameDatabaseSO Labels =>
            labelsCache ??= Resources.Load<ItemDisplayNameDatabaseSO>(DatabaseResourcePath);

        /// <summary>DB에서 key를 찾아 반환하고, DB가 없거나 key가 없으면 fallback(원래 하드코딩 문구)을 쓴다.</summary>
        private static string Get(string key, string fallback)
        {
            string label = Labels != null ? Labels.GetLabel(key) : null;
            return string.IsNullOrEmpty(label) ? fallback : label;
        }

        public static Dictionary<ItemRarity, string> GradeNames => new Dictionary<ItemRarity, string>
        {
            { ItemRarity.Common, Get("grade.common", "일반") },
            { ItemRarity.Advanced, Get("grade.advanced", "고급") },
            { ItemRarity.Rare, Get("grade.rare", "희귀") },
            { ItemRarity.Unique, Get("grade.unique", "유일") },
            { ItemRarity.Legendary, Get("grade.legendary", "전설") },
        };

        public static readonly Dictionary<ItemRarity, string> GradeColorHex = new Dictionary<ItemRarity, string>
        {
            { ItemRarity.Common, "#FFFFFF" },
            { ItemRarity.Advanced, "#82D17D" },
            { ItemRarity.Rare, "#4D9CE4" },
            { ItemRarity.Unique, "#C27BA0" },
            { ItemRarity.Legendary, "#FFD175" },
        };

        public static Dictionary<ElementType, string> ElementNames => new Dictionary<ElementType, string>
        {
            { ElementType.None, Get("element.none", "무속성") },
            { ElementType.Fire, Get("element.fire", "불") },
            { ElementType.Ice, Get("element.ice", "얼음") },
            { ElementType.Electric, Get("element.electric", "번개") },
        };

        public static readonly Dictionary<ElementType, string> ElementColorHex = new Dictionary<ElementType, string>
        {
            { ElementType.None, "#A8A8A8" },
            { ElementType.Fire, "#E06666" },
            { ElementType.Ice, "#6D9EEB" },
            { ElementType.Electric, "#FFD966" },
        };

        public static Dictionary<ItemCategory, string> CategoryNames => new Dictionary<ItemCategory, string>
        {
            { ItemCategory.Weapon, Get("category.weapon", "무기") },
            { ItemCategory.Armor, Get("category.armor", "방어구") },
            { ItemCategory.Relic, Get("category.relic", "유물") },
            { ItemCategory.Potion, Get("category.potion", "포션") },
        };

        public static Dictionary<CharacterClass, string> ClassNames => new Dictionary<CharacterClass, string>
        {
            { CharacterClass.Fighter, Get("class.fighter", "파이터") },
            { CharacterClass.Gunner, Get("class.gunner", "거너") },
        };

        public static Dictionary<WeaponType, string> WeaponNames => new Dictionary<WeaponType, string>
        {
            { WeaponType.Greatsword, Get("weapon.greatsword", "대검") },
            { WeaponType.Blunt, Get("weapon.blunt", "둔기") },
            { WeaponType.Axe, Get("weapon.axe", "도끼") },
            { WeaponType.GrenadeLauncher, Get("weapon.grenadelauncher", "유탄발사기") },
            { WeaponType.Shotgun, Get("weapon.shotgun", "샷건") },
            { WeaponType.Rifle, Get("weapon.rifle", "소총") },
        };

        public static Dictionary<ArmorType, string> ArmorNames => new Dictionary<ArmorType, string>
        {
            { ArmorType.Helmet, Get("armor.helmet", "헬멧") },
            { ArmorType.Armor, Get("armor.armor", "갑옷") },
            { ArmorType.Boots, Get("armor.boots", "신발") },
        };

        // 마스터 변수 시트 기준 이름
        //
        // 퍼센트 스탯도 이름에는 %를 붙이지 않는다. "공격속도% -45" 대신 "공격속도 -45%"로 쓰도록
        // 수치 뒤에 StatUnit()을 붙인다(2026-09-28 사용자 요청). 그래서 공격력/공격력%처럼
        // 이름이 같은 스탯은 수치 뒤의 %로 구분된다.
        public static Dictionary<StatType, string> StatNames => new Dictionary<StatType, string>
        {
            { StatType.healthFlat, Get("stat.healthflat", "체력") },
            { StatType.healthPercent, Get("stat.healthpercent", "체력") },
            { StatType.attackPowerFlat, Get("stat.attackpowerflat", "공격력") },
            { StatType.attackPowerPercent, Get("stat.attackpowerpercent", "공격력") },
            { StatType.defensePowerFlat, Get("stat.defensepowerflat", "방어력") },
            { StatType.defensePowerPercent, Get("stat.defensepowerpercent", "방어력") },
            { StatType.moveSpeedFlat, Get("stat.movespeedflat", "이동속도") },
            { StatType.moveSpeedPercent, Get("stat.movespeedpercent", "이동속도") },
            { StatType.attackSpeedFlat, Get("stat.attackspeedflat", "공격속도") },
            { StatType.attackSpeedPercent, Get("stat.attackspeedpercent", "공격속도") },
            { StatType.critRateFlat, Get("stat.critrateflat", "크리티컬 확률") },
            { StatType.critMultFlat, Get("stat.critmultflat", "크리티컬 배율") },
            { StatType.cdrFlat, Get("stat.cdrflat", "스킬 쿨타임 감소") },
            { StatType.mpRegenFlat, Get("stat.mpregenflat", "MP 재생력") },
            { StatType.mpRegenPercent, Get("stat.mpregenpercent", "MP 재생력") },
            { StatType.penetrationFlat, Get("stat.penetrationflat", "관통력") },
            { StatType.skillRangeFlat, Get("stat.skillrangeflat", "스킬 범위") },
            { StatType.skillRangePercent, Get("stat.skillrangepercent", "스킬 범위") },
            { StatType.normalDamagePercent, Get("stat.normaldamagepercent", "일반공격 피해") },
            { StatType.skillDamagePercent, Get("stat.skilldamagepercent", "스킬 피해") },
            { StatType.fireBonusFlat, Get("stat.firebonusflat", "불 속성 보너스") },
            { StatType.iceBonusFlat, Get("stat.icebonusflat", "얼음 속성 보너스") },
            { StatType.electricBonusFlat, Get("stat.electricbonusflat", "전기 속성 보너스") },
        };

        /// <summary>
        /// 스탯 수치 뒤에 붙일 단위. 퍼센트 스탯(StatTypeUtility.IsPercent)이면 "%", 아니면 빈 문자열.
        /// 스탯 이름(StatNames)에는 %가 없으므로, 수치를 표시하는 UI는 "이름 +수치" 뒤에 이 값을 붙인다.
        /// 예: $"{StatNames[type]} {sign}{value:F1}{StatUnit(type)}" → "공격속도 -45.0%"
        ///
        /// 크리티컬 확률·쿨타임 감소는 합연산 전용이라 이름이 Flat이지만 값 자체가 %p(0~100, 0~70)라서
        /// 표시할 때는 %를 붙인다(PlayerStatUIManager도 같은 표기). IsPercent는 계산 분류에 쓰이므로 건드리지 않는다.
        /// </summary>
        public static string StatUnit(StatType statType)
        {
            if (statType == StatType.critRateFlat || statType == StatType.cdrFlat)
                return "%";

            return StatTypeUtility.IsPercent(statType) ? "%" : string.Empty;
        }
    }
}

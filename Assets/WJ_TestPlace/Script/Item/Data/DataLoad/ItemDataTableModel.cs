using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class ItemDataTableJsonData
    {
        public List<ArmorDefinitionRow> armorDefinitions = new List<ArmorDefinitionRow>();
        public List<WeaponDefinitionRow> weaponDefinitions = new List<WeaponDefinitionRow>();
        public List<PotionDefinitionRow> potionDefinitions = new List<PotionDefinitionRow>();
        public List<RelicDefinitionRow> relicDefinitions = new List<RelicDefinitionRow>();
    }

    [Serializable]
    public class ArmorDefinitionRow
    {
        public string itemId; // Category+Class+Index 8자리 고유키. 앞자리 0이 있어서 int가 아니라 string.
        public string itemName;
        public string category; // 항상 "Armor" - 시트 자체가 Armor 전용이라 참고용
        public string armorType;
        public string rarity;
        public string mainStat1Type;
        public float mainStat1Value;
        public string mainStat2Type;
        public float mainStat2Value;
        public string uniqueEffectId;
        public string description;
        public int itemWidth = 1;
        public int itemHeight = 1;
        public int itemPrice;
    }

    /// <summary>
    /// 실제 엑셀 헤더와 그대로 맞춤. 기존 weaponClass/category 컴럼을 characterClass/weaponType으로 직접 바끈.
    /// </summary>
    [Serializable]
    public class WeaponDefinitionRow
    {
        public string itemId; // Category+Class+Index 8자리 고유키. 앞자리 0이 있어서 int가 아니라 string.
        public string itemName;
        public string characterClass;
        public string weaponType;
        public string rarity;
        public string mainStat1Type;
        public float mainStat1Value;
        public string mainStat2Type;
        public float mainStat2Value;
        public string EnchantedElement;
        public string uniqueEffectId;
        public string description;
        public int itemWidth = 1;
        public int itemHeight = 1;
        public int sellPrice;
    }

    [Serializable]
    public class PotionDefinitionRow
    {
        public string itemId; // Category+Class+Index 8자리 고유키. 앞자리 0이 있어서 int가 아니라 string.
        public string itemName;
        public string category; // 항상 "Potion" - 시트 자체가 Potion 전용이라 참고용
        public string rarity;
        public string uniqueEffectId;
        public string description;
        public int itemWidth = 1;
        public int itemHeight = 1;
        public int itemPrice;

        // 포션 효과 - 회복 또는 능력치 증가, 수치, 지속시간
        // 사용 가능 횟수(충전량)는 포션마다가 아니라 플레이어 공유 풀(PotionUseManager)이 관리한다.
        public string potionEffectType; // Heal / StatBoost
        public string potionStatType;   // potionEffectType이 StatBoost일 때만 사용
        public float potionEffectValue;
        public float potionEffectDuration;
    }

    /// <summary>
    /// 유물. 메인/서브 옵션 없이 보유만으로 uniqueEffect가 상시 적용되는 카테고리라
    /// 스탯 컬럼이 없고 시트 구조가 포션과 동일하다.
    /// </summary>
    [Serializable]
    public class RelicDefinitionRow
    {
        public string itemId;
        public string itemName;
        public string category; // 항상 "Relic" - 시트 자체가 Relic 전용이라 참고용
        public string rarity;
        public string uniqueEffectId;
        public string description;
        public int itemWidth = 1;
        public int itemHeight = 1;
        public int itemPrice;
    }
}

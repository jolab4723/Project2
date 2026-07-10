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
    }

    [Serializable]
    public class ArmorDefinitionRow
    {
        public int itemId;
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
        public int itemId;
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
        public int itemId;
        public string itemName;
        public string category; // 항상 "Potion" - 시트 자체가 Potion 전용이라 참고용
        public string rarity;
        public string uniqueEffectId;
        public string description;
        public int itemWidth = 1;
        public int itemHeight = 1;
        public int itemPrice;
    }
}

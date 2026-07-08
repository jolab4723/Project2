using System;
using System.Collections.Generic;

namespace SW.Test.ItemTablePipeline
{
    [Serializable]
    public class SwTestItemTableJsonData
    {
        public List<SwTestItemDefinitionRow> itemDefinitions = new List<SwTestItemDefinitionRow>();
        public List<SwTestEquipmentDefinitionRow> equipmentDefinitions = new List<SwTestEquipmentDefinitionRow>();
        public List<SwTestWeaponDefinitionRow> weaponDefinitions = new List<SwTestWeaponDefinitionRow>();
        public List<SwTestArmorDefinitionRow> armorDefinitions = new List<SwTestArmorDefinitionRow>();
        public List<SwTestPotionDefinitionRow> potionDefinitions = new List<SwTestPotionDefinitionRow>();
        public List<SwTestRelicDefinitionRow> relicDefinitions = new List<SwTestRelicDefinitionRow>();
        public List<SwTestOptionDefinitionRow> optionDefinitions = new List<SwTestOptionDefinitionRow>();
        public List<SwTestSubStatPoolRow> subStatPools = new List<SwTestSubStatPoolRow>();
        public List<SwTestRarityOptionRuleRow> rarityOptionRules = new List<SwTestRarityOptionRuleRow>();
        public List<SwTestElementalBonusConfigRow> elementalBonusConfigs = new List<SwTestElementalBonusConfigRow>();
        public List<SwTestBuffDefinitionRow> buffDefinitions = new List<SwTestBuffDefinitionRow>();
        public List<SwTestUniqueEffectDefinitionRow> uniqueEffectDefinitions = new List<SwTestUniqueEffectDefinitionRow>();
        public List<SwTestDisplayNameRow> displayNames = new List<SwTestDisplayNameRow>();
        public List<SwTestDraftSourceRow> draftSourceRows = new List<SwTestDraftSourceRow>();
    }

    [Serializable]
    public class SwTestItemDefinitionRow
    {
        public string itemId;
        public string itemName;
        public string description;
        public string category;
        public string rarity;
        public int itemWidth = 1;
        public int itemHeight = 1;
        public string originalSizeText;
        public int sellPrice;
        public int buyPrice;
        public string iconKey;
        public string acquireSourceId;
        public string mainStat1Type;
        public float mainStat1Value;
        public string mainStat2Type;
        public float mainStat2Value;
        public float upgradeBonusPerLevel = 0.1f;
        public string elementalConfigId;
        public string weaponEnchantElement;
        public string combatPoolId;
        public string utilityPoolId;
        public string uniqueEffectId;
        public string draftEffectText;
        public bool enabled = true;
        public string memo;
    }

    [Serializable]
    public class SwTestEquipmentDefinitionRow
    {
        public string itemId;
        public bool isEquippable;
        public string allowedEquipSlots;
        public string characterClass;
        public string weaponType;
        public string armorType;
        public int requiredLevel;
        public string memo;
    }

    [Serializable]
    public class SwTestWeaponDefinitionRow
    {
        public string itemId;
        public string characterClass;
        public string weaponType;
        public float attackPower;
        public float attackSpeedMultiplier;
        public float attackRange;
        public string elementType;
        public string hitEffectId;
        public string attackAnimationKey;
        public string projectilePrefabKey;
        public string draftEffectText;
        public string memo;
    }

    [Serializable]
    public class SwTestArmorDefinitionRow
    {
        public string itemId;
        public string armorType;
        public float defensePower;
        public float moveSpeedMultiplier;
        public string draftEffectText;
        public string memo;
    }

    [Serializable]
    public class SwTestPotionDefinitionRow
    {
        public string itemId;
        public string potionType;
        public string target;
        public float value;
        public string statusToCleanse;
        public string buffId;
        public float duration;
        public float cooldown;
        public string draftEffectText;
        public string memo;
    }

    [Serializable]
    public class SwTestRelicDefinitionRow
    {
        public string itemId;
        public string uniqueEffectId;
        public string triggerCondition;
        public float effectValue;
        public string valueApplyType;
        public string statType;
        public int maxStack;
        public string counterTarget;
        public string draftEffectText;
        public string memo;
    }

    [Serializable]
    public class SwTestOptionDefinitionRow
    {
        public string optionId;
        public string optionName;
        public string statType;
        public string slotType;
        public string elementType;
        public float minValue;
        public float maxValue;
        public string targetCategory;
        public string targetEquipSlot;
        public string minRarity;
        public string maxRarity;
        public int weight = 100;
        public string valueType;
        public string draftEffectText;
        public string memo;
    }

    [Serializable]
    public class SwTestSubStatPoolRow
    {
        public string poolId;
        public string poolType;
        public string optionId;
        public float weightOverride;
        public bool enabled = true;
        public string memo;
    }

    [Serializable]
    public class SwTestRarityOptionRuleRow
    {
        public string rarity;
        public int mainOptionCount;
        public bool hasElementalBonusSlot;
        public string slot1;
        public string slot2;
        public string slot3;
        public string slot4;
        public string memo;
    }

    [Serializable]
    public class SwTestElementalBonusConfigRow
    {
        public string elementalConfigId;
        public float elementBonusValue;
        public float atkFallbackValue;
        public float missChance;
        public string memo;
    }

    [Serializable]
    public class SwTestBuffDefinitionRow
    {
        public string buffId;
        public string buffName;
        public string description;
        public string iconKey;
        public float duration;
        public string stackBehavior;
        public int maxStack;
        public string stat1Type;
        public float stat1Value;
        public string stat2Type;
        public float stat2Value;
        public string memo;
    }

    [Serializable]
    public class SwTestUniqueEffectDefinitionRow
    {
        public string uniqueEffectId;
        public string effectName;
        public string effectType;
        public string effectDescription;
        public float coefficient1;
        public float coefficient2;
        public float coefficient3;
        public string buffId;
        public string triggerCondition;
        public string memo;
    }

    [Serializable]
    public class SwTestDisplayNameRow
    {
        public string keyType;
        public string key;
        public string koName;
        public string colorHex;
        public string memo;
    }

    [Serializable]
    public class SwTestDraftSourceRow
    {
        public string sourceSheet;
        public int sourceRow;
        public string itemId;
        public string name;
        public string part;
        public string rarityKo;
        public string category;
        public string size;
        public string effect;
        public string note;
    }
}

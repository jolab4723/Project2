using System;
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

namespace SW.Test.RandomDrop
{
    public enum SwTestMonsterDropGrade
    {
        Normal,
        Champion,
        Elite,
        Boss
    }

    public enum SwTestDropItemKind
    {
        Weapon,
        Helmet,
        Chest,
        Boots,
        Potion
    }

    [Serializable]
    public class SwTestRarityChance
    {
        public ItemRarity rarity;
        [Min(0f)] public float weight;
    }

    [Serializable]
    public class SwTestItemKindChance
    {
        public SwTestDropItemKind itemKind;
        [Min(0f)] public float weight;
    }

    [Serializable]
    public class SwTestMonsterDropRate
    {
        public SwTestMonsterDropGrade monsterGrade;
        [Range(0f, 100f)] public float itemDropChance;
        public List<SwTestRarityChance> rarityChances = new List<SwTestRarityChance>();
    }

    [Serializable]
    public class SwTestEquipmentDropResult
    {
        public bool success;
        public string failReason;
        public SwTestMonsterDropGrade monsterGrade;
        public ItemRarity rarity;
        public SwTestDropItemKind itemKind;
        public ItemDefinitionSO itemDefinition;

        public static SwTestEquipmentDropResult Failed(SwTestMonsterDropGrade monsterGrade, string reason)
        {
            return new SwTestEquipmentDropResult
            {
                success = false,
                monsterGrade = monsterGrade,
                failReason = reason
            };
        }

        public static SwTestEquipmentDropResult Succeeded(
            SwTestMonsterDropGrade monsterGrade,
            ItemRarity rarity,
            SwTestDropItemKind itemKind,
            ItemDefinitionSO itemDefinition)
        {
            return new SwTestEquipmentDropResult
            {
                success = true,
                monsterGrade = monsterGrade,
                rarity = rarity,
                itemKind = itemKind,
                itemDefinition = itemDefinition
            };
        }
    }
}

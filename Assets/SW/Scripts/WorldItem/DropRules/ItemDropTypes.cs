using System;
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

public static class ItemDropTypes
{
    public enum ItemDropKind : byte
    {
        Weapon = 0,
        Helmet = 1,
        Chest = 2,
        Boots = 3,
        Potion = 4
    }

    [Serializable]
    public sealed class ItemDropRarityWeight
    {
        public ItemRarity rarity;

        [Min(0f)]
        public float weight;
    }

    [Serializable]
    public sealed class ItemDropKindWeight
    {
        public ItemDropKind itemKind;

        [Min(0f)]
        public float weight;
    }

    [Serializable]
    public sealed class EnemyItemDropRule
    {
        public EnemyGrade enemyGrade;

        [Range(0f, 100f)]
        public float itemDropChance;

        public List<ItemDropRarityWeight> rarityWeights = new List<ItemDropRarityWeight>();
    }
}

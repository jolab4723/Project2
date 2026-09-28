using System;
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

public sealed class ItemDropRollService
{
    public ItemDropRollResultData Roll(
        ItemDropTableSO dropTable,
        ItemDatabaseSO itemDatabase,
        EnemyGrade enemyGrade,
        System.Random random = null,
        PlayerContext rarityOwner = null)
    {
        if (dropTable == null)
        {
            return ItemDropRollResultData.Failed(ItemDropRollResult.InvalidTable, enemyGrade);
        }

        if (itemDatabase == null || itemDatabase.allItems == null || itemDatabase.allItems.Count == 0)
        {
            return ItemDropRollResultData.Failed(ItemDropRollResult.ItemDatabaseEmpty, enemyGrade);
        }

        if (!dropTable.TryGetEnemyRule(enemyGrade,
                out ItemDropTypes.EnemyItemDropRule enemyRule))
        {
            return ItemDropRollResultData.Failed(
                ItemDropRollResult.EnemyRuleNotFound,
                enemyGrade);
        }

        if (!RollPercent(enemyRule.itemDropChance, random))
            return ItemDropRollResultData.NoDrop(enemyGrade);

        if (!TryPickWeighted(
                enemyRule.rarityWeights,
                row => row.weight *
                       DropRarityModifierUniqueEffectSO.GetRarityWeightMultiplier(row.rarity, rarityOwner),
                random,
                out ItemDropTypes.ItemDropRarityWeight rarityWeight))
        {
            return ItemDropRollResultData.Failed(
                ItemDropRollResult.RarityWeightsEmpty,
                enemyGrade);
        }

        if (!TryPickWeighted(
                dropTable.ItemKindWeights,
                row => HasCandidate(
                    itemDatabase.allItems,
                    rarityWeight.rarity,
                    row.itemKind)
                    ? row.weight
                    : 0f,
                random,
                out ItemDropTypes.ItemDropKindWeight kindWeight))
        {
            return ItemDropRollResultData.Failed(
                ItemDropRollResult.ItemKindWeightsEmpty,
                enemyGrade);
        }

        List<ItemDefinitionSO> candidates = BuildCandidates(
            itemDatabase.allItems,
            rarityWeight.rarity,
            kindWeight.itemKind);

        if (candidates.Count == 0)
        {
            return ItemDropRollResultData.Failed(
                ItemDropRollResult.NoCandidateItem,
                enemyGrade);
        }

        int selectedIndex = NextInt(
            0,
            candidates.Count,
            random);

        return ItemDropRollResultData.Success(
            enemyGrade,
            rarityWeight.rarity,
            kindWeight.itemKind,
            candidates[selectedIndex]);
    }

    private static bool HasCandidate(
        IReadOnlyList<ItemDefinitionSO> itemDefinitions,
        ItemRarity rarity,
        ItemDropTypes.ItemDropKind itemKind)
    {
        if (itemDefinitions == null)
            return false;

        for (int i = 0; i < itemDefinitions.Count; i++)
        {
            ItemDefinitionSO definition = itemDefinitions[i];
            if (definition != null &&
                definition.rarity == rarity &&
                MatchesItemKind(definition, itemKind))
            {
                return true;
            }
        }

        return false;
    }

    private static List<ItemDefinitionSO> BuildCandidates(
        IReadOnlyList<ItemDefinitionSO> itemDefinitions,
        ItemRarity rarity,
        ItemDropTypes.ItemDropKind itemKind)
    {
        var candidates = new List<ItemDefinitionSO>();
        var seenDefinitions = new HashSet<ItemDefinitionSO>();
        var seenItemIds = new HashSet<string>();

        for (int i = 0; i < itemDefinitions.Count; i++)
        {
            ItemDefinitionSO definition = itemDefinitions[i];

            if (definition == null ||
                !seenDefinitions.Add(definition))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(definition.itemId) &&
                !seenItemIds.Add(definition.itemId))
            {
                continue;
            }

            if (definition.rarity != rarity)
                continue;

            if (!MatchesItemKind(definition, itemKind))
                continue;

            candidates.Add(definition);
        }

        return candidates;
    }

    private static bool MatchesItemKind(
        ItemDefinitionSO definition,
        ItemDropTypes.ItemDropKind itemKind)
    {
        switch (itemKind)
        {
            case ItemDropTypes.ItemDropKind.Weapon:
                return definition.category == ItemCategory.Weapon;

            case ItemDropTypes.ItemDropKind.Helmet:
                return definition.category == ItemCategory.Armor &&
                       definition.armorType == ArmorType.Helmet;

            case ItemDropTypes.ItemDropKind.Chest:
                return definition.category == ItemCategory.Armor &&
                       definition.armorType == ArmorType.Armor;

            case ItemDropTypes.ItemDropKind.Boots:
                return definition.category == ItemCategory.Armor &&
                       definition.armorType == ArmorType.Boots;

            case ItemDropTypes.ItemDropKind.Potion:
                return definition.category == ItemCategory.Potion;

            case ItemDropTypes.ItemDropKind.Relic:
                return definition.category == ItemCategory.Relic;

            default:
                return false;
        }
    }

    private static bool RollPercent(float chance, System.Random random)
    {
        if (chance <= 0f)
            return false;

        if (chance >= 100f)
            return true;

        return NextFloat(random) < chance / 100f;
    }

    private static bool TryPickWeighted<T>(
        IReadOnlyList<T> rows,
        Func<T, float> getWeight,
        System.Random random,
        out T selected)
        where T : class
    {
        selected = null;

        if (rows == null || rows.Count == 0)
            return false;

        float totalWeight = 0f;

        for (int i = 0; i < rows.Count; i++)
        {
            T row = rows[i];

            if (row == null)
                continue;

            totalWeight += Mathf.Max(0f, getWeight(row));
        }

        if (totalWeight <= 0f)
            return false;

        float roll = NextFloat(random) * totalWeight;
        T lastValidRow = null;

        for (int i = 0; i < rows.Count; i++)
        {
            T row = rows[i];

            if (row == null)
                continue;

            float weight = Mathf.Max(0f, getWeight(row));

            if (weight <= 0f)
                continue;

            lastValidRow = row;

            if (roll < weight)
            {
                selected = row;
                return true;
            }

            roll -= weight;
        }

        // 부동소수점 오차에 대한 안전 처리
        selected = lastValidRow;
        return selected != null;
    }

    private static float NextFloat(System.Random random)
    {
        return random != null ? (float)random.NextDouble() : UnityEngine.Random.value;
    }

    private static int NextInt(
        int minInclusive,
        int maxExclusive,
        System.Random random)
    {
        return random != null
            ? random.Next(minInclusive, maxExclusive)
            : UnityEngine.Random.Range(minInclusive, maxExclusive);
    }
}

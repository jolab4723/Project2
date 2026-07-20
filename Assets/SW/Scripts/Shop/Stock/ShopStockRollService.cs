using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

public sealed class ShopStockRollService
{
    public bool TryRoll(
        ItemDatabaseSO itemDatabase,
        IReadOnlyList<ShopRarityChance> rarityChances,
        out ItemDefinitionSO selectedDefinition,
        System.Random random = null)
    {
        selectedDefinition = null;

        if (itemDatabase == null ||
            itemDatabase.allItems == null ||
            itemDatabase.allItems.Count == 0 ||
            rarityChances == null ||
            rarityChances.Count == 0)
        {
            return false;
        }

        List<ItemDefinitionSO> uniqueItems =
            BuildUniqueItems(itemDatabase.allItems);

        if (uniqueItems.Count == 0)
            return false;

        float totalWeight =
            GetAvailableWeight(uniqueItems, rarityChances);

        if (totalWeight <= 0f)
            return false;

        float roll = NextFloat(random) * totalWeight;

        for (int i = 0; i < rarityChances.Count; i++)
        {
            ShopRarityChance chance = rarityChances[i];

            if (chance == null ||
                chance.Weight <= 0f ||
                !HasCandidate(uniqueItems, chance.Rarity))
            {
                continue;
            }

            roll -= chance.Weight;

            if (roll > 0f)
                continue;

            return TryPickItemOfRarity(
                uniqueItems,
                chance.Rarity,
                random,
                out selectedDefinition);
        }

        return false;
    }

    private static List<ItemDefinitionSO> BuildUniqueItems(
        IReadOnlyList<ItemDefinitionSO> source)
    {
        var result = new List<ItemDefinitionSO>();
        var seenDefinitions = new HashSet<ItemDefinitionSO>();
        var seenItemIds = new HashSet<string>();

        for (int i = 0; i < source.Count; i++)
        {
            ItemDefinitionSO definition = source[i];

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

            result.Add(definition);
        }

        return result;
    }

    private static float GetAvailableWeight(
        IReadOnlyList<ItemDefinitionSO> items,
        IReadOnlyList<ShopRarityChance> rarityChances)
    {
        float totalWeight = 0f;

        for (int i = 0; i < rarityChances.Count; i++)
        {
            ShopRarityChance chance = rarityChances[i];

            if (chance == null ||
                chance.Weight <= 0f ||
                !HasCandidate(items, chance.Rarity))
            {
                continue;
            }

            totalWeight += chance.Weight;
        }

        return totalWeight;
    }

    private static bool HasCandidate(
        IReadOnlyList<ItemDefinitionSO> items,
        ItemRarity rarity)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].rarity == rarity)
                return true;
        }

        return false;
    }

    private static bool TryPickItemOfRarity(
        IReadOnlyList<ItemDefinitionSO> items,
        ItemRarity rarity,
        System.Random random,
        out ItemDefinitionSO selectedDefinition)
    {
        selectedDefinition = null;

        var candidates = new List<ItemDefinitionSO>();

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].rarity == rarity)
                candidates.Add(items[i]);
        }

        if (candidates.Count == 0)
            return false;

        int index = random != null
            ? random.Next(0, candidates.Count)
            : UnityEngine.Random.Range(0, candidates.Count);

        selectedDefinition = candidates[index];
        return true;
    }

    private static float NextFloat(System.Random random)
    {
        return random != null
            ? (float)random.NextDouble()
            : UnityEngine.Random.value;
    }
}
using System;
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

namespace SW.Test.RandomDrop
{
    public static class SwTestEquipmentDropService
    {
        public const string NoItemDroppedReason = "아이템이 드랍되지 않았습니다.";
        public const string NoCandidateItemReasonPrefix = "후보 아이템이 없습니다.";

        public static SwTestEquipmentDropResult Roll(
            SwTestEquipmentDropTableSO table,
            IReadOnlyList<ItemDefinitionSO> itemDefinitions,
            SwTestMonsterDropGrade monsterGrade,
            System.Random random = null)
        {
            if (table == null)
                return SwTestEquipmentDropResult.Failed(monsterGrade, "드랍 테이블이 비어 있습니다.");

            if (itemDefinitions == null || itemDefinitions.Count == 0)
                return SwTestEquipmentDropResult.Failed(monsterGrade, "아이템 정의 목록이 비어 있습니다.");

            SwTestMonsterDropRate monsterRule = table.GetMonsterRule(monsterGrade);
            if (monsterRule == null)
                return SwTestEquipmentDropResult.Failed(monsterGrade, $"몬스터 등급 규칙을 찾지 못했습니다: {GetMonsterGradeName(monsterGrade)}");

            if (!RollPercent(monsterRule.itemDropChance, random))
                return SwTestEquipmentDropResult.Failed(monsterGrade, NoItemDroppedReason);

            if (!TryPickWeighted(monsterRule.rarityChances, row => row.weight, random, out SwTestRarityChance rarityChance))
                return SwTestEquipmentDropResult.Failed(monsterGrade, "아이템 등급 확률표가 비어 있습니다.");

            if (!TryPickWeighted(table.itemKindChances, row => row.weight, random, out SwTestItemKindChance kindChance))
                return SwTestEquipmentDropResult.Failed(monsterGrade, "아이템 종류 확률표가 비어 있습니다.");

            List<ItemDefinitionSO> candidates = BuildCandidates(itemDefinitions, rarityChance.rarity, kindChance.itemKind);
            if (candidates.Count == 0)
            {
                return SwTestEquipmentDropResult.Failed(
                    monsterGrade,
                    $"후보 아이템이 없습니다. 등급: {GetRarityName(rarityChance.rarity)}, 종류: {GetItemKindName(kindChance.itemKind)}");
            }

            int index = NextInt(0, candidates.Count, random);
            return SwTestEquipmentDropResult.Succeeded(monsterGrade, rarityChance.rarity, kindChance.itemKind, candidates[index]);
        }

        public static List<ItemDefinitionSO> BuildCandidates(
            IReadOnlyList<ItemDefinitionSO> itemDefinitions,
            ItemRarity rarity,
            SwTestDropItemKind itemKind)
        {
            List<ItemDefinitionSO> result = new List<ItemDefinitionSO>();

            for (int i = 0; i < itemDefinitions.Count; i++)
            {
                ItemDefinitionSO item = itemDefinitions[i];
                if (item == null)
                    continue;

                if (item.rarity != rarity)
                    continue;

                if (!MatchesItemKind(item, itemKind))
                    continue;

                result.Add(item);
            }

            return result;
        }

        public static bool MatchesItemKind(ItemDefinitionSO item, SwTestDropItemKind itemKind)
        {
            if (item == null)
                return false;

            switch (itemKind)
            {
                case SwTestDropItemKind.Weapon:
                    return item.category == ItemCategory.Weapon && item.characterClass == CharacterClass.Fighter;

                case SwTestDropItemKind.Helmet:
                    return item.category == ItemCategory.Armor && item.armorType == ArmorType.Helmet;

                case SwTestDropItemKind.Chest:
                    return item.category == ItemCategory.Armor && item.armorType == ArmorType.Armor;

                case SwTestDropItemKind.Boots:
                    return item.category == ItemCategory.Armor && item.armorType == ArmorType.Boots;

                case SwTestDropItemKind.Potion:
                    return item.category == ItemCategory.Potion;

                default:
                    return false;
            }
        }

        public static string GetMonsterGradeName(SwTestMonsterDropGrade monsterGrade)
        {
            switch (monsterGrade)
            {
                case SwTestMonsterDropGrade.Normal:
                    return "일반";
                case SwTestMonsterDropGrade.Champion:
                    return "대장급 일반";
                case SwTestMonsterDropGrade.Elite:
                    return "엘리트";
                case SwTestMonsterDropGrade.Boss:
                    return "보스";
                default:
                    return monsterGrade.ToString();
            }
        }

        public static string GetRarityName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common:
                    return "일반";
                case ItemRarity.Advanced:
                    return "고급";
                case ItemRarity.Rare:
                    return "희귀";
                case ItemRarity.Unique:
                    return "유일";
                case ItemRarity.Legendary:
                    return "전설";
                default:
                    return rarity.ToString();
            }
        }

        public static string GetItemKindName(SwTestDropItemKind itemKind)
        {
            switch (itemKind)
            {
                case SwTestDropItemKind.Weapon:
                    return "무기";
                case SwTestDropItemKind.Helmet:
                    return "투구";
                case SwTestDropItemKind.Chest:
                    return "갑옷";
                case SwTestDropItemKind.Boots:
                    return "장화";
                case SwTestDropItemKind.Potion:
                    return "포션";
                default:
                    return itemKind.ToString();
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
        {
            selected = default;

            if (rows == null || rows.Count == 0)
                return false;

            float total = 0f;
            for (int i = 0; i < rows.Count; i++)
                total += Mathf.Max(0f, getWeight(rows[i]));

            if (total <= 0f)
                return false;

            float roll = NextFloat(random) * total;
            float current = 0f;

            for (int i = 0; i < rows.Count; i++)
            {
                current += Mathf.Max(0f, getWeight(rows[i]));
                if (roll <= current)
                {
                    selected = rows[i];
                    return true;
                }
            }

            selected = rows[rows.Count - 1];
            return true;
        }

        private static float NextFloat(System.Random random)
        {
            return random != null ? (float)random.NextDouble() : UnityEngine.Random.value;
        }

        private static int NextInt(int minInclusive, int maxExclusive, System.Random random)
        {
            return random != null ? random.Next(minInclusive, maxExclusive) : UnityEngine.Random.Range(minInclusive, maxExclusive);
        }
    }
}

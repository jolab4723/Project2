using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

namespace SW.Test.RandomDrop
{
    [CreateAssetMenu(menuName = "SW/TEST/Random Drop/Equipment Drop Table")]
    public class SwTestEquipmentDropTableSO : ScriptableObject
    {
        [Header("몬스터 등급별 아이템 드랍률")]
        public List<SwTestMonsterDropRate> monsterDropRates = new List<SwTestMonsterDropRate>();

        [Header("드랍 성공 시 장비 종류 확률 - 유물 제외")]
        public List<SwTestItemKindChance> itemKindChances = new List<SwTestItemKindChance>();

        public SwTestMonsterDropRate GetMonsterRule(EnemyGrade monsterGrade)
        {
            for (int i = 0; i < monsterDropRates.Count; i++)
            {
                if (monsterDropRates[i].monsterGrade == monsterGrade)
                    return monsterDropRates[i];
            }

            return null;
        }

        public void ResetToDefaultTable()
        {
            monsterDropRates = new List<SwTestMonsterDropRate>
            {
                CreateMonsterRule(EnemyGrade.Normal, 25f, 85f, 10f, 3f, 1.5f, 0.5f),
                CreateMonsterRule(EnemyGrade.Advanced, 55f, 55f, 25f, 13f, 5f, 2f),
                CreateMonsterRule(EnemyGrade.Elite, 100f, 10f, 20f, 30f, 30f, 10f),
                CreateMonsterRule(EnemyGrade.Boss, 100f, 0f, 0f, 35f, 35f, 30f),
            };

            itemKindChances = new List<SwTestItemKindChance>
            {
                new SwTestItemKindChance { itemKind = SwTestDropItemKind.Weapon, weight = 38f },
                new SwTestItemKindChance { itemKind = SwTestDropItemKind.Helmet, weight = 20f },
                new SwTestItemKindChance { itemKind = SwTestDropItemKind.Chest, weight = 20f },
                new SwTestItemKindChance { itemKind = SwTestDropItemKind.Boots, weight = 20f },
                new SwTestItemKindChance { itemKind = SwTestDropItemKind.Potion, weight = 2f },
            };
        }

        private void Reset()
        {
            ResetToDefaultTable();
        }

        private void OnValidate()
        {
            for (int i = 0; i < monsterDropRates.Count; i++)
            {
                if (monsterDropRates[i] == null)
                    continue;

                monsterDropRates[i].itemDropChance = Mathf.Clamp(monsterDropRates[i].itemDropChance, 0f, 100f);
                ClampRarityWeights(monsterDropRates[i].rarityChances);
            }

            for (int i = 0; i < itemKindChances.Count; i++)
            {
                if (itemKindChances[i] != null)
                    itemKindChances[i].weight = Mathf.Max(0f, itemKindChances[i].weight);
            }
        }

        private static SwTestMonsterDropRate CreateMonsterRule(
            EnemyGrade monsterGrade,
            float itemDropChance,
            float common,
            float advanced,
            float rare,
            float unique,
            float legendary)
        {
            return new SwTestMonsterDropRate
            {
                monsterGrade = monsterGrade,
                itemDropChance = itemDropChance,
                rarityChances = new List<SwTestRarityChance>
                {
                    new SwTestRarityChance { rarity = ItemRarity.Common, weight = common },
                    new SwTestRarityChance { rarity = ItemRarity.Advanced, weight = advanced },
                    new SwTestRarityChance { rarity = ItemRarity.Rare, weight = rare },
                    new SwTestRarityChance { rarity = ItemRarity.Unique, weight = unique },
                    new SwTestRarityChance { rarity = ItemRarity.Legendary, weight = legendary },
                }
            };
        }

        private static void ClampRarityWeights(List<SwTestRarityChance> rarityChances)
        {
            if (rarityChances == null)
                return;

            for (int i = 0; i < rarityChances.Count; i++)
            {
                if (rarityChances[i] != null)
                    rarityChances[i].weight = Mathf.Max(0f, rarityChances[i].weight);
            }
        }
    }
}

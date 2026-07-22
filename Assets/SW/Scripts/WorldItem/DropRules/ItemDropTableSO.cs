using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemDropTable",
    menuName = "Item/Drop Table")]
public sealed class ItemDropTableSO : ScriptableObject
{
    [Header("적 등급별 드랍 규칙")]
    [SerializeField]
    private List<ItemDropTypes.EnemyItemDropRule> enemyDropRules =
        new List<ItemDropTypes.EnemyItemDropRule>();

    [Header("아이템 종류별 가중치")]
    [SerializeField]
    private List<ItemDropTypes.ItemDropKindWeight> itemKindWeights =
        new List<ItemDropTypes.ItemDropKindWeight>();

    public IReadOnlyList<ItemDropTypes.ItemDropKindWeight> ItemKindWeights => itemKindWeights;

    public bool TryGetEnemyRule(EnemyGrade enemyGrade, out ItemDropTypes.EnemyItemDropRule rule)
    {
        if (enemyDropRules != null)
        {
            for (int i = 0; i < enemyDropRules.Count; i++)
            {
                ItemDropTypes.EnemyItemDropRule candidate = enemyDropRules[i];

                if (candidate == null || candidate.enemyGrade != enemyGrade)
                    continue;

                rule = candidate;
                return true;
            }
        }

        rule = null;
        return false;
    }

    private void OnValidate()
    {
        if (enemyDropRules != null)
        {
            for (int i = 0; i < enemyDropRules.Count; i++)
            {
                ItemDropTypes.EnemyItemDropRule rule = enemyDropRules[i];

                if (rule == null)
                    continue;

                rule.itemDropChance = Mathf.Clamp(rule.itemDropChance, 0f, 100f);

                ClampRarityWeights(rule.rarityWeights);
            }
        }

        ClampItemKindWeights(itemKindWeights);
    }

    private static void ClampRarityWeights(
        List<ItemDropTypes.ItemDropRarityWeight> weights)
    {
        if (weights == null)
            return;

        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i] == null)
                continue;

            weights[i].weight = Mathf.Max(0f, weights[i].weight);
        }
    }

    private static void ClampItemKindWeights(
        List<ItemDropTypes.ItemDropKindWeight> weights)
    {
        if (weights == null)
            return;

        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i] == null)
                continue;

            weights[i].weight = Mathf.Max(0f, weights[i].weight);
        }
    }
}
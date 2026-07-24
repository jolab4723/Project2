using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class ItemDropTableJsonData
    {
        public List<EnemyDropRuleRow> enemyDropRules = new List<EnemyDropRuleRow>();
        public List<RarityWeightRow> rarityWeights = new List<RarityWeightRow>();
        public List<ItemKindWeightRow> itemKindWeights = new List<ItemKindWeightRow>();
    }

    /// <summary>적 등급 하나당 한 줄. 세부 희귀도 가중치는 RarityWeightRow 시트에 enemyGrade로 연결된다.</summary>
    [Serializable]
    public class EnemyDropRuleRow
    {
        public string enemyGrade;
        public float itemDropChance;
    }

    /// <summary>적 등급 + 희귀도 하나당 한 줄 (EnemyDropRuleRow에 enemyGrade로 종속).</summary>
    [Serializable]
    public class RarityWeightRow
    {
        public string enemyGrade;
        public string rarity;
        public float weight;
    }

    /// <summary>아이템 종류 하나당 한 줄. 적 등급과 무관하게 테이블 전체에 공통 적용된다.</summary>
    [Serializable]
    public class ItemKindWeightRow
    {
        public string itemKind;
        public float weight;
    }
}

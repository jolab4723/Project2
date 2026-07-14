using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class ItemTableJsonData
    {
        public List<SubStatPoolRow> subStatPools = new List<SubStatPoolRow>();

        // 딱 하나만 쓰는 전역 설정이라 리스트가 아니라 단일 객체.
        public ElementalBonusConfigRow elementalBonusConfig;
    }

    /// <summary>
    /// 서브스탯 옵션 한 줄. 기존엔 SubStatPools(poolId+optionId 참조) + OptionDefinitions(실제 값) 두 시트로
    /// 나뉘어 있었는데, 이제 한 줄에 다 담는 구조로 바뀜.
    /// </summary>
    [Serializable]
    public class SubStatPoolRow
    {
        public string statPoolType; // "Combat" / "Utility"
        public string statType;
        public string optionName;
        public float minValue;
        public float maxValue;
    }

    /// <summary>전역 원소 보너스 설정. ID 없음 - 프로젝트 전체에서 딱 하나만 쓴다.</summary>
    [Serializable]
    public class ElementalBonusConfigRow
    {
        public float elementBonusValue;
        public float atkFallbackValue;
        public float missChance;
    }
}

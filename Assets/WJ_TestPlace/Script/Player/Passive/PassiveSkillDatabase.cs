using System;
using UnityEngine;

/// <summary>
/// 패시브 스킬 하나의 디자인 데이터(이름/최대 레벨/레벨별 수치/해금 비용). 세이브 데이터가 아니라 고정된 밸런스 값.
/// valuesPerLevel[level - 1] = 그 레벨에서의 효과 수치(대부분 %).
/// extraRerollCount는 ShopEnhance 전용 부가 효과(상점 리롤 횟수 추가), 그 외 스킬은 0.
///
/// !! 해금 비용 공식: GetUnlockCost(level) = baseCost * level (레벨이 올라갈수록 그 레벨 하나의 비용도 커짐).
///    정확한 밸런스 수치는 아직 안 정해져서 baseCost는 임시값. 나중에 정확한 값 받으면 교체.
///
/// !! PassiveSkillDatabaseSO의 리스트 항목으로 직렬화되므로 [Serializable]이 필요하다.
/// </summary>
[Serializable]
public class PassiveSkillDefinition
{
    public Core.PassiveSkillId id;
    public string displayName;
    [TextArea]
    [Tooltip("스킬 설명(한국어 기본값). 다국어는 PassiveSkillLabelDatabaseSO.GetDescription이 우선하고, 없으면 이 값으로 폴백한다.")]
    public string description;
    public int maxLevel;
    public float[] valuesPerLevel;
    public int extraRerollCount;
    public int baseCost;

    public float GetValue(int level)
    {
        if (level <= 0 || level > valuesPerLevel.Length)
            return 0f;

        return valuesPerLevel[level - 1];
    }

    /// <summary>level 그 자체(한 단계)를 해금하는 데 드는 골드. level은 1부터.</summary>
    public int GetUnlockCost(int level)
    {
        return baseCost * level;
    }
}

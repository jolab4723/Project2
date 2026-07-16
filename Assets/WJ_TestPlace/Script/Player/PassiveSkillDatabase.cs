using System.Collections.Generic;
using Core;

/// <summary>
/// 패시브 스킬 하나의 디자인 데이터(이름/최대 레벨/레벨별 수치/해금 비용). 세이브 데이터가 아니라 고정된 밸런스 값.
/// valuesPerLevel[level - 1] = 그 레벨에서의 효과 수치(대부분 %).
/// extraRerollCount는 ShopEnhance 전용 부가 효과(상점 리롤 횟수 추가), 그 외 스킬은 0.
///
/// !! 해금 비용 공식: GetUnlockCost(level) = baseCost * level (레벨이 올라갈수록 그 레벨 하나의 비용도 커짐).
///    정확한 밸런스 수치는 아직 안 정해져서 baseCost는 임시값. 나중에 정확한 값 받으면 교체.
/// </summary>
public class PassiveSkillDefinition
{
    public PassiveSkillId id;
    public string displayName;
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

/// <summary>총 12개 패시브 스킬의 고정 디자인 데이터. 밸런스 수치를 여기서만 관리한다.</summary>
public static class PassiveSkillDatabase
{
    public static readonly Dictionary<PassiveSkillId, PassiveSkillDefinition> Definitions = new Dictionary<PassiveSkillId, PassiveSkillDefinition>
    {
        { PassiveSkillId.MaxHealth, new PassiveSkillDefinition { id = PassiveSkillId.MaxHealth, displayName = "최대 체력 증가", maxLevel = 5, valuesPerLevel = new float[] { 3f, 6f, 9f, 12f, 15f }, baseCost = 100 } },
        { PassiveSkillId.AttackPower, new PassiveSkillDefinition { id = PassiveSkillId.AttackPower, displayName = "공격력 증가", maxLevel = 5, valuesPerLevel = new float[] { 3f, 6f, 9f, 12f, 15f }, baseCost = 100 } },
        { PassiveSkillId.DefensePower, new PassiveSkillDefinition { id = PassiveSkillId.DefensePower, displayName = "방어력 증가", maxLevel = 5, valuesPerLevel = new float[] { 3f, 6f, 9f, 12f, 15f }, baseCost = 100 } },
        { PassiveSkillId.AllSpeed, new PassiveSkillDefinition { id = PassiveSkillId.AllSpeed, displayName = "모든 속도 증가", maxLevel = 5, valuesPerLevel = new float[] { 3f, 6f, 9f, 12f, 15f }, baseCost = 100 } },

        { PassiveSkillId.CritRate, new PassiveSkillDefinition { id = PassiveSkillId.CritRate, displayName = "크리티컬 확률 증가", maxLevel = 3, valuesPerLevel = new float[] { 5f, 10f, 15f }, baseCost = 200 } },
        { PassiveSkillId.CritDamage, new PassiveSkillDefinition { id = PassiveSkillId.CritDamage, displayName = "크리티컬 피해 증가", maxLevel = 3, valuesPerLevel = new float[] { 10f, 20f, 30f }, baseCost = 200 } },
        { PassiveSkillId.CooldownReduction, new PassiveSkillDefinition { id = PassiveSkillId.CooldownReduction, displayName = "스킬 쿨타임 감소", maxLevel = 3, valuesPerLevel = new float[] { 10f, 15f, 20f }, baseCost = 200 } },
        { PassiveSkillId.AllElementalBonus, new PassiveSkillDefinition { id = PassiveSkillId.AllElementalBonus, displayName = "모든 속성 보너스 증가", maxLevel = 3, valuesPerLevel = new float[] { 10f, 15f, 25f }, baseCost = 200 } },

        { PassiveSkillId.Revive, new PassiveSkillDefinition { id = PassiveSkillId.Revive, displayName = "부활 1회 활성화", maxLevel = 1, valuesPerLevel = new float[] { 20f }, baseCost = 1000 } },
        { PassiveSkillId.CampHealBonus, new PassiveSkillDefinition { id = PassiveSkillId.CampHealBonus, displayName = "캠프 회복량 증가", maxLevel = 1, valuesPerLevel = new float[] { 40f }, baseCost = 1000 } },
        { PassiveSkillId.ShopEnhance, new PassiveSkillDefinition { id = PassiveSkillId.ShopEnhance, displayName = "상점 강화", maxLevel = 1, valuesPerLevel = new float[] { 10f }, extraRerollCount = 1, baseCost = 1000 } },
        { PassiveSkillId.Undecided, new PassiveSkillDefinition { id = PassiveSkillId.Undecided, displayName = "미정", maxLevel = 1, valuesPerLevel = new float[] { 0f }, baseCost = 500 } },
    };

    public static PassiveSkillDefinition Get(PassiveSkillId id)
    {
        return Definitions.TryGetValue(id, out var def) ? def : null;
    }
}

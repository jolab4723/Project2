using UnityEngine;

public interface WBH_ICombatStatus
{
    float CurrentHp { get; }
    float MaxHealth { get; }
    float AttackPower { get; }
    float DefensePower { get; }
    float CritRate { get; }
    float CritMult { get; }
    float Pen { get; }
    float FireBonus { get; }
    float IceBonus { get; }
    float ElectricBonus { get; }
    /// <summary>Marked 등 "받는 데미지 배율" 상태이상의 최종 배율. 없으면 1(영향 없음).</summary>
    float DamageTakenModifier { get; }
    /// <summary>일반공격(WBH_AttackType.Normal)으로 "가하는 피해" 배율. 없으면 1(영향 없음).</summary>
    float NormalDamageModifier { get; }
    /// <summary>스킬(WBH_AttackType.Skill)로 "가하는 피해" 배율. 없으면 1(영향 없음).</summary>
    float SkillDamageModifier { get; }
    bool IsDead { get; }
}

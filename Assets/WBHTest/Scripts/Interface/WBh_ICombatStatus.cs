using UnityEngine;

public interface WBH_ICombatStatus
{
    float CurrentHp { get; }
    float MaxHealth { get; }
    float AttackPower { get; }
    float DefensePower { get; }
    float CritRate { get; }
    float CritMult { get; }
    float FireBonus { get; }
    float IceBonus { get; }
    float ElectricBonus { get; }
    bool IsDead { get; }
}

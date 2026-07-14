using UnityEngine;

public interface WBH_ICombatStatus
{
    float CurrentHp { get; }
    float MaxHp { get; }
    float Attack { get; }
    float Defense { get; }
    float CriticalChace { get; }
    float CriticalMultiplier { get; }
    float FireBonus { get; }
    float IceBonus { get; }
    float ElectricBonus { get; }
}

using UnityEngine;

public interface WBH_ICombat
{
    WBH_ICombatStatus Status { get; }

    void TakeDamage(WBH_DamageResult result);
}

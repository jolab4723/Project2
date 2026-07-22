using ItemSystem;
using UnityEngine;

public readonly struct WBH_DamageRequest
{
    public readonly WBH_ICombat Attacker;
    public readonly WBH_ICombat Target;

    public readonly WBH_AttackType AttackType;
    public readonly ElementType ElementType;

    // 스킬 데미지 계수
    public readonly float DamageMultiplier;

    public WBH_DamageRequest(WBH_ICombat attacker,
                             WBH_ICombat target,
                             WBH_AttackType attackType,
                             ElementType elementType,
                             float damageMultiplier)
    {
        Attacker = attacker;
        Target = target;
        AttackType = attackType;
        ElementType = elementType;
        DamageMultiplier = damageMultiplier;
    }
}

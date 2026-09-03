using ItemSystem;
using UnityEngine;

public readonly struct WBH_DamageRequest
{
    public readonly WBH_ICombat Attacker;
    public readonly WBH_ICombat Target;

    public readonly WBH_AttackType AttackType;
    public readonly ElementType ElementType;

    public readonly WBH_StatusEffectData? StatusEffect;
    public readonly WBH_EffectData EffectData;
    public readonly Vector3? HitPosition;
    public readonly Vector3? HitEffectDirection;

    // 스킬 데미지 계수
    public readonly float DamageMultiplier;

    public WBH_DamageRequest(WBH_ICombat attacker,
                             WBH_ICombat target,
                             WBH_AttackType attackType,
                             ElementType elementType,
                             float damageMultiplier,
                             WBH_StatusEffectData? statusEffect = null,
                             WBH_EffectData effectData = null,
                             Vector3? hitPosition = null,
                             Vector3? hitEffectDirection = null)
    {
        Attacker = attacker;
        Target = target;
        AttackType = attackType;
        ElementType = elementType;
        DamageMultiplier = damageMultiplier;
        StatusEffect = statusEffect;
        EffectData = effectData;
        HitPosition = hitPosition;
        HitEffectDirection = hitEffectDirection;
    }
}

using ItemSystem;
using UnityEngine;

public enum DamageCause : byte
{
    Direct = 0,
    Skill = 1,
    Effect = 2,
    DoT = 3,
}

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
    public readonly DamageCause DamageCause;
    public readonly uint AttackId;

    // 스킬 데미지 계수
    public readonly float DamageMultiplier;

    public WBH_DamageRequest(WBH_ICombat attacker,
                             WBH_ICombat target,
                             WBH_AttackType attackType,
                             ElementType elementType,
                             float damageMultiplier,
                             WBH_StatusEffectData? statusEffect,
                             WBH_EffectData effectData,
                             Vector3? hitPosition,
                             Vector3? hitEffectDirection,
                             DamageCause damageCause,
                             uint attackId)
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
        DamageCause = damageCause;
        AttackId = attackId;
    }

    public WBH_DamageRequest(WBH_ICombat attacker,
                             WBH_ICombat target,
                             WBH_AttackType attackType,
                             ElementType elementType,
                             float damageMultiplier,
                             WBH_StatusEffectData? statusEffect = null,
                             WBH_EffectData effectData = null,
                             Vector3? hitPosition = null,
                             Vector3? hitEffectDirection = null,
                             DamageCause? damageCause = null,
                             uint attackId = 0)
        : this(attacker,
               target,
               attackType,
               elementType,
               damageMultiplier,
               statusEffect,
               effectData,
               hitPosition,
               hitEffectDirection,
               damageCause ?? (attackType == WBH_AttackType.Skill ? DamageCause.Skill : DamageCause.Direct),
               attackId)
    {
    }
}

using ItemSystem;
using UnityEngine;

/// <summary>
/// SW 수정: 피해가 직접 공격, 스킬, 고유효과 추가타, 지속 피해 중 어디서 발생했는지 구분합니다.
/// </summary>
public enum DamageCause : byte
{
    Direct = 0,
    Skill = 1,
    Effect = 2,
    DoT = 3,
}

/// <summary>
/// SW 수정: 기존 피해 요청에 원인과 공격 식별자를 함께 전달해 후속 고유효과의 재발동과 중복 요청을 구분합니다.
/// </summary>
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

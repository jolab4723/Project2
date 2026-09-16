using ItemSystem;
using UnityEngine;

/// <summary>
/// SW 수정: 처리된 피해 결과에도 원인과 공격 식별자를 보존해 이벤트와 처치 귀속에서 같은 정보를 사용합니다.
/// </summary>
public readonly struct WBH_DamageResult
{
    public readonly WBH_ICombat Attacker;
    public readonly float FinalDamage;
    public readonly bool IsCritical;
    public readonly WBH_StatusEffectData? StatusEffect;
    public readonly WBH_EffectData EffectData;
    public readonly Vector3? HitPosition;
    public readonly Vector3? HitEffectDirection;

    // -- 차후 넉백, 고정데미지 등 추가
    //-- 아직 미활용 상태이상 적용 시 활용하면 될 것
    public readonly ElementType ElementType;
    public readonly DamageCause DamageCause;
    public readonly uint AttackId;

    public WBH_DamageResult(WBH_ICombat attacker,
                            float finalDamage,
                            bool isCritical,
                            ElementType elementType,
                            WBH_StatusEffectData? statusEffect,
                            WBH_EffectData effectData,
                            Vector3? hitPosition,
                            Vector3? hitEffectDirection,
                            DamageCause damageCause,
                            uint attackId)
    {
        Attacker = attacker;
        FinalDamage = finalDamage;
        IsCritical = isCritical;
        ElementType = elementType;
        StatusEffect = statusEffect;
        EffectData = effectData;
        HitPosition = hitPosition;
        HitEffectDirection = hitEffectDirection;
        DamageCause = damageCause;
        AttackId = attackId;
    }

    public WBH_DamageResult(WBH_ICombat attacker,
                            float finalDamage,
                            bool isCritical,
                            ElementType elementType,
                            WBH_StatusEffectData? statusEffect = null,
                            WBH_EffectData effectData = null,
                            Vector3? hitPosition = null,
                            Vector3? hitEffectDirection = null)
        : this(attacker,
               finalDamage,
               isCritical,
               elementType,
               statusEffect,
               effectData,
               hitPosition,
               hitEffectDirection,
               DamageCause.Direct,
               0)
    {
    }
}

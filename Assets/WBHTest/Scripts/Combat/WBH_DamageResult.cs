using ItemSystem;
using UnityEngine;

public readonly struct WBH_DamageResult
{
    public readonly WBH_ICombat Attacker;
    public readonly float FinalDamage;
    public readonly bool IsCritical;
    public readonly WBH_StatusEffectData? StatusEffect;
    public readonly WBH_EffectData EffectData;

    // -- 차후 넉백, 고정데미지 등 추가
    //-- 아직 미활용 상태이상 적용 시 활용하면 될 것
    public readonly ElementType ElementType;

    public WBH_DamageResult(WBH_ICombat attacker,
                            float finalDamage,
                            bool isCritical,
                            ElementType elementType,
                            WBH_StatusEffectData? statusEffect = null,
                            WBH_EffectData effectData = null)
    {
        Attacker = attacker;
        FinalDamage = finalDamage;
        IsCritical = isCritical;
        ElementType = elementType;
        StatusEffect = statusEffect;
        EffectData = effectData;
    }
}

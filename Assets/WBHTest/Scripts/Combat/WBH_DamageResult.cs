using UnityEngine;

public readonly struct WBH_DamageResult
{
    public readonly WBH_ICombat Attacker;
    public readonly float FinalDamage;
    public readonly bool IsCritical;

    // -- 차후 넉백, 고정데미지 등 추가
    //-- 아직 미활용 상태이상 적용 시 활용하면 될 것
    public readonly WBH_ElementalType ElementalType;

    public WBH_DamageResult(WBH_ICombat attacker,
                            float finalDamage,
                            bool isCritical,
                            WBH_ElementalType elementalType)
    {
        Attacker = attacker;
        FinalDamage = finalDamage;
        IsCritical = isCritical;
        ElementalType = elementalType;
    }
}

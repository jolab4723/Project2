using ItemSystem;
using UnityEngine;

public class WBH_CombatManager
{
    public static void ProcessDamage(WBH_DamageRequest request)
    {
        if (request.Attacker == null || request.Target == null)
            return;
        WBH_ICombatStatus attackerStat = request.Attacker.Status;
        WBH_ICombatStatus targetStat = request.Target.Status;

        float damage = CalculateBaseDamage(attackerStat, request); // 1차 데미지 계산

        // 속성 데미지 계산
        damage = CalculateElementDamage(damage, attackerStat, request.ElementType);

        // 크리티컬 여부
        bool isCritical = CalculateCritical(attackerStat);

        // 크리티컬 데미지 적용
        if(isCritical)
            damage *= attackerStat.CritMult;

        // 타겟 방어력 적용
        damage = CalculateDefense(damage, targetStat);

        // 최소 데미지 보장
        damage = Mathf.Max(1f, damage);

        // 데미지 결과 구조체 생성
        WBH_DamageResult result = new WBH_DamageResult(request.Attacker, damage, isCritical, request.ElementType);

        request.Target.TakeDamage(result);

        if(!request.Target.Status.IsDead && request.StatusEffect.HasValue)
        {
            request.Target.AddStatusEffect(request.StatusEffect.Value);
        }
    }


    private static float CalculateBaseDamage(WBH_ICombatStatus attackerStat, WBH_DamageRequest request)
    {
        return attackerStat.AttackPower * request.DamageMultiplier;
    }

    private static float CalculateElementDamage(float damage, WBH_ICombatStatus attackerStat, ElementType elementType)
    {
        float bonus = GetElementBonus(attackerStat, elementType);

        return damage * (1f + bonus);
    }

    private static float GetElementBonus(WBH_ICombatStatus attackerStat, ElementType elementType)
    {
        switch(elementType)
        {
            case ElementType.Fire:
                return attackerStat.FireBonus;
            case ElementType.Ice:
                return attackerStat.IceBonus;
            case ElementType.Electric:
                return attackerStat.ElectricBonus;
            default:
                return 0f;
        }
    }

    private static bool CalculateCritical(WBH_ICombatStatus attackerStat)
    {
        return Random.value <= attackerStat.CritRate;
    }

    private static float CalculateDefense(float damage, WBH_ICombatStatus targetStat)
    {
        return damage - targetStat.DefensePower;
    }

    private static void ApplyStatusEffect(WBH_DamageRequest request)
    {
        if (!request.StatusEffect.HasValue)
            return;

        WBH_StatusEffectData data = request.StatusEffect.Value;
    }
}

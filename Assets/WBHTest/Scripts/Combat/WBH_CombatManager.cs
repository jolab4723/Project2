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
        damage = CalculateDefense(damage, targetStat, attackerStat);

        // Marked 등 "받는 데미지 배율" 상태이상 적용(없으면 1, 영향 없음) - 공격자가 누구든(플레이어/적
        // 스킬 구분 없이) 이 지점 하나만 거치면 다 적용되도록 파이프라인 끝쪽에 둠(118번, WJ 이우진)
        damage *= targetStat.DamageTakenModifier;

        // 최소 데미지 보장
        damage = Mathf.Max(1f, damage);

        // 데미지 결과 구조체 생성
        WBH_DamageResult result = new WBH_DamageResult(request.Attacker, damage, isCritical, request.ElementType);

        request.Target.TakeDamage(result);

        // 플레이어가 가한 피해일 때만 플레이어 장착템의 발동형 고유 효과를 건드린다.
        // (적이 다른 적을 때리거나 적이 플레이어를 때릴 때는 이 매니저를 공유해서 쓰므로 여기서 걸러야 함)
        // !! WBH_DamageRequest.Attacker는 T_PlayerCombat.CreateDamageRequest가 controller(T_PlayerController)를
        //    넘기므로 T_PlayerCombat이 아니라 T_PlayerController로 들어온다.
        if (request.Attacker is T_PlayerController)
        {
            ItemTriggerManager.Instance?.Fire(TriggerCondition.OnDamageDealt);

            if (isCritical)
                ItemTriggerManager.Instance?.Fire(TriggerCondition.OnCrit);
        }

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

    private static float CalculateDefense(float damage, WBH_ICombatStatus targetStat, WBH_ICombatStatus attackerStat)
    {
        return damage - (targetStat.DefensePower - attackerStat.Pen);
    }

    private static void ApplyStatusEffect(WBH_DamageRequest request)
    {
        if (!request.StatusEffect.HasValue)
            return;

        WBH_StatusEffectData data = request.StatusEffect.Value;
    }
}

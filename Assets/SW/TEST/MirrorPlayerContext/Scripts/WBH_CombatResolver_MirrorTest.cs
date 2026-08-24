using ItemSystem;
using UnityEngine;

/// <summary>
/// BH 원본 <c>WBH_CombatManager</c>의 Mirror 전투 검증용 계산기다.
/// <para>원본: <c>Assets/WBHTest/Scripts/Combat/WBH_CombatManager.cs</c></para>
/// <para>데미지 공식은 유지하고, 플레이어 발동 효과만 전역 <c>ItemTriggerManager.Instance</c> 대신
/// 공격자의 <c>PlayerContext.ItemTriggers</c>에 전달한다.</para>
/// <para>서버에서만 호출하며 컴포넌트나 인터페이스를 네트워크 메시지로 직렬화하지 않는다.</para>
/// </summary>
public static class WBH_CombatResolver_MirrorTest
{
    public static bool TryProcessPlayerDamage(
        PlayerContext attacker,
        WBH_ICombat target,
        ElementType elementType,
        float damageMultiplier,
        WBH_StatusEffectData? statusEffect,
        out WBH_DamageResult result)
    {
        result = default;

        if (attacker?.Controller == null || target == null || damageMultiplier <= 0f)
            return false;

        WBH_ICombatStatus attackerStatus = attacker.Controller.Status;
        WBH_ICombatStatus targetStatus = target.Status;
        if (attackerStatus == null || targetStatus == null || attackerStatus.IsDead || targetStatus.IsDead)
            return false;

        float damage = attackerStatus.AttackPower * damageMultiplier;
        damage *= 1f + GetElementBonus(attackerStatus, elementType);

        bool isCritical = Random.value <= attackerStatus.CritRate;
        if (isCritical)
            damage *= attackerStatus.CritMult;

        damage -= targetStatus.DefensePower - attackerStatus.Pen;
        damage = Mathf.Max(1f, damage);

        result = new WBH_DamageResult(
            attacker.Controller,
            damage,
            isCritical,
            elementType,
            statusEffect);

        target.TakeDamage(result);
        attacker.ItemTriggers?.Fire(TriggerCondition.OnDamageDealt);
        if (isCritical)
            attacker.ItemTriggers?.Fire(TriggerCondition.OnCrit);

        if (!target.Status.IsDead && statusEffect.HasValue)
        {
            if (target is Component targetComponent &&
                targetComponent.TryGetComponent(out NetworkEnemyAuthority_MirrorTest networkEnemy))
            {
                networkEnemy.ServerTryApplyStatusEffect(statusEffect.Value);
            }
            else
            {
                target.AddStatusEffect(statusEffect.Value);
            }
        }

        return true;
    }

    private static float GetElementBonus(WBH_ICombatStatus status, ElementType elementType)
    {
        return elementType switch
        {
            ElementType.Fire => status.FireBonus,
            ElementType.Ice => status.IceBonus,
            ElementType.Electric => status.ElectricBonus,
            _ => 0f,
        };
    }
}

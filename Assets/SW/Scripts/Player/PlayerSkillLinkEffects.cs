using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>SW 추가: 스킬 연계 고유효과(시전 트리거, 처치 시 쿨타임 감소, 표식 추가 피해, 원소 과부하)의 싱글·서버 공통 규칙이다.</summary>
public sealed partial class PlayerItemEffectState
{
    // 서로 다른 원소 두 종 이상이 겹친 대상의 공명 폭발 피해 배율. 협동 원소 조합 보상이다.
    private const float ResonatorDualElementMultiplier = 1.5f;
    private readonly Dictionary<WBH_ICombat, double> resonatorReadyAt = new();

    /// <summary>스킬 시전 확정 한 번에 OnSkillCast 효과를 슬롯 필터로 발동한다. 트리거 관리자(싱글·서버)가 호출한다.</summary>
    public void FireSkillCast(in SkillCastInfo info)
    {
        if (!CanExecute) return;
        Fire(TriggerCondition.OnSkillCast, new TriggerContext(false, info.Slot));
    }

    /// <summary>처치 시 내부 쿨타임이 끝났으면 지정 슬롯의 쿨타임을 줄인다. 같은 효과 사본은 쿨타임을 공유해 중첩되지 않는다.</summary>
    private void FireSkillCooldownOnKill(SkillCooldownOnKillUniqueEffectSO effect)
    {
        if (!CanExecute || effect == null || !float.IsFinite(effect.reduceSeconds) || effect.reduceSeconds <= 0f ||
            effect.skillSlot < 1)
            return;
        ISkillController skills = context.Skills;
        if (skills == null || effect.skillSlot > skills.SkillCount)
            return;
        string key = GetCooldownKey(effect, null);
        double now = Now;
        if (IsCoolingDown(key, now))
            return;
        SetCooldownEnd(key, now + Mathf.Max(0f, effect.cooldownSeconds));
        skills.ReduceCooldown(effect.skillSlot - 1, effect.reduceSeconds);
    }

    /// <summary>대상에 표식이 있으면 소유한 표식 추가 피해 효과의 배율을 돌려준다. 모든 피해 출처에 같은 규칙을 적용한다.</summary>
    internal float GetMarkedDamageMultiplier(WBH_ICombat target)
    {
        // 피해마다 불리므로 싼 대상 상태 검사를 먼저 하고, 표식이 있을 때만 장비·유물을 훑는다.
        if (!CanExecute || target is not Component component || component == null ||
            component.GetComponentInParent<WBH_StatusEffectController>() is not { } statuses ||
            !statuses.HasStatusEffect(WBH_StatusEffectType.Marked) ||
            !TryFindOwnedEffect(out MarkedDamageBonusUniqueEffectSO effect) ||
            !float.IsFinite(effect.bonusFraction) || effect.bonusFraction <= 0f)
            return 1f;
        return 1f + effect.bonusFraction;
    }

    /// <summary>화상·빙결·감전 중 하나 이상이 걸린 적을 적중하면 그 위치에 무속성 폭발을 후속 피해 큐에 등록한다. 서로 다른 두 가지 이상이면 피해를 키운다. 대상마다 재발동 대기시간을 둔다.</summary>
    private void TryFireElementResonator(in WBH_DamageResult result, WBH_ICombat firstTarget)
    {
        // 무기 속성은 하나뿐이라 두 원소 조건만 두면 혼자서는 발동하지 않는다. 한 종으로 발동하고, 동료 원소가 겹치면 강화한다.
        int elementCount;
        if (firstTarget is not Component component || component == null || firstTarget.Status == null ||
            component.GetComponentInParent<WBH_StatusEffectController>() is not { } statuses ||
            (elementCount = CountElementStatuses(statuses)) < 1 ||
            !TryFindOwnedEffect(out ElementResonatorUniqueEffectSO effect) ||
            !float.IsFinite(effect.radius) || effect.radius <= 0f ||
            !float.IsFinite(effect.damageMultiplier) || effect.damageMultiplier <= 0f)
            return;

        double now = Now;
        if (resonatorReadyAt.TryGetValue(firstTarget, out double readyAt) && now < readyAt)
            return;
        if (resonatorReadyAt.Count > 64)
            PruneResonatorTargets(now);
        resonatorReadyAt[firstTarget] = now + Mathf.Max(0f, effect.perTargetCooldownSeconds);

        Vector3 center = GetBodyPosition(component);
        Vector3 sightStart = center + Vector3.up;
        List<WBH_ICombat> targets = CollectLivingBodies(
            Physics.OverlapSphere(sightStart, effect.radius, EnemyLayerMask, QueryTriggerInteraction.Collide), sightStart,
            position =>
            {
                Vector3 offset = position - center;
                return Mathf.Abs(offset.y) <= 0.5f && offset.sqrMagnitude <= effect.radius * effect.radius;
            });
        targets.Sort((left, right) => CompareBodyDistance(left, right, center));
        float multiplier = effect.damageMultiplier * (elementCount >= 2 ? ResonatorDualElementMultiplier : 1f);
        // 기점 대상은 직접 피해를 받은 뒤라 사망했을 수 있어 CollectLivingBodies가 이미 거른다.
        for (int index = 0; index < Mathf.Min(targets.Count, Mathf.Clamp(effect.maxTargets, 1, 16)); index++)
            PlayerDamageResolver.EnqueueFollowUpDamage(context, targets[index], ElementType.None,
                multiplier, null, DamageCause.Effect, result.AttackId, canCrit: false,
                effectKey: "element-resonator");
        ElementResonatorPresented?.Invoke(center, effect.radius);
    }

    private static int CountElementStatuses(WBH_StatusEffectController statuses) =>
        (statuses.HasStatusEffect(WBH_StatusEffectType.Burn) ? 1 : 0) +
        (statuses.HasStatusEffect(WBH_StatusEffectType.Freeze) ? 1 : 0) +
        (statuses.HasStatusEffect(WBH_StatusEffectType.Electric) ? 1 : 0);

    private void PruneResonatorTargets(double now)
    {
        var expired = new List<WBH_ICombat>();
        foreach (KeyValuePair<WBH_ICombat, double> pair in resonatorReadyAt)
            if (now >= pair.Value || pair.Key is not Component component || component == null)
                expired.Add(pair.Key);
        foreach (WBH_ICombat target in expired)
            resonatorReadyAt.Remove(target);
    }

    /// <summary>장착 장비와 인벤토리 유물에서 해당 유형의 첫 효과를 찾는다. 같은 효과 사본은 중첩하지 않는다(패시브 1회 적용 규칙과 같음).</summary>
    private bool TryFindOwnedEffect<T>(out T effect) where T : UniqueEffectSO
    {
        effect = null;
        if (inventory?.EquipmentSystem != null)
            foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in inventory.EquipmentSystem.GetEquippedItems())
                if (pair.Value?.itemData?.definition?.uniqueEffect is T equipped)
                {
                    effect = equipped;
                    return true;
                }
        if (inventory?.PlayerGrid == null)
            return false;
        foreach (InventoryItem item in inventory.PlayerGrid.GetAllItems())
            if (item?.itemData?.definition?.category == ItemCategory.Relic &&
                item.itemData.definition.uniqueEffect is T relic)
            {
                effect = relic;
                return true;
            }
        return false;
    }
}

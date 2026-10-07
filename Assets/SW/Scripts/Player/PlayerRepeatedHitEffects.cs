using ItemSystem;
using UnityEngine;

/// <summary>SW 수정 : 같은 소유자의 코어 노출·냉각 누적만 보관하고 기존 적 상태이상 경계로 적용한다.</summary>
public sealed partial class PlayerItemEffectState
{
    // 이 무기의 기본 냉기 상태는 아래 누적 냉각 규칙이 대체한다.
    public bool UsesRepeatedCooling(in WBH_DamageRequest request)
        => request.AttackType == WBH_AttackType.Normal && request.DamageCause == DamageCause.Direct &&
           TryGetRepeatedHitWeapon(out ItemInstance weapon) &&
           weapon.definition.uniqueEffect is SuperRefrigerantFreezeUniqueEffectSO;

    private WBH_EnemyController repeatedHitTarget;
    private EnemyFreezeRecovery repeatedHitRecovery;
    private uint repeatedHitTargetLifetime;
    private UniqueEffectSO repeatedHitEffect;
    private string repeatedHitItemInstanceId;
    private uint lastRepeatedHitAttackId;
    private int repeatedHitCount;
    private double repeatedHitWindowEndsAt;

    /// <summary>SW 수정 : 냉각 누적과 빙결 성공·취소 상태를 기존 적 표시 경로로 전달하며 실제 상태 판정은 바꾸지 않는다.</summary>
    private void PresentCooling(WBH_ICombat target, int count)
    {
        var component = target as Component;
        var recovery = component != null ? component.GetComponentInParent<EnemyFreezeRecovery>() : null;
        if (recovery != null)
            recovery.PresentCooling(context, count, Now, count >= 3 ? 0.6f : 3f);
    }

    /// <summary>SW 수정 : 싱글·서버의 살아 있는 실제 Fighter 기본 적중만 대상으로 같은 적·다른 공격 번호를 누적한다.</summary>
    private void ResolveRepeatedHitEffects(in WBH_DamageResult result, WBH_ICombat firstTarget)
    {
        ClearRepeatedHitEffects();
        if (result.DamageCause != DamageCause.Direct || result.AttackId == 0 ||
            !IsDirectTargetForAttack(result.AttackId, firstTarget))
            return;
        if (!TryGetRepeatedHitWeapon(out ItemInstance weapon) || firstTarget is not Component component)
        {
            ClearRepeatedHitEffects(force: true);
            return;
        }

        WBH_EnemyController enemy = component.GetComponentInParent<WBH_EnemyController>();
        if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.Info == null ||
            enemy.Status == null || enemy.Status.IsDead)
        {
            ClearRepeatedHitEffects(force: true);
            return;
        }
        EnemyFreezeRecovery recovery = enemy.GetComponent<EnemyFreezeRecovery>() ??
            enemy.gameObject.AddComponent<EnemyFreezeRecovery>();
        UniqueEffectSO effect = weapon.definition.uniqueEffect;
        if (repeatedHitTarget != enemy || repeatedHitEffect != effect ||
            repeatedHitItemInstanceId != weapon.instanceId || repeatedHitTargetLifetime != recovery.Lifetime)
        {
            ClearRepeatedHitEffects(force: true);
            repeatedHitTarget = enemy;
            repeatedHitRecovery = recovery;
            repeatedHitTargetLifetime = recovery.Lifetime;
            repeatedHitEffect = effect;
            repeatedHitItemInstanceId = weapon.instanceId;
        }
        if (lastRepeatedHitAttackId == result.AttackId)
            return;
        if (!IsRepeatedHitLifetimeValid())
        {
            ClearRepeatedHitEffects(force: true);
            return;
        }
        lastRepeatedHitAttackId = result.AttackId;
        double now = Now;
        bool boss = enemy.Info.enemyGrade == EnemyGrade.Boss;

        var cooling = effect as SuperRefrigerantFreezeUniqueEffectSO;
        if (cooling != null)
        {
            var slow = new WBH_StatusEffectData(WBH_StatusEffectType.Slow,
                cooling.slowDurationSeconds, Mathf.Clamp01(cooling.slowMultiplier))
            {
                Attacker = context.Controller,
                AttackId = result.AttackId
            };
            bool slowed = TryApplyRepeatedHitStatus(enemy, slow);
            if (!slowed || boss || !recovery.CanPrepareFreeze(now))
            {
                repeatedHitCount = 0;
                repeatedHitWindowEndsAt = 0d;
                PresentCooling(enemy, 0);
                return;
            }
        }
        if (!IsRepeatedHitLifetimeValid())
        {
            ClearRepeatedHitEffects(force: true);
            return;
        }

        var expose = effect as CoreBreakerExposeUniqueEffectSO;
        int requiredHits = Mathf.Max(1, expose != null ? expose.requiredHits : cooling.requiredHits);
        float hitWindow = expose != null ? expose.hitWindowSeconds : cooling.hitWindowSeconds;
        if (repeatedHitCount == 0)
            repeatedHitWindowEndsAt = now + Mathf.Max(0.1f, hitWindow);
        repeatedHitCount++;
        if (repeatedHitCount < requiredHits)
        {
            if (cooling != null)
                PresentCooling(enemy, repeatedHitCount);

            return;
        }
        // SW 수정 : 설정된 필수 적중 수에 도달하면 성공·면역 여부와 무관하게 누적만 비우고 같은 공격 번호는 다시 세지 않는다.
        repeatedHitCount = 0;
        repeatedHitWindowEndsAt = 0d;
        if (expose != null)
        {
            float reduction = boss ? expose.bossDefenseReduction : expose.defenseReduction;
            var weakened = new WBH_StatusEffectData(WBH_StatusEffectType.DefenseDown,
                expose.exposeDurationSeconds, 1f - Mathf.Clamp01(reduction))
            {
                Attacker = context.Controller,
                AttackId = result.AttackId
            };
            TryApplyRepeatedHitStatus(enemy, weakened);
            return;
        }

        var freeze = new WBH_StatusEffectData(WBH_StatusEffectType.Freeze, cooling.freezeDurationSeconds, 0f)
        {
            Attacker = context.Controller,
            AttackId = result.AttackId
        };
        bool frozen = recovery.CanPrepareFreeze(now) && TryApplyRepeatedHitStatus(enemy, freeze);
        if (frozen)
            recovery.BeginRecovery(now, cooling.freezeRecoverySeconds);
        PresentCooling(enemy, frozen ? requiredHits : 0);
    }

    /// <summary>SW 수정 : 장착된 실제 Fighter 무기의 최종 SO와 살아 있는 소유자를 확인하며 다른 효과를 대신 발동하지 않는다.</summary>
    private bool TryGetRepeatedHitWeapon(out ItemInstance weapon)
    {
        weapon = null;
        if (context == null || !CanExecute || !context.isActiveAndEnabled ||
            context.Controller == null || context.Health == null || context.Health.CurrentHealth <= 0f ||
            inventory?.EquipmentSystem == null ||
            (inventory.EquipmentSystem.CurrentCharacterClass.HasValue &&
             inventory.EquipmentSystem.CurrentCharacterClass.Value != CharacterClass.Fighter) ||
            !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out weapon) ||
            weapon?.definition?.characterClass != CharacterClass.Fighter)
            return false;
        return weapon.definition.uniqueEffect is CoreBreakerExposeUniqueEffectSO or SuperRefrigerantFreezeUniqueEffectSO;
    }

    /// <summary>SW 수정 : 기존 면역·상태 수신 API를 통과하고 실제 상태 등록을 확인해 빙결 실패에 제한을 소비하지 않는다.</summary>
    private bool TryApplyRepeatedHitStatus(WBH_EnemyController enemy, WBH_StatusEffectData data)
    {
        if (!CanExecute || enemy == null || !enemy.gameObject.activeInHierarchy || enemy.Status == null || enemy.Status.IsDead)
            return false;
        var statusEffects = enemy.GetComponent<WBH_StatusEffectController>();
        if (statusEffects == null || !statusEffects.CanApplyStatusEffect(data))
            return false;
        var network = enemy.GetComponent<NetworkEnemyAuthority>();
        if (network != null && network.IsServerDamageHandlingActive)
            return network.ServerTryApplyStatusEffect(data);
        enemy.AddStatusEffect(data);
        bool applied = statusEffects.HasStatusEffect(data.Type);
        // 오프라인 방어 감소는 공용 상태이상 연출 슬롯이 없으므로 적 표시기가 실제 상태 목록을 따라 표시한다.
        if (applied && data.Type == WBH_StatusEffectType.DefenseDown && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active)
            (enemy.GetComponent<EnemyEffectIndicator>() ?? enemy.gameObject.AddComponent<EnemyEffectIndicator>()).TrackOfflineStatus(statusEffects);
        return applied;
    }

    /// <summary>SW 수정 : 장착 인스턴스·소유자·같은 적의 풀 생애가 모두 유지될 때만 이전 누적을 이어간다.</summary>
    private bool IsRepeatedHitLifetimeValid()
    {
        return TryGetRepeatedHitWeapon(out ItemInstance weapon) &&
            repeatedHitEffect == weapon.definition.uniqueEffect && repeatedHitItemInstanceId == weapon.instanceId &&
            repeatedHitTarget != null && repeatedHitTarget.gameObject.activeInHierarchy &&
            repeatedHitTarget.Status != null && !repeatedHitTarget.Status.IsDead &&
            repeatedHitRecovery != null && repeatedHitRecovery.isActiveAndEnabled &&
            repeatedHitRecovery.Lifetime == repeatedHitTargetLifetime;
    }

    /// <summary>
    /// SW 수정 : 장비 재조정은 만료·공유 빙결 제한·잘못된 생애만 정리하고, 사망·씬 이동은 누적을 강제 초기화한다.
    /// 이미 적에게 적용한 상태이상과 대상 공통 빙결 제한은 제거하지 않는다.
    /// </summary>
    private void ClearRepeatedHitEffects(bool force = false)
    {
        if (!force && repeatedHitEffect == null)
            return;
        if (!force && IsRepeatedHitLifetimeValid())
        {
            if (repeatedHitCount > 0 && (Now >= repeatedHitWindowEndsAt ||
                (repeatedHitEffect is SuperRefrigerantFreezeUniqueEffectSO && !repeatedHitRecovery.CanPrepareFreeze(Now))))
            {
                repeatedHitCount = 0;
                repeatedHitWindowEndsAt = 0d;
                if (repeatedHitEffect is SuperRefrigerantFreezeUniqueEffectSO)
                    PresentCooling(repeatedHitTarget, 0);
            }
            return;
        }

        WBH_EnemyController previousTarget = repeatedHitTarget;
        bool wasCooling = repeatedHitEffect is SuperRefrigerantFreezeUniqueEffectSO;
        repeatedHitTarget = null;
        repeatedHitRecovery = null;
        repeatedHitTargetLifetime = 0;
        repeatedHitEffect = null;
        repeatedHitItemInstanceId = null;
        lastRepeatedHitAttackId = 0;
        repeatedHitCount = 0;
        repeatedHitWindowEndsAt = 0d;
        if (wasCooling && previousTarget != null)
            PresentCooling(previousTarget, 0);
    }
}

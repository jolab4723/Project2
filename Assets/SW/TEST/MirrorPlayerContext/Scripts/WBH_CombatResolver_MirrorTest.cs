using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>BH 원본 전투 계산을 Mirror 서버 권한 경계에서 수행한다.</summary>
public static class WBH_CombatResolver_MirrorTest
{
    private readonly struct DamageSourceSnapshot
    {
        public readonly float AttackPower;
        public readonly float CritRate;
        public readonly float CritMult;
        public readonly float Pen;
        public readonly float FireBonus;
        public readonly float IceBonus;
        public readonly float ElectricBonus;
        public readonly float NormalDamageModifier;
        public readonly float SkillDamageModifier;

        public DamageSourceSnapshot(WBH_ICombatStatus status)
        {
            AttackPower = status.AttackPower;
            CritRate = status.CritRate;
            CritMult = status.CritMult;
            Pen = status.Pen;
            FireBonus = status.FireBonus;
            IceBonus = status.IceBonus;
            ElectricBonus = status.ElectricBonus;
            NormalDamageModifier = status.NormalDamageModifier;
            SkillDamageModifier = status.SkillDamageModifier;
        }

        /// <summary>WBH_CombatManager.GetAttackTypeModifier와 같은 규칙. 두 공식이 갈라지지 않게 맞춰야 한다.</summary>
        public float GetAttackTypeModifier(WBH_AttackType attackType)
        {
            return attackType switch
            {
                WBH_AttackType.Normal => NormalDamageModifier,
                WBH_AttackType.Skill => SkillDamageModifier,
                _ => 1f,
            };
        }

        public float GetElementBonus(ElementType elementType)
        {
            return elementType switch
            {
                ElementType.Fire => FireBonus,
                ElementType.Ice => IceBonus,
                ElementType.Electric => ElectricBonus,
                _ => 0f,
            };
        }
    }

    private struct PendingFollowUpDamage
    {
        public PlayerContext Attacker;
        public WBH_ICombat Target;
        public ElementType ElementType;
        public float DamageMultiplier;
        public WBH_StatusEffectData? StatusEffect;
        public DamageCause DamageCause;
        public uint AttackId;
        public System.Action<WBH_DamageResult> OnResolved;
        public bool CanCrit;
    }

    private sealed class DamageResolutionState
    {
        public readonly Queue<PendingFollowUpDamage> PendingQueue = new();
        public readonly HashSet<(uint AttackId, DamageCause Cause, WBH_ICombat Target)> FollowUpTargets = new();
        public DamageSourceSnapshot SourceSnapshot;
        public bool IsResolving;
    }

    private static readonly Dictionary<PlayerContext, DamageResolutionState> resolutionStates = new();

    /// <summary>플레이어별로 분리된 동기적 피해 처리 진입점이다.</summary>
    public static bool TryProcessPlayerDamage(PlayerContext attacker, WBH_ICombat target,
        ElementType elementType, float damageMultiplier, WBH_StatusEffectData? statusEffect,
        out WBH_DamageResult result, DamageCause damageCause = DamageCause.Direct, uint attackId = 0)
    {
        result = default;
        if (attacker == null || !Mirror.NetworkServer.active || attacker.Controller == null || target == null ||
            !float.IsFinite(damageMultiplier) || damageMultiplier <= 0f)
            return false;

        if (resolutionStates.TryGetValue(attacker, out DamageResolutionState state) && state.IsResolving)
        {
            Debug.LogWarning("[WBH_CombatResolver_MirrorTest] TryProcessPlayerDamage 재진입 거절: 후속 피해는 EnqueueFollowUpDamage를 사용해야 합니다.");
            return false;
        }

        if (attackId != 0 && attacker.CombatAuthority != null &&
            !attacker.CombatAuthority.TryRegisterResolvedTarget(attackId, target))
            return false;

        WBH_ICombatStatus attackerStatus = attacker.Controller.Status;
        if (attackerStatus == null || attackerStatus.IsDead)
            return false;

        if (state == null)
        {
            state = new DamageResolutionState();
            resolutionStates.Add(attacker, state);
        }

        state.SourceSnapshot = new DamageSourceSnapshot(attackerStatus);
        state.IsResolving = true;
        bool success;
        try
        {
            success = ExecuteDamageInternal(attacker, target, elementType, damageMultiplier, statusEffect,
                damageCause, attackId, state.SourceSnapshot, out result);
            DrainPendingQueue(state);
        }
        catch (System.Exception)
        {
            PurgePendingQueue(state, "피해 처리 도중 예외 발생");
            throw;
        }
        finally
        {
            state.IsResolving = false;
            resolutionStates.Remove(attacker);
        }
        return success;
    }

    /// <summary>현재 공격자의 활성 피해 처리 경계에 후속 피해를 FIFO로 등록한다.</summary>
    public static bool EnqueueFollowUpDamage(PlayerContext attacker, WBH_ICombat target,
        ElementType elementType, float damageMultiplier, WBH_StatusEffectData? statusEffect,
        DamageCause damageCause, uint attackId, System.Action<WBH_DamageResult> onResolved = null,
        bool canCrit = true)
    {
        if (attacker == null || !resolutionStates.TryGetValue(attacker, out DamageResolutionState state) ||
            !state.IsResolving)
        {
            Debug.LogWarning("[WBH_CombatResolver_MirrorTest] EnqueueFollowUpDamage 거절: 활성 피해 처리 경계 밖입니다.");
            return false;
        }
        if (!Mirror.NetworkServer.active || attacker.Controller == null || target == null ||
            !float.IsFinite(damageMultiplier) || damageMultiplier <= 0f)
            return false;

        if (attackId != 0 && !state.FollowUpTargets.Add((attackId, damageCause, target)))
            return false;

        state.PendingQueue.Enqueue(new PendingFollowUpDamage
        {
            Attacker = attacker,
            Target = target,
            ElementType = elementType,
            DamageMultiplier = damageMultiplier,
            StatusEffect = statusEffect,
            DamageCause = damageCause,
            AttackId = attackId,
            OnResolved = onResolved,
            CanCrit = canCrit,
        });
        return true;
    }

    private static void DrainPendingQueue(DamageResolutionState state)
    {
        while (state.PendingQueue.Count > 0)
        {
            PendingFollowUpDamage pending = state.PendingQueue.Dequeue();
            if (pending.Target == null || (pending.Target is Component comp && comp == null))
                continue;
            WBH_ICombatStatus targetStatus = pending.Target.Status;
            if (targetStatus == null || targetStatus.IsDead)
                continue;

            if (ExecuteDamageInternal(pending.Attacker, pending.Target, pending.ElementType,
                    pending.DamageMultiplier, pending.StatusEffect, pending.DamageCause, pending.AttackId,
                    state.SourceSnapshot, out WBH_DamageResult resolvedResult, pending.CanCrit))
                pending.OnResolved?.Invoke(resolvedResult);
        }
    }

    private static void PurgePendingQueue(DamageResolutionState state, string reason)
    {
        int count = state.PendingQueue.Count;
        state.PendingQueue.Clear();
        if (count > 0)
            Debug.LogWarning($"[WBH_CombatResolver_MirrorTest] {reason}: 대기 중인 후속 피해 {count}건을 폐기했습니다.");
    }

    /// <summary>
    /// 이 경로는 WBH_AttackType을 들고 다니지 않고 DamageCause만 받는다. WBH_DamageRequest가
    /// AttackType을 DamageCause로 옮길 때 쓰는 규칙(Skill -> Skill, 그 외 -> Direct)을 되짚어
    /// 공격 유형을 복원한다.
    ///
    /// !! Effect(고유효과 추가타)와 DoT(지속 피해)는 Normal로 본다. 단일 경로(WBH_CombatManager)에서도
    ///    이런 요청은 호출부가 WBH_AttackType.Normal을 담아 보내므로 NormalDamageModifier를 타는데,
    ///    여기서만 1f로 빼면 두 공식의 결과가 갈라진다.
    ///
    /// !! 복원이라 완전하지는 않다. 단일 경로에서 AttackType.Skill과 DamageCause.Effect를 함께 넘기면
    ///    그쪽은 스킬 배율을 타지만 여기서는 일반 배율을 탄다. 현재 그런 호출부는 없다.
    /// </summary>
    private static WBH_AttackType ToAttackType(DamageCause damageCause)
    {
        return damageCause == DamageCause.Skill ? WBH_AttackType.Skill : WBH_AttackType.Normal;
    }

    private static bool ExecuteDamageInternal(PlayerContext attacker, WBH_ICombat target,
        ElementType elementType, float damageMultiplier, WBH_StatusEffectData? statusEffect,
        DamageCause damageCause, uint attackId, DamageSourceSnapshot sourceSnapshot,
        out WBH_DamageResult result, bool canCrit = true)
    {
        result = default;
        if (!Mirror.NetworkServer.active || attacker?.Controller == null || target == null ||
            !float.IsFinite(damageMultiplier) || damageMultiplier <= 0f)
            return false;
        WBH_ICombatStatus attackerStatus = attacker.Controller.Status;
        WBH_ICombatStatus targetStatus = target.Status;
        if (attackerStatus == null || targetStatus == null || attackerStatus.IsDead || targetStatus.IsDead)
            return false;

        float damage = sourceSnapshot.AttackPower * damageMultiplier * sourceSnapshot.GetAttackTypeModifier(ToAttackType(damageCause));
        damage *= 1f + sourceSnapshot.GetElementBonus(elementType);
        bool isCritical = canCrit && Random.value <= sourceSnapshot.CritRate;
        if (isCritical) damage *= sourceSnapshot.CritMult;
        damage -= targetStatus.DefensePower - sourceSnapshot.Pen;
        damage *= targetStatus.DamageTakenModifier;
        damage = Mathf.Max(1f, damage);

        if (statusEffect.HasValue)
        {
            WBH_StatusEffectData applied = statusEffect.Value;
            applied.Attacker = attacker.Controller;
            applied.AttackId = attackId;
            statusEffect = applied;
        }
        result = new WBH_DamageResult(attacker.Controller, damage, isCritical, elementType, statusEffect,
            null, null, null, damageCause, attackId);
        target.TakeDamage(result);

        if (!target.Status.IsDead && statusEffect.HasValue)
        {
            if (target is Component targetComponent &&
                targetComponent.TryGetComponent(out NetworkEnemyAuthority_MirrorTest networkEnemy))
                networkEnemy.ServerTryApplyStatusEffect(statusEffect.Value);
            else
                target.AddStatusEffect(statusEffect.Value);
        }
        return true;
    }

}

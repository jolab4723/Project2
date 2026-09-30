using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>BH 원본 계산과 후속 피해 큐를 같은 플레이어의 싱글·서버에서 공유한다.</summary>
public static class PlayerDamageResolver
{
    private struct PendingFollowUpDamage
    {
        public PlayerContext Attacker;
        public WBH_DamageRequest Request;
        public System.Action<WBH_DamageResult> OnResolved;
        public bool CanCrit;
    }

    private sealed class DamageResolutionState
    {
        public readonly Queue<PendingFollowUpDamage> PendingQueue = new();
        public readonly HashSet<(uint AttackId, DamageCause Cause, WBH_ICombat Target)> FollowUpTargets = new();
        public WBH_CombatManager.DamageSourceSnapshot SourceSnapshot;
        public bool IsResolving;
    }

    private static readonly Dictionary<PlayerContext, DamageResolutionState> resolutionStates = new();

    /// <summary>플레이어별로 분리된 동기적 피해 처리 진입점이다.</summary>
    public static bool TryProcessPlayerDamage(PlayerContext attacker, WBH_ICombat target,
        ElementType elementType, float damageMultiplier, WBH_StatusEffectData? statusEffect,
        out WBH_DamageResult result, DamageCause damageCause = DamageCause.Direct, uint attackId = 0)
        => TryProcessPlayerDamage(attacker, new WBH_DamageRequest(attacker?.Controller, target,
            ToAttackType(damageCause), elementType, damageMultiplier, statusEffect, null, null, null,
            damageCause, attackId), out result);

    /// <summary>기존 요청의 공격 유형·출처·피격 연출을 보존하면서 서버 권한 경계를 통과한다.</summary>
    public static bool TryProcessPlayerDamage(PlayerContext attacker, WBH_DamageRequest request,
        out WBH_DamageResult result)
    {
        result = default;
        WBH_ICombat target = request.Target;
        uint attackId = request.AttackId;
        float damageMultiplier = request.DamageMultiplier;
        if (attacker == null || !attacker.Effects.CanExecute || attacker.Controller == null || target == null ||
            request.Attacker != attacker.Controller ||
            !float.IsFinite(damageMultiplier) || damageMultiplier <= 0f)
            return false;

        if (resolutionStates.TryGetValue(attacker, out DamageResolutionState state) && state.IsResolving)
        {
            Debug.LogWarning("[PlayerDamageResolver] TryProcessPlayerDamage 재진입 거절: 후속 피해는 EnqueueFollowUpDamage를 사용해야 합니다.");
            return false;
        }

        WBH_ICombatStatus attackerStatus = attacker.Controller.Status;
        WBH_ICombatStatus targetStatus = target.Status;
        if (attackerStatus == null || attackerStatus.IsDead || targetStatus == null || targetStatus.IsDead)
            return false;

        damageMultiplier *= attacker.Effects.ConsumePreparedAttackMultiplier(request.DamageCause, attackId);
        request = new WBH_DamageRequest(request.Attacker, target, request.AttackType, request.ElementType,
            damageMultiplier, request.StatusEffect, request.EffectData, request.HitPosition,
            request.HitEffectDirection, request.DamageCause, attackId);

        if (state == null)
        {
            state = new DamageResolutionState();
            resolutionStates.Add(attacker, state);
        }

        state.SourceSnapshot = new WBH_CombatManager.DamageSourceSnapshot(attackerStatus);
        state.IsResolving = true;
        bool success;
        try
        {
            success = ExecuteDamageInternal(attacker, request, state.SourceSnapshot, out result);
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
            Debug.LogWarning("[PlayerDamageResolver] EnqueueFollowUpDamage 거절: 활성 피해 처리 경계 밖입니다.");
            return false;
        }
        if (!attacker.Effects.CanExecute || attacker.Controller == null || target == null ||
            !float.IsFinite(damageMultiplier) || damageMultiplier <= 0f)
            return false;

        if (attackId != 0 && !state.FollowUpTargets.Add((attackId, damageCause, target)))
            return false;

        state.PendingQueue.Enqueue(new PendingFollowUpDamage
        {
            Attacker = attacker,
            Request = new WBH_DamageRequest(attacker.Controller, target, ToAttackType(damageCause),
                elementType, damageMultiplier, statusEffect, null, null, null, damageCause, attackId),
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
            WBH_ICombat target = pending.Request.Target;
            if (target == null || (target is Component comp && comp == null))
                continue;
            WBH_ICombatStatus targetStatus = target.Status;
            if (targetStatus == null || targetStatus.IsDead)
                continue;

            if (ExecuteDamageInternal(pending.Attacker, pending.Request,
                    state.SourceSnapshot, out WBH_DamageResult resolvedResult, pending.CanCrit))
                pending.OnResolved?.Invoke(resolvedResult);
        }
    }

    private static void PurgePendingQueue(DamageResolutionState state, string reason)
    {
        int count = state.PendingQueue.Count;
        state.PendingQueue.Clear();
        if (count > 0)
            Debug.LogWarning($"[PlayerDamageResolver] {reason}: 대기 중인 후속 피해 {count}건을 폐기했습니다.");
    }

    /// <summary>
    /// 기존 인자형 API는 WBH_AttackType을 들고 다니지 않고 DamageCause만 받는다. WBH_DamageRequest가
    /// AttackType을 DamageCause로 옮길 때 쓰는 규칙(Skill -> Skill, 그 외 -> Direct)을 되짚어
    /// 공격 유형을 복원한다.
    ///
    /// !! Effect(고유효과 추가타)와 DoT(지속 피해)는 Normal로 본다. 단일 경로(WBH_CombatManager)에서도
    ///    이런 요청은 호출부가 WBH_AttackType.Normal을 담아 보내므로 NormalDamageModifier를 타는데,
    ///    여기서만 1f로 빼면 두 공식의 결과가 갈라진다.
    ///
    /// !! 복원이라 완전하지는 않다. 단일 경로에서 AttackType.Skill과 DamageCause.Effect를 함께 넘기면
    ///    그쪽은 스킬 배율을 타지만 기존 인자형 API는 일반 배율을 탄다. 이런 조합은 요청형 API를 사용한다.
    /// </summary>
    private static WBH_AttackType ToAttackType(DamageCause damageCause)
    {
        return damageCause == DamageCause.Skill ? WBH_AttackType.Skill : WBH_AttackType.Normal;
    }

    /// <summary>SW 수정: 싱글·서버 공통 경계에서 피해를 적용하고 Fighter 처치 파동 또는 Shotgun 명중 폭발을 피격 전에 보존한 출처·위치로 등록한다.</summary>
    private static bool ExecuteDamageInternal(PlayerContext attacker, WBH_DamageRequest request,
        WBH_CombatManager.DamageSourceSnapshot sourceSnapshot,
        out WBH_DamageResult result, bool canCrit = true)
    {
        result = default;
        WBH_ICombat target = request.Target;
        if (attacker?.Controller == null || !attacker.Effects.CanExecute || target == null ||
            !float.IsFinite(request.DamageMultiplier) || request.DamageMultiplier <= 0f)
            return false;
        WBH_ICombatStatus attackerStatus = attacker.Controller.Status;
        WBH_ICombatStatus targetStatus = target.Status;
        if (attackerStatus == null || targetStatus == null || attackerStatus.IsDead || targetStatus.IsDead)
            return false;

        result = WBH_CombatManager.CalculateDamage(request, sourceSnapshot, canCrit);
        WBH_StatusEffectData? statusEffect = result.StatusEffect;
        NetworkEnemyAuthority authority = (target as Component)?.GetComponentInParent<NetworkEnemyAuthority>();
        bool handledByAuthority = authority != null && authority.IsServerDamageHandlingActive;
        // SW 수정: 적이 사망 처리 중 풀로 반환되어도 처치 원점·정면과 Shotgun 피격점은 바뀌지 않는다.
        Component component = target as Component;
        bool hasWaveSource = attacker.Effects.TryGetPhaseHarvesterSource(request, out PhaseHarvesterWaveUniqueEffectSO wave,
            out Vector3 attackForward) && component != null;
        bool hasExplosionSource = attacker.Effects.TryGetStarBreacherSource(request, out StarBreacherExplosionUniqueEffectSO explosion,
            out Vector3 hitPosition) && component != null;
        Vector3 bodyPosition = hasWaveSource || hasExplosionSource
            ? (authority != null ? authority.transform.position : component.transform.position) : default;
        target.TakeDamage(result);
        if (hasWaveSource && targetStatus.IsDead)
            attacker.Effects.FirePhaseHarvesterKill(request.AttackId, bodyPosition, attackForward, wave);
        if (hasExplosionSource)
            attacker.Effects.FireStarBreacherHit(request.AttackId, hitPosition, bodyPosition, explosion);
        // 네트워크 적은 TakeDamage 내부의 서버 이벤트가 같은 플레이어 효과를 발행한다.
        if (!handledByAuthority)
            attacker.Effects.FireDamageDealt(result, target);

        if (!target.Status.IsDead && statusEffect.HasValue)
        {
            if (target is Component targetComponent &&
                targetComponent.TryGetComponent(out NetworkEnemyAuthority networkEnemy))
                networkEnemy.ServerTryApplyStatusEffect(statusEffect.Value);
            else
                target.AddStatusEffect(statusEffect.Value);
        }
        return true;
    }

}

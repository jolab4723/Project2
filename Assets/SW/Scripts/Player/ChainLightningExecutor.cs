using System;
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>연쇄 번개의 표적 선택, 감쇠, 후속 피해 등록을 싱글·서버에서 함께 수행한다.</summary>
internal static class ChainLightningExecutor
{
    public static int Enqueue(PlayerContext context, ChainLightningUniqueEffectSO effect,
        in WBH_DamageResult directResult, WBH_ICombat firstTarget,
        Action<Vector3, Vector3> onSegmentResolved)
    {
        if (context == null || effect == null || firstTarget == null || directResult.AttackId == 0 ||
            !TryGetCombatPoint(firstTarget, out Vector3 currentPoint))
        {
            return 0;
        }

        var excluded = new HashSet<WBH_ICombat> { firstTarget };
        int maximumTargets = Mathf.Clamp(effect.maxAdditionalTargets, 1, 16);
        float damageMultiplier = Mathf.Max(0f, effect.firstDamageMultiplier);
        int queuedCount = 0;

        for (int jump = 0; jump < maximumTargets && damageMultiplier > 0f; jump++)
        {
            if (!TryFindNearestTarget(context, currentPoint, effect.jumpRadius, directResult.AttackId, excluded,
                    out WBH_ICombat nextTarget, out Vector3 nextPoint))
            {
                break;
            }

            excluded.Add(nextTarget);
            Vector3 segmentStart = currentPoint;
            Vector3 segmentEnd = nextPoint;
            if (!PlayerDamageResolver.EnqueueFollowUpDamage(
                    context,
                    nextTarget,
                    ElementType.Electric,
                    damageMultiplier,
                    null,
                    DamageCause.Effect,
                    directResult.AttackId,
                    _ => onSegmentResolved?.Invoke(segmentStart, segmentEnd),
                    canCrit: false))
            {
                break;
            }

            queuedCount++;
            currentPoint = nextPoint;
            damageMultiplier *= Mathf.Clamp01(effect.subsequentDamageMultiplier);
        }

        return queuedCount;
    }

    private static bool TryFindNearestTarget(PlayerContext context, Vector3 sourcePoint, float radius, uint attackId,
        HashSet<WBH_ICombat> excluded, out WBH_ICombat target, out Vector3 targetPoint)
    {
        target = null;
        targetPoint = default;
        float bestDistance = float.PositiveInfinity;
        int obstacles = PlayerItemEffectState.ObstacleLayerMask;

        foreach (Collider hit in Physics.OverlapSphere(sourcePoint, Mathf.Max(0.1f, radius), PlayerItemEffectState.EnemyLayerMask,
                     QueryTriggerInteraction.Collide))
        {
            WBH_ICombat candidate = PlayerCombatAuthority.FindCombatTarget(hit);
            if (candidate == null || excluded.Contains(candidate) || candidate.Status == null || candidate.Status.IsDead ||
                context.Effects.IsDirectTargetForAttack(attackId, candidate) ||
                !TryGetCombatPoint(candidate, out Vector3 candidatePoint) ||
                Physics.Linecast(sourcePoint, candidatePoint, obstacles, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            float distance = (candidatePoint - sourcePoint).sqrMagnitude;
            // SW 수정: 같은 거리면 범위 효과와 같은 netId → InstanceId 순서로 고른다.
            if (distance > bestDistance ||
                (distance == bestDistance && PlayerItemEffectState.CompareStableIds(candidate, target) >= 0))
            {
                continue;
            }

            target = candidate;
            targetPoint = candidatePoint;
            bestDistance = distance;
        }

        return target != null;
    }

    private static bool TryGetCombatPoint(WBH_ICombat target, out Vector3 point)
    {
        if (target is not Component component || component == null)
        {
            point = default;
            return false;
        }

        point = PlayerItemEffectState.GetBodyPosition(component) + Vector3.up;
        return true;
    }
}

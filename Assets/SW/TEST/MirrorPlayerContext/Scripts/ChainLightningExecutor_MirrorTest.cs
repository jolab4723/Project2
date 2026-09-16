using System;
using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>연쇄 번개의 표적 선택, 감쇠, 후속 피해 등록만 담당하는 서버 실행기다.</summary>
internal static class ChainLightningExecutor_MirrorTest
{
    private const int EnemyLayerMask = 1 << 10;
    private static int ObstacleLayerMask => LayerMask.GetMask("Wall", "Prop", "Ground");

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
            if (!WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(
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
        uint bestNetId = uint.MaxValue;
        int bestInstanceId = int.MaxValue;

        foreach (Collider hit in Physics.OverlapSphere(sourcePoint, Mathf.Max(0.1f, radius), EnemyLayerMask,
                     QueryTriggerInteraction.Collide))
        {
            WBH_ICombat candidate = PlayerCombatAuthority_MirrorTest.FindCombatTarget(hit);
            if (candidate == null || excluded.Contains(candidate) || candidate.Status == null || candidate.Status.IsDead ||
                context.CombatAuthority?.IsDirectTargetForAttack(attackId, candidate) == true ||
                !TryGetCombatPoint(candidate, out Vector3 candidatePoint) ||
                Physics.Linecast(sourcePoint, candidatePoint, ObstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            float distance = (candidatePoint - sourcePoint).sqrMagnitude;
            GetStableIds(candidate, out uint netId, out int instanceId);
            if (distance > bestDistance ||
                (distance == bestDistance && (netId > bestNetId ||
                                              (netId == bestNetId && instanceId >= bestInstanceId))))
            {
                continue;
            }

            target = candidate;
            targetPoint = candidatePoint;
            bestDistance = distance;
            bestNetId = netId;
            bestInstanceId = instanceId;
        }

        return target != null;
    }

    private static void GetStableIds(WBH_ICombat target, out uint netId, out int instanceId)
    {
        if (target is Component component)
        {
            netId = component.GetComponentInParent<NetworkIdentity>()?.netId ?? 0u;
            instanceId = component.GetInstanceID();
            return;
        }

        netId = 0u;
        instanceId = 0;
    }

    private static bool TryGetCombatPoint(WBH_ICombat target, out Vector3 point)
    {
        if (target is not Component component || component == null)
        {
            point = default;
            return false;
        }

        Transform targetTransform = component.GetComponentInParent<NetworkEnemyAuthority_MirrorTest>()?.transform
                                    ?? component.transform;
        point = targetTransform.position + Vector3.up;
        return true;
    }
}

using System;
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

public sealed partial class PlayerItemEffectState
{
    private uint lastEchoAttackId, lastWorldEnderAttackId;
    private bool worldEnderReady;
    public event Action<Vector3, Vector3, float, float> EchoReplayPresented;
    public event Action<Vector3, float> WorldEnderBlastPresented;
    public event Action<bool> WorldEnderReadyChanged;
    public bool WorldEnderReady => worldEnderReady;

    private bool TryGetEquippedWeaponEffect<T>(out T effect) where T : UniqueEffectSO
    {
        effect = null;
        if (!CanExecute || context.Health == null || context.Health.CurrentHealth <= 0f ||
            inventory?.EquipmentSystem == null ||
            !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out var weapon))
            return false;

        effect = weapon?.definition?.uniqueEffect as T;
        return effect != null;
    }

    /// <summary>SW 수정 : 실제 실행된 산탄의 빈 공간까지 기록하며 명중 여부와 무관하게 예약 대기를 소비한다.</summary>
    public void ReserveEchoReplay(uint attackId, Vector3 origin, Vector3 forward, float range, float angle)
    {
        if (attackId == 0 || attackId == lastEchoAttackId ||
            !TryGetEquippedWeaponEffect<EchoVaultReplayUniqueEffectSO>(out var effect))
            return;

        lastEchoAttackId = attackId;
        string key = GetCooldownKey(effect, null);
        if (IsCoolingDown(key, Now))
            return;

        forward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f || range <= 0f)
            return;

        SetCooldownEnd(key, Now + effect.cooldownSeconds);
        var runner = new GameObject("Echo Vault Replay");
        runner.transform.position = origin;
        runner.AddComponent<PlayerGrenadeEffect>().InitializeEchoReplay(context, effect, attackId,
            forward.normalized, range, angle, () => UnityEngine.Object.Destroy(runner));
    }

    /// <summary>SW 수정 : 과거 사격 공간의 현재 대상을 다시 찾고 재생 시작 시 스탯 하나로 비치명 피해를 처리한다.</summary>
    internal void ExecuteEchoReplay(EchoVaultReplayUniqueEffectSO effect, uint attackId, Vector3 origin,
        Vector3 forward, float range, float angle)
    {
        var targets = CollectLivingBodies(Physics.OverlapSphere(origin, range, EnemyLayerMask, QueryTriggerInteraction.Collide),
            origin, position =>
            {
                Vector3 offset = position - origin;
                Vector3 flat = Vector3.ProjectOnPlane(offset, Vector3.up);
                return Mathf.Abs(offset.y) <= 2f && flat.sqrMagnitude <= range * range &&
                    Vector3.Angle(forward, flat) <= angle * 0.5f;
            });
        SortSpatialTargets(targets, origin);
        var snapshot = new WBH_CombatManager.DamageSourceSnapshot(context.Controller.Status);
        ResolveSpatialTargets(targets, effect.maxTargets, effect.damageMultiplier, attackId, snapshot);
        EchoReplayPresented?.Invoke(origin, forward, range, angle);
    }

    /// <summary>SW 수정 : 최초 장착부터 설정된 재충전 시간만큼 충전하며 탈착해도 같은 소유자의 재충전 시각을 보존한다.</summary>
    private void ReconcileWorldEnder()
    {
        bool ready = false;
        if (TryGetEquippedWeaponEffect<WorldEnderChargedBlastUniqueEffectSO>(out var effect))
        {
            string key = GetCooldownKey(effect, null);
            if (!cooldownEndTimes.ContainsKey(key))
                SetCooldownEnd(key, Now + effect.rechargeSeconds);

            ready = !IsCoolingDown(key, Now);
        }
        SetWorldEnderReady(ready);
    }

    private void SetWorldEnderReady(bool ready)
    {
        if (worldEnderReady == ready)
            return;

        worldEnderReady = ready;
        WorldEnderReadyChanged?.Invoke(ready);
    }

    /// <summary>SW 수정 : 승인 유탄 한 발에만 준비를 싣고 충돌 실패에도 환불하지 않는다.</summary>
    public bool ReserveWorldEnderShot(uint attackId, WorldEnderChargedBlastUniqueEffectSO effect)
    {
        if (attackId == 0 || lastWorldEnderAttackId == attackId ||
            !TryGetEquippedWeaponEffect<WorldEnderChargedBlastUniqueEffectSO>(out var current) || current != effect)
            return false;

        ReconcileWorldEnder();
        lastWorldEnderAttackId = attackId;
        if (!worldEnderReady)
            return false;

        SetCooldownEnd(GetCooldownKey(effect, null), Now + effect.rechargeSeconds);
        SetWorldEnderReady(false);
        return true;
    }

    public readonly struct ChargedBlastImpact
    {
        internal readonly List<WBH_ICombat> Targets;
        internal readonly WBH_CombatManager.DamageSourceSnapshot Snapshot;

        internal ChargedBlastImpact(List<WBH_ICombat> targets, WBH_CombatManager.DamageSourceSnapshot snapshot)
        {
            Targets = targets;
            Snapshot = snapshot;
        }
    }

    /// <summary>SW 수정 : 기본 유탄의 처치 버프가 같은 충돌의 추가 폭발을 바꾸지 않도록 먼저 공간과 능력치를 보존한다.</summary>
    public ChargedBlastImpact PrepareWorldEnderImpact(Vector3 position, WorldEnderChargedBlastUniqueEffectSO effect)
    {
        if (!CanExecute || effect == null || context.Controller.Status.IsDead)
            return default;

        Vector3 sight = position + Vector3.up * 0.15f;
        var targets = CollectLivingBodies(Physics.OverlapSphere(position, effect.radius, EnemyLayerMask, QueryTriggerInteraction.Collide),
            sight, body => Mathf.Abs(body.y - position.y) <= 2f && (body - position).sqrMagnitude <= effect.radius * effect.radius);
        SortSpatialTargets(targets, position);
        return new ChargedBlastImpact(targets, new WBH_CombatManager.DamageSourceSnapshot(context.Controller.Status));
    }

    public void ResolveWorldEnderImpact(in ChargedBlastImpact impact, Vector3 position, uint attackId,
        WorldEnderChargedBlastUniqueEffectSO effect)
    {
        if (impact.Targets == null || !CanExecute || context.Controller.Status.IsDead)
            return;

        ResolveSpatialTargets(impact.Targets, effect.maxTargets, effect.damageMultiplier, attackId, impact.Snapshot);
        WorldEnderBlastPresented?.Invoke(position, effect.radius);
    }

    private static void SortSpatialTargets(List<WBH_ICombat> targets, Vector3 origin)
    {
        targets.Sort((a, b) =>
        {
            float firstDistanceSquared = (GetBodyPosition((Component)a) - origin).sqrMagnitude;
            float secondDistanceSquared = (GetBodyPosition((Component)b) - origin).sqrMagnitude;
            int distanceComparison = firstDistanceSquared.CompareTo(secondDistanceSquared);
            return distanceComparison != 0 ? distanceComparison : CompareStableIds(a, b);
        });
    }

    private void ResolveSpatialTargets(List<WBH_ICombat> targets, int maximum, float multiplier,
        uint attackId, WBH_CombatManager.DamageSourceSnapshot snapshot)
    {
        for (int i = 0; i < Mathf.Min(targets.Count, Mathf.Clamp(maximum, 1, 16)); i++)
        {
            if (!context.isActiveAndEnabled || context.Controller.Status.IsDead)
                break;
            if (targets[i] is not Component body || body == null || !body.gameObject.activeInHierarchy)
                continue;

            if (PlayerDamageResolver.TryProcessPlayerDamage(context, targets[i], ElementType.None, multiplier, null,
                out var result, DamageCause.Effect, attackId, canCrit: false, sourceSnapshot: snapshot))
                context.CombatAuthority?.ServerRecordGunnerHit(targets[i], result);
        }
    }
}

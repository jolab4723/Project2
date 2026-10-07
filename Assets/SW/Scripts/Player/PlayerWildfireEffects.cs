using System;
using ItemSystem;
using UnityEngine;

public sealed partial class PlayerItemEffectState
{
    public event Action<Vector3, Vector3> WildfirePresented;

    /// <summary>SW 수정 : 현재 도끼의 실제 기본 적중으로 적용한 화상에만 인스턴스와 0세대 출처를 새긴다.</summary>
    internal WBH_StatusEffectData StampWildfireBurn(WBH_StatusEffectData data, in WBH_DamageRequest request)
    {
        if (data.Type != WBH_StatusEffectType.Burn || request.AttackType != WBH_AttackType.Normal ||
            request.DamageCause != DamageCause.Direct || !IsDirectTargetForAttack(request.AttackId, request.Target) ||
            !TryGetEquippedWeaponEffect<WildfireSpreadUniqueEffectSO>(out var effect) ||
            !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out var item))
            return data;

        data.SourceItemInstanceId = item.instanceId;
        data.SourceEffectId = effect.name;
        data.PropagationGeneration = 0;
        return data;
    }

    /// <summary>SW 수정 : 죽기 전 실제 유지된 자기 무기의 0세대 Burn만 기존 상태 수신 경로로 한 번 전파한다.</summary>
    public void FireWildfireKill(in WBH_StatusEffectData source, Vector3 position, uint attackId)
    {
        if (!TryGetEquippedWeaponEffect<WildfireSpreadUniqueEffectSO>(out var effect) ||
            !ReferenceEquals(source.Attacker, context.Controller) || source.PropagationGeneration != 0 || source.SourceEffectId != effect.name ||
            !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out var item) ||
            string.IsNullOrEmpty(source.SourceItemInstanceId) || source.SourceItemInstanceId != item.instanceId)
            return;

        string key = GetCooldownKey(effect, null);
        if (IsCoolingDown(key, Now))
            return;

        var targets = CollectLivingBodies(Physics.OverlapSphere(position, effect.radius, EnemyLayerMask, QueryTriggerInteraction.Collide),
            position + Vector3.up, body => Mathf.Abs(body.y - position.y) <= 2f &&
                Vector3.ProjectOnPlane(body - position, Vector3.up).sqrMagnitude <= effect.radius * effect.radius);
        SortSpatialTargets(targets, position);
        int count = 0;
        foreach (var target in targets)
        {
            if (count >= Mathf.Clamp(effect.maxTargets, 1, 16))
                break;

            var component = (Component)target;
            var statuses = component.GetComponentInParent<WBH_StatusEffectController>();
            var burn = WBH_StatusEffectPresets.Burn1;
            burn.Attacker = context.Controller;
            burn.AttackId = attackId;
            burn.SourceItemInstanceId = source.SourceItemInstanceId;
            burn.SourceEffectId = source.SourceEffectId;
            burn.PropagationGeneration = 1;
            if (statuses == null || !statuses.CanApplyStatusEffect(burn))
                continue;

            var network = component.GetComponentInParent<NetworkEnemyAuthority>();
            if (network != null && network.IsServerDamageHandlingActive)
            {
                if (!network.ServerTryApplyStatusEffect(burn))
                    continue;
            }
            else
                target.AddStatusEffect(burn);

            if (!statuses.TryGetBurnSource(out var applied) || applied.Attacker != burn.Attacker ||
                applied.SourceItemInstanceId != burn.SourceItemInstanceId || applied.PropagationGeneration != 1)
                continue;

            count++;
            SetCooldownEnd(key, Now + effect.cooldownSeconds);
            WildfirePresented?.Invoke(position + Vector3.up, GetBodyPosition(component) + Vector3.up);
        }
    }
}

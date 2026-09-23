using System;
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>같은 플레이어의 싱글·서버가 공유하는 발동 조건, 아이템별 쿨타임과 공격 준비 상태다.</summary>
public sealed class PlayerItemEffectState
{
    private readonly PlayerContext context;
    private InventoryController inventory => context.Inventory;
    private PlayerBuffManager buffs => context.Buffs;
    private string preparedAttackSourceInstanceId;
    private uint empoweredAttackId;
    private readonly Dictionary<string, double> cooldownEndTimes = new();
    private readonly Dictionary<ChainLightningUniqueEffectSO, uint> lastChainAttackIds = new();
    private uint chainLightningTriggerCount, chainLightningResolvedHitCount;
    private uint infernoTriggerCount, infernoResolvedHitCount, glassRailTriggerCount, glassRailResolvedHitCount;
    private bool preparedAttackReady;
    private uint preparedAttackConsumeCount;
    private uint directAttackId, hitAttackId;
    private readonly HashSet<WBH_ICombat> directTargets = new();
    private readonly List<PlayerGrenadeEffect> grenadeEffects = new();
    private UniqueEffectSO hitEffect;
    private GunnerWeaponType hitWeapon;
    public event Action<Vector3, Vector3> ChainPresented;
    public event Action<Vector3> InfernoPresented;
    public event Action<bool> PreparedChanged;
    public event Action<ItemInstance> StackChanged;
    public PlayerItemEffectState(PlayerContext context) => this.context = context;
    public IReadOnlyDictionary<string, double> Cooldowns => cooldownEndTimes;
    public uint ChainLightningTriggerCount => chainLightningTriggerCount;
    public uint ChainLightningResolvedHitCount => chainLightningResolvedHitCount;
    public uint InfernoTriggerCount => infernoTriggerCount;
    public uint InfernoResolvedHitCount => infernoResolvedHitCount;
    public uint GlassRailTriggerCount => glassRailTriggerCount;
    public uint GlassRailResolvedHitCount => glassRailResolvedHitCount;
    public bool PreparedAttackReady => preparedAttackReady;
    public uint PreparedAttackConsumeCount => preparedAttackConsumeCount;
    private double Now => context.GetComponent<Mirror.NetworkIdentity>() != null ? Mirror.NetworkTime.time : Time.timeAsDouble;
    public bool CanExecute => context.GetComponent<Mirror.NetworkIdentity>() is { } identity
        ? identity.isServer : !Mirror.NetworkServer.active && !Mirror.NetworkClient.active;

    public void SetDirectTargets(uint attackId, IEnumerable<WBH_ICombat> targets)
    {
        directAttackId = attackId;
        directTargets.Clear();
        if (targets != null) directTargets.UnionWith(targets);
    }

    public bool IsDirectTargetForAttack(uint attackId, WBH_ICombat target)
        => context.CombatAuthority != null ? context.CombatAuthority.IsDirectTargetForAttack(attackId, target)
            : attackId != 0 && directAttackId == attackId && directTargets.Contains(target);

    public IDisposable BeginGunnerHitScope(uint attackId, GunnerWeaponType weapon, UniqueEffectSO effect)
    {
        if (attackId == 0 || hitAttackId != 0) return null;
        hitAttackId = attackId; hitWeapon = weapon; hitEffect = effect;
        return new HitScope(this);
    }

    public bool TryGetGunnerHitSource(uint attackId, out UniqueEffectSO effect, out GunnerWeaponType weapon)
    {
        if (context.CombatAuthority != null)
            return context.CombatAuthority.TryGetGunnerHitSource(attackId, out effect, out weapon);
        effect = hitEffect; weapon = hitWeapon;
        return attackId != 0 && hitAttackId == attackId;
    }

    private sealed class HitScope : IDisposable
    {
        private PlayerItemEffectState owner;
        public HitScope(PlayerItemEffectState owner) => this.owner = owner;
        public void Dispose()
        {
            if (owner == null) return;
            owner.hitAttackId = 0; owner.hitEffect = null; owner = null;
        }
    }

    /// <summary>공격 적중 피해의 Direct 전용 고유효과 진입점이다.</summary>
    public void FireDamageDealt(in WBH_DamageResult result, WBH_ICombat firstTarget = null)
    {
        if (result.DamageCause != DamageCause.Direct)
            return;
        if (!CanExecute || inventory == null || buffs == null)
            return;

        Fire(TriggerCondition.OnDamageDealt);
        if (result.IsCritical)
            Fire(TriggerCondition.OnCrit);

        if (firstTarget != null)
        {
            TryFireChainLightning(result, firstTarget);
            TryFireInfernoExtraHit(result, firstTarget);
            TryFireGlassRailExtraHit(result, firstTarget);
        }
    }

    /// <summary>장비와 인벤토리의 유물 중 조건이 일치하는 발동형 고유효과를 발동시킨다.</summary>
    public void Fire(TriggerCondition condition)
    {
        if (!CanExecute || inventory == null || buffs == null)
            return;

        if (inventory.EquipmentSystem != null)
        {
            foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in inventory.EquipmentSystem.GetEquippedItems())
                FireIfReady(pair.Value?.itemData, condition);
        }

        if (inventory.PlayerGrid == null)
            return;

        foreach (InventoryItem item in inventory.PlayerGrid.GetAllItems())
        {
            if (item?.itemData?.definition?.category == ItemCategory.Relic)
                FireIfReady(item.itemData, condition);
        }
    }

    public float GetRemainingCooldown(ItemInstance item)
    {
        UniqueEffectSO effect = item?.definition?.uniqueEffect;
        float cooldownSeconds;
        string key;
        switch (effect)
        {
            case TriggeredBuffUniqueEffectSO triggered:
                cooldownSeconds = triggered.cooldownSeconds;
                key = GetCooldownKey(triggered, item);
                break;
            case ChainLightningUniqueEffectSO chain:
                cooldownSeconds = chain.cooldownSeconds;
                key = GetChainCooldownKey(chain);
                break;
            default:
                return 0f;
        }

        if (cooldownSeconds <= 0f)
            return 0f;

        return cooldownEndTimes.TryGetValue(key, out double cooldownEnd)
            ? Mathf.Max(0f, (float)(cooldownEnd - Now))
            : 0f;
    }

    /// <summary>공격 번호가 다시 시작되는 플레이어 수명 경계에서 공격별 중복 기록을 초기화한다.</summary>
    public void ResetAttackLifetime()
    {
        while (grenadeEffects.Count > 0)
        {
            PlayerGrenadeEffect effect = grenadeEffects[0];
            grenadeEffects.RemoveAt(0);
            if (effect != null) effect.Finish();
        }
        lastChainAttackIds.Clear();
        directTargets.Clear();
        directAttackId = hitAttackId = 0;
        hitEffect = null;
        ClearPreparedAttack();
    }

    public void RegisterGrenadeEffect(PlayerGrenadeEffect effect, int maximum)
    {
        var sameKind = grenadeEffects.FindAll(active => active != null && !active.IsFinished && active.IsGravity == effect.IsGravity);
        while (sameKind.Count >= maximum)
        {
            PlayerGrenadeEffect oldest = sameKind[0];
            sameKind.RemoveAt(0);
            oldest.Finish();
        }
        grenadeEffects.Add(effect);
    }

    public void UnregisterGrenadeEffect(PlayerGrenadeEffect effect) => grenadeEffects.Remove(effect);

    /// <summary>유효한 직접 기본 공격이 확정될 때 공격 번호당 한 번 준비 상태를 소비합니다.</summary>
    public float ConsumePreparedAttackMultiplier(DamageCause cause, uint attackId)
    {
        if (!CanExecute || cause != DamageCause.Direct || attackId == 0)
            return 1f;
        if (!TryGetPreparedAttackEffect(out ItemInstance weapon, out DodgePreparedAttackUniqueEffectSO effect) ||
            (!string.IsNullOrEmpty(preparedAttackSourceInstanceId) &&
             preparedAttackSourceInstanceId != weapon.instanceId))
        {
            ClearPreparedAttack();
            return 1f;
        }
        if (empoweredAttackId == attackId)
            return ValidDamageMultiplier(effect);

        empoweredAttackId = 0;
        if (!preparedAttackReady)
            return 1f;

        empoweredAttackId = attackId;
        preparedAttackReady = false;
        PreparedChanged?.Invoke(false);
        preparedAttackConsumeCount++;
        return ValidDamageMultiplier(effect);
    }

    private void TryFireChainLightning(in WBH_DamageResult result, WBH_ICombat firstTarget)
    {
        if (result.AttackId == 0 || inventory?.EquipmentSystem == null || firstTarget == null ||
            !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance weapon) ||
            weapon?.definition?.uniqueEffect is not ChainLightningUniqueEffectSO effect)
        {
            return;
        }

        if (lastChainAttackIds.TryGetValue(effect, out uint lastAttackId) && lastAttackId == result.AttackId)
            return;

        // 공격당 한 번만 평가한다. 추가 표적이 없는 공격은 쿨다운을 소비하지 않는다.
        lastChainAttackIds[effect] = result.AttackId;

        string cooldownKey = GetChainCooldownKey(effect);
        double now = Now;
        if (cooldownEndTimes.TryGetValue(cooldownKey, out double cooldownEnd) && now < cooldownEnd)
            return;

        int queuedCount = ChainLightningExecutor_MirrorTest.Enqueue(
            context,
            effect,
            result,
            firstTarget,
            (segmentStart, segmentEnd) =>
            {
                chainLightningResolvedHitCount++;
                ChainPresented?.Invoke(segmentStart, segmentEnd);
            });

        if (queuedCount <= 0)
            return;

        chainLightningTriggerCount++;
        cooldownEndTimes[cooldownKey] = now + Mathf.Max(0f, effect.cooldownSeconds);
    }

    /// <summary>실제 Fighter 근접 기본 공격의 살아 있는 직접 대상에게 화염 후속 피해를 한 번 등록한다.</summary>
    private void TryFireInfernoExtraHit(in WBH_DamageResult result, WBH_ICombat firstTarget)
    {
        if (result.AttackId == 0 || firstTarget?.Status == null || firstTarget.Status.IsDead ||
            !IsDirectTargetForAttack(result.AttackId, firstTarget) ||
            inventory?.EquipmentSystem == null ||
            !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance weapon) ||
            weapon?.definition?.characterClass != CharacterClass.Fighter ||
            weapon.definition.uniqueEffect is not InfernoExtraHitUniqueEffectSO effect)
        {
            return;
        }

        // 인터페이스의 Transform을 가정하지 않는다. 처치·풀 반환 전에 값만 저장한다.
        Component targetComponent = firstTarget as Component;
        bool hasImpactPoint = targetComponent != null;
        Vector3 impactPoint = hasImpactPoint
            ? targetComponent.transform.position + Vector3.up
            : Vector3.zero;

        if (!PlayerDamageResolver.EnqueueFollowUpDamage(
                context, firstTarget, ElementType.Fire, effect.damageMultiplier,
                WBH_StatusEffectPresets.Burn1, DamageCause.Effect, result.AttackId,
                _ =>
                {
                    infernoResolvedHitCount++;
                    if (hasImpactPoint) InfernoPresented?.Invoke(impactPoint);
                },
                canCrit: false))
        {
            return;
        }

        infernoTriggerCount++;
    }

    /// <summary>발사 당시 유리빛 궤도 효과를 품고 날아간 라이플 탄에 냉기 추가타를 등록한다. (무기 교체·해제 후에도 유지)</summary>
    private void TryFireGlassRailExtraHit(in WBH_DamageResult result, WBH_ICombat firstTarget)
    {
        if (result.AttackId == 0 || firstTarget?.Status == null || firstTarget.Status.IsDead ||
            context == null)
        {
            return;
        }

        if (!TryGetGunnerHitSource(result.AttackId, out UniqueEffectSO sourceEffect, out GunnerWeaponType weaponType) ||
            weaponType != GunnerWeaponType.Rifle ||
            sourceEffect is not GlassRailExtraHitUniqueEffectSO effect)
        {
            return;
        }

        if (!PlayerDamageResolver.EnqueueFollowUpDamage(
                context, firstTarget, ElementType.Ice, effect.damageMultiplier,
                null, DamageCause.Effect, result.AttackId,
                _ => glassRailResolvedHitCount++,
                canCrit: false))
        {
            return;
        }

        glassRailTriggerCount++;
    }

    public void PrepareDodgeAttack()
    {
        if (!CanExecute || !TryGetPreparedAttackEffect(out ItemInstance weapon, out _))
            return;
        preparedAttackSourceInstanceId = weapon.instanceId;
        empoweredAttackId = 0;
        preparedAttackReady = true;
        PreparedChanged?.Invoke(true);
    }

    private bool TryGetPreparedAttackEffect(out ItemInstance weapon, out DodgePreparedAttackUniqueEffectSO effect)
    {
        weapon = null;
        effect = null;
        if (inventory?.EquipmentSystem == null ||
            !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out weapon) ||
            weapon?.definition?.characterClass != CharacterClass.Fighter)
            return false;
        effect = weapon.definition.uniqueEffect as DodgePreparedAttackUniqueEffectSO;
        return effect != null;
    }

    public void ReconcileEquipment()
    {
        if (!preparedAttackReady && empoweredAttackId == 0)
            return;
        if (!TryGetPreparedAttackEffect(out ItemInstance weapon, out _) ||
            preparedAttackSourceInstanceId != weapon.instanceId)
            ClearPreparedAttack();
    }

    public void ClearPreparedAttack()
    {
        preparedAttackReady = false;
        PreparedChanged?.Invoke(false);
        preparedAttackSourceInstanceId = null;
        empoweredAttackId = 0;
    }

    private static float ValidDamageMultiplier(DodgePreparedAttackUniqueEffectSO effect)
        => effect != null && float.IsFinite(effect.damageMultiplier)
            ? Mathf.Max(1f, effect.damageMultiplier)
            : 1f;

    private void FireIfReady(ItemInstance item, TriggerCondition condition)
    {
        if (item?.definition?.uniqueEffect is not TriggeredBuffUniqueEffectSO effect ||
            effect.triggerCondition != condition)
        {
            return;
        }

        string key = GetCooldownKey(effect, item);
        double now = Now;
        if (cooldownEndTimes.TryGetValue(key, out double cooldownEnd) && now < cooldownEnd)
        {
            return;
        }

        cooldownEndTimes[key] = now + Mathf.Max(0f, effect.cooldownSeconds);
        if (effect.persistStackOnItem)
        {
            int next = (int)System.Math.Min((long)Mathf.Max(0, item.persistedStackCount) + 1, int.MaxValue);
            item.persistedStackCount = effect.buffSpec.maxStack > 0 ? Mathf.Min(next, effect.buffSpec.maxStack) : next;
            buffs.SetBuffStack(effect, item.persistedStackCount);
            StackChanged?.Invoke(item);
        }
        else buffs.ApplyBuff(effect);
    }

    private static string GetCooldownKey(TriggeredBuffUniqueEffectSO effect, ItemInstance item)
    {
        string itemKey = effect.duplicatePolicy == DuplicateTriggerPolicy.PerItem
            ? item?.instanceId
            : null;

        return effect.name + ":" + (itemKey ?? "shared");
    }

    private static string GetChainCooldownKey(ChainLightningUniqueEffectSO effect)
    {
        return effect.name + ":chain";
    }
}

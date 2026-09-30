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
    // SW 수정: 실제 기본 공격의 정면과 장착 효과만 공격 범위 동안 보존한다.
    private Vector3 directAttackForward;
    private PhaseHarvesterWaveUniqueEffectSO directWaveEffect;
    private uint lastPhaseHarvesterAttackId;
    private readonly List<PlayerGrenadeEffect> grenadeEffects = new();
    private UniqueEffectSO hitEffect;
    private GunnerWeaponType hitWeapon;
    public event Action<Vector3, Vector3> ChainPresented;
    public event Action<Vector3> InfernoPresented;
    public event Action<Vector3, Vector3, float> PhaseHarvesterPresented;
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

    /// <summary>SW 수정: 싱글·서버의 실제 기본 공격 범위에서 직접 대상·Fighter 정면·장착 파동 효과를 저장하고 종료 시 해제한다.</summary>
    public void SetDirectTargets(uint attackId, IEnumerable<WBH_ICombat> targets,
        Vector3? attackForward = null, bool fighterAttack = false)
    {
        directAttackId = attackId;
        directTargets.Clear();
        if (targets != null) directTargets.UnionWith(targets);
        directWaveEffect = null;
        directAttackForward = Vector3.zero;
        if (attackId == 0 || inventory?.EquipmentSystem == null ||
            !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance weapon) ||
            weapon?.definition == null)
            return;

        if (fighterAttack && attackForward.HasValue && weapon.definition.characterClass == CharacterClass.Fighter)
        {
            Vector3 forward = attackForward.Value;
            forward.y = 0f;
            if (float.IsFinite(forward.x) && float.IsFinite(forward.z) && forward.sqrMagnitude > 0.0001f)
            {
                directAttackForward = forward.normalized;
                directWaveEffect = weapon.definition.uniqueEffect as PhaseHarvesterWaveUniqueEffectSO;
            }
        }
    }

    /// <summary>SW 수정: 싱글·서버의 피해 적용 전에 실제 Fighter Direct 대상의 파동 설정과 공격 정면을 값으로 확보한다.</summary>
    internal bool TryGetPhaseHarvesterSource(in WBH_DamageRequest request,
        out PhaseHarvesterWaveUniqueEffectSO effect, out Vector3 forward)
    {
        effect = directWaveEffect;
        forward = directAttackForward;
        return CanExecute && request.DamageCause == DamageCause.Direct && request.AttackType == WBH_AttackType.Normal &&
            request.AttackId != 0 && request.AttackId == directAttackId &&
            effect != null && directTargets.Contains(request.Target);
    }

    /// <summary>SW 수정: 실제 Fighter 기본 공격의 생존→사망 확정 뒤 싱글·서버에서 최초 처치 위치의 파동을 FIFO에 한 번 등록한다. 보상과 처치 이벤트는 재발행하지 않는다.</summary>
    internal void FirePhaseHarvesterKill(uint attackId, Vector3 deathPosition, Vector3 forward,
        PhaseHarvesterWaveUniqueEffectSO effect)
    {
        if (!CanExecute || effect == null || attackId == 0 || lastPhaseHarvesterAttackId == attackId)
            return;
        lastPhaseHarvesterAttackId = attackId;
        string key = GetPhaseHarvesterCooldownKey(effect);
        double now = Now;
        if (cooldownEndTimes.TryGetValue(key, out double end) && now < end)
            return;
        if (!float.IsFinite(effect.length) || effect.length <= 0f ||
            !float.IsFinite(effect.width) || effect.width <= 0f ||
            !float.IsFinite(effect.damageMultiplier) || effect.damageMultiplier <= 0f ||
            !float.IsFinite(effect.cooldownSeconds) || effect.cooldownSeconds < 0f)
            return;

        cooldownEndTimes[key] = now + effect.cooldownSeconds;
        Vector3 start = deathPosition + Vector3.up;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        var targets = new List<WBH_ICombat>();
        var seen = new HashSet<WBH_ICombat>();
        int obstacles = LayerMask.GetMask("Wall", "Prop", "Ground");
        foreach (Collider hit in Physics.OverlapBox(start + forward * (effect.length * 0.5f),
                     new Vector3(effect.width * 0.5f, 1f, effect.length * 0.5f),
                     Quaternion.LookRotation(forward), 1 << 10, QueryTriggerInteraction.Collide))
        {
            WBH_ICombat target = PlayerCombatAuthority.FindCombatTarget(hit);
            if (target is not Component component || component == null || !component.gameObject.activeInHierarchy ||
                !seen.Add(target) || target.Status == null || target.Status.IsDead)
                continue;
            Vector3 position = component.GetComponentInParent<NetworkEnemyAuthority>()?.transform.position
                ?? component.transform.position;
            Vector3 offset = position - deathPosition;
            float distance = Vector3.Dot(offset, forward);
            // SW 수정: 본체 원점을 기준으로 통로를 제한하며 0.5m를 넘는 높이 차는 다른 층/단차로 제외한다.
            if (distance < 0f || distance > effect.length ||
                Mathf.Abs(Vector3.Dot(offset, right)) > effect.width * 0.5f || Mathf.Abs(offset.y) > 0.5f ||
                Physics.Linecast(start, position + Vector3.up, obstacles, QueryTriggerInteraction.Ignore))
                continue;
            targets.Add(target);
        }
        targets.Sort((left, rightTarget) => CompareBodyDistance(left, rightTarget, deathPosition));
        for (int index = 0; index < Mathf.Min(targets.Count, Mathf.Clamp(effect.maxTargets, 1, 16)); index++)
            PlayerDamageResolver.EnqueueFollowUpDamage(context, targets[index], ElementType.None,
                effect.damageMultiplier, null, DamageCause.Effect, attackId, canCrit: false);
        PhaseHarvesterPresented?.Invoke(start, start + forward * effect.length, effect.width);
    }

    /// <summary>SW 수정: 싱글·서버의 본체 범위 효과를 거리·netId·InstanceId 순으로 정렬해 Collider 순서와 무관한 대상 상한을 적용한다.</summary>
    private static int CompareBodyDistance(WBH_ICombat left, WBH_ICombat right, Vector3 origin)
    {
        Component a = (Component)left, b = (Component)right;
        Transform aRoot = a.GetComponentInParent<NetworkEnemyAuthority>()?.transform ?? a.transform;
        Transform bRoot = b.GetComponentInParent<NetworkEnemyAuthority>()?.transform ?? b.transform;
        int byDistance = (aRoot.position - origin).sqrMagnitude.CompareTo((bRoot.position - origin).sqrMagnitude);
        if (byDistance != 0) return byDistance;
        uint aId = a.GetComponentInParent<Mirror.NetworkIdentity>()?.netId ?? 0u;
        uint bId = b.GetComponentInParent<Mirror.NetworkIdentity>()?.netId ?? 0u;
        int byId = aId.CompareTo(bId);
        return byId != 0 ? byId : a.GetInstanceID().CompareTo(b.GetInstanceID());
    }

    /// <summary>SW 수정: 같은 소유자의 동일 효과는 장비 개체와 무관하게 공유하며 싱글·서버·HUD에서 같은 키로 조회한다.</summary>
    internal static string GetPhaseHarvesterCooldownKey(PhaseHarvesterWaveUniqueEffectSO effect)
        => effect.name + ":phase-harvester";

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

    /// <summary>SW 수정: 싱글·서버의 장비 효과별 남은 시간을 같은 소유자의 저장된 쿨다운에서 조회한다.</summary>
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
            case PhaseHarvesterWaveUniqueEffectSO wave:
                cooldownSeconds = wave.cooldownSeconds;
                key = GetPhaseHarvesterCooldownKey(wave);
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

    /// <summary>SW 수정: 싱글·서버의 플레이어 수명 경계에서 공격별 중복·출처를 초기화하며 장비 교체·사망으로 쿨다운을 우회하지 않는다.</summary>
    public void ResetAttackLifetime()
    {
        while (grenadeEffects.Count > 0)
        {
            PlayerGrenadeEffect effect = grenadeEffects[0];
            grenadeEffects.RemoveAt(0);
            if (effect != null) effect.Finish();
        }
        lastChainAttackIds.Clear();
        lastPhaseHarvesterAttackId = 0;
        directWaveEffect = null;
        directAttackForward = Vector3.zero;
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

        int queuedCount = ChainLightningExecutor.Enqueue(
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

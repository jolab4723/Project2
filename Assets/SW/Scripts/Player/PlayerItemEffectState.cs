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
    // SW 수정: 실제 기본 공격의 정면·발사 원점과 장착 효과만 공격 범위 동안 보존한다.
    private Vector3 directAttackForward, directAttackOrigin;
    private PhaseHarvesterWaveUniqueEffectSO directWaveEffect;
    private StarBreacherExplosionUniqueEffectSO directExplosionEffect;
    // SW 수정: 폐열 값은 기존 BuffInstance 한 곳이 소유하고 여기에는 장착 생애·공격 중복·무적중 시각만 보존한다.
    private WasteHeatDischargeUniqueEffectSO heatEffect, directHeatEffect;
    private string heatItemInstanceId;
    private uint heatGeneration, directHeatGeneration, lastHeatAttackId;
    private double lastHeatHitTime;
    private uint lastPhaseHarvesterAttackId, lastStarBreacherAttackId;
    private readonly List<PlayerGrenadeEffect> grenadeEffects = new();
    private UniqueEffectSO hitEffect;
    private GunnerWeaponType hitWeapon;
    public event Action<Vector3, Vector3> ChainPresented;
    public event Action<Vector3> InfernoPresented;
    public event Action<Vector3, Vector3, float> PhaseHarvesterPresented;
    public event Action<Vector3, float> StarBreacherPresented;
    public event Action<Vector3, Vector3, float, float> WasteHeatPresented;
    public event Action<bool> WasteHeatReadyChanged;
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

    /// <summary>SW 수정: 싱글·서버의 실제 기본 공격 범위에서 직접 대상·Fighter 정면/원점/장착 생애 또는 Shotgun 발사 원점·무기 효과를 저장하고 종료 시 해제한다.</summary>
    public void SetDirectTargets(uint attackId, IEnumerable<WBH_ICombat> targets,
        Vector3? attackForward = null, bool fighterAttack = false,
        Vector3? attackOrigin = null, bool shotgunAttack = false)
    {
        directAttackId = attackId;
        directTargets.Clear();
        if (targets != null) directTargets.UnionWith(targets);
        directWaveEffect = null;
        directExplosionEffect = null;
        directHeatEffect = null;
        directAttackForward = Vector3.zero;
        directAttackOrigin = Vector3.zero;
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
                if (weapon.definition.uniqueEffect is WasteHeatDischargeUniqueEffectSO)
                {
                    ReconcileEquipment();
                    Vector3 origin = attackOrigin ?? context.transform.position;
                    if (float.IsFinite(origin.x) && float.IsFinite(origin.y) && float.IsFinite(origin.z))
                    {
                        directAttackOrigin = origin;
                        directHeatEffect = heatEffect;
                        directHeatGeneration = heatGeneration;
                    }
                }
            }
        }
        if (shotgunAttack && attackOrigin.HasValue && weapon.definition.characterClass == CharacterClass.Gunner &&
            weapon.definition.uniqueEffect is StarBreacherExplosionUniqueEffectSO explosion)
        {
            Vector3 origin = attackOrigin.Value;
            if (float.IsFinite(origin.x) && float.IsFinite(origin.y) && float.IsFinite(origin.z))
            {
                directAttackOrigin = origin;
                directExplosionEffect = explosion;
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

    /// <summary>SW 수정: 싱글·서버의 유효 Shotgun Direct 피해 전에 저장된 발사 원점과 실제 적중점의 3m 조건을 확인해 폭발 출처를 확보한다.</summary>
    internal bool TryGetStarBreacherSource(in WBH_DamageRequest request,
        out StarBreacherExplosionUniqueEffectSO effect, out Vector3 hitPosition)
    {
        effect = directExplosionEffect;
        hitPosition = request.HitPosition ?? Vector3.zero;
        return CanExecute && request.DamageCause == DamageCause.Direct && request.AttackType == WBH_AttackType.Normal &&
            request.AttackId != 0 && request.AttackId == directAttackId && directTargets.Contains(request.Target) &&
            effect != null && request.HitPosition.HasValue &&
            float.IsFinite(hitPosition.x) && float.IsFinite(hitPosition.y) && float.IsFinite(hitPosition.z) &&
            float.IsFinite(effect.triggerDistance) && effect.triggerDistance > 0f &&
            (hitPosition - directAttackOrigin).sqrMagnitude <= effect.triggerDistance * effect.triggerDistance;
    }

    /// <summary>SW 수정: 싱글·서버의 유효 Fighter 기본 피해 전에 폐열 출처·원점·정면·장착 생애를 확보하며 Skill/Effect/DoT는 거부한다.</summary>
    internal bool TryGetWasteHeatSource(in WBH_DamageRequest request, out WasteHeatDischargeUniqueEffectSO effect,
        out Vector3 origin, out Vector3 forward, out uint generation)
    {
        effect = directHeatEffect;
        origin = directAttackOrigin;
        forward = directAttackForward;
        generation = directHeatGeneration;
        return CanExecute && request.DamageCause == DamageCause.Direct && request.AttackType == WBH_AttackType.Normal &&
            request.AttackId != 0 && request.AttackId == directAttackId && directTargets.Contains(request.Target) &&
            effect != null && effect == heatEffect && generation == heatGeneration;
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

    /// <summary>SW 수정: 싱글·서버의 유효 Fighter 기본 적중을 공격당 한 번 충전하며 최대 열 다음 적중에 저장한 부채꼴의 비치명 Fire 피해를 동일 FIFO로 등록한다.</summary>
    internal void FireWasteHeatHit(uint attackId, WasteHeatDischargeUniqueEffectSO effect, Vector3 origin, Vector3 forward, uint generation)
    {
        if (!CanExecute || !context.isActiveAndEnabled || context.Controller?.Status == null || context.Controller.Status.IsDead ||
            effect == null || effect != heatEffect || generation != heatGeneration || attackId == 0 || lastHeatAttackId == attackId ||
            inventory?.EquipmentSystem == null || !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance weapon) ||
            weapon?.instanceId != heatItemInstanceId || weapon.definition.uniqueEffect != effect ||
            effect.requiredHeat < 1 || !float.IsFinite(effect.length) || effect.length <= 0f ||
            !float.IsFinite(effect.angleDegrees) || effect.angleDegrees <= 0f || effect.angleDegrees > 180f ||
            !float.IsFinite(effect.damageMultiplier) || effect.damageMultiplier <= 0f || effect.maxTargets < 1 || effect.maxTargets > 16 ||
            !float.IsFinite(effect.idleResetSeconds) || effect.idleResetSeconds <= 0f)
            return;
        BuffInstance heat = FindWasteHeatBuff();
        if (heat == null) return;
        double now = Now;
        int count = heat.stackCount;
        if (count > 0 && now - lastHeatHitTime >= effect.idleResetSeconds)
        {
            count = 0;
            buffs.SetBuffStack(effect, 0);
            if (generation != heatGeneration || heatEffect != effect || !context.isActiveAndEnabled || context.Controller.Status.IsDead) return;
            WasteHeatReadyChanged?.Invoke(false);
        }
        if (generation != heatGeneration || heatEffect != effect || !context.isActiveAndEnabled || context.Controller.Status.IsDead) return;
        lastHeatAttackId = attackId;
        lastHeatHitTime = now;
        if (count < effect.requiredHeat)
        {
            buffs.SetBuffStack(effect, count + 1);
            if (count + 1 == effect.requiredHeat && generation == heatGeneration && heatEffect == effect &&
                context.isActiveAndEnabled && !context.Controller.Status.IsDead)
                WasteHeatReadyChanged?.Invoke(true);
            return;
        }
        // SW 수정: 소비한 공격 번호도 유지해 같은 광역 공격의 다음 직접 표적에서 열 1이 다시 쌓이지 않게 한다.
        buffs.SetBuffStack(effect, 0);
        if (generation != heatGeneration || heatEffect != effect || !context.isActiveAndEnabled || context.Controller.Status.IsDead) return;
        WasteHeatReadyChanged?.Invoke(false);
        if (generation != heatGeneration || heatEffect != effect || !context.isActiveAndEnabled || context.Controller.Status.IsDead) return;
        var targets = new List<WBH_ICombat>();
        var seen = new HashSet<WBH_ICombat>();
        float minimumDot = Mathf.Cos(effect.angleDegrees * 0.5f * Mathf.Deg2Rad);
        Vector3 start = origin + Vector3.up;
        int obstacles = LayerMask.GetMask("Wall", "Prop", "Ground");
        foreach (Collider hit in Physics.OverlapSphere(start, effect.length, 1 << 10, QueryTriggerInteraction.Collide))
        {
            WBH_ICombat target = PlayerCombatAuthority.FindCombatTarget(hit);
            if (target is not Component component || component == null || !component.gameObject.activeInHierarchy ||
                !seen.Add(target) || target.Status == null || target.Status.IsDead) continue;
            Vector3 position = component.GetComponentInParent<NetworkEnemyAuthority>()?.transform.position ?? component.transform.position;
            Vector3 offset = position - origin;
            if (Mathf.Abs(offset.y) > 0.5f) continue;
            offset.y = 0f;
            // SW 수정: A1과 같은 본체 원점 기준을 쓰며 큰 Collider의 표면만 부채꼴에 들어온 적은 제외한다.
            if (offset.sqrMagnitude > effect.length * effect.length ||
                (offset.sqrMagnitude > 0.0001f && Vector3.Dot(offset.normalized, forward) < minimumDot) ||
                Physics.Linecast(start, position + Vector3.up, obstacles, QueryTriggerInteraction.Ignore)) continue;
            targets.Add(target);
        }
        targets.Sort((left, rightTarget) => CompareBodyDistance(left, rightTarget, origin));
        for (int index = 0; index < Mathf.Min(targets.Count, effect.maxTargets); index++)
            PlayerDamageResolver.EnqueueFollowUpDamage(context, targets[index], ElementType.Fire,
                effect.damageMultiplier, null, DamageCause.Effect, attackId, canCrit: false);
        WasteHeatPresented?.Invoke(start, forward, effect.length, effect.angleDegrees);
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

    /// <summary>SW 수정: 싱글·서버의 최초 유효 근거리 Shotgun 명중 뒤 실제 피격점에서 Fire 비치명타 폭발을 FIFO에 한 번 등록한다. 시작 표적의 직접타 사망과 무관하며 추가 Burn은 예약하지 않는다.</summary>
    internal void FireStarBreacherHit(uint attackId, Vector3 hitPosition, Vector3 sourceBodyPosition,
        StarBreacherExplosionUniqueEffectSO effect)
    {
        if (!CanExecute || effect == null || attackId == 0 || lastStarBreacherAttackId == attackId)
            return;
        lastStarBreacherAttackId = attackId;
        string key = GetStarBreacherCooldownKey(effect);
        double now = Now;
        if (cooldownEndTimes.TryGetValue(key, out double end) && now < end)
            return;
        if (!float.IsFinite(effect.radius) || effect.radius <= 0f ||
            !float.IsFinite(effect.damageMultiplier) || effect.damageMultiplier <= 0f ||
            !float.IsFinite(effect.cooldownSeconds) || effect.cooldownSeconds < 0f)
            return;

        cooldownEndTimes[key] = now + effect.cooldownSeconds;
        var candidates = new Dictionary<WBH_ICombat, Vector3>();
        int obstacles = LayerMask.GetMask("Wall", "Prop", "Ground");
        foreach (Collider hit in Physics.OverlapSphere(hitPosition, effect.radius, 1 << 10, QueryTriggerInteraction.Collide))
        {
            WBH_ICombat target = PlayerCombatAuthority.FindCombatTarget(hit);
            if (target is not Component component || component == null || !component.gameObject.activeInHierarchy ||
                target.Status == null || target.Status.IsDead)
                continue;
            Vector3 bodyPosition = component.GetComponentInParent<NetworkEnemyAuthority>()?.transform.position
                ?? component.transform.position;
            Vector3 point = hit.ClosestPoint(hitPosition);
            float distanceSquared = (point - hitPosition).sqrMagnitude;
            // SW 수정: 피격점의 높이와 발 위치를 혼동하지 않고 A1과 같은 본체 간 0.5m 단차 기준을 쓴다.
            if (Mathf.Abs(bodyPosition.y - sourceBodyPosition.y) > 0.5f ||
                distanceSquared > effect.radius * effect.radius ||
                Physics.Linecast(hitPosition, point, obstacles, QueryTriggerInteraction.Ignore))
                continue;
            // SW 수정: 유효한 Collider만 모아 같은 본체의 가장 가까운 실제 표면을 한 번 선택한다.
            if (!candidates.TryGetValue(target, out Vector3 previous) ||
                distanceSquared < (previous - hitPosition).sqrMagnitude)
                candidates[target] = point;
        }
        var targets = new List<WBH_ICombat>(candidates.Keys);
        targets.Sort((left, right) =>
        {
            int byDistance = (candidates[left] - hitPosition).sqrMagnitude.CompareTo((candidates[right] - hitPosition).sqrMagnitude);
            if (byDistance != 0) return byDistance;
            Component a = (Component)left, b = (Component)right;
            uint aId = a.GetComponentInParent<Mirror.NetworkIdentity>()?.netId ?? 0u;
            uint bId = b.GetComponentInParent<Mirror.NetworkIdentity>()?.netId ?? 0u;
            int byId = aId.CompareTo(bId);
            return byId != 0 ? byId : a.GetInstanceID().CompareTo(b.GetInstanceID());
        });
        for (int index = 0; index < Mathf.Min(targets.Count, Mathf.Clamp(effect.maxTargets, 1, 16)); index++)
            PlayerDamageResolver.EnqueueFollowUpDamage(context, targets[index], ElementType.Fire,
                effect.damageMultiplier, null, DamageCause.Effect, attackId, canCrit: false);
        StarBreacherPresented?.Invoke(hitPosition, effect.radius);
    }

    /// <summary>SW 수정: 싱글·서버·HUD가 같은 소유자의 스타 브리처 효과 쿨다운을 장비 개체와 무관하게 공유한다.</summary>
    internal static string GetStarBreacherCooldownKey(StarBreacherExplosionUniqueEffectSO effect)
        => effect.name + ":star-breacher";

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
            case StarBreacherExplosionUniqueEffectSO explosion:
                cooldownSeconds = explosion.cooldownSeconds;
                key = GetStarBreacherCooldownKey(explosion);
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

    /// <summary>SW 수정: 싱글·서버에서 실제 장착 생애를 재조정하고 무적중 5초 등 설정된 시간이 지나면 폐열 스택과 준비 표시를 0으로 돌린다.</summary>
    internal void Tick()
    {
        if (!CanExecute) return;
        ReconcileEquipment();
        BuffInstance heat = FindWasteHeatBuff();
        if (heat == null || heat.stackCount == 0 || Now - lastHeatHitTime < heatEffect.idleResetSeconds) return;
        WasteHeatDischargeUniqueEffectSO effect = heatEffect;
        uint generation = heatGeneration;
        buffs.SetBuffStack(effect, 0);
        // SW 수정: 스탯 갱신 콜백이 새 장착 생애를 충전했다면 이전 만료의 표시로 덮지 않는다.
        if (generation != heatGeneration || heatEffect != effect || !context.isActiveAndEnabled || context.Controller.Status.IsDead) return;
        WasteHeatReadyChanged?.Invoke(false);
    }

    /// <summary>SW 수정: 싱글·서버의 플레이어 수명 경계에서 공격별 중복·출처와 폐열을 초기화하며 장비 교체·사망으로 쿨다운을 우회하지 않는다.</summary>
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
        lastStarBreacherAttackId = 0;
        directWaveEffect = null;
        directExplosionEffect = null;
        directHeatEffect = null;
        directAttackForward = Vector3.zero;
        directAttackOrigin = Vector3.zero;
        directTargets.Clear();
        directAttackId = hitAttackId = 0;
        hitEffect = null;
        ClearWasteHeat();
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

    /// <summary>SW 수정: 싱글·서버에서 장착 인스턴스가 바뀌거나 플레이어가 죽으면 폐열·준비를 정리하고, 유효한 폐열 무기의 기존 버프 아이콘을 0스택으로 연결한다.</summary>
    public void ReconcileEquipment()
    {
        if (CanExecute)
        {
            ItemInstance heatWeapon = null;
            bool alive = context.isActiveAndEnabled && context.Controller?.Status != null && !context.Controller.Status.IsDead;
            if (alive && inventory?.EquipmentSystem != null)
                inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out heatWeapon);
            var equippedHeat = heatWeapon?.definition?.characterClass == CharacterClass.Fighter
                ? heatWeapon.definition.uniqueEffect as WasteHeatDischargeUniqueEffectSO : null;
            string equippedHeatInstanceId = equippedHeat != null ? heatWeapon.instanceId : null;
            if (equippedHeat != heatEffect || equippedHeatInstanceId != heatItemInstanceId)
            {
                uint clearedGeneration = heatGeneration + 1;
                ClearWasteHeat();
                if (equippedHeat != null && heatGeneration == clearedGeneration &&
                    IsCurrentWasteHeatWeapon(equippedHeat, equippedHeatInstanceId))
                {
                    heatEffect = equippedHeat;
                    heatItemInstanceId = equippedHeatInstanceId;
                }
            }
            if (heatEffect != null && buffs != null && FindWasteHeatBuff() == null)
            {
                // SW 수정: 기존 Tracker는 신규 0스택 생성을 거절하므로 실제 표시 버프를 만든 뒤 0으로 설정한다.
                WasteHeatDischargeUniqueEffectSO source = heatEffect;
                string instanceId = heatItemInstanceId;
                uint generation = heatGeneration;
                buffs.ApplyBuff(source);
                if (heatGeneration == generation && heatEffect == source &&
                    IsCurrentWasteHeatWeapon(source, instanceId)) buffs.SetBuffStack(source, 0);
            }
        }
        if (!preparedAttackReady && empoweredAttackId == 0)
            return;
        if (!TryGetPreparedAttackEffect(out ItemInstance weapon, out _) ||
            preparedAttackSourceInstanceId != weapon.instanceId)
            ClearPreparedAttack();
    }

    /// <summary>SW 수정: 싱글·서버의 현재 폐열 표시 버프를 읽으며 별도 열 복제 상태나 검증 전용 공개 API를 만들지 않는다.</summary>
    private BuffInstance FindWasteHeatBuff()
    {
        if (heatEffect == null || buffs == null) return null;
        foreach (BuffInstance buff in buffs.ActiveBuffs)
            if (ReferenceEquals(buff.source, heatEffect)) return buff;
        return null;
    }

    /// <summary>SW 수정: 버프 변경 콜백 뒤에도 싱글·서버 플레이어가 살아 있고 같은 폐열 무기를 실제로 장착했는지 확인한다.</summary>
    private bool IsCurrentWasteHeatWeapon(WasteHeatDischargeUniqueEffectSO effect, string instanceId)
        => context.isActiveAndEnabled && context.Controller?.Status != null && !context.Controller.Status.IsDead &&
           inventory?.EquipmentSystem != null &&
           inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance weapon) &&
           weapon?.instanceId == instanceId && weapon.definition?.characterClass == CharacterClass.Fighter &&
           weapon.definition.uniqueEffect == effect;

    /// <summary>SW 수정: 싱글·서버의 교체·사망·비활성화에서 열·적중 시각·공격 기록을 제거하고 피격 콜백 이전 출처의 재충전을 막도록 생애를 바꾼다.</summary>
    private void ClearWasteHeat()
    {
        WasteHeatDischargeUniqueEffectSO previous = heatEffect;
        heatEffect = null;
        heatItemInstanceId = null;
        lastHeatAttackId = 0;
        lastHeatHitTime = 0;
        heatGeneration++;
        uint generation = heatGeneration;
        if (previous != null) buffs?.RemoveBuff(previous);
        if (heatGeneration == generation) WasteHeatReadyChanged?.Invoke(false);
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

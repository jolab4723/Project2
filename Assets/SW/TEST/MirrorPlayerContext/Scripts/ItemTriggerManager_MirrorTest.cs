using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>
/// WJ 원본 <c>ItemTriggerManager</c>의 PlayerContext 전환 검증용 복제본이다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Player/ItemTriggerManager.cs</c></para>
/// <para><c>Instance</c>와 로컬 NetworkIdentity 판정을 제거하고, 같은 플레이어의 Inventory·Health·Buff·StateMachine을 직접 참조한다.</para>
/// <para>피격·회피·처치 호출은 이 컴포넌트가 소유한 플레이어의 장비와 유물만 검사한다.</para>
/// <para>고유 효과 SO에 있던 공유 쿨타임 대신 플레이어 컴포넌트의 딕셔너리에 아이템별 실행 시간을 보관한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemTriggerManager_MirrorTest : NetworkBehaviour
{
    [SerializeField] private PlayerContext context;
    [SerializeField] private InventoryController inventory;
    [SerializeField] private PlayerHealthManager health;
    [SerializeField] private PlayerBuffManager buffs;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;
    private UniqueEffectPresentation_MirrorTest presentation;
    private bool missingInfernoPresenterReported;
    private double nextDodgeTriggerAt;
    private string preparedAttackSourceInstanceId;
    private uint empoweredAttackId;

    private readonly SyncDictionary<string, double> cooldownEndTimes = new();
    private readonly Dictionary<ChainLightningUniqueEffectSO, uint> lastChainAttackIds = new();

    [SyncVar] private uint chainLightningTriggerCount;
    [SyncVar] private uint chainLightningResolvedHitCount;
    [SyncVar] private uint infernoTriggerCount;
    [SyncVar] private uint infernoResolvedHitCount;
    [SyncVar] private uint glassRailTriggerCount;
    [SyncVar] private uint glassRailResolvedHitCount;
    [SyncVar(hook = nameof(OnPreparedAttackChanged))] private bool preparedAttackReady;
    [SyncVar] private uint preparedAttackConsumeCount;

    public static uint LocalChainLightningPresentationCount { get; private set; }
    public uint ChainLightningTriggerCount => chainLightningTriggerCount;
    public uint ChainLightningResolvedHitCount => chainLightningResolvedHitCount;
    public uint InfernoTriggerCount => infernoTriggerCount;
    public uint InfernoResolvedHitCount => infernoResolvedHitCount;
    public uint GlassRailTriggerCount => glassRailTriggerCount;
    public uint GlassRailResolvedHitCount => glassRailResolvedHitCount;
    public bool PreparedAttackReady => preparedAttackReady;
    public uint PreparedAttackConsumeCount => preparedAttackConsumeCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDiagnostics()
    {
        LocalChainLightningPresentationCount = 0;
    }

    public int ActiveCooldownCount
    {
        get
        {
            int count = 0;
            foreach (KeyValuePair<string, double> pair in cooldownEndTimes)
            {
                if (pair.Value > NetworkTime.time)
                    count++;
            }

            return count;
        }
    }

    private void Awake()
    {
        context ??= GetComponent<PlayerContext>();
        inventory ??= GetComponentInChildren<InventoryController>(true);
        health ??= GetComponent<PlayerHealthManager>();
        buffs ??= GetComponent<PlayerBuffManager>();
        stateMachine ??= GetComponent<WBH_PlayerStateMachine>();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDamageTaken += HandleHitTaken;
            health.OnDeath += HandleDeath;
        }

        if (stateMachine != null)
            stateMachine.OnEnterState += HandleStateEntered;

        if (isClient)
            SetPreparedAttackPresentation(preparedAttackReady);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamageTaken -= HandleHitTaken;
            health.OnDeath -= HandleDeath;
        }

        if (stateMachine != null)
            stateMachine.OnEnterState -= HandleStateEntered;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        context ??= GetComponent<PlayerContext>();
        if (context?.Equipment != null)
        {
            context.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            context.Equipment.OnEquipmentChanged += HandleEquipmentChanged;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        SetPreparedAttackPresentation(preparedAttackReady);
    }

    public override void OnStopServer()
    {
        if (context?.Equipment != null)
            context.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        ClearPreparedAttack();
        base.OnStopServer();
    }

    /// <summary>공격 적중 피해의 Direct 전용 고유효과 진입점이다.</summary>
    public void FireDamageDealt(in WBH_DamageResult result, WBH_ICombat firstTarget = null)
    {
        if (result.DamageCause != DamageCause.Direct)
            return;
        if (netIdentity == null || !netIdentity.isServer || inventory == null || buffs == null)
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

    public void Fire(TriggerCondition condition)
    {
        if (netIdentity == null || !netIdentity.isServer || inventory == null || buffs == null)
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
            ? Mathf.Max(0f, (float)(cooldownEnd - NetworkTime.time))
            : 0f;
    }

    /// <summary>공격 번호가 다시 시작되는 서버 수명 경계에서 공격별 중복 기록만 초기화한다.</summary>
    [Server]
    public void ResetAttackLifetime()
    {
        lastChainAttackIds.Clear();
        ClearPreparedAttack();
    }

    /// <summary>유효한 직접 기본 공격이 확정될 때 공격 번호당 한 번 준비 상태를 소비합니다.</summary>
    [Server]
    public float ConsumePreparedAttackMultiplier(DamageCause cause, uint attackId)
    {
        if (cause != DamageCause.Direct || attackId == 0)
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
        preparedAttackConsumeCount++;
        return ValidDamageMultiplier(effect);
    }

    [Server]
    private void TryFireChainLightning(in WBH_DamageResult result, WBH_ICombat firstTarget)
    {
        context ??= GetComponent<PlayerContext>();
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
        double now = NetworkTime.time;
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
                RpcPresentChainLightning(segmentStart, segmentEnd);
            });

        if (queuedCount <= 0)
            return;

        chainLightningTriggerCount++;
        cooldownEndTimes[cooldownKey] = now + Mathf.Max(0f, effect.cooldownSeconds);
    }

    /// <summary>실제 Fighter 근접 기본 공격의 살아 있는 직접 대상에게 화염 후속 피해를 한 번 등록한다.</summary>
    [Server]
    private void TryFireInfernoExtraHit(in WBH_DamageResult result, WBH_ICombat firstTarget)
    {
        context ??= GetComponent<PlayerContext>();
        if (result.AttackId == 0 || firstTarget?.Status == null || firstTarget.Status.IsDead ||
            context?.CombatAuthority == null ||
            !context.CombatAuthority.IsDirectTargetForAttack(result.AttackId, firstTarget) ||
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

        if (!WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(
                context, firstTarget, ElementType.Fire, effect.damageMultiplier,
                WBH_StatusEffectPresets.Burn1, DamageCause.Effect, result.AttackId,
                _ =>
                {
                    infernoResolvedHitCount++;
                    if (hasImpactPoint && netIdentity != null && netIdentity.netId != 0 &&
                        NetworkServer.spawned.TryGetValue(netIdentity.netId, out NetworkIdentity spawnedIdentity) &&
                        spawnedIdentity == netIdentity)
                    {
                        RpcPresentInfernoHit(impactPoint);
                    }
                },
                canCrit: false))
        {
            return;
        }

        infernoTriggerCount++;
    }

    /// <summary>발사 당시 유리빛 궤도 효과를 품고 날아간 라이플 탄에 냉기 추가타를 등록한다. (무기 교체·해제 후에도 유지)</summary>
    [Server]
    private void TryFireGlassRailExtraHit(in WBH_DamageResult result, WBH_ICombat firstTarget)
    {
        context ??= GetComponent<PlayerContext>();
        if (result.AttackId == 0 || firstTarget?.Status == null || firstTarget.Status.IsDead ||
            context?.CombatAuthority == null)
        {
            return;
        }

        if (!context.CombatAuthority.TryGetGunnerHitSource(result.AttackId, out UniqueEffectSO sourceEffect, out GunnerWeaponType weaponType) ||
            weaponType != GunnerWeaponType.Rifle ||
            sourceEffect is not GlassRailExtraHitUniqueEffectSO effect)
        {
            return;
        }

        if (!WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(
                context, firstTarget, ElementType.Ice, effect.damageMultiplier,
                null, DamageCause.Effect, result.AttackId,
                _ => glassRailResolvedHitCount++,
                canCrit: false))
        {
            return;
        }

        glassRailTriggerCount++;
    }

    [ClientRpc(channel = Channels.Reliable)]
    private void RpcPresentInfernoHit(Vector3 position)
    {
        if (presentation == null)
            presentation = GetComponent<UniqueEffectPresentation_MirrorTest>();
        if (presentation != null)
        {
            presentation.PresentInfernoHit(position);
            return;
        }

        if (!missingInfernoPresenterReported)
        {
            missingInfernoPresenterReported = true;
            Debug.LogWarning("[ItemTriggerManager_MirrorTest] 설정된 인페르노 Presenter가 없습니다.", this);
        }
    }

    [ClientRpc]
    private void RpcPresentChainLightning(Vector3 start, Vector3 end)
    {
        LocalChainLightningPresentationCount++;
        presentation ??= GetComponent<UniqueEffectPresentation_MirrorTest>();
        presentation ??= gameObject.AddComponent<UniqueEffectPresentation_MirrorTest>();
        presentation.PresentChainLightning(start, end);
    }

    private void HandleHitTaken(float amount)
    {
        Fire(TriggerCondition.OnHitTaken);
    }

    private void HandleStateEntered(PlayerState state)
    {
        if (state == PlayerState.Dead)
        {
            if (isServer) ClearPreparedAttack();
            return;
        }
        if (state != PlayerState.Dodge)
            return;
        if (isServer)
            ConfirmDodgeTrigger();
        else if (isLocalPlayer)
            CmdNotifyDodge();
    }

    /// <summary>원격 소유자가 실제 회피 상태에 들어갔을 때 서버에 알립니다.</summary>
    [Command]
    private void CmdNotifyDodge()
    {
        ConfirmDodgeTrigger();
    }

    /// <summary>회피 이동은 기존 소유자 경로를 유지하고, 장비 발동은 서버의 생존·조작·대기 시간으로 제한합니다.</summary>
    private void ConfirmDodgeTrigger()
    {
        if (!isServer || health == null || health.CurrentHealth <= 0f ||
            context?.Controller == null || !context.Controller.IsControlEnabled ||
            NetworkTime.time < nextDodgeTriggerAt)
            return;
        var status = GetComponent<WBH_PlayerStatus>();
        if (status == null)
            return;
        nextDodgeTriggerAt = NetworkTime.time + Mathf.Max(0f, status.DodgeCooltime);
        Fire(TriggerCondition.OnDodge);
        PrepareDodgeAttack();
    }

    private void HandleDeath()
    {
        if (isServer)
            ClearPreparedAttack();
    }

    [Server]
    private void PrepareDodgeAttack()
    {
        if (!TryGetPreparedAttackEffect(out ItemInstance weapon, out _))
            return;
        preparedAttackSourceInstanceId = weapon.instanceId;
        empoweredAttackId = 0;
        preparedAttackReady = true;
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

    private void HandleEquipmentChanged(EquippedItemInfo[] equipmentSnapshot)
    {
        if (!isServer || !preparedAttackReady && empoweredAttackId == 0)
            return;
        if (!TryGetPreparedAttackEffect(out ItemInstance weapon, out _) ||
            preparedAttackSourceInstanceId != weapon.instanceId)
            ClearPreparedAttack();
    }

    [Server]
    private void ClearPreparedAttack()
    {
        preparedAttackReady = false;
        preparedAttackSourceInstanceId = null;
        empoweredAttackId = 0;
    }

    private void OnPreparedAttackChanged(bool _, bool ready)
        => SetPreparedAttackPresentation(ready);

    private void SetPreparedAttackPresentation(bool ready)
    {
        if (!isClient) return;
        presentation ??= GetComponent<UniqueEffectPresentation_MirrorTest>();
        presentation ??= gameObject.AddComponent<UniqueEffectPresentation_MirrorTest>();
        presentation.SetPreparedAttack(ready);
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
        double now = NetworkTime.time;
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
            GetComponent<PlayerInventorySync_MirrorTest>()?.ServerSyncPersistedStack(item);
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

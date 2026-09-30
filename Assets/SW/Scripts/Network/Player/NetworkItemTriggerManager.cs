using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>
/// 공통 PlayerItemEffectState의 서버 권한·관찰자 동기화 어댑터다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Player/ItemTriggerManager.cs</c></para>
/// <para><c>Instance</c>와 로컬 NetworkIdentity 판정을 제거하고, 같은 플레이어의 Inventory·Health·Buff·StateMachine을 직접 참조한다.</para>
/// <para>피격·회피·처치 호출은 이 컴포넌트가 소유한 플레이어의 장비와 유물만 검사한다.</para>
/// <para>고유 효과 SO에 있던 공유 쿨타임 대신 플레이어 컴포넌트의 딕셔너리에 아이템별 실행 시간을 보관한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkItemTriggerManager : NetworkBehaviour
{
    [SerializeField] private PlayerContext context;
    [SerializeField] private InventoryController inventory;
    [SerializeField] private PlayerHealthManager health;
    [SerializeField] private PlayerBuffManager buffs;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;
    private UniqueEffectPresentation presentation;
    private bool missingInfernoPresenterReported;
    private double nextDodgeTriggerAt;

    private readonly SyncDictionary<string, double> cooldownEndTimes = new();

    [SyncVar] private uint chainLightningTriggerCount;
    [SyncVar] private uint chainLightningResolvedHitCount;
    [SyncVar] private uint infernoTriggerCount;
    [SyncVar] private uint infernoResolvedHitCount;
    [SyncVar] private uint glassRailTriggerCount;
    [SyncVar] private uint glassRailResolvedHitCount;
    [SyncVar(hook = nameof(OnPreparedAttackChanged))] private bool preparedAttackReady;
    [SyncVar] private uint preparedAttackConsumeCount;
    [SyncVar(hook = nameof(OnWasteHeatReadyChanged))] private bool wasteHeatReady;

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

    /// <summary>SW 수정: 실제 체력·상태 구독을 연결하고 클라이언트의 기존 준비 상태와 폐열 충전 완료 표시를 복원한다.</summary>
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
        {
            SetPreparedAttackPresentation(preparedAttackReady);
            SetWasteHeatPresentation(wasteHeatReady);
        }
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

    /// <summary>SW 수정: 서버 소유 플레이어의 확정 효과만 구독해 관찰자에게 한 번 표시하고 기존 장비 구독을 유지한다.</summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        context ??= GetComponent<PlayerContext>();
        context.Effects.ChainPresented += RpcPresentChainLightning;
        context.Effects.InfernoPresented += RpcPresentInfernoHit;
        context.Effects.PhaseHarvesterPresented += RpcPresentPhaseHarvesterWave;
        context.Effects.StarBreacherPresented += RpcPresentStarBreacherExplosion;
        context.Effects.WasteHeatPresented += RpcPresentWasteHeatDischarge;
        context.Effects.WasteHeatReadyChanged += SetWasteHeatReady;
        context.Effects.StackChanged += SyncStack;
        if (context?.Equipment != null)
        {
            context.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            context.Equipment.OnEquipmentChanged += HandleEquipmentChanged;
        }
    }

    /// <summary>SW 수정: 늦게 참여한 관찰자도 서버가 복제한 준비 상태와 폐열 충전 완료 표시를 적용한다.</summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        SetPreparedAttackPresentation(preparedAttackReady);
        SetWasteHeatPresentation(wasteHeatReady);
    }

    /// <summary>SW 수정: 서버 종료 시 소유 플레이어의 효과 표시·장비 구독을 해제하며 공격 수명 정리는 기존 경로를 따른다.</summary>
    public override void OnStopServer()
    {
        if (context?.Equipment != null)
            context.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        ClearPreparedAttack();
        context.Effects.ChainPresented -= RpcPresentChainLightning;
        context.Effects.InfernoPresented -= RpcPresentInfernoHit;
        context.Effects.PhaseHarvesterPresented -= RpcPresentPhaseHarvesterWave;
        context.Effects.StarBreacherPresented -= RpcPresentStarBreacherExplosion;
        context.Effects.WasteHeatPresented -= RpcPresentWasteHeatDischarge;
        context.Effects.WasteHeatReadyChanged -= SetWasteHeatReady;
        wasteHeatReady = false;
        context.Effects.StackChanged -= SyncStack;
        base.OnStopServer();
    }

    /// <summary>공격 적중 피해의 Direct 전용 고유효과 진입점이다.</summary>
    public void FireDamageDealt(in WBH_DamageResult result, WBH_ICombat firstTarget = null)
    {
        if (isServer) context.Effects.FireDamageDealt(result, firstTarget);
        PublishState();
    }

    public void Fire(TriggerCondition condition)
    {
        if (isServer) context.Effects.Fire(condition);
        PublishState();
    }

    /// <summary>SW 수정: 클라이언트는 서버가 복제한 소유자별 쿨다운으로 장비 효과의 남은 시간을 조회한다.</summary>
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
                key = PlayerItemEffectState.GetPhaseHarvesterCooldownKey(wave);
                break;
            case StarBreacherExplosionUniqueEffectSO explosion:
                cooldownSeconds = explosion.cooldownSeconds;
                key = PlayerItemEffectState.GetStarBreacherCooldownKey(explosion);
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

    public void ResetAttackLifetime()
    {
        context.Effects.ResetAttackLifetime();
        PublishState();
    }

    public float ConsumePreparedAttackMultiplier(DamageCause cause, uint attackId)
    {
        float multiplier = context.Effects.ConsumePreparedAttackMultiplier(cause, attackId);
        PublishState();
        return multiplier;
    }

    [ClientRpc(channel = Channels.Reliable)]
    private void RpcPresentInfernoHit(Vector3 position)
    {
        if (presentation == null)
            presentation = GetComponent<UniqueEffectPresentation>();
        if (presentation != null)
        {
            presentation.PresentInfernoHit(position);
            return;
        }

        if (!missingInfernoPresenterReported)
        {
            missingInfernoPresenterReported = true;
            Debug.LogWarning("[NetworkItemTriggerManager] 설정된 인페르노 Presenter가 없습니다.", this);
        }
    }

    /// <summary>SW 수정: 서버가 확정한 즉시 처형 파동을 신뢰 채널로 관찰자에게 표시하며 Host도 RPC 한 경로만 사용한다.</summary>
    [ClientRpc(channel = Channels.Reliable)]
    private void RpcPresentPhaseHarvesterWave(Vector3 start, Vector3 end, float width)
    {
        presentation ??= GetComponent<UniqueEffectPresentation>();
        presentation ??= gameObject.AddComponent<UniqueEffectPresentation>();
        presentation.PresentPhaseHarvesterWave(start, end, width);
    }

    /// <summary>SW 수정: 서버 확정 Shotgun 폭발의 피격점·반경을 신뢰 채널로 관찰자에게 표시하며 Host도 RPC 한 경로만 사용한다.</summary>
    [ClientRpc(channel = Channels.Reliable)]
    private void RpcPresentStarBreacherExplosion(Vector3 position, float radius)
    {
        presentation ??= GetComponent<UniqueEffectPresentation>();
        presentation ??= gameObject.AddComponent<UniqueEffectPresentation>();
        presentation.PresentStarBreacherExplosion(position, radius);
    }

    /// <summary>SW 수정: 서버가 확정한 폐열 방출 영역을 Reliable RPC로 관찰자에게 한 번 표시하며 피해는 서버 FIFO에서만 처리한다.</summary>
    [ClientRpc(channel = Channels.Reliable)]
    private void RpcPresentWasteHeatDischarge(Vector3 origin, Vector3 forward, float length, float angleDegrees)
    {
        presentation ??= GetComponent<UniqueEffectPresentation>();
        presentation ??= gameObject.AddComponent<UniqueEffectPresentation>();
        presentation.PresentWasteHeatDischarge(origin, forward, length, angleDegrees);
    }

    [ClientRpc]
    private void RpcPresentChainLightning(Vector3 start, Vector3 end)
    {
        LocalChainLightningPresentationCount++;
        presentation ??= GetComponent<UniqueEffectPresentation>();
        presentation ??= gameObject.AddComponent<UniqueEffectPresentation>();
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
            ResetAttackLifetime();
    }

    [Server]
    private void PrepareDodgeAttack()
    {
        context.Effects.PrepareDodgeAttack();
        PublishState();
    }



    private void HandleEquipmentChanged(EquippedItemInfo[] equipmentSnapshot)
    {
        if (isServer) context.Effects.ReconcileEquipment();
        PublishState();
    }

    [Server]
    private void ClearPreparedAttack()
    {
        context.Effects.ClearPreparedAttack();
        PublishState();
    }

    private void OnPreparedAttackChanged(bool _, bool ready)
        => SetPreparedAttackPresentation(ready);

    private void SetPreparedAttackPresentation(bool ready)
    {
        if (!isClient) return;
        presentation ??= GetComponent<UniqueEffectPresentation>();
        presentation ??= gameObject.AddComponent<UniqueEffectPresentation>();
        presentation.SetPreparedAttack(ready);
    }

    /// <summary>SW 수정: 서버 소유 열의 충전 완료 여부만 기존 관찰자 SyncVar 경로로 전달한다. 열 개수의 원본은 BuffInstance에 둔다.</summary>
    private void SetWasteHeatReady(bool ready) => wasteHeatReady = ready;

    /// <summary>SW 수정: 서버가 복제한 폐열 충전 완료 상태를 클라이언트 실제 장착 무기의 짧은 발광으로 표시한다.</summary>
    private void OnWasteHeatReadyChanged(bool _, bool ready) => SetWasteHeatPresentation(ready);

    /// <summary>SW 수정: 클라이언트만 폐열 충전 완료 표시를 적용해 Host에서 서버 표시가 중복되지 않도록 한다.</summary>
    private void SetWasteHeatPresentation(bool ready)
    {
        if (!isClient) return;
        presentation ??= GetComponent<UniqueEffectPresentation>();
        presentation ??= gameObject.AddComponent<UniqueEffectPresentation>();
        presentation.SetWasteHeatReady(ready);
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

    private void SyncStack(ItemInstance item) => GetComponent<PlayerInventorySync>()?.ServerSyncPersistedStack(item);
    private void LateUpdate() => PublishState();
    private void PublishState()
    {
        if (!isServer || context == null) return;
        var effects = context.Effects;
        chainLightningTriggerCount = effects.ChainLightningTriggerCount;
        chainLightningResolvedHitCount = effects.ChainLightningResolvedHitCount;
        infernoTriggerCount = effects.InfernoTriggerCount;
        infernoResolvedHitCount = effects.InfernoResolvedHitCount;
        glassRailTriggerCount = effects.GlassRailTriggerCount;
        glassRailResolvedHitCount = effects.GlassRailResolvedHitCount;
        preparedAttackReady = effects.PreparedAttackReady;
        preparedAttackConsumeCount = effects.PreparedAttackConsumeCount;
        foreach (var pair in effects.Cooldowns)
            if (!cooldownEndTimes.TryGetValue(pair.Key, out double end) || end != pair.Value)
                cooldownEndTimes[pair.Key] = pair.Value;
    }
}

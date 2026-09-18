using System;
using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>
/// Mirror 테스트 플레이어의 실제 Stat·Buff·HP·MP·포션 상태를 서버 원본으로 유지하고
/// 모든 관전자에게 같은 최종값을 복제한다. 팀 원본 Manager에는 네트워크 책임을 추가하지 않는다.
/// <para>서버에서는 같은 <see cref="PlayerContext"/>의 Manager만 읽고, 클라이언트에서는
/// 서버 스냅샷을 그 Context의 기존 Manager에 투영해 HUD와 전투 어댑터가 같은 값을 읽게 한다.</para>
/// <para>정식 계정 서버에서는 클라이언트가 보고한 패시브 StatSet 대신 인증된 프로필을 서버가 불러와야 한다.</para>
/// <para>4-B 차이: 체력 0과 별도로 사망 여부를 스냅샷에 저장하고, 서버가 입력·Controller·타깃 제외 기준을 확정한다.
/// 기존 <c>T_PlayerController.Update</c>의 자동 부활 대신 서버 상황판의 명시적 테스트 부활만 허용한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerContext), typeof(NetworkShopPlayerState_MirrorTest))]
public sealed class PlayerRuntimeStateSync_MirrorTest : NetworkBehaviour
{
    private const int MaxSyncedBuffCount = 32;
    private const string TestBuffPath = "DataFiles/BuffData/3. GeneratedAssets/buff.attack_up";

    private static Dictionary<string, BuffDefinitionSO> buffDefinitions;
    private static Dictionary<string, UniqueEffectSO> uniqueEffects;

    [SerializeField] private PlayerContext context;

    [SyncVar(hook = nameof(HandleSnapshotJsonChanged))]
    private string snapshotJson;

    private NetworkShopPlayerState_MirrorTest shopPlayerState;
    private RuntimeSnapshot latestSnapshot;
    private uint nextRevision;
    private bool serverPublishQueued;
    private bool clientApplyQueued;
    private bool applyingClientSnapshot;
    private bool recalculatingServerStats;
    private bool testMutationActive;
    private float nextTimedBuffPublishAt;
    private uint reviveSequence;

    public bool HasSnapshot => latestSnapshot != null;
    public uint StateRevision => latestSnapshot?.revision ?? 0;
    public float CurrentHealth => latestSnapshot?.currentHealth ?? context?.Health?.CurrentHealth ?? 0f;
    public float MaxHealth => latestSnapshot?.maxHealth ?? context?.Health?.MaxHealth ?? 0f;
    public float CurrentMana => latestSnapshot?.currentMana ?? context?.Mana?.CurrentMana ?? 0f;
    public float MaxMana => latestSnapshot?.maxMana ?? context?.Mana?.MaxMana ?? 0f;
    public float AttackPower => latestSnapshot?.attackPower ?? context?.Stats?.Stat?.attackPower ?? 0f;
    public float DefensePower => latestSnapshot?.defensePower ?? context?.Stats?.Stat?.defensePower ?? 0f;
    public int ActiveBuffCount => latestSnapshot?.buffs?.Length ?? context?.Buffs?.ActiveBuffs?.Count ?? 0;
    public int PotionCharges => latestSnapshot?.potionCharges ?? context?.Potions?.CurrentCharges ?? 0;
    public int MaxPotionCharges => latestSnapshot?.maxPotionCharges ?? context?.Potions?.MaxCharges ?? 0;
    public bool TestMutationActive => latestSnapshot?.testMutationActive ?? testMutationActive;
    public bool IsDead => isServer
        ? (context?.Health?.CurrentHealth ?? 0f) <= 0f
        : (latestSnapshot?.isDead ?? (context?.Health?.CurrentHealth ?? 0f) <= 0f);

    public event Action StateApplied;

    private void Awake()
    {
        context ??= GetComponent<PlayerContext>();
        shopPlayerState = GetComponent<NetworkShopPlayerState_MirrorTest>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        context ??= GetComponent<PlayerContext>();
    }
#endif

    public override void OnStartServer()
    {
        base.OnStartServer();
        SubscribeServerState();
        RecalculateServerStats();
        serverPublishQueued = true;
    }

    public override void OnStopServer()
    {
        UnsubscribeServerState();
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isServer)
            return;

        // 서버만 시간 경과·자동 재생·아이템 발동을 계산한다. 클라이언트 Manager는 스냅샷 표시용이다.
        if (context?.Buffs != null)
            context.Buffs.enabled = false;
        if (context?.Mana != null)
            context.Mana.enabled = false;
        if (context?.ItemTriggers != null)
            context.ItemTriggers.enabled = false;

        SubscribeClientState();
        clientApplyQueued = !string.IsNullOrWhiteSpace(snapshotJson);
    }

    public override void OnStopClient()
    {
        if (!isServer)
            UnsubscribeClientState();

        latestSnapshot = null;
        clientApplyQueued = false;
        base.OnStopClient();
    }

    private void LateUpdate()
    {
        if (isServer)
        {
            if (HasTimedServerBuff() && Time.unscaledTime >= nextTimedBuffPublishAt)
            {
                nextTimedBuffPublishAt = Time.unscaledTime + 1f;
                serverPublishQueued = true;
            }

            if (serverPublishQueued)
                PublishServerSnapshot();
        }

        if (isClient && !isServer && clientApplyQueued)
            ApplyClientSnapshot();
    }

    public bool RequestUsePotion()
    {
        if (!isLocalPlayer || !NetworkClient.active || !NetworkClient.ready)
            return false;

        CmdUsePotion();
        return true;
    }

    public bool RequestToggleTestMutation()
    {
        if (!isLocalPlayer || !NetworkClient.active || !NetworkClient.ready)
            return false;

        CmdToggleTestMutation();
        return true;
    }

    /// <summary>
    /// 종합상황실에서만 호출하는 서버 전용 테스트 부활이다.
    /// 이미 살아 있어도 체력과 사망 연출을 다시 초기화하므로 반복 검증에 횟수 제한이 없다.
    /// </summary>
    [Server]
    public bool ServerReviveForTest()
    {
        if (NetworkManager.singleton is not MirrorTestNetworkManager manager ||
            !manager.ServerDevelopmentCommandsEnabled || context?.Health == null)
            return false;

        context.Health.FillHealth();
        reviveSequence++;
        if (reviveSequence == 0)
            reviveSequence = 1;

        ApplyDeadState(false, true);
        serverPublishQueued = true;
        return true;
    }

    [Command]
    private void CmdUsePotion()
    {
        if (GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent == true) return;
        context?.Potions?.TryUsePotion();
        serverPublishQueued = true;
    }

    [Command]
    private void CmdToggleTestMutation()
    {
        if (NetworkManager.singleton is not MirrorTestNetworkManager manager ||
            !manager.ServerDevelopmentCommandsEnabled) return;
        if (context?.Stats?.Stat == null || context.Health == null || context.Mana == null || context.Buffs == null)
            return;

        BuffDefinitionSO testBuff = Resources.Load<BuffDefinitionSO>(TestBuffPath);

        if (!testMutationActive)
        {
            context.Health.TakeDamage(Mathf.Max(1f, context.Health.MaxHealth * 0.25f));
            context.Mana.UseMana(Mathf.Max(1f, context.Mana.MaxMana * 0.25f));

            if (testBuff != null)
                context.Buffs.ApplyBuff(testBuff);
            else
                Debug.LogWarning($"[PlayerRuntimeStateSync_MirrorTest] 테스트 버프를 찾지 못했습니다: {TestBuffPath}", this);

            testMutationActive = true;
        }
        else
        {
            if (testBuff != null)
                context.Buffs.RemoveBuff(testBuff);

            context.Health.FillHealth();
            context.Mana.FillMana();
            context.Potions?.RechargeAllPotions();
            testMutationActive = false;
        }

        serverPublishQueued = true;
    }

    [Server]
    private void RecalculateServerStats()
    {
        if (recalculatingServerStats || context?.Stats?.Stat == null)
            return;

        recalculatingServerStats = true;
        try
        {
            context.Stats.SetPassiveStats(shopPlayerState?.ServerPassiveStats ?? StatSet.Zero);
            context.Health?.RefreshMaxHealth();
            context.Mana?.RefreshMaxMana();
        }
        finally
        {
            recalculatingServerStats = false;
        }

        serverPublishQueued = true;
    }

    [Server]
    private void PublishServerSnapshot()
    {
        serverPublishQueued = false;

        if (context?.Stats?.Stat == null || context.Health == null || context.Mana == null)
            return;

        nextRevision++;
        if (nextRevision == 0)
            nextRevision++;

        latestSnapshot = CaptureSnapshot(nextRevision);
        snapshotJson = JsonUtility.ToJson(latestSnapshot);
        StateApplied?.Invoke();
    }

    private RuntimeSnapshot CaptureSnapshot(uint revision)
    {
        PlayerStat stat = context.Stats.Stat;
        IReadOnlyList<BuffInstance> activeBuffs = context.Buffs.ActiveBuffs;
        int buffCount = Mathf.Min(activeBuffs.Count, MaxSyncedBuffCount);
        List<BuffSnapshot> buffs = new(buffCount);

        for (int i = 0; i < buffCount; i++)
        {
            BuffInstance active = activeBuffs[i];
            if (active?.source == null)
                continue;

            buffs.Add(BuffSnapshot.Capture(active));
        }

        return new RuntimeSnapshot
        {
            revision = revision,
            currentLevel = stat.currentLevel,
            currentExp = stat.currentExp,
            passiveStats = shopPlayerState?.ServerPassiveStats ?? StatSet.Zero,
            maxHealth = stat.maxHealth,
            currentHealth = context.Health.CurrentHealth,
            maxMana = stat.maxMana,
            currentMana = context.Mana.CurrentMana,
            attackPower = stat.attackPower,
            defensePower = stat.defensePower,
            moveSpeed = stat.moveSpeed,
            attackSpeed = stat.attackSpeed,
            critRate = stat.critRate,
            critMult = stat.critMult,
            cdr = stat.cdr,
            mpRegen = stat.mpRegen,
            pen = stat.pen,
            skillRange = stat.skillRange,
            fireBonus = stat.fireBonus,
            iceBonus = stat.iceBonus,
            electricBonus = stat.electricBonus,
            potionCharges = context.Potions?.CurrentCharges ?? 0,
            maxPotionCharges = context.Potions?.MaxCharges ?? 0,
            testMutationActive = testMutationActive,
            isDead = IsDead,
            reviveSequence = reviveSequence,
            buffs = buffs.ToArray(),
        };
    }

    private void HandleSnapshotJsonChanged(string oldValue, string newValue)
    {
        if (!isServer && !string.IsNullOrWhiteSpace(newValue))
            clientApplyQueued = true;
    }

    private void ApplyClientSnapshot()
    {
        clientApplyQueued = false;

        if (context?.Stats?.Stat == null || string.IsNullOrWhiteSpace(snapshotJson))
            return;

        RuntimeSnapshot snapshot;
        try
        {
            snapshot = JsonUtility.FromJson<RuntimeSnapshot>(snapshotJson);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[PlayerRuntimeStateSync_MirrorTest] 상태 스냅샷을 읽지 못했습니다: {exception.Message}", this);
            return;
        }

        if (snapshot == null || snapshot.revision == 0)
            return;

        applyingClientSnapshot = true;
        try
        {
            ApplyClientBuffs(snapshot.buffs);
            context.Stats.SetPassiveStats(snapshot.passiveStats);
            ApplyFinalStats(snapshot, context.Stats.Stat);
            InvokeStatChanged(context.Stats.Stat);

            context.Health.RefreshMaxHealth();
            context.Health.SetCurrentHealth(snapshot.currentHealth);
            context.Mana.RefreshMaxMana();
            context.Mana.SetCurrentMana(snapshot.currentMana);
            context.Potions?.ApplyAuthoritativeCharges(snapshot.potionCharges);
            bool forceReviveVisual = latestSnapshot != null &&
                                     snapshot.reviveSequence != latestSnapshot.reviveSequence;
            bool forceDeathVisual = snapshot.isDead &&
                                    (latestSnapshot == null || !latestSnapshot.isDead);
            latestSnapshot = snapshot;
            ApplyDeadState(snapshot.isDead, forceReviveVisual, forceDeathVisual);
        }
        finally
        {
            applyingClientSnapshot = false;
        }

        StateApplied?.Invoke();
    }

    private void ApplyClientBuffs(BuffSnapshot[] snapshots)
    {
        if (context?.Buffs == null)
            return;

        context.Buffs.ClearAllBuffs();
        if (snapshots == null)
            return;

        foreach (BuffSnapshot snapshot in snapshots)
        {
            IBuffSource source = ResolveBuffSource(snapshot);
            if (source == null)
                continue;

            int applications = Mathf.Max(1, snapshot.stackCount);
            for (int i = 0; i < applications; i++)
                context.Buffs.ApplyBuff(source);

            foreach (BuffInstance active in context.Buffs.ActiveBuffs)
            {
                if (!ReferenceEquals(active.source, source))
                    continue;

                active.stackCount = Mathf.Max(1, snapshot.stackCount);
                active.remainingTime = Mathf.Max(0f, snapshot.remainingTime);
                break;
            }
        }
    }

    private static void ApplyFinalStats(RuntimeSnapshot source, PlayerStat target)
    {
        target.currentLevel = source.currentLevel;
        target.currentExp = source.currentExp;
        target.maxHealth = source.maxHealth;
        target.maxMana = source.maxMana;
        target.attackPower = source.attackPower;
        target.defensePower = source.defensePower;
        target.moveSpeed = source.moveSpeed;
        target.attackSpeed = source.attackSpeed;
        target.critRate = source.critRate;
        target.critMult = source.critMult;
        target.cdr = source.cdr;
        target.mpRegen = source.mpRegen;
        target.pen = source.pen;
        target.skillRange = source.skillRange;
        target.fireBonus = source.fireBonus;
        target.iceBonus = source.iceBonus;
        target.electricBonus = source.electricBonus;
    }

    private static void InvokeStatChanged(PlayerStat stat)
    {
        stat.NotifyValuesChanged();
    }

    private static IBuffSource ResolveBuffSource(BuffSnapshot snapshot)
    {
        EnsureBuffLookup();

        if (snapshot.sourceKind == BuffSnapshot.BuffDefinitionKind &&
            buffDefinitions.TryGetValue(snapshot.sourceId, out BuffDefinitionSO definition))
        {
            return definition;
        }

        if (snapshot.sourceKind == BuffSnapshot.UniqueEffectKind &&
            uniqueEffects.TryGetValue(snapshot.sourceId, out UniqueEffectSO effect))
        {
            return effect as IBuffSource;
        }

        return new SnapshotBuffSource(snapshot);
    }

    private static void EnsureBuffLookup()
    {
        if (buffDefinitions != null && uniqueEffects != null)
            return;

        buffDefinitions = new Dictionary<string, BuffDefinitionSO>(StringComparer.Ordinal);
        AddBuffDefinitions(Resources.LoadAll<BuffDefinitionSO>("DataFiles/BuffData/3. GeneratedAssets"));
        AddBuffDefinitions(Resources.LoadAll<BuffDefinitionSO>("DataFiles/ItemData/3. GeneratedAssets/PotionBuffPool"));

        uniqueEffects = new Dictionary<string, UniqueEffectSO>(StringComparer.Ordinal);
        foreach (UniqueEffectSO effect in Resources.LoadAll<UniqueEffectSO>("DataFiles/ItemData/3. GeneratedAssets/UniqueEffectPool"))
        {
            if (effect != null)
                uniqueEffects[effect.name] = effect;
        }
    }

    private static void AddBuffDefinitions(IEnumerable<BuffDefinitionSO> definitions)
    {
        foreach (BuffDefinitionSO definition in definitions)
        {
            if (definition == null)
                continue;

            string id = string.IsNullOrWhiteSpace(definition.buffId) ? definition.name : definition.buffId;
            buffDefinitions[id] = definition;
        }
    }

    private bool HasTimedServerBuff()
    {
        if (!isServer || context?.Buffs == null)
            return false;

        foreach (BuffInstance buff in context.Buffs.ActiveBuffs)
        {
            if (buff?.source != null && !buff.source.IsPermanent)
                return true;
        }

        return false;
    }

    private void SubscribeServerState()
    {
        if (context?.Stats?.Stat != null)
            context.Stats.Stat.OnStatChanged += HandleServerStatChanged;
        if (context?.Health != null)
        {
            context.Health.OnHealthChanged += QueueServerPublish;
            context.Health.OnDeath += HandleServerDeath;
        }
        if (context?.Mana != null)
            context.Mana.OnManaChanged += QueueServerPublish;
        if (context?.Buffs != null)
            context.Buffs.OnBuffsChanged += QueueServerPublish;
        if (context?.Potions != null)
            context.Potions.ChargesChanged += HandleServerChargesChanged;
        if (shopPlayerState != null)
            shopPlayerState.ServerPassiveStatsChanged += RecalculateServerStats;
    }

    private void UnsubscribeServerState()
    {
        if (context?.Stats?.Stat != null)
            context.Stats.Stat.OnStatChanged -= HandleServerStatChanged;
        if (context?.Health != null)
        {
            context.Health.OnHealthChanged -= QueueServerPublish;
            context.Health.OnDeath -= HandleServerDeath;
        }
        if (context?.Mana != null)
            context.Mana.OnManaChanged -= QueueServerPublish;
        if (context?.Buffs != null)
            context.Buffs.OnBuffsChanged -= QueueServerPublish;
        if (context?.Potions != null)
            context.Potions.ChargesChanged -= HandleServerChargesChanged;
        if (shopPlayerState != null)
            shopPlayerState.ServerPassiveStatsChanged -= RecalculateServerStats;
    }

    private void SubscribeClientState()
    {
        if (context?.Stats?.Stat != null)
            context.Stats.Stat.OnStatChanged += QueueClientApply;
        if (context?.Health != null)
            context.Health.OnHealthChanged += QueueClientApply;
        if (context?.Mana != null)
            context.Mana.OnManaChanged += QueueClientApply;
        if (context?.Buffs != null)
            context.Buffs.OnBuffsChanged += QueueClientApply;
        if (context?.Potions != null)
            context.Potions.ChargesChanged += HandleClientChargesChanged;
    }

    private void UnsubscribeClientState()
    {
        if (context?.Stats?.Stat != null)
            context.Stats.Stat.OnStatChanged -= QueueClientApply;
        if (context?.Health != null)
            context.Health.OnHealthChanged -= QueueClientApply;
        if (context?.Mana != null)
            context.Mana.OnManaChanged -= QueueClientApply;
        if (context?.Buffs != null)
            context.Buffs.OnBuffsChanged -= QueueClientApply;
        if (context?.Potions != null)
            context.Potions.ChargesChanged -= HandleClientChargesChanged;
    }

    private void HandleServerStatChanged()
    {
        if (!recalculatingServerStats)
            RecalculateServerStats();
    }

    private void QueueServerPublish()
    {
        serverPublishQueued = true;
    }

    [Server]
    private void HandleServerDeath()
    {
        ApplyDeadState(true, false, true);
        serverPublishQueued = true;
    }

    private void ApplyDeadState(
        bool dead,
        bool forceReviveVisual = false,
        bool forceDeathVisual = false)
    {
        if (context == null)
            return;

        T_PlayerController controller = context.Controller;
        WBH_PlayerStateMachine stateMachine = context.StateMachine;
        PlayerState stateBeforeApply = stateMachine != null
            ? stateMachine.CurrentState
            : PlayerState.Idle;

        if (dead)
        {
            if (controller != null)
            {
                controller.SetControlEnable(false);
                controller.enabled = false;
            }

            stateMachine?.ChangeState(PlayerState.Dead);
            if (forceDeathVisual)
                GetComponent<WBH_PlayerAnimation_MirrorTest>()?.ApplyAuthoritativeDeath();
        }
        else
        {
            if (controller != null)
                controller.enabled = true;

            bool shouldRevive = forceReviveVisual || stateBeforeApply == PlayerState.Dead;
            if (shouldRevive)
            {
                stateMachine?.ChangeState(PlayerState.Idle);
                GetComponent<WBH_PlayerAnimation_MirrorTest>()?.ApplyAuthoritativeRevive();
            }

            Debug.Assert(
                shouldRevive || stateMachine == null || stateMachine.CurrentState == stateBeforeApply,
                "[PlayerRuntimeStateSync_MirrorTest] 생존 스냅샷이 현재 플레이 상태를 변경했습니다.",
                this);
            controller?.SetControlEnable(true);
        }

        GetComponent<MirrorSpawnedPlayerBinder>()?.SetLocalInputEnabled(!dead);
    }

    private void HandleServerChargesChanged(int current, int max)
    {
        QueueServerPublish();
    }

    private void QueueClientApply()
    {
        if (!applyingClientSnapshot && !string.IsNullOrWhiteSpace(snapshotJson))
            clientApplyQueued = true;
    }

    private void HandleClientChargesChanged(int current, int max)
    {
        QueueClientApply();
    }

    [Serializable]
    private sealed class RuntimeSnapshot
    {
        public uint revision;
        public int currentLevel;
        public float currentExp;
        public float maxHealth;
        public float currentHealth;
        public float maxMana;
        public float currentMana;
        public float attackPower;
        public float defensePower;
        public float moveSpeed;
        public float attackSpeed;
        public float critRate;
        public float critMult;
        public float cdr;
        public float mpRegen;
        public float pen;
        public float skillRange;
        public float fireBonus;
        public float iceBonus;
        public float electricBonus;
        public int potionCharges;
        public int maxPotionCharges;
        public bool testMutationActive;
        public bool isDead;
        public uint reviveSequence;
        public BuffSnapshot[] buffs;
        public StatSet passiveStats;
    }

    [Serializable]
    private sealed class BuffSnapshot
    {
        public const byte RuntimeKind = 0;
        public const byte BuffDefinitionKind = 1;
        public const byte UniqueEffectKind = 2;

        public byte sourceKind;
        public string sourceId;
        public string displayName;
        public string description;
        public float remainingTime;
        public int stackCount;
        public float duration;
        public BuffStackBehavior stackBehavior;
        public int maxStack;
        public bool isPermanent;
        public FixedStatSnapshot[] statEffects;

        public static BuffSnapshot Capture(BuffInstance active)
        {
            IBuffSource source = active.source;
            byte kind = RuntimeKind;
            string id = source.BuffDisplayName;

            if (source is BuffDefinitionSO definition)
            {
                kind = BuffDefinitionKind;
                id = string.IsNullOrWhiteSpace(definition.buffId) ? definition.name : definition.buffId;
            }
            else if (source is UniqueEffectSO effect)
            {
                kind = UniqueEffectKind;
                id = effect.name;
            }

            FixedStatValue[] effects = source.StatEffects;
            FixedStatSnapshot[] snapshots = new FixedStatSnapshot[effects?.Length ?? 0];
            for (int i = 0; i < snapshots.Length; i++)
            {
                snapshots[i] = new FixedStatSnapshot
                {
                    statType = effects[i].statType,
                    value = effects[i].value,
                };
            }

            return new BuffSnapshot
            {
                sourceKind = kind,
                sourceId = id,
                displayName = source.BuffDisplayName,
                description = source.BuffDescription,
                remainingTime = active.remainingTime,
                stackCount = active.stackCount,
                duration = source.Duration,
                stackBehavior = source.StackBehavior,
                maxStack = source.MaxStack,
                isPermanent = source.IsPermanent,
                statEffects = snapshots,
            };
        }
    }

    [Serializable]
    private sealed class FixedStatSnapshot
    {
        public StatType statType;
        public float value;
    }

    private sealed class SnapshotBuffSource : IBuffSource
    {
        private readonly string displayName;
        private readonly string description;
        private readonly FixedStatValue[] statEffects;
        private readonly float duration;
        private readonly BuffStackBehavior stackBehavior;
        private readonly int maxStack;
        private readonly bool isPermanent;

        public string BuffDisplayName => displayName;
        public string BuffDescription => description;
        public Sprite BuffIcon => null;
        public FixedStatValue[] StatEffects => statEffects;
        public float Duration => duration;
        public BuffStackBehavior StackBehavior => stackBehavior;
        public int MaxStack => maxStack;
        public bool IsPermanent => isPermanent;

        // 스냅샷에는 표시 성격을 싣지 않는다. Auto면 표시하는 쪽이 스탯 값의 부호로 추정해서
        // 이 필드가 생기기 전과 똑같이 동작한다.
        public BuffDisplayKind DisplayKind => BuffDisplayKind.Auto;

        public SnapshotBuffSource(BuffSnapshot snapshot)
        {
            displayName = snapshot.displayName;
            description = snapshot.description;
            duration = snapshot.duration;
            stackBehavior = snapshot.stackBehavior;
            maxStack = snapshot.maxStack;
            isPermanent = snapshot.isPermanent;

            FixedStatSnapshot[] snapshots = snapshot.statEffects;
            statEffects = new FixedStatValue[snapshots?.Length ?? 0];
            for (int i = 0; i < statEffects.Length; i++)
            {
                statEffects[i] = new FixedStatValue
                {
                    statType = snapshots[i].statType,
                    value = snapshots[i].value,
                };
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using Core;
using Mirror;
using UnityEngine;

public enum MirrorTestShopOperation : byte
{
    Buy = 1,
    Sell = 2,
    Reroll = 3,
}

public enum MirrorTestShopRequestResult : byte
{
    Success = 0,
    InvalidRequest = 1,
    DuplicateRequest = 2,
    ShopStateChanged = 3,
    InventoryStateChanged = 4,
    ItemUnavailable = 5,
    NotEnoughGold = 6,
    InventoryFull = 7,
    ShopFull = 8,
    RerollUnavailable = 9,
    ServerSetupInvalid = 10,
    StateApplyFailed = 11,
    RecoveryFailed = 12,
}

public readonly struct MirrorTestShopRequestCompleted
{
    public uint RequestId { get; }
    public MirrorTestShopOperation Operation { get; }
    public MirrorTestShopRequestResult Result { get; }
    public uint RequestedShopRevision { get; }
    public uint AuthoritativeShopRevision { get; }
    public uint AuthoritativeInventoryRevision { get; }

    public MirrorTestShopRequestCompleted(
        uint requestId,
        MirrorTestShopOperation operation,
        MirrorTestShopRequestResult result,
        uint requestedShopRevision,
        uint authoritativeShopRevision,
        uint authoritativeInventoryRevision)
    {
        RequestId = requestId;
        Operation = operation;
        Result = result;
        RequestedShopRevision = requestedShopRevision;
        AuthoritativeShopRevision = authoritativeShopRevision;
        AuthoritativeInventoryRevision = authoritativeInventoryRevision;
    }
}

/// <summary>
/// 플레이어마다 다른 상점 골드·패시브 값과 상점 요청 권한을 소유하는 Mirror 테스트 컴포넌트다.
/// <para>팀 원본과의 차이: 전역 <see cref="PassiveSkillManager"/>를 서버의 파티 상태로 사용하지 않는다.
/// 각 로컬 프로필에서 상점 강화 값만 읽어 자신의 네트워크 플레이어로 전달하고, 서버 상점은
/// 연결된 플레이어 값 중 가장 높은 효과만 선택한다.</para>
/// <para>골드는 Owner에게만 복제하고 실제 <see cref="PlayerWallet"/>에 반영한다. 구매·판매·리롤
/// Command도 이 권한 있는 플레이어 객체에서 시작해 임의의 다른 플레이어 지갑을 바꾸지 못하게 한다.</para>
/// <para>이 테스트 값 전달은 실제 계정 서버 검증을 대신하지 않는다. 정식 서버에서는 인증된 프로필을
/// 서버가 불러온 뒤 같은 필드에 넣어야 한다.</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(PlayerContext), typeof(PlayerInventorySync_MirrorTest))]
public sealed class NetworkShopPlayerState_MirrorTest : NetworkBehaviour
{
    private const int ProcessedRequestHistorySize = 128;
    private const int MaxPassiveStatJsonLength = 4096;
    private const float MaxPassiveStatValue = 10000f;

    private static readonly FieldInfo[] StatSetFields =
        typeof(StatSet).GetFields(BindingFlags.Instance | BindingFlags.Public);

    private static readonly FieldInfo PassiveDatabaseField =
        typeof(PassiveSkillManager).GetField(
            "database",
            BindingFlags.Instance | BindingFlags.NonPublic);

    [SerializeField] private PlayerContext context;
    [SerializeField] private PassiveSkillDatabaseSO passiveSkillDatabase;
    [SerializeField, Min(0)] private int startingTestGold = 10000;
    [SerializeField, Min(0)] private int fallbackShopEnhanceLevel;
    [SerializeField, Min(0)] private int fallbackExtraRerollCount;
    [SerializeField, Range(0f, 0.95f)] private float fallbackDiscountPercent;

    [SyncVar(hook = nameof(HandleGoldChanged))]
    private int syncedGold;

    [SyncVar]
    private int shopEnhanceLevel;

    [SyncVar]
    private int extraRerollCount;

    [SyncVar]
    private float discountPercent;

    private readonly HashSet<uint> pendingRequestIds = new();
    private readonly Dictionary<uint, MirrorTestShopRequestCompleted> waitingForState = new();
    private readonly HashSet<uint> processedRequestIds = new();
    private readonly Queue<uint> processedRequestOrder = new();

    private PlayerInventorySync_MirrorTest inventorySync;
    private PassiveSkillManager passiveSkillManager;
    private StatSet serverPassiveStats;
    private uint nextRequestId;

    public int Gold => syncedGold;
    public int ShopEnhanceLevel => shopEnhanceLevel;
    public int ExtraRerollCount => extraRerollCount;
    public float DiscountPercent => discountPercent;
    public int PendingRequestCount => pendingRequestIds.Count;
    public PlayerContext Context => context;
    internal PlayerInventorySync_MirrorTest InventorySync => inventorySync;
    internal StatSet ServerPassiveStats => serverPassiveStats;

    public event Action<MirrorTestShopRequestCompleted> RequestCompleted;
    internal event Action ServerPassiveStatsChanged;

    private void Awake()
    {
        ApplyPassiveSkillDatabase();
        syncMode = SyncMode.Owner;
        context ??= GetComponent<PlayerContext>();
        inventorySync = GetComponent<PlayerInventorySync_MirrorTest>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        syncMode = SyncMode.Owner;
        context ??= GetComponent<PlayerContext>();
    }
#endif

    private void ApplyPassiveSkillDatabase()
    {
        if (passiveSkillDatabase == null || PassiveDatabaseField == null)
            return;

        PassiveSkillManager manager = PassiveSkillManager.Instance;
        if (manager != null && PassiveDatabaseField.GetValue(manager) == null)
            PassiveDatabaseField.SetValue(manager, passiveSkillDatabase);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (context?.Wallet == null)
        {
            Debug.LogError(
                "[NetworkShopPlayerState_MirrorTest] 서버 PlayerWallet 참조가 없습니다.",
                this);
            return;
        }

        if (context.Wallet.Gold <= 0 && startingTestGold > 0)
            context.Wallet.SetGold(startingTestGold);

        syncedGold = context.Wallet.Gold;
        shopEnhanceLevel = fallbackShopEnhanceLevel;
        extraRerollCount = fallbackExtraRerollCount;
        discountPercent = fallbackDiscountPercent;
        serverPassiveStats = StatSet.Zero;
        ResolveShopState()?.ServerRefreshPartyBenefits();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyGoldToLocalWallet(syncedGold);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        passiveSkillManager = PassiveSkillManager.Instance;
        if (passiveSkillManager != null)
        {
            passiveSkillManager.OnProfileChanged -= SendCurrentPassiveState;
            passiveSkillManager.OnProfileChanged += SendCurrentPassiveState;
        }

        SendCurrentPassiveState();
    }

    public override void OnStopLocalPlayer()
    {
        if (passiveSkillManager != null)
            passiveSkillManager.OnProfileChanged -= SendCurrentPassiveState;

        passiveSkillManager = null;
        pendingRequestIds.Clear();
        waitingForState.Clear();
        base.OnStopLocalPlayer();
    }

    public override void OnStopServer()
    {
        NetworkShopState_MirrorTest shop = ResolveShopState();
        processedRequestIds.Clear();
        processedRequestOrder.Clear();
        base.OnStopServer();
        shop?.ServerRefreshPartyBenefits();
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer || waitingForState.Count == 0)
            return;

        NetworkShopState_MirrorTest shop =
            FindFirstObjectByType<NetworkShopState_MirrorTest>();
        if (shop == null || inventorySync == null)
            return;

        List<uint> completedIds = null;
        foreach (KeyValuePair<uint, MirrorTestShopRequestCompleted> pair in waitingForState)
        {
            MirrorTestShopRequestCompleted completed = pair.Value;
            bool shopReady = shop.StateRevision >= completed.AuthoritativeShopRevision;
            bool inventoryReady = completed.Operation == MirrorTestShopOperation.Reroll ||
                                  inventorySync.StateRevision >= completed.AuthoritativeInventoryRevision;

            if (!shopReady || !inventoryReady)
                continue;

            completedIds ??= new List<uint>();
            completedIds.Add(pair.Key);
        }

        if (completedIds == null)
            return;

        foreach (uint requestId in completedIds)
        {
            MirrorTestShopRequestCompleted completed = waitingForState[requestId];
            waitingForState.Remove(requestId);
            CompleteLocalRequest(completed);
        }
    }

    public bool TryRequestBuy(
        string instanceId,
        int targetX,
        int targetY,
        bool isRotated,
        out uint requestId)
    {
        requestId = 0;
        NetworkShopState_MirrorTest shop = ResolveShopState();

        if (string.IsNullOrWhiteSpace(instanceId) ||
            shop == null ||
            inventorySync == null ||
            !TryBeginLocalRequest(out requestId))
        {
            return false;
        }

        CmdBuy(
            requestId,
            shop.StateRevision,
            inventorySync.StateRevision,
            instanceId,
            targetX,
            targetY,
            isRotated);
        return true;
    }

    public bool TryRequestSell(
        string instanceId,
        int targetX,
        int targetY,
        bool isRotated,
        out uint requestId)
    {
        requestId = 0;
        NetworkShopState_MirrorTest shop = ResolveShopState();

        if (string.IsNullOrWhiteSpace(instanceId) ||
            shop == null ||
            inventorySync == null ||
            !TryBeginLocalRequest(out requestId))
        {
            return false;
        }

        CmdSell(
            requestId,
            shop.StateRevision,
            inventorySync.StateRevision,
            instanceId,
            targetX,
            targetY,
            isRotated);
        return true;
    }

    public bool TryRequestReroll(out uint requestId)
    {
        requestId = 0;
        NetworkShopState_MirrorTest shop = ResolveShopState();

        if (shop == null || !TryBeginLocalRequest(out requestId))
            return false;

        CmdReroll(requestId, shop.StateRevision);
        return true;
    }

    public bool RequestToggleTestShopPassive()
    {
        if (!isLocalPlayer || !NetworkClient.active || !NetworkClient.ready)
            return false;

        bool enable = shopEnhanceLevel <= 0;
        CmdSetShopPassiveState(
            enable ? 1 : 0,
            enable ? 1 : 0,
            enable ? 0.1f : 0f,
            GetLocalPassiveStatJson());
        return true;
    }

    [Server]
    internal bool ServerTrySpendGold(int amount)
    {
        if (context?.Wallet == null || amount < 0 || !context.Wallet.TrySpendGold(amount))
            return false;

        syncedGold = context.Wallet.Gold;
        return true;
    }

    [Server]
    internal void ServerAddGold(int amount)
    {
        if (context?.Wallet == null || amount < 0)
            return;

        context.Wallet.AddGold(amount);
        syncedGold = context.Wallet.Gold;
    }

    [Server]
    internal void ServerSetGold(int amount)
    {
        if (context?.Wallet == null)
            return;

        context.Wallet.SetGold(Mathf.Max(0, amount));
        syncedGold = context.Wallet.Gold;
    }

    /// <summary>
    /// 클라이언트에서 먼저 실행한 시험용 거래가 실패했을 때 화면 지갑을
    /// 서버가 확정한 금액으로 되돌린다. 서버 지갑은 변경하지 않는다.
    /// </summary>
    public void RestoreLocalGold()
    {
        if (!isLocalPlayer || context?.Wallet == null)
            return;

        context.Wallet.SetGold(Mathf.Max(0, syncedGold));
    }

    [Command]
    private void CmdBuy(
        uint requestId,
        uint requestedShopRevision,
        uint requestedInventoryRevision,
        string instanceId,
        int targetX,
        int targetY,
        bool isRotated)
    {
        MirrorTestShopRequestResult result = TryAcceptServerRequest(requestId)
            ? ResolveShopState()?.ServerTryBuy(
                this,
                requestedShopRevision,
                requestedInventoryRevision,
                instanceId,
                targetX,
                targetY,
                isRotated) ?? MirrorTestShopRequestResult.ServerSetupInvalid
            : MirrorTestShopRequestResult.DuplicateRequest;

        CompleteServerRequest(
            requestId,
            MirrorTestShopOperation.Buy,
            result,
            requestedShopRevision);
    }

    [Command]
    private void CmdSell(
        uint requestId,
        uint requestedShopRevision,
        uint requestedInventoryRevision,
        string instanceId,
        int targetX,
        int targetY,
        bool isRotated)
    {
        MirrorTestShopRequestResult result = TryAcceptServerRequest(requestId)
            ? ResolveShopState()?.ServerTrySell(
                this,
                requestedShopRevision,
                requestedInventoryRevision,
                instanceId,
                targetX,
                targetY,
                isRotated) ?? MirrorTestShopRequestResult.ServerSetupInvalid
            : MirrorTestShopRequestResult.DuplicateRequest;

        CompleteServerRequest(
            requestId,
            MirrorTestShopOperation.Sell,
            result,
            requestedShopRevision);
    }

    [Command]
    private void CmdReroll(uint requestId, uint requestedShopRevision)
    {
        MirrorTestShopRequestResult result = TryAcceptServerRequest(requestId)
            ? ResolveShopState()?.ServerTryReroll(this, requestedShopRevision)
              ?? MirrorTestShopRequestResult.ServerSetupInvalid
            : MirrorTestShopRequestResult.DuplicateRequest;

        CompleteServerRequest(
            requestId,
            MirrorTestShopOperation.Reroll,
            result,
            requestedShopRevision);
    }

    [Command]
    private void CmdSetShopPassiveState(
        int level,
        int extraRerolls,
        float discount,
        string passiveStatsJson)
    {
        shopEnhanceLevel = Mathf.Clamp(level, 0, 20);
        extraRerollCount = Mathf.Clamp(extraRerolls, 0, 20);
        discountPercent = Mathf.Clamp(discount, 0f, 0.95f);

        if (TryReadPassiveStats(passiveStatsJson, out StatSet receivedStats))
        {
            serverPassiveStats = receivedStats;
            ServerPassiveStatsChanged?.Invoke();
        }
        else
        {
            Debug.LogWarning(
                "[NetworkShopPlayerState_MirrorTest] 유효하지 않은 패시브 StatSet 보고를 무시했습니다.",
                this);
        }

        ResolveShopState()?.ServerRefreshPartyBenefits();
    }

    [Server]
    private void CompleteServerRequest(
        uint requestId,
        MirrorTestShopOperation operation,
        MirrorTestShopRequestResult result,
        uint requestedShopRevision)
    {
        NetworkShopState_MirrorTest shop = ResolveShopState();
        TargetCompleteShopRequest(
            requestId,
            operation,
            result,
            requestedShopRevision,
            shop != null ? shop.StateRevision : 0,
            inventorySync != null ? inventorySync.StateRevision : 0);
    }

    [TargetRpc]
    private void TargetCompleteShopRequest(
        uint requestId,
        MirrorTestShopOperation operation,
        MirrorTestShopRequestResult result,
        uint requestedShopRevision,
        uint authoritativeShopRevision,
        uint authoritativeInventoryRevision)
    {
        MirrorTestShopRequestCompleted completed = new(
            requestId,
            operation,
            result,
            requestedShopRevision,
            authoritativeShopRevision,
            authoritativeInventoryRevision);

        NetworkShopState_MirrorTest shop = ResolveShopState();
        bool shopReady = shop != null && shop.StateRevision >= authoritativeShopRevision;
        bool inventoryReady = operation == MirrorTestShopOperation.Reroll ||
                              (inventorySync != null &&
                               inventorySync.StateRevision >= authoritativeInventoryRevision);

        if (!shopReady || !inventoryReady)
        {
            waitingForState[requestId] = completed;
            return;
        }

        CompleteLocalRequest(completed);
    }

    private bool TryBeginLocalRequest(out uint requestId)
    {
        requestId = 0;

        if (!isLocalPlayer ||
            !NetworkClient.active ||
            !NetworkClient.ready ||
            pendingRequestIds.Count > 0)
        {
            return false;
        }

        nextRequestId++;
        if (nextRequestId == 0)
            nextRequestId++;

        requestId = nextRequestId;
        pendingRequestIds.Add(requestId);
        return true;
    }

    [Server]
    private bool TryAcceptServerRequest(uint requestId)
    {
        if (requestId == 0 || !processedRequestIds.Add(requestId))
            return false;

        processedRequestOrder.Enqueue(requestId);
        while (processedRequestOrder.Count > ProcessedRequestHistorySize)
            processedRequestIds.Remove(processedRequestOrder.Dequeue());

        return true;
    }

    private void CompleteLocalRequest(MirrorTestShopRequestCompleted completed)
    {
        pendingRequestIds.Remove(completed.RequestId);
        RequestCompleted?.Invoke(completed);
    }

    private void SendCurrentPassiveState()
    {
        if (!isLocalPlayer || !NetworkClient.active || !NetworkClient.ready)
            return;

        int level = fallbackShopEnhanceLevel;
        int rerolls = fallbackExtraRerollCount;
        float discount = fallbackDiscountPercent;

        if (passiveSkillManager != null)
        {
            level = passiveSkillManager.GetCurrentLevel(PassiveSkillId.ShopEnhance);
            rerolls = passiveSkillManager.ShopExtraRerollCount;
            discount = passiveSkillManager.ShopDiscountPercent;
        }

        CmdSetShopPassiveState(level, rerolls, discount, GetLocalPassiveStatJson());
    }

    private string GetLocalPassiveStatJson()
    {
        StatSet passiveStats = passiveSkillManager != null
            ? passiveSkillManager.GetStatSet()
            : StatSet.Zero;

        return JsonUtility.ToJson(passiveStats);
    }

    private static bool TryReadPassiveStats(string json, out StatSet stats)
    {
        stats = StatSet.Zero;
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxPassiveStatJsonLength)
            return false;

        object boxed;
        try
        {
            boxed = JsonUtility.FromJson<StatSet>(json);
        }
        catch (Exception)
        {
            return false;
        }

        foreach (FieldInfo field in StatSetFields)
        {
            if (field.FieldType != typeof(float))
                return false;

            float value = (float)field.GetValue(boxed);
            if (float.IsNaN(value) || float.IsInfinity(value))
                return false;

            field.SetValue(boxed, Mathf.Clamp(value, 0f, MaxPassiveStatValue));
        }

        stats = (StatSet)boxed;
        return true;
    }

    private void HandleGoldChanged(int oldGold, int newGold)
    {
        ApplyGoldToLocalWallet(newGold);
    }

    private void ApplyGoldToLocalWallet(int amount)
    {
        if (isServer || context?.Wallet == null)
            return;

        context.Wallet.SetGold(Mathf.Max(0, amount));
    }

    private NetworkShopState_MirrorTest ResolveShopState()
    {
        return FindFirstObjectByType<NetworkShopState_MirrorTest>();
    }
}

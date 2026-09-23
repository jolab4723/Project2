using System;
using System.Collections.Generic;
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
/// 플레이어의 서버 확정 골드·상점 혜택과 연결별 거래 요청 권한을 소유한다.
/// <para>새 런은 참가 시 서버가 검증한 개인 패시브와 상점 혜택을 적용한다.
/// 재접속은 기존 지갑과 상태를 유지하고 중복 요청 기록만 새 소유 연결에 맞춰 초기화한다.</para>
/// <para>골드는 Owner에게만 복제하고 실제 <see cref="PlayerWallet"/>에 반영한다. 구매·판매·리롤
/// Command도 이 권한 있는 플레이어 객체에서 시작해 임의의 다른 플레이어 지갑을 바꾸지 못하게 한다.</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(PlayerContext), typeof(PlayerInventorySync_MirrorTest))]
public sealed class NetworkShopPlayerState_MirrorTest : NetworkBehaviour
{
    private const int ProcessedRequestHistorySize = 128;
    [SerializeField] private PlayerContext context;

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

        context.Wallet.OnGoldChanged += HandleServerGoldChanged;
        context.Wallet.SetGold(0);
        shopEnhanceLevel = 0;
        extraRerollCount = 0;
        discountPercent = 0;
        serverPassiveStats = StatSet.Zero;
        ServerPassiveStatsChanged?.Invoke();
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

        nextRequestId = 0;
        pendingRequestIds.Clear();
        waitingForState.Clear();
    }

    public override void OnStopLocalPlayer()
    {
        pendingRequestIds.Clear();
        waitingForState.Clear();
        base.OnStopLocalPlayer();
    }

    public override void OnStopServer()
    {
        if (context?.Wallet != null)
            context.Wallet.OnGoldChanged -= HandleServerGoldChanged;
        NetworkShopState_MirrorTest shop = ResolveShopState();
        processedRequestIds.Clear();
        processedRequestOrder.Clear();
        base.OnStopServer();
        shop?.ServerRefreshPartyBenefits();
    }

    /// <summary>새 소유 연결의 요청 번호를 허용한다. 골드와 상점·패시브 상태는 변경하지 않는다.</summary>
    [Server]
    public void ServerResetOwnerRequests()
    {
        processedRequestIds.Clear();
        processedRequestOrder.Clear();
    }

    /// <summary>
    /// 서버 지갑의 런 크레딧을 소유 클라이언트에 전달한 뒤 서버 지갑을 비웁니다.
    /// 각 클라이언트가 자신의 Firebase 로그인 계정에 영구 크레딧을 저장합니다.
    /// </summary>
    [Server]
    public bool ServerTransferRunCreditsToOwner()
    {
        if (context?.Wallet == null || connectionToClient == null)
            return false;

        int runCredits = context.Wallet.Gold;
        if (runCredits <= 0)
            return true;

        TargetSaveRunCredits(runCredits);
        context.Wallet.SetGold(0);
        return true;
    }

    /// <summary>
    /// 서버가 확정한 런 크레딧을 로컬 프로필에 더하고 기존 Firebase 저장 경로로 전달합니다.
    /// </summary>
    [TargetRpc]
    private void TargetSaveRunCredits(int runCredits)
    {
        DataManager dataManager = DataManager.Instance;
        PlayerProfileData profile = PassiveSkillManager.Instance?.CurrentProfile ??
                                    dataManager?.LoadSinglePlayerSlot()?.profile;
        if (dataManager == null || !dataManager.TrySaveRunCredits(profile, runCredits))
        {
            Debug.LogError(
                $"[NetworkShopPlayerState_MirrorTest] 액트 클리어 크레딧 저장 실패: {runCredits}",
                this);
        }
    }

    [Server]
    public void ServerApplyPassiveProfile(MirrorPassiveProfile_MirrorTest profile)
    {
        ServerReviveHealthFraction = profile?.ReviveHealthFraction ?? 0f;
        serverPassiveStats = profile?.Stats ?? StatSet.Zero;
        shopEnhanceLevel = profile?.ShopLevel ?? 0;
        extraRerollCount = profile?.ExtraRerolls ?? 0;
        discountPercent = profile?.DiscountFraction ?? 0f;
        ServerPassiveStatsChanged?.Invoke();
        ResolveShopState()?.ServerRefreshPartyBenefits();
    }

    public float ServerReviveHealthFraction { get; private set; }

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

    [Server]
    internal bool ServerTrySpendGold(int amount)
    {
        if (context?.Wallet == null || amount < 0 || !context.Wallet.TrySpendGold(amount))
            return false;

        return true;
    }

    [Server]
    internal void ServerAddGold(int amount)
    {
        if (context?.Wallet == null || amount < 0)
            return;

        context.Wallet.AddGold(amount);
    }

    [Server]
    internal void ServerSetGold(int amount)
    {
        if (context?.Wallet == null)
            return;

        context.Wallet.SetGold(Mathf.Max(0, amount));
    }

    /// <summary>공통 거래·강화 서비스가 바꾼 서버 지갑을 소유 클라이언트에 복제합니다.</summary>
    private void HandleServerGoldChanged(int amount)
    {
        syncedGold = amount;
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
        if (GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent == true ||
            requestId == 0 || !processedRequestIds.Add(requestId))
            return false;

        processedRequestOrder.Enqueue(requestId);
        while (processedRequestOrder.Count > ProcessedRequestHistorySize)
            processedRequestIds.Remove(processedRequestOrder.Dequeue());

        return true;
    }

    private void CompleteLocalRequest(MirrorTestShopRequestCompleted completed)
    {
        pendingRequestIds.Remove(completed.RequestId);
        if (completed.Result != MirrorTestShopRequestResult.Success)
            Debug.LogWarning($"[NetworkShopPlayerState_MirrorTest] {completed.Operation}: {completed.Result}", this);
        RequestCompleted?.Invoke(completed);
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

    /// <summary>요청 UI도 동일한 연결 상태의 공유 상점을 사용한다.</summary>
    public NetworkShopState_MirrorTest ResolveShopState()
    {
        return FindFirstObjectByType<NetworkShopState_MirrorTest>();
    }
}

using System;
using System.Collections.Generic;
using Core;
using ItemSystem;
using Mirror;
using UnityEngine;

public enum MirrorTestInventoryOperation : byte
{
    GrantDistinctItem = 0,
    DropFirstItem = 1,
    PickupWorldItem = 2,
    DropInventoryItem = 3,
    MoveGridItem = 4,
    ChangeEquipment = 5,
    UpgradeItem = 6,
}

public enum MirrorTestInventoryRequestResult : byte
{
    Success = 0,
    InvalidRequest = 1,
    DuplicateRequest = 2,
    StaleRevision = 3,
    InventoryUnavailable = 4,
    ItemUnavailable = 5,
    InventoryFull = 6,
    PickupUnavailable = 7,
    OutOfRange = 8,
    AlreadyClaimed = 9,
    ServerSetupInvalid = 10,
    SpawnFailed = 11,
    StateApplyFailed = 12,
    RecoveryFailed = 13,
    UpgradeUnavailable = 14,
    NotEnoughGold = 15,
}

public readonly struct MirrorTestInventoryRequestCompleted
{
    public uint RequestId { get; }
    public MirrorTestInventoryOperation Operation { get; }
    public MirrorTestInventoryRequestResult Result { get; }
    public uint RequestedRevision { get; }
    public uint AuthoritativeRevision { get; }

    public MirrorTestInventoryRequestCompleted(
        uint requestId,
        MirrorTestInventoryOperation operation,
        MirrorTestInventoryRequestResult result,
        uint requestedRevision,
        uint authoritativeRevision)
    {
        RequestId = requestId;
        Operation = operation;
        Result = result;
        RequestedRevision = requestedRevision;
        AuthoritativeRevision = authoritativeRevision;
    }
}

/// <summary>
/// Mirror PlayerContext C단계의 인벤토리 서버 권한 검증용 컴포넌트다.
/// 서버의 <see cref="InventoryController"/>를 원본으로 사용하고 기존 <see cref="ItemSaveData"/>를
/// JSON 문자열로 바꿔 <see cref="SyncList{T}"/>에 복제한다. 필드 드랍·획득도 서버가 판정한다.
/// HP, MP, Stat 동기화는 이 테스트 범위에 포함하지 않는다.
/// <para>3-1 차이: 원본 플레이어 시스템을 수정하지 않고 요청 ID, 서버 상태 revision,
/// Owner 전용 동기화와 TargetRpc 결과 반환을 이 테스트 컴포넌트 안에서만 검증한다.</para>
/// <para>3-2 차이: 실제 드래그가 보낸 item instanceId를 검증해 그 아이템만 서버에서 드랍하고,
/// 기존 요청 완료 이벤트로 비동기 성공·실패를 로컬 UI에 돌려준다.</para>
/// </summary>
/// <para>3-3 차이: 실제 인벤토리 드래그의 최종 그리드 위치와 회전을 서버로 보낸다.
/// 교환으로 함께 움직인 아이템도 Owner 스냅샷에 반영하고, 거절된 요청은 해당 스냅샷으로 로컬 그리드를 복구한다.</para>
/// <para>3-4 차이: 기존 장비 트랜잭션이 만든 장착·해제·교환 결과를 서버가 다시 검증하고 확정한다.
/// 장비와 그리드를 하나의 소유 상태로 스냅샷에 기록하며, 거절되면 로컬 화면까지 서버 확정 상태로 복구한다.</para>
/// <para>3-7 차이: 강화 요청도 같은 아이템 소유 기록과 상태 번호를 사용한다. 서버가 골드를 차감하고
/// 강화 수치를 바꾼 뒤 Owner 스냅샷으로 돌려주므로 다른 플레이어 아이템이나 로컬 임시 골드가 원본이 되지 않는다.</para>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerContext))]
public sealed class PlayerInventorySync_MirrorTest : NetworkBehaviour
{
    private const int ProcessedRequestHistorySize = 64;
    private const int MaxInstanceIdLength = 128;
    private const float UpgradeCostMultiplier = 1.15f;
    // 전투·강화·비교 툴팁을 눈으로 확인하기 쉽도록 현재 ItemDatabase에서
    // 방어력 메인 옵션이 가장 높은 갑옷(태양의 은혜, 방어력 80)을 최초 테스트 장비로 지급한다.
    private const string DefaultTestItemId = "item.armor.chest.solargrace";

    [SerializeField] private PlayerContext context;
    [SerializeField] private NetworkWorldItem_MirrorTest worldItemPrefab;
    [SerializeField, Min(0.5f)] private float pickupDistance = 3.5f;
    [SerializeField, Min(0.5f)] private float dropDistance = 1.5f;

    /// <summary>적 보상 드랍도 같은 네트워크 월드 아이템 Prefab을 쓰도록 읽기 전용으로 제공한다.</summary>
    public NetworkWorldItem_MirrorTest WorldItemPrefab => worldItemPrefab;

    private readonly SyncList<string> itemSnapshots = new();
    private readonly HashSet<uint> pendingRequestIds = new();
    private readonly Dictionary<uint, MirrorTestInventoryRequestCompleted> waitingForRevision = new();
    private readonly HashSet<uint> processedRequestIds = new();
    private readonly Queue<uint> processedRequestOrder = new();

    private InventoryView localInventoryView;
    private InventoryItemUISpawner localItemSpawner;

    [SyncVar(hook = nameof(HandleStateRevisionChanged))]
    private uint stateRevision;

    private uint nextRequestId;

    public int SyncedItemCount => itemSnapshots.Count;
    public uint StateRevision => stateRevision;
    public int PendingRequestCount => pendingRequestIds.Count;

    /// <summary>
    /// 상점 서버 거래가 이미 플레이어 모델에 추가한 아이템을 Owner 소유 기록에 확정한다.
    /// 팀 원본 인벤토리에는 네트워크 책임을 추가하지 않고 3-5 테스트 거래에서만 호출한다.
    /// </summary>
    [Server]
    internal bool ServerCommitShopItemAdded(InventoryItem item, uint expectedRevision)
    {
        if (item?.itemData == null ||
            stateRevision != expectedRevision ||
            FindOwnedItem(item.itemData.instanceId) != item ||
            FindSnapshotIndex(item.itemData.instanceId) >= 0)
        {
            return false;
        }

        try
        {
            itemSnapshots.Add(ToSnapshotJson(item));
            AdvanceStateRevision();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    /// <summary>
    /// 상점 서버 거래가 플레이어 모델에서 제거한 아이템을 Owner 소유 기록에서도 제거한다.
    /// 모델에 같은 instance가 남아 있으면 소유권 중복 가능성이 있으므로 확정하지 않는다.
    /// </summary>
    [Server]
    internal bool ServerCommitShopItemRemoved(string instanceId, uint expectedRevision)
    {
        int snapshotIndex = FindSnapshotIndex(instanceId);

        if (!IsValidInstanceId(instanceId) ||
            stateRevision != expectedRevision ||
            FindOwnedItem(instanceId) != null ||
            snapshotIndex < 0)
        {
            return false;
        }

        itemSnapshots.RemoveAt(snapshotIndex);
        AdvanceStateRevision();
        return true;
    }

    /// <summary>
    /// 상점 판매를 확정하기 전에 서버가 보관 중인 플레이어 소유 기록을 읽는다.
    /// 로컬 Host가 먼저 화면 거래를 적용해 실제 Grid에서 아이템이 빠진 경우에도
    /// 클라이언트가 보낸 임의 데이터가 아니라 서버 기록을 판매 원본으로 사용한다.
    /// </summary>
    [Server]
    internal bool ServerTryGetOwnedSnapshot(
        string instanceId,
        uint expectedRevision,
        out string snapshotJson)
    {
        snapshotJson = null;

        if (!IsValidInstanceId(instanceId) || stateRevision != expectedRevision)
            return false;

        int snapshotIndex = FindSnapshotIndex(instanceId);
        if (snapshotIndex < 0)
            return false;

        snapshotJson = itemSnapshots[snapshotIndex];
        return !string.IsNullOrWhiteSpace(snapshotJson);
    }

    public event Action<MirrorTestInventoryRequestCompleted> RequestCompleted;

    /// <summary>
    /// 로컬 플레이어 화면을 서버 스냅샷 복구 경로에 연결한다.
    /// 서버와 원격 복제본에는 화면이 없으므로 로컬 Context에만 호출한다.
    /// </summary>
    public void BindLocalInventoryView(InventoryView view)
    {
        localInventoryView = view;
        localItemSpawner = view != null
            ? view.GetComponentInChildren<InventoryItemUISpawner>(true)
            : null;

        if (isLocalPlayer)
            EnsureLocalEquipmentVisuals();
    }

    public void UnbindLocalInventoryView(InventoryView view)
    {
        if (view != null && localInventoryView != view)
            return;

        localInventoryView = null;
        localItemSpawner = null;
    }

    private void Awake()
    {
        syncMode = SyncMode.Owner;
        context ??= GetComponent<PlayerContext>();
        itemSnapshots.Callback += HandleSnapshotChanged;
    }

    private void OnDestroy()
    {
        itemSnapshots.Callback -= HandleSnapshotChanged;
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

        MirrorTestInventoryRequestResult result = ServerGrantDistinctTestItem();
        if (result == MirrorTestInventoryRequestResult.Success)
        {
            AdvanceStateRevision();
        }
        else
        {
            Debug.LogError(
                $"[PlayerInventorySync_MirrorTest] 서버 시작 아이템 지급 실패: {result}",
                this);
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isServer)
            return;

        foreach (string snapshot in itemSnapshots)
            ApplyAddedSnapshot(snapshot);
    }

    public override void OnStopClient()
    {
        pendingRequestIds.Clear();
        waitingForRevision.Clear();
        base.OnStopClient();
    }

    public override void OnStopServer()
    {
        processedRequestIds.Clear();
        processedRequestOrder.Clear();
        base.OnStopServer();
    }

    public bool RequestGrantDistinctTestItem()
    {
        if (!TryBeginLocalRequest(out uint requestId, out uint requestedRevision))
            return false;

        CmdGrantDistinctTestItem(requestId, requestedRevision);
        return true;
    }

    public bool RequestDropFirstInventoryItem()
    {
        if (context?.Inventory == null ||
            !TryBeginLocalRequest(out uint requestId, out uint requestedRevision))
        {
            return false;
        }

        CmdDropFirstInventoryItem(requestId, requestedRevision);
        return true;
    }

    /// <summary>
    /// 3-2 실제 드래그가 지정한 아이템 하나를 서버에 드랍 요청한다.
    /// 로컬 UI는 반환된 requestId로 비동기 완료 이벤트를 구분한다.
    /// </summary>
    public bool TryRequestDropInventoryItem(string instanceId, out uint requestId)
    {
        requestId = 0;

        if (!IsValidInstanceId(instanceId) ||
            context?.Inventory == null ||
            !TryBeginLocalRequest(out requestId, out uint requestedRevision))
        {
            return false;
        }

        CmdDropInventoryItem(requestId, requestedRevision, instanceId);
        return true;
    }

    /// <summary>
    /// 3-3 실제 인벤토리 드래그가 결정한 최종 위치와 회전을 서버에 요청한다.
    /// 클라이언트가 먼저 표시한 결과는 확정값이 아니며, 요청 실패 시 서버 스냅샷으로 다시 구성한다.
    /// </summary>
    public bool TryRequestMoveGridItem(
        string instanceId,
        int targetX,
        int targetY,
        bool isRotated,
        out uint requestId)
    {
        requestId = 0;
        InventoryGrid grid = context?.Inventory?.PlayerGrid;

        if (!IsValidInstanceId(instanceId) ||
            !IsValidGridTarget(grid, targetX, targetY) ||
            !TryBeginLocalRequest(out requestId, out uint requestedRevision))
        {
            return false;
        }

        CmdMoveGridItem(
            requestId,
            requestedRevision,
            instanceId,
            targetX,
            targetY,
            isRotated);
        return true;
    }

    /// <summary>
    /// 실제 UI 조작 뒤의 최종 장비 상태를 서버에 요청한다.
    /// 장착이면 장비 칸을, 해제면 인벤토리 칸과 회전 상태를 사용한다.
    /// </summary>
    public bool TryRequestEquipmentChange(
        string instanceId,
        bool shouldBeEquipped,
        EquipSlotType targetSlot,
        int targetX,
        int targetY,
        bool isRotated,
        out uint requestId)
    {
        requestId = 0;
        InventoryGrid grid = context?.Inventory?.PlayerGrid;

        if (!IsValidInstanceId(instanceId) ||
            (shouldBeEquipped
                ? targetSlot == EquipSlotType.None
                : !IsValidGridTarget(grid, targetX, targetY)) ||
            !TryBeginLocalRequest(out requestId, out uint requestedRevision))
        {
            return false;
        }

        CmdChangeEquipment(
            requestId,
            requestedRevision,
            instanceId,
            shouldBeEquipped,
            targetSlot,
            targetX,
            targetY,
            isRotated);
        return true;
    }

    /// <summary>
    /// 현재 로컬 플레이어가 소유한 아이템 하나의 강화를 서버에 요청한다.
    /// 비용과 강화 수치는 서버가 다시 계산하며 클라이언트는 instance만 전달한다.
    /// </summary>
    public bool TryRequestUpgradeItem(string instanceId, out uint requestId)
    {
        requestId = 0;

        if (!IsValidInstanceId(instanceId) ||
            !TryBeginLocalRequest(out requestId, out uint requestedRevision))
        {
            return false;
        }

        CmdUpgradeItem(requestId, requestedRevision, instanceId);
        return true;
    }

    public bool TryRequestPickup(Ray ray)
    {
        if (!isLocalPlayer ||
            !Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Collide))
        {
            return false;
        }

        NetworkWorldItem_MirrorTest pickup =
            hit.collider.GetComponentInParent<NetworkWorldItem_MirrorTest>();

        if (pickup == null ||
            pickup.netId == 0 ||
            !TryBeginLocalRequest(out uint requestId, out uint requestedRevision))
        {
            return false;
        }

        CmdTryPickup(requestId, requestedRevision, pickup.netId);
        return true;
    }

    [Command]
    private void CmdGrantDistinctTestItem(uint requestId, uint requestedRevision)
    {
        if (!TryValidateServerRequest(requestId, requestedRevision, out MirrorTestInventoryRequestResult result))
        {
            TargetCompleteInventoryRequest(
                requestId,
                MirrorTestInventoryOperation.GrantDistinctItem,
                result,
                requestedRevision,
                stateRevision);
            return;
        }

        result = ServerGrantDistinctTestItem();
        CompleteServerRequest(
            requestId,
            MirrorTestInventoryOperation.GrantDistinctItem,
            result,
            requestedRevision);
    }

    [Command]
    private void CmdDropFirstInventoryItem(uint requestId, uint requestedRevision)
    {
        if (!TryValidateServerRequest(requestId, requestedRevision, out MirrorTestInventoryRequestResult result))
        {
            TargetCompleteInventoryRequest(
                requestId,
                MirrorTestInventoryOperation.DropFirstItem,
                result,
                requestedRevision,
                stateRevision);
            return;
        }

        result = ServerDropFirstInventoryItem();
        CompleteServerRequest(
            requestId,
            MirrorTestInventoryOperation.DropFirstItem,
            result,
            requestedRevision);
    }

    [Command]
    private void CmdDropInventoryItem(
        uint requestId,
        uint requestedRevision,
        string instanceId)
    {
        if (!TryValidateServerRequest(requestId, requestedRevision, out MirrorTestInventoryRequestResult result))
        {
            TargetCompleteInventoryRequest(
                requestId,
                MirrorTestInventoryOperation.DropInventoryItem,
                result,
                requestedRevision,
                stateRevision);
            return;
        }

        result = IsValidInstanceId(instanceId)
            ? ServerDropInventoryItem(instanceId)
            : MirrorTestInventoryRequestResult.InvalidRequest;

        CompleteServerRequest(
            requestId,
            MirrorTestInventoryOperation.DropInventoryItem,
            result,
            requestedRevision);
    }

    [Command]
    private void CmdMoveGridItem(
        uint requestId,
        uint requestedRevision,
        string instanceId,
        int targetX,
        int targetY,
        bool isRotated)
    {
        if (!TryValidateServerRequest(requestId, requestedRevision, out MirrorTestInventoryRequestResult result))
        {
            TargetCompleteInventoryRequest(
                requestId,
                MirrorTestInventoryOperation.MoveGridItem,
                result,
                requestedRevision,
                stateRevision);
            return;
        }

        result = ServerMoveGridItem(instanceId, targetX, targetY, isRotated);
        CompleteServerRequest(
            requestId,
            MirrorTestInventoryOperation.MoveGridItem,
            result,
            requestedRevision);
    }

    [Command]
    private void CmdChangeEquipment(
        uint requestId,
        uint requestedRevision,
        string instanceId,
        bool shouldBeEquipped,
        EquipSlotType targetSlot,
        int targetX,
        int targetY,
        bool isRotated)
    {
        if (!TryValidateServerRequest(requestId, requestedRevision, out MirrorTestInventoryRequestResult result))
        {
            TargetCompleteInventoryRequest(
                requestId,
                MirrorTestInventoryOperation.ChangeEquipment,
                result,
                requestedRevision,
                stateRevision);
            return;
        }

        result = ServerChangeEquipment(
            instanceId,
            shouldBeEquipped,
            targetSlot,
            targetX,
            targetY,
            isRotated);
        CompleteServerRequest(
            requestId,
            MirrorTestInventoryOperation.ChangeEquipment,
            result,
            requestedRevision);
    }

    [Command]
    private void CmdUpgradeItem(
        uint requestId,
        uint requestedRevision,
        string instanceId)
    {
        if (!TryValidateServerRequest(requestId, requestedRevision, out MirrorTestInventoryRequestResult result))
        {
            TargetCompleteInventoryRequest(
                requestId,
                MirrorTestInventoryOperation.UpgradeItem,
                result,
                requestedRevision,
                stateRevision);
            return;
        }

        result = ServerUpgradeItem(instanceId);
        CompleteServerRequest(
            requestId,
            MirrorTestInventoryOperation.UpgradeItem,
            result,
            requestedRevision);
    }

    [Command]
    private void CmdTryPickup(uint requestId, uint requestedRevision, uint pickupNetId)
    {
        if (!TryValidateServerRequest(requestId, requestedRevision, out MirrorTestInventoryRequestResult result))
        {
            TargetCompleteInventoryRequest(
                requestId,
                MirrorTestInventoryOperation.PickupWorldItem,
                result,
                requestedRevision,
                stateRevision);
            return;
        }

        result = ServerTryPickup(pickupNetId);
        CompleteServerRequest(
            requestId,
            MirrorTestInventoryOperation.PickupWorldItem,
            result,
            requestedRevision);
    }

    [Server]
    private MirrorTestInventoryRequestResult ServerGrantDistinctTestItem()
    {
        if (context?.Inventory == null)
            return MirrorTestInventoryRequestResult.InventoryUnavailable;

        ItemDefinitionSO definition = ResolveDefinition(DefaultTestItemId);

        if (definition == null)
        {
            Debug.LogError($"[PlayerInventorySync_MirrorTest] 테스트 아이템을 찾지 못했습니다: {DefaultTestItemId}", this);
            return MirrorTestInventoryRequestResult.ItemUnavailable;
        }

        ItemInstance item = ItemDataCreator.CreateItemData(definition);
        return ServerAddItemAndSnapshot(item, out _);
    }

    [Server]
    private MirrorTestInventoryRequestResult ServerDropFirstInventoryItem()
    {
        if (context?.Inventory == null)
            return MirrorTestInventoryRequestResult.InventoryUnavailable;

        IReadOnlyList<InventoryItem> items = context.Inventory.GetAllInventoryItems();
        if (items.Count == 0)
            return MirrorTestInventoryRequestResult.ItemUnavailable;

        return ServerDropInventoryItem(items[0]);
    }

    [Server]
    private MirrorTestInventoryRequestResult ServerMoveGridItem(
        string instanceId,
        int targetX,
        int targetY,
        bool isRotated)
    {
        InventoryGrid grid = context?.Inventory?.PlayerGrid;
        if (grid == null)
            return MirrorTestInventoryRequestResult.InventoryUnavailable;

        InventoryItem item = IsValidInstanceId(instanceId)
            ? FindGridItem(instanceId)
            : null;

        if (item?.itemData?.definition == null)
            return MirrorTestInventoryRequestResult.ItemUnavailable;

        int width = isRotated
            ? item.itemData.definition.itemHeight
            : item.itemData.definition.itemWidth;
        int height = isRotated
            ? item.itemData.definition.itemWidth
            : item.itemData.definition.itemHeight;

        if (!IsValidGridTarget(grid, targetX, targetY) ||
            targetX + width > grid.GridWidth ||
            targetY + height > grid.GridHeight ||
            !HasOneSnapshotPerOwnedItem())
        {
            return MirrorTestInventoryRequestResult.InvalidRequest;
        }

        // 호스트는 클라이언트와 서버가 같은 모델을 사용하므로 Command 도착 전에 드래그 결과가 이미 반영되어 있다.
        if (grid.ContainsItem(item) &&
            item.x == targetX &&
            item.y == targetY &&
            item.isRotated == isRotated)
        {
            return TrySynchronizeOwnedSnapshots()
                ? MirrorTestInventoryRequestResult.Success
                : MirrorTestInventoryRequestResult.StateApplyFailed;
        }

        Dictionary<InventoryItem, InventoryPlacementSnapshot> beforeMove =
            CaptureGridState(grid);
        InventoryPlacementSnapshot originalPlacement =
            InventoryPlacementSnapshot.Capture(grid, item);

        if (!originalPlacement.IsValid || !grid.TryRemoveItem(item))
            return MirrorTestInventoryRequestResult.StateApplyFailed;

        item.isRotated = isRotated;
        InventoryMoveResultData moveResult = InventoryMoveService.TryMoveOnGrid(
            grid,
            item,
            targetX,
            targetY,
            originalPlacement,
            default);

        if (moveResult.Result != InventoryMoveResult.Success &&
            moveResult.Result != InventoryMoveResult.Swapped)
        {
            return TryRestoreGridState(grid, beforeMove)
                ? MirrorTestInventoryRequestResult.StateApplyFailed
                : MirrorTestInventoryRequestResult.RecoveryFailed;
        }

        if (TrySynchronizeOwnedSnapshots())
            return MirrorTestInventoryRequestResult.Success;

        return TryRestoreGridState(grid, beforeMove)
            ? MirrorTestInventoryRequestResult.StateApplyFailed
            : MirrorTestInventoryRequestResult.RecoveryFailed;
    }

    [Server]
    private MirrorTestInventoryRequestResult ServerChangeEquipment(
        string instanceId,
        bool shouldBeEquipped,
        EquipSlotType targetSlot,
        int targetX,
        int targetY,
        bool isRotated)
    {
        InventoryController inventory = context?.Inventory;
        InventoryGrid grid = inventory?.PlayerGrid;
        EquipmentSystem equipment = context?.Equipment;

        if (inventory == null || grid == null || equipment == null)
            return MirrorTestInventoryRequestResult.InventoryUnavailable;

        InventoryItem item = IsValidInstanceId(instanceId)
            ? FindOwnedItem(instanceId)
            : null;

        if (item?.itemData?.definition == null || !HasOneSnapshotPerOwnedItem())
            return MirrorTestInventoryRequestResult.InvalidRequest;

        if (shouldBeEquipped)
        {
            if (targetSlot == EquipSlotType.None ||
                !EquipSlotRules.CanEquipTo(item.itemData.definition, targetSlot))
            {
                return MirrorTestInventoryRequestResult.InvalidRequest;
            }
        }
        else
        {
            int width = isRotated
                ? item.itemData.definition.itemHeight
                : item.itemData.definition.itemWidth;
            int height = isRotated
                ? item.itemData.definition.itemWidth
                : item.itemData.definition.itemHeight;

            if (!IsValidGridTarget(grid, targetX, targetY) ||
                targetX + width > grid.GridWidth ||
                targetY + height > grid.GridHeight)
            {
                return MirrorTestInventoryRequestResult.InvalidRequest;
            }
        }

        // Host는 UI와 서버가 같은 모델을 사용하므로 Command 도착 전에 최종 장비 상태가 반영될 수 있다.
        if (MatchesRequestedEquipmentState(
                item,
                shouldBeEquipped,
                targetSlot,
                targetX,
                targetY,
                isRotated))
        {
            return TrySynchronizeOwnedSnapshots()
                ? MirrorTestInventoryRequestResult.Success
                : MirrorTestInventoryRequestResult.StateApplyFailed;
        }

        List<string> beforeChange = CopySnapshots();
        EquipmentTransaction transaction = new(equipment);
        EquipmentTransactionResult transactionResult;

        if (shouldBeEquipped)
        {
            InventoryItem gridItem = FindGridItem(instanceId);
            InventoryPlacementSnapshot incomingPlacement =
                InventoryPlacementSnapshot.Capture(grid, gridItem);

            if (gridItem == null || !incomingPlacement.IsValid)
                return MirrorTestInventoryRequestResult.InvalidRequest;

            if (equipment.TryGetEquippedItem(targetSlot, out InventoryItem outgoingItem) &&
                outgoingItem != null)
            {
                InventoryPlacementSnapshot outgoingPlacement =
                    InventoryPlacementSnapshot.FromOriginalState(
                        grid,
                        outgoingItem,
                        incomingPlacement.Rect.X,
                        incomingPlacement.Rect.Y,
                        outgoingItem.isRotated);

                transactionResult = transaction.TrySwap(
                    targetSlot,
                    gridItem,
                    grid,
                    incomingPlacement,
                    grid,
                    outgoingPlacement,
                    true);
            }
            else
            {
                transactionResult = transaction.TryEquip(
                    grid,
                    gridItem,
                    incomingPlacement,
                    targetSlot);
            }
        }
        else
        {
            if (!TryFindEquippedSlot(instanceId, out EquipSlotType sourceSlot, out InventoryItem equippedItem))
                return MirrorTestInventoryRequestResult.InvalidRequest;

            InventoryPlacementSnapshot targetPlacement =
                InventoryPlacementSnapshot.FromOriginalState(
                    grid,
                    equippedItem,
                    targetX,
                    targetY,
                    isRotated);

            if (!targetPlacement.IsValid)
                return MirrorTestInventoryRequestResult.InvalidRequest;

            if (grid.CanPlaceItem(
                    targetX,
                    targetY,
                    targetPlacement.Rect.Width,
                    targetPlacement.Rect.Height))
            {
                transactionResult = transaction.TryUnequip(
                    sourceSlot,
                    grid,
                    targetPlacement);
            }
            else if (grid.TryGetItemInArea(
                         targetX,
                         targetY,
                         targetPlacement.Rect.Width,
                         targetPlacement.Rect.Height,
                         out InventoryItem incomingItem) &&
                     incomingItem != null &&
                     incomingItem.x == targetX &&
                     incomingItem.y == targetY)
            {
                InventoryPlacementSnapshot incomingPlacement =
                    InventoryPlacementSnapshot.Capture(grid, incomingItem);

                transactionResult = transaction.TrySwap(
                    sourceSlot,
                    incomingItem,
                    grid,
                    incomingPlacement,
                    grid,
                    targetPlacement,
                    false);
            }
            else
            {
                return MirrorTestInventoryRequestResult.InvalidRequest;
            }
        }

        if (!transactionResult.IsSuccess)
            return transactionResult.HasRecoveryFailure
                ? MirrorTestInventoryRequestResult.RecoveryFailed
                : MirrorTestInventoryRequestResult.StateApplyFailed;

        if (TrySynchronizeOwnedSnapshots())
            return MirrorTestInventoryRequestResult.Success;

        bool rebuildHostVisuals = isLocalPlayer && localItemSpawner != null;
        return TryRestoreOwnedStateFromSnapshots(beforeChange, rebuildHostVisuals)
            ? MirrorTestInventoryRequestResult.StateApplyFailed
            : MirrorTestInventoryRequestResult.RecoveryFailed;
    }

    /// <summary>
    /// 원본 UpgradeController의 로컬 선택 화면은 유지하되 실제 결제와 강화 확정만 서버에서 처리한다.
    /// 스냅샷 반영에 실패하면 강화 수치와 골드를 모두 되돌려 부분 적용을 남기지 않는다.
    /// </summary>
    [Server]
    private MirrorTestInventoryRequestResult ServerUpgradeItem(string instanceId)
    {
        InventoryItem item = IsValidInstanceId(instanceId)
            ? FindOwnedItem(instanceId)
            : null;
        NetworkShopPlayerState_MirrorTest playerState =
            GetComponent<NetworkShopPlayerState_MirrorTest>();

        if (item?.itemData == null)
            return MirrorTestInventoryRequestResult.ItemUnavailable;

        if (playerState == null || context?.Equipment == null)
            return MirrorTestInventoryRequestResult.ServerSetupInvalid;

        if (!UpgradeService.CanUpgrade(item.itemData) ||
            !TryGetUpgradeCost(item.itemData, out int cost))
        {
            return MirrorTestInventoryRequestResult.UpgradeUnavailable;
        }

        int snapshotIndex = FindSnapshotIndex(instanceId);
        if (snapshotIndex < 0 || !DoesOwnedStateMatchSnapshot(itemSnapshots[snapshotIndex]))
            return MirrorTestInventoryRequestResult.StateApplyFailed;

        bool isEquipped = TryFindEquippedSlot(
            instanceId,
            out EquipSlotType equippedSlot,
            out InventoryItem equippedItem);

        if (isEquipped && !ReferenceEquals(item, equippedItem))
            return MirrorTestInventoryRequestResult.StateApplyFailed;

        if (!playerState.ServerTrySpendGold(cost))
            return MirrorTestInventoryRequestResult.NotEnoughGold;

        int previousLevel = item.itemData.upgradeLevel;
        string previousSnapshot = itemSnapshots[snapshotIndex];

        try
        {
            item.itemData.upgradeLevel++;
            itemSnapshots[snapshotIndex] = ToSnapshotJson(item, isEquipped, equippedSlot);

            if (isEquipped)
                context.Equipment.NotifyEquippedItemChanged(item.itemData);

            return MirrorTestInventoryRequestResult.Success;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);

            try
            {
                item.itemData.upgradeLevel = previousLevel;
                itemSnapshots[snapshotIndex] = previousSnapshot;
                playerState.ServerAddGold(cost);

                if (isEquipped)
                    context.Equipment.NotifyEquippedItemChanged(item.itemData);

                return MirrorTestInventoryRequestResult.StateApplyFailed;
            }
            catch (Exception recoveryException)
            {
                Debug.LogException(recoveryException, this);
                return MirrorTestInventoryRequestResult.RecoveryFailed;
            }
        }
    }

    [Server]
    private MirrorTestInventoryRequestResult ServerDropInventoryItem(string instanceId)
    {
        InventoryItem item = FindGridItem(instanceId);
        return item != null
            ? ServerDropInventoryItem(item)
            : MirrorTestInventoryRequestResult.ItemUnavailable;
    }

    [Server]
    private MirrorTestInventoryRequestResult ServerDropInventoryItem(InventoryItem item)
    {
        if (context?.Inventory == null)
            return MirrorTestInventoryRequestResult.InventoryUnavailable;

        if (worldItemPrefab == null)
            return MirrorTestInventoryRequestResult.ServerSetupInvalid;

        if (item?.itemData == null)
            return MirrorTestInventoryRequestResult.ItemUnavailable;

        string snapshot = ToSnapshotJson(item);
        int snapshotIndex = FindSnapshotIndex(item.itemData.instanceId);
        InventoryPlacementSnapshot originalPlacement =
            InventoryPlacementSnapshot.Capture(context.Inventory.PlayerGrid, item);

        if (snapshotIndex < 0 || !originalPlacement.IsValid)
            return MirrorTestInventoryRequestResult.StateApplyFailed;

        // ponytail: C단계 평면 테스트용 배치다. 실제 맵 적용 시 WorldItemDropService의
        // 충돌·빈자리 탐색을 서버 전용 API로 올린 뒤 이 위치 계산을 교체한다.
        Vector3 position = transform.position + transform.forward * dropDistance + Vector3.up * 0.5f;
        NetworkWorldItem_MirrorTest pickup = null;
        bool removed = false;

        try
        {
            if (!NetworkWorldItemSpawnService_MirrorTest.TryCreateUnspawned(
                    worldItemPrefab,
                    snapshot,
                    position,
                    out pickup))
            {
                return MirrorTestInventoryRequestResult.SpawnFailed;
            }

            removed = context.Inventory.TryRemoveInventoryItem(item) == InventoryRemoveResult.Success;
            if (!removed)
            {
                Destroy(pickup.gameObject);
                return MirrorTestInventoryRequestResult.StateApplyFailed;
            }

            if (!NetworkWorldItemSpawnService_MirrorTest.TrySpawnCreated(pickup))
                throw new InvalidOperationException("Mirror failed to assign a netId to the dropped item.");

            itemSnapshots.RemoveAt(snapshotIndex);
            return MirrorTestInventoryRequestResult.Success;
        }
        catch (Exception exception)
        {
            bool recoveryFailed = false;

            if (removed)
            {
                InventoryAddResultData rollback = context.Inventory.TryAddItemAt(
                    item,
                    originalPlacement.Rect.X,
                    originalPlacement.Rect.Y);

                if (rollback.Result != InventoryAddResult.Success)
                {
                    recoveryFailed = true;
                    RemoveSnapshot(item.itemData.instanceId);
                    AdvanceStateRevision();
                    Debug.LogError(
                        $"[PlayerInventorySync_MirrorTest] 드랍 실패 후 인벤토리 복구도 실패했습니다: " +
                        $"{item.itemData.definition.itemId}, result={rollback.Result}",
                        this);
                }
            }

            if (pickup != null)
            {
                if (pickup.netId != 0)
                    NetworkServer.Destroy(pickup.gameObject);
                else
                    Destroy(pickup.gameObject);
            }

            Debug.LogException(exception, this);
            return recoveryFailed
                ? MirrorTestInventoryRequestResult.RecoveryFailed
                : MirrorTestInventoryRequestResult.SpawnFailed;
        }
    }

    [Server]
    private MirrorTestInventoryRequestResult ServerTryPickup(uint pickupNetId)
    {
        if (context?.Inventory == null)
            return MirrorTestInventoryRequestResult.InventoryUnavailable;

        if (!NetworkServer.spawned.TryGetValue(pickupNetId, out NetworkIdentity identity) ||
            identity == null ||
            !identity.TryGetComponent(out NetworkWorldItem_MirrorTest pickup))
            return MirrorTestInventoryRequestResult.PickupUnavailable;

        if (Vector3.Distance(transform.position, pickup.transform.position) > pickupDistance)
            return MirrorTestInventoryRequestResult.OutOfRange;

        if (!pickup.TryClaimServer())
            return MirrorTestInventoryRequestResult.AlreadyClaimed;

        ItemInstance item = pickup.CreateItemInstance();
        if (item == null)
        {
            pickup.ReleaseClaimServer();
            return MirrorTestInventoryRequestResult.ItemUnavailable;
        }

        MirrorTestInventoryRequestResult addResult =
            ServerAddItemAndSnapshot(item, out _);

        if (addResult != MirrorTestInventoryRequestResult.Success)
        {
            if (addResult == MirrorTestInventoryRequestResult.RecoveryFailed)
                NetworkServer.Destroy(pickup.gameObject);
            else
                pickup.ReleaseClaimServer();

            return addResult;
        }

        NetworkServer.Destroy(pickup.gameObject);
        return MirrorTestInventoryRequestResult.Success;
    }

    [Server]
    private MirrorTestInventoryRequestResult ServerAddItemAndSnapshot(
        ItemInstance item,
        out InventoryItem added)
    {
        added = null;

        if (context?.Inventory == null)
            return MirrorTestInventoryRequestResult.InventoryUnavailable;

        // 호스트 드래그 중에는 같은 서버 Grid에서 아이템이 잠시 분리된다.
        // 이 짧은 구간을 빈자리로 오인해 새 아이템을 겹쳐 놓지 않도록 추가 요청을 거절한다.
        if (!IsOwnedStateEqualToSnapshots())
            return MirrorTestInventoryRequestResult.StateApplyFailed;

        InventoryAddResultData addResult = context.Inventory.TryAddItemData(item);
        if (addResult.Result != InventoryAddResult.Success)
            return MapInventoryAddResult(addResult.Result);

        added = FindGridItem(item.instanceId);
        if (added == null)
        {
            Debug.LogError(
                $"[PlayerInventorySync_MirrorTest] 서버 추가 후 소유 아이템을 다시 찾지 못했습니다: {item.instanceId}",
                this);
            return MirrorTestInventoryRequestResult.StateApplyFailed;
        }

        try
        {
            itemSnapshots.Add(ToSnapshotJson(added));
            return MirrorTestInventoryRequestResult.Success;
        }
        catch (Exception exception)
        {
            InventoryRemoveResult rollback = context.Inventory.TryRemoveInventoryItem(added);
            bool recoveryFailed = rollback != InventoryRemoveResult.Success;

            if (recoveryFailed)
            {
                TryEnsureSnapshot(added);
                AdvanceStateRevision();
                Debug.LogError(
                    $"[PlayerInventorySync_MirrorTest] 스냅샷 추가 실패 후 인벤토리 복구도 실패했습니다: " +
                    $"{item.definition.itemId}, result={rollback}",
                    this);
            }

            Debug.LogException(exception, this);
            return recoveryFailed
                ? MirrorTestInventoryRequestResult.RecoveryFailed
                : MirrorTestInventoryRequestResult.StateApplyFailed;
        }
    }

    private bool TryBeginLocalRequest(out uint requestId, out uint requestedRevision)
    {
        requestId = 0;
        requestedRevision = stateRevision;

        if (!isLocalPlayer || !NetworkClient.active || !NetworkClient.ready)
            return false;

        nextRequestId++;
        if (nextRequestId == 0)
            nextRequestId++;

        requestId = nextRequestId;
        pendingRequestIds.Add(requestId);
        return true;
    }

    private static bool IsValidInstanceId(string instanceId)
    {
        return !string.IsNullOrWhiteSpace(instanceId) &&
               instanceId.Length <= MaxInstanceIdLength;
    }

    private static bool TryGetUpgradeCost(ItemInstance item, out int cost)
    {
        cost = 0;
        if (item == null || item.upgradeLevel < 0 || item.upgradeLevel == int.MaxValue)
            return false;

        float rawCost = 500f * Mathf.Pow(UpgradeCostMultiplier, item.upgradeLevel);
        if (float.IsNaN(rawCost) ||
            float.IsInfinity(rawCost) ||
            rawCost <= 0f ||
            rawCost > int.MaxValue - 9f)
        {
            return false;
        }

        cost = Mathf.CeilToInt(rawCost / 10f) * 10;
        return cost > 0;
    }

    [Server]
    private bool TryValidateServerRequest(
        uint requestId,
        uint requestedRevision,
        out MirrorTestInventoryRequestResult result)
    {
        if (requestId == 0)
        {
            result = MirrorTestInventoryRequestResult.InvalidRequest;
            return false;
        }

        if (!processedRequestIds.Add(requestId))
        {
            result = MirrorTestInventoryRequestResult.DuplicateRequest;
            return false;
        }

        processedRequestOrder.Enqueue(requestId);
        while (processedRequestOrder.Count > ProcessedRequestHistorySize)
            processedRequestIds.Remove(processedRequestOrder.Dequeue());

        if (requestedRevision != stateRevision)
        {
            result = MirrorTestInventoryRequestResult.StaleRevision;
            return false;
        }

        result = MirrorTestInventoryRequestResult.Success;
        return true;
    }

    [Server]
    private void CompleteServerRequest(
        uint requestId,
        MirrorTestInventoryOperation operation,
        MirrorTestInventoryRequestResult result,
        uint requestedRevision)
    {
        if (result == MirrorTestInventoryRequestResult.Success)
            AdvanceStateRevision();

        TargetCompleteInventoryRequest(
            requestId,
            operation,
            result,
            requestedRevision,
            stateRevision);
    }

    [Server]
    private void AdvanceStateRevision()
    {
        stateRevision++;
        if (stateRevision == 0)
            stateRevision = 1;
    }

    [TargetRpc]
    private void TargetCompleteInventoryRequest(
        uint requestId,
        MirrorTestInventoryOperation operation,
        MirrorTestInventoryRequestResult result,
        uint requestedRevision,
        uint authoritativeRevision)
    {
        MirrorTestInventoryRequestCompleted completed = new(
            requestId,
            operation,
            result,
            requestedRevision,
            authoritativeRevision);

        if (stateRevision < authoritativeRevision)
        {
            waitingForRevision[requestId] = completed;
            return;
        }

        CompleteLocalRequest(completed);
    }

    private void HandleStateRevisionChanged(uint _, uint newRevision)
    {
        if (waitingForRevision.Count == 0)
            return;

        List<uint> readyRequestIds = null;
        foreach (KeyValuePair<uint, MirrorTestInventoryRequestCompleted> pair in waitingForRevision)
        {
            if (pair.Value.AuthoritativeRevision > newRevision)
                continue;

            readyRequestIds ??= new List<uint>();
            readyRequestIds.Add(pair.Key);
        }

        if (readyRequestIds == null)
            return;

        foreach (uint requestId in readyRequestIds)
        {
            MirrorTestInventoryRequestCompleted completed = waitingForRevision[requestId];
            waitingForRevision.Remove(requestId);
            CompleteLocalRequest(completed);
        }
    }

    private void CompleteLocalRequest(MirrorTestInventoryRequestCompleted completed)
    {
        pendingRequestIds.Remove(completed.RequestId);
        RequestCompleted?.Invoke(completed);
    }

    private void HandleSnapshotChanged(
        SyncList<string>.Operation operation,
        int index,
        string oldSnapshot,
        string newSnapshot)
    {
        if (isServer)
            return;

        switch (operation)
        {
            case SyncList<string>.Operation.OP_ADD:
            case SyncList<string>.Operation.OP_INSERT:
                ApplyAddedSnapshot(newSnapshot);
                break;

            case SyncList<string>.Operation.OP_SET:
                if (DoesOwnedStateMatchSnapshot(newSnapshot))
                    break;

                ApplyRemovedSnapshot(oldSnapshot);
                ApplyAddedSnapshot(newSnapshot);
                break;

            case SyncList<string>.Operation.OP_REMOVEAT:
                ApplyRemovedSnapshot(oldSnapshot);
                break;
        }
    }

    private void ApplyAddedSnapshot(string snapshotJson)
    {
        ItemSaveData saved = FromSnapshotJson(snapshotJson);
        if (saved == null || FindOwnedItem(saved.instanceId) != null)
            return;

        ItemInstance item = CreateItemInstance(saved);
        if (item == null)
            return;

        InventoryItem inventoryItem = new(item)
        {
            isRotated = saved.isRotated,
        };

        bool restored = saved.isEquipped
            ? TryRestoreEquippedItem(
                inventoryItem,
                saved.equippedSlotType,
                localItemSpawner != null)
            : context.Inventory.TryAddItemAt(
                inventoryItem,
                saved.gridX,
                saved.gridY).Result == InventoryAddResult.Success;

        if (!restored)
        {
            Debug.LogError(
                $"[PlayerInventorySync_MirrorTest] 동기화 아이템 상태 적용 실패: {saved.itemId}",
                this);
        }
    }

    private void ApplyRemovedSnapshot(string snapshotJson)
    {
        ItemSaveData saved = FromSnapshotJson(snapshotJson);
        InventoryItem item = saved != null ? FindOwnedItem(saved.instanceId) : null;

        if (item == null)
            return;

        InventoryItem gridItem = FindGridItem(saved.instanceId);
        if (gridItem != null)
        {
            context.Inventory.TryRemoveInventoryItem(gridItem);
            return;
        }

        if (!TryFindEquippedSlot(saved.instanceId, out EquipSlotType slotType, out InventoryItem equippedItem))
            return;

        EquipmentTransactionResult result =
            new EquipmentTransaction(context.Equipment)
                .TryUnequipForTransfer(slotType, equippedItem);

        if (!result.IsSuccess)
            return;

        ClearLocalEquipmentVisual(slotType, equippedItem);
    }

    /// <summary>
    /// 로컬에서 먼저 적용한 이동·장비 변경을 버리고 서버 확정 소유 상태로 다시 구성한다.
    /// 기존 3-3 호출부 이름은 유지하지만 3-4부터 장비와 장비 슬롯 화면도 함께 복구한다.
    /// </summary>
    public bool TryRestoreGridFromAuthoritativeSnapshots()
    {
        if (!isLocalPlayer || context?.Inventory?.PlayerGrid == null || context.Equipment == null)
            return false;

        return TryRestoreOwnedStateFromSnapshots(CopySnapshots(), true) &&
               IsOwnedStateEqualToSnapshots();
    }

    private InventoryItem FindOwnedItem(string instanceId)
    {
        InventoryItem gridItem = FindGridItem(instanceId);
        if (gridItem != null)
            return gridItem;

        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in context.Equipment.GetEquippedItems())
        {
            if (pair.Value?.itemData?.instanceId == instanceId)
                return pair.Value;
        }

        return null;
    }

    private bool DoesOwnedStateMatchSnapshot(string snapshotJson)
    {
        ItemSaveData saved = FromSnapshotJson(snapshotJson);
        InventoryItem item = saved != null ? FindOwnedItem(saved.instanceId) : null;
        if (item?.itemData == null || item.itemData.upgradeLevel != saved.upgradeLevel)
            return false;

        if (saved.isEquipped)
        {
            return TryFindEquippedSlot(
                       saved.instanceId,
                       out EquipSlotType slotType,
                       out InventoryItem equippedItem) &&
                   slotType == saved.equippedSlotType &&
                   ReferenceEquals(item, equippedItem);
        }

        return context.Inventory.PlayerGrid.ContainsItem(item) &&
               item.x == saved.gridX &&
               item.y == saved.gridY &&
               item.isRotated == saved.isRotated;
    }

    private bool TryFindEquippedSlot(
        string instanceId,
        out EquipSlotType slotType,
        out InventoryItem equippedItem)
    {
        slotType = EquipSlotType.None;
        equippedItem = null;

        if (string.IsNullOrWhiteSpace(instanceId) || context?.Equipment == null)
            return false;

        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in context.Equipment.GetEquippedItems())
        {
            if (pair.Value?.itemData?.instanceId != instanceId)
                continue;

            slotType = pair.Key;
            equippedItem = pair.Value;
            return true;
        }

        return false;
    }

    private bool MatchesRequestedEquipmentState(
        InventoryItem item,
        bool shouldBeEquipped,
        EquipSlotType targetSlot,
        int targetX,
        int targetY,
        bool isRotated)
    {
        if (item?.itemData == null)
            return false;

        if (shouldBeEquipped)
        {
            return TryFindEquippedSlot(
                       item.itemData.instanceId,
                       out EquipSlotType currentSlot,
                       out InventoryItem equippedItem) &&
                   currentSlot == targetSlot &&
                   ReferenceEquals(item, equippedItem);
        }

        return context.Inventory.PlayerGrid.ContainsItem(item) &&
               item.x == targetX &&
               item.y == targetY &&
               item.isRotated == isRotated;
    }

    private List<string> CopySnapshots()
    {
        List<string> snapshots = new(itemSnapshots.Count);
        foreach (string snapshot in itemSnapshots)
            snapshots.Add(snapshot);

        return snapshots;
    }

    private bool TryRestoreOwnedStateFromSnapshots(
        IReadOnlyList<string> snapshots,
        bool rebuildVisuals)
    {
        if (context?.Inventory?.PlayerGrid == null ||
            context.Equipment == null ||
            snapshots == null)
        {
            return false;
        }

        List<ItemSaveData> savedItems = new(snapshots.Count);
        HashSet<string> instanceIds = new();
        foreach (string snapshot in snapshots)
        {
            ItemSaveData saved = FromSnapshotJson(snapshot);
            if (saved == null ||
                !IsValidInstanceId(saved.instanceId) ||
                !instanceIds.Add(saved.instanceId) ||
                CreateItemInstance(saved) == null ||
                (saved.isEquipped && saved.equippedSlotType == EquipSlotType.None))
            {
                return false;
            }

            savedItems.Add(saved);
        }

        if (!IsValidSavedStateLayout(savedItems))
            return false;

        if (!TryClearOwnedState(rebuildVisuals))
            return false;

        // 장비 교환 복구 시 그리드 자리를 먼저 채운 뒤 장비 슬롯을 복원한다.
        foreach (ItemSaveData saved in savedItems)
        {
            if (saved.isEquipped)
                continue;

            if (!TryRestoreSavedItem(saved, rebuildVisuals))
                return false;
        }

        foreach (ItemSaveData saved in savedItems)
        {
            if (!saved.isEquipped)
                continue;

            if (!TryRestoreSavedItem(saved, rebuildVisuals))
                return false;
        }

        return CountOwnedItems() == savedItems.Count;
    }

    private bool IsValidSavedStateLayout(IReadOnlyList<ItemSaveData> savedItems)
    {
        InventoryGrid grid = context.Inventory.PlayerGrid;
        bool[,] occupied = new bool[grid.GridWidth, grid.GridHeight];
        HashSet<EquipSlotType> occupiedSlots = new();

        foreach (ItemSaveData saved in savedItems)
        {
            ItemInstance item = CreateItemInstance(saved);
            ItemDefinitionSO definition = item?.definition;
            if (definition == null)
                return false;

            if (saved.isEquipped)
            {
                if (!occupiedSlots.Add(saved.equippedSlotType) ||
                    !EquipSlotRules.CanEquipTo(definition, saved.equippedSlotType))
                {
                    return false;
                }

                continue;
            }

            int width = saved.isRotated ? definition.itemHeight : definition.itemWidth;
            int height = saved.isRotated ? definition.itemWidth : definition.itemHeight;
            if (!IsValidGridTarget(grid, saved.gridX, saved.gridY) ||
                saved.gridX + width > grid.GridWidth ||
                saved.gridY + height > grid.GridHeight)
            {
                return false;
            }

            for (int x = saved.gridX; x < saved.gridX + width; x++)
            {
                for (int y = saved.gridY; y < saved.gridY + height; y++)
                {
                    if (occupied[x, y])
                        return false;

                    occupied[x, y] = true;
                }
            }
        }

        return true;
    }

    private bool TryRestoreSavedItem(ItemSaveData saved, bool rebuildVisuals)
    {
        ItemInstance itemData = CreateItemInstance(saved);
        if (itemData == null)
            return false;

        InventoryItem item = new(itemData)
        {
            isRotated = saved.isRotated,
        };

        if (saved.isEquipped)
            return TryRestoreEquippedItem(item, saved.equippedSlotType, rebuildVisuals);

        return context.Inventory.TryAddItemAt(
            item,
            saved.gridX,
            saved.gridY).Result == InventoryAddResult.Success;
    }

    private bool TryRestoreEquippedItem(
        InventoryItem item,
        EquipSlotType slotType,
        bool rebuildVisuals)
    {
        ItemUI spawnedUI = null;
        ItemEquipHandler equipHandler = null;
        EquipSlotUI slotUI = null;

        if (rebuildVisuals)
        {
            slotUI = FindLocalEquipmentSlot(slotType);
            if (localItemSpawner == null || slotUI == null || !slotUI.IsEmpty)
                return false;

            spawnedUI = localItemSpawner.SpawnItemUIAndGet(item);
            equipHandler = spawnedUI != null
                ? spawnedUI.GetComponent<ItemEquipHandler>()
                : null;

            if (equipHandler == null)
            {
                if (spawnedUI != null)
                    Destroy(spawnedUI.gameObject);
                return false;
            }
        }

        EquipmentTransactionResult result =
            new EquipmentTransaction(context.Equipment)
                .TryRestoreEquippedItem(item, slotType);

        if (!result.IsSuccess)
        {
            if (spawnedUI != null)
                Destroy(spawnedUI.gameObject);
            return false;
        }

        if (rebuildVisuals)
            equipHandler.SetEquipSlotVisual(slotUI);

        return true;
    }

    private bool TryClearOwnedState(bool clearVisuals)
    {
        List<KeyValuePair<EquipSlotType, InventoryItem>> equippedItems = new();
        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in context.Equipment.GetEquippedItems())
            equippedItems.Add(pair);

        EquipmentTransaction transaction = new(context.Equipment);
        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in equippedItems)
        {
            EquipmentTransactionResult result =
                transaction.TryUnequipForTransfer(pair.Key, pair.Value);
            if (!result.IsSuccess)
                return false;

            if (clearVisuals)
                ClearLocalEquipmentVisual(pair.Key, pair.Value);
        }

        List<InventoryItem> gridItems = new(context.Inventory.GetAllInventoryItems());
        foreach (InventoryItem item in gridItems)
        {
            if (context.Inventory.TryRemoveInventoryItem(item) != InventoryRemoveResult.Success)
                return false;
        }

        return true;
    }

    private EquipSlotUI FindLocalEquipmentSlot(EquipSlotType slotType)
    {
        if (localInventoryView == null)
            return null;

        foreach (EquipSlotUI slot in localInventoryView.EquipmentSlots)
        {
            if (slot != null && slot.SlotType == slotType)
                return slot;
        }

        return null;
    }

    private void EnsureLocalEquipmentVisuals()
    {
        if (localItemSpawner == null || localInventoryView == null || context?.Equipment == null)
            return;

        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in context.Equipment.GetEquippedItems())
        {
            EquipSlotUI slot = FindLocalEquipmentSlot(pair.Key);
            if (slot == null || !slot.IsEmpty || pair.Value?.itemData == null)
                continue;

            ItemUI spawnedUI = localItemSpawner.SpawnItemUIAndGet(pair.Value);
            ItemEquipHandler equipHandler = spawnedUI != null
                ? spawnedUI.GetComponent<ItemEquipHandler>()
                : null;

            if (equipHandler != null)
                equipHandler.SetEquipSlotVisual(slot);
            else if (spawnedUI != null)
                Destroy(spawnedUI.gameObject);
        }
    }

    private void ClearLocalEquipmentVisual(EquipSlotType slotType, InventoryItem item)
    {
        EquipSlotUI slot = FindLocalEquipmentSlot(slotType);
        ItemUI itemUI = slot?.EquippedItemUI;

        if (itemUI == null || !ReferenceEquals(itemUI.Item, item))
            return;

        slot.ClearItemUI();
        itemUI.ClearCurrentEquipSlot();
        Destroy(itemUI.gameObject);
    }

    private int CountOwnedItems()
    {
        int count = context.Inventory.GetAllInventoryItems().Count;
        foreach (KeyValuePair<EquipSlotType, InventoryItem> _ in context.Equipment.GetEquippedItems())
            count++;

        return count;
    }

    private InventoryItem FindGridItem(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || context?.Inventory == null)
            return null;

        foreach (InventoryItem item in context.Inventory.GetAllInventoryItems())
        {
            if (item?.itemData?.instanceId == instanceId)
                return item;
        }

        return null;
    }

    private int FindSnapshotIndex(string instanceId)
    {
        for (int i = 0; i < itemSnapshots.Count; i++)
        {
            ItemSaveData saved = FromSnapshotJson(itemSnapshots[i]);
            if (saved?.instanceId == instanceId)
                return i;
        }

        return -1;
    }

    private bool HasOneSnapshotPerOwnedItem()
    {
        IReadOnlyList<InventoryItem> gridItems = context.Inventory.GetAllInventoryItems();
        int ownedItemCount = gridItems.Count;
        foreach (KeyValuePair<EquipSlotType, InventoryItem> _ in context.Equipment.GetEquippedItems())
            ownedItemCount++;

        if (ownedItemCount != itemSnapshots.Count)
            return false;

        HashSet<int> matchedIndexes = new();
        foreach (InventoryItem item in gridItems)
        {
            int index = item?.itemData != null
                ? FindSnapshotIndex(item.itemData.instanceId)
                : -1;

            if (index < 0 || !matchedIndexes.Add(index))
                return false;
        }

        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in context.Equipment.GetEquippedItems())
        {
            int index = pair.Value?.itemData != null
                ? FindSnapshotIndex(pair.Value.itemData.instanceId)
                : -1;

            if (index < 0 || !matchedIndexes.Add(index))
                return false;
        }

        return true;
    }

    private bool IsOwnedStateEqualToSnapshots()
    {
        if (!HasOneSnapshotPerOwnedItem())
            return false;

        foreach (InventoryItem item in context.Inventory.GetAllInventoryItems())
        {
            int index = item?.itemData != null
                ? FindSnapshotIndex(item.itemData.instanceId)
                : -1;
            ItemSaveData saved = index >= 0
                ? FromSnapshotJson(itemSnapshots[index])
                : null;

            if (saved == null ||
                saved.isEquipped ||
                saved.gridX != item.x ||
                saved.gridY != item.y ||
                saved.isRotated != item.isRotated)
            {
                return false;
            }
        }

        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in context.Equipment.GetEquippedItems())
        {
            int index = pair.Value?.itemData != null
                ? FindSnapshotIndex(pair.Value.itemData.instanceId)
                : -1;
            ItemSaveData saved = index >= 0
                ? FromSnapshotJson(itemSnapshots[index])
                : null;

            if (saved == null ||
                !saved.isEquipped ||
                saved.equippedSlotType != pair.Key)
            {
                return false;
            }
        }

        return true;
    }

    private bool TrySynchronizeOwnedSnapshots()
    {
        if (!HasOneSnapshotPerOwnedItem())
            return false;

        List<int> indexes = new(itemSnapshots.Count);
        List<string> snapshots = new(itemSnapshots.Count);
        HashSet<int> matchedIndexes = new();

        try
        {
            // 그리드 항목을 먼저 갱신하면 장비 교환으로 돌아온 아이템 위치가 먼저 확정된다.
            foreach (InventoryItem item in context.Inventory.GetAllInventoryItems())
            {
                int index = item?.itemData != null
                    ? FindSnapshotIndex(item.itemData.instanceId)
                    : -1;

                if (index < 0 || !matchedIndexes.Add(index))
                    return false;

                indexes.Add(index);
                snapshots.Add(ToSnapshotJson(item, false, EquipSlotType.None));
            }

            foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in context.Equipment.GetEquippedItems())
            {
                int index = pair.Value?.itemData != null
                    ? FindSnapshotIndex(pair.Value.itemData.instanceId)
                    : -1;

                if (index < 0 || !matchedIndexes.Add(index))
                    return false;

                indexes.Add(index);
                snapshots.Add(ToSnapshotJson(pair.Value, true, pair.Key));
            }

            // ponytail: 테스트 소유 아이템이 적으므로 전체 항목을 확인해 장비 교환 동기화를 단순하게 유지한다.
            // 실제 대규모 인벤토리로 승격할 때만 instanceId 인덱스 캐시로 교체한다.
            for (int i = 0; i < indexes.Count; i++)
            {
                if (!string.Equals(itemSnapshots[indexes[i]], snapshots[i], StringComparison.Ordinal))
                    itemSnapshots[indexes[i]] = snapshots[i];
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    private static Dictionary<InventoryItem, InventoryPlacementSnapshot> CaptureGridState(
        InventoryGrid grid)
    {
        Dictionary<InventoryItem, InventoryPlacementSnapshot> state = new();
        foreach (InventoryItem item in grid.GetAllItems())
            state[item] = InventoryPlacementSnapshot.Capture(grid, item);

        return state;
    }

    private static bool TryRestoreGridState(
        InventoryGrid grid,
        Dictionary<InventoryItem, InventoryPlacementSnapshot> state)
    {
        List<InventoryItem> currentItems = new(grid.GetAllItems());
        foreach (InventoryItem currentItem in currentItems)
        {
            if (!grid.TryRemoveItem(currentItem))
                return false;
        }

        foreach (KeyValuePair<InventoryItem, InventoryPlacementSnapshot> pair in state)
        {
            InventoryPlacementSnapshot placement = pair.Value;
            pair.Key.isRotated = placement.IsRotated;
            if (!grid.TryPlaceItem(pair.Key, placement.Rect.X, placement.Rect.Y))
                return false;
        }

        return true;
    }

    private static bool IsValidGridTarget(InventoryGrid grid, int targetX, int targetY)
    {
        return grid != null &&
               targetX >= 0 &&
               targetY >= 0 &&
               targetX < grid.GridWidth &&
               targetY < grid.GridHeight;
    }

    private void RemoveSnapshot(string instanceId)
    {
        int snapshotIndex = FindSnapshotIndex(instanceId);
        if (snapshotIndex >= 0)
            itemSnapshots.RemoveAt(snapshotIndex);
    }

    private bool TryEnsureSnapshot(InventoryItem item)
    {
        if (item?.itemData == null)
            return false;

        if (FindSnapshotIndex(item.itemData.instanceId) >= 0)
            return true;

        try
        {
            itemSnapshots.Add(ToSnapshotJson(item));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    private static MirrorTestInventoryRequestResult MapInventoryAddResult(InventoryAddResult result)
    {
        return result switch
        {
            InventoryAddResult.NoSpace => MirrorTestInventoryRequestResult.InventoryFull,
            InventoryAddResult.InvalidItem => MirrorTestInventoryRequestResult.ItemUnavailable,
            InventoryAddResult.GridUnavailable => MirrorTestInventoryRequestResult.InventoryUnavailable,
            _ => MirrorTestInventoryRequestResult.StateApplyFailed,
        };
    }

    internal static ItemInstance CreateItemInstance(string snapshotJson)
    {
        return CreateItemInstance(FromSnapshotJson(snapshotJson));
    }

    internal static string CreateSnapshotJson(InventoryItem item)
    {
        return item?.itemData?.definition != null
            ? ToSnapshotJson(item)
            : null;
    }

    private static ItemInstance CreateItemInstance(ItemSaveData saved)
    {
        if (saved == null)
            return null;

        ItemDefinitionSO definition = ResolveDefinition(saved.itemId);
        if (definition == null)
            return null;

        return new ItemInstance
        {
            instanceId = saved.instanceId,
            definition = definition,
            rolledSubStats = saved.rolledSubStats ?? new List<RolledSubStat>(),
            rolledElement = saved.rolledElement,
            upgradeLevel = saved.upgradeLevel,
        };
    }

    private static string ToSnapshotJson(InventoryItem item)
    {
        return ToSnapshotJson(item, false, EquipSlotType.None);
    }

    private static string ToSnapshotJson(
        InventoryItem item,
        bool isEquipped,
        EquipSlotType equippedSlotType)
    {
        ItemInstance data = item.itemData;
        ItemSaveData saved = new()
        {
            instanceId = data.instanceId,
            itemId = data.definition.itemId,
            rolledSubStats = data.rolledSubStats,
            rolledElement = data.rolledElement,
            upgradeLevel = data.upgradeLevel,
            gridX = item.x,
            gridY = item.y,
            isRotated = item.isRotated,
            isEquipped = isEquipped,
            equippedSlotType = isEquipped
                ? equippedSlotType
                : EquipSlotType.None,
        };

        return JsonUtility.ToJson(saved);
    }

    private static ItemSaveData FromSnapshotJson(string snapshotJson)
    {
        return string.IsNullOrWhiteSpace(snapshotJson)
            ? null
            : JsonUtility.FromJson<ItemSaveData>(snapshotJson);
    }

    private static ItemDefinitionSO ResolveDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        // StageSelect에는 실제 ItemManager Prefab이 아직 없다. 여기서 Singleton.Instance를 읽으면
        // Singleton<T>가 데이터베이스가 비어 있는 임시 ItemManager를 생성하고 DontDestroyOnLoad로
        // 남겨서, 다음 전투 Scene의 정상 ItemManager가 중복으로 제거된다. 존재하는 매니저만 조회하고
        // 없으면 아래 Resources 경로로 해석해 Scene 전환 전에는 전역 상태를 만들지 않는다.
        ItemManager itemManager =
            UnityEngine.Object.FindFirstObjectByType<ItemManager>(FindObjectsInactive.Include);
        ItemDefinitionSO definition = itemManager?.ItemDatabase?.GetById(itemId);

        if (definition != null)
            return definition;

        foreach (ItemDefinitionSO candidate in
                 Resources.LoadAll<ItemDefinitionSO>("DataFiles/ItemData/3. GeneratedAssets/Items"))
        {
            if (candidate != null && candidate.itemId == itemId)
                return candidate;
        }

        return null;
    }
}

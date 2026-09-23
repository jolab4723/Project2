using System;
using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;

[Serializable]
internal sealed class MirrorTestShopItemSnapshot
{
    public string itemSnapshotJson;
    public ShopItemSource source;
    public int pricePaidToPlayer;
    public int gridX;
    public int gridY;
    public bool isRotated;
}

/// <summary>
/// 3-5 Mirror 테스트에서 모든 플레이어가 함께 보는 상점 재고의 서버 원본이다.
/// <para>원본 상점과의 차이: 원본 <see cref="ShopController"/>와 <see cref="ShopStockInitializer"/>는
/// 클라이언트 한 명의 Grid만 다룬다. 이 컴포넌트는 아이템 instance, 출처, 위치를 서버의
/// <see cref="SyncList{T}"/>에 보관하고 각 클라이언트 상점 화면을 같은 목록으로 다시 그린다.</para>
/// <para>구매·판매·리롤은 모두 요청 당시 상점 상태 번호를 확인한다. Mirror Command는 서버에서
/// 한 번에 하나씩 처리되므로 첫 요청이 상태 번호를 올린 뒤 도착한 요청은 돈과 아이템을 변경하기 전에
/// 거절된다. 별도의 전역 잠금 Manager는 만들지 않는다.</para>
/// <para>기존 상점의 명시적인 재고 연결 API와 추첨 설정을 재사용한다.</para>
/// <para>6-C 실제 StageSelect 복제 Scene에는 상점 Grid가 없으므로 그 Scene에서는 빈 서버 상태로 대기한다.
/// Camp 또는 전투 Scene의 상점 UI와 함께 생성된 인스턴스만 실제 공유 재고를 초기화한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class NetworkShopState_MirrorTest : NetworkBehaviour
{
    [Header("공유 리롤 규칙")]
    [SerializeField, Min(0)] private int baseFreeRerollCount = 1;
    [SerializeField, Min(0)] private int paidRerollGoldCost = 100;
    [SerializeField] private int deterministicSeed = 3505;

    private readonly SyncList<string> stockSnapshots = new();

    [SyncVar(hook = nameof(HandleStateRevisionChanged))]
    private uint stateRevision;

    [SyncVar] private int usedFreeRerollCount;
    [SyncVar] private int highestShopEnhanceLevel;
    [SyncVar] private int partyExtraRerollCount;
    [SyncVar] private uint highestBenefitPlayerNetId;
    [SyncVar] private string lastServerEvent = "상점 서버 준비 중";

    private PlayerContext localContext;
    private ShopController localShopController;
    private InventoryItemUISpawner localItemSpawner;
    private bool localViewRebuildQueued;
    private int rerollSequence;

    public uint StateRevision => stateRevision;
    public int StockCount => stockSnapshots.Count;
    public int GeneratedStockCount => CountStock(ShopItemSource.Generated);
    public int PlayerSoldStockCount => CountStock(ShopItemSource.PlayerSold);
    public int UsedFreeRerollCount => usedFreeRerollCount;
    public int TotalFreeRerollCount => Mathf.Max(0, baseFreeRerollCount + partyExtraRerollCount);
    public int RemainingFreeRerollCount => Mathf.Max(0, TotalFreeRerollCount - usedFreeRerollCount);
    public int PaidRerollGoldCost => paidRerollGoldCost;
    public int HighestShopEnhanceLevel => highestShopEnhanceLevel;
    public uint HighestBenefitPlayerNetId => highestBenefitPlayerNetId;
    public string LastServerEvent => lastServerEvent;

    public event Action StateChanged;

    private void Awake()
    {
        stockSnapshots.Callback += HandleStockChanged;
    }

    private void OnDestroy()
    {
        stockSnapshots.Callback -= HandleStockChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ServerRefreshPartyBenefits();

        if (!TryBuildGeneratedStock(new List<string>(), out List<string> initialStock))
        {
            MirrorTestNetworkManager manager =
                NetworkManager.singleton as MirrorTestNetworkManager;
            if (manager != null && manager.CurrentSessionRoute == MirrorSessionRoute.StageSelect)
            {
                // 실제 StageSelect에는 상점 Grid와 초기화 UI가 없다. 선택 화면에서는 빈 상태로 대기하고,
                // Camp/전투 Scene에 생성되는 별도 NetworkShopState가 해당 Scene의 UI 설정으로 재고를 만든다.
                lastServerEvent = "스테이지 선택 화면에서는 공유 재고 생성을 대기";
                return;
            }

            lastServerEvent = "초기 공유 재고 생성 실패";
            Debug.LogError("[NetworkShopState_MirrorTest] 초기 공유 재고를 만들지 못했습니다.", this);
            return;
        }

        foreach (string snapshot in initialStock)
            stockSnapshots.Add(snapshot);

        lastServerEvent = $"초기 공유 재고 {initialStock.Count}개 생성";
        AdvanceStateRevision();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        QueueLocalViewRebuild();
    }

    public override void OnStopClient()
    {
        UnbindLocalView(localContext);
        base.OnStopClient();
    }

    private void LateUpdate()
    {
        if (!localViewRebuildQueued)
            return;

        // 닫힌 상점 패널의 InventoryGrid는 Awake 전이라 재고를 받을 배열이 없다.
        // 패널이 실제로 열린 첫 프레임까지 요청을 유지한다.
        if (localShopController != null && !localShopController.gameObject.activeInHierarchy)
            return;

        localViewRebuildQueued = false;
        RebuildLocalShopView();
    }

    /// <summary>
    /// 로컬 Canvas를 공유 재고의 화면 투영 대상으로 연결한다.
    /// 원격 플레이어 복제본은 이 메서드를 호출하지 않으므로 별도 상점 UI를 만들지 않는다.
    /// </summary>
    public void BindLocalView(PlayerContext context, InventoryView inventoryView)
    {
        if (context == null || inventoryView == null)
            return;

        localContext = context;
        localShopController = FindInOwningScene<ShopController>();
        localItemSpawner = inventoryView.GetComponentInChildren<InventoryItemUISpawner>(true);

        ShopStockInitializer initializer =
            FindInOwningScene<ShopStockInitializer>();
        if (initializer != null)
            initializer.enabled = false;

        QueueLocalViewRebuild();
    }

    public void UnbindLocalView(PlayerContext context)
    {
        if (context != null && localContext != context)
            return;

        if (localShopController != null && localContext?.Inventory != null)
            localShopController.UnbindPlayer(localContext.Inventory);

        localContext = null;
        localShopController = null;
        localItemSpawner = null;
        localViewRebuildQueued = false;
    }

    public void ForceRebuildLocalView()
    {
        QueueLocalViewRebuild();
    }

    [Server]
    public void ServerRefreshPartyBenefits()
    {
        MirrorTestNetworkManager manager = NetworkManager.singleton as MirrorTestNetworkManager;
        int bestLevel = 0;
        int bestExtraRerolls = 0;
        uint bestNetId = 0;

        if (manager != null)
        {
            foreach (PlayerContext player in manager.ServerPlayerContexts)
            {
                NetworkShopPlayerState_MirrorTest playerState =
                    player != null ? player.GetComponent<NetworkShopPlayerState_MirrorTest>() : null;
                if (playerState == null)
                    continue;

                bool isBetter = playerState.ShopEnhanceLevel > bestLevel ||
                                (playerState.ShopEnhanceLevel == bestLevel &&
                                 playerState.ExtraRerollCount > bestExtraRerolls);
                if (!isBetter)
                    continue;

                bestLevel = playerState.ShopEnhanceLevel;
                bestExtraRerolls = playerState.ExtraRerollCount;
                NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
                bestNetId = identity != null ? identity.netId : 0;
            }
        }

        highestShopEnhanceLevel = Mathf.Max(0, bestLevel);
        partyExtraRerollCount = Mathf.Max(0, bestExtraRerolls);
        highestBenefitPlayerNetId = bestNetId;
    }

    [Server]
    internal MirrorTestShopRequestResult ServerTryBuy(
        NetworkShopPlayerState_MirrorTest requester,
        uint requestedShopRevision,
        uint requestedInventoryRevision,
        string instanceId,
        int targetX,
        int targetY,
        bool isRotated)
    {
        if (!ValidateRequester(requester) || string.IsNullOrWhiteSpace(instanceId))
            return MirrorTestShopRequestResult.InvalidRequest;
        if (requestedShopRevision != stateRevision)
            return MirrorTestShopRequestResult.ShopStateChanged;
        if (requester.InventorySync.StateRevision != requestedInventoryRevision)
            return MirrorTestShopRequestResult.InventoryStateChanged;

        int stockIndex = FindStockIndex(instanceId, out MirrorTestShopItemSnapshot stock);
        if (stockIndex < 0 || stock == null)
            return MirrorTestShopRequestResult.ItemUnavailable;

        ItemInstance itemData = PlayerInventorySync_MirrorTest.CreateItemInstance(stock.itemSnapshotJson);
        InventoryController inventory = requester.Context.Inventory;
        InventoryGrid playerGrid = inventory.PlayerGrid;
        if (itemData?.definition == null || playerGrid == null)
            return MirrorTestShopRequestResult.ItemUnavailable;

        int width = isRotated ? itemData.definition.itemHeight : itemData.definition.itemWidth;
        int height = isRotated ? itemData.definition.itemWidth : itemData.definition.itemHeight;
        InventoryItem ownedItem = FindOwnedItem(requester.Context, instanceId, out _);
        if (ownedItem != null)
            return MirrorTestShopRequestResult.InventoryStateChanged;
        if (!playerGrid.CanPlaceItem(targetX, targetY, width, height))
        {
            return MirrorTestShopRequestResult.InventoryFull;
        }

        int price = ShopPricing.GetBuyPrice(itemData.definition.sellPrice, requester.DiscountPercent);
        if (!requester.ServerTrySpendGold(price))
            return MirrorTestShopRequestResult.NotEnoughGold;

        ownedItem = new InventoryItem(itemData) { isRotated = isRotated };
        InventoryAddResultData addResult = inventory.TryAddItemAt(ownedItem, targetX, targetY);
        if (addResult.Result != InventoryAddResult.Success)
        {
            requester.ServerAddGold(price);
            return MirrorTestShopRequestResult.StateApplyFailed;
        }

        if (!requester.InventorySync.ServerCommitShopItemAdded(ownedItem, requestedInventoryRevision))
        {
            requester.ServerAddGold(price);
            inventory.TryRemoveInventoryItem(ownedItem);
            return MirrorTestShopRequestResult.StateApplyFailed;
        }

        stockSnapshots.RemoveAt(stockIndex);
        lastServerEvent = $"netId={requester.netId} 구매: {itemData.definition.itemName} / {price}골드";
        AdvanceStateRevision();
        if (NetworkManager.singleton is MirrorTestNetworkManager session)
            session.ServerReportQuestItem(requester.Context, itemData.definition.itemId);
        return MirrorTestShopRequestResult.Success;
    }

    [Server]
    internal MirrorTestShopRequestResult ServerTrySell(
        NetworkShopPlayerState_MirrorTest requester,
        uint requestedShopRevision,
        uint requestedInventoryRevision,
        string instanceId,
        int targetX,
        int targetY,
        bool isRotated)
    {
        if (!ValidateRequester(requester) || string.IsNullOrWhiteSpace(instanceId))
            return MirrorTestShopRequestResult.InvalidRequest;
        if (requestedShopRevision != stateRevision)
            return MirrorTestShopRequestResult.ShopStateChanged;
        if (requester.InventorySync.StateRevision != requestedInventoryRevision)
            return MirrorTestShopRequestResult.InventoryStateChanged;
        if (FindStockIndex(instanceId, out _) >= 0)
            return MirrorTestShopRequestResult.ItemUnavailable;
        if (!requester.InventorySync.ServerTryGetOwnedSnapshot(
                instanceId,
                requestedInventoryRevision,
                out string ownedSnapshotJson))
        {
            return MirrorTestShopRequestResult.ItemUnavailable;
        }

        ItemInstance itemData = PlayerInventorySync_MirrorTest.CreateItemInstance(ownedSnapshotJson);
        if (itemData?.definition == null)
            return MirrorTestShopRequestResult.ItemUnavailable;

        if (!CanPlaceSharedStock(itemData.definition, targetX, targetY, isRotated))
            return MirrorTestShopRequestResult.ShopFull;

        InventoryItem ownedItem = FindOwnedItem(requester.Context, instanceId, out EquipSlotType equippedSlot);
        if (ownedItem == null)
            return MirrorTestShopRequestResult.ItemUnavailable;

        InventoryPlacementSnapshot beforeSale = InventoryPlacementSnapshot.Capture(requester.Context.Inventory.PlayerGrid, ownedItem);
        if (!TryRemoveOwnedItem(requester.Context, ownedItem, equippedSlot))
            return MirrorTestShopRequestResult.StateApplyFailed;

        if (!requester.InventorySync.ServerCommitShopItemRemoved(instanceId, requestedInventoryRevision))
        {
            bool restored = equippedSlot != EquipSlotType.None
                ? new EquipmentTransaction(requester.Context.Equipment).TryRestoreEquippedItem(ownedItem, equippedSlot).IsSuccess
                : beforeSale.IsValid && requester.Context.Inventory.TryAddItemAt(ownedItem, beforeSale.Rect.X, beforeSale.Rect.Y).Result == InventoryAddResult.Success;
            if (!restored)
                Debug.LogError($"[NetworkShopState_MirrorTest] 판매 실패 후 소유권 복구 실패: {instanceId}", this);
            return MirrorTestShopRequestResult.StateApplyFailed;
        }

        int sellPrice = Mathf.Max(0, itemData.definition.sellPrice);
        MirrorTestShopItemSnapshot soldStock = new()
        {
            itemSnapshotJson = ownedSnapshotJson,
            source = ShopItemSource.PlayerSold,
            pricePaidToPlayer = sellPrice,
            gridX = targetX,
            gridY = targetY,
            isRotated = isRotated,
        };

        stockSnapshots.Add(JsonUtility.ToJson(soldStock));
        requester.ServerAddGold(sellPrice);
        lastServerEvent = $"netId={requester.netId} 판매: {itemData.definition.itemName} / {sellPrice}골드";
        AdvanceStateRevision();
        return MirrorTestShopRequestResult.Success;
    }

    [Server]
    internal MirrorTestShopRequestResult ServerTryReroll(
        NetworkShopPlayerState_MirrorTest requester,
        uint requestedShopRevision)
    {
        if (!ValidateRequester(requester))
            return MirrorTestShopRequestResult.InvalidRequest;
        if (requestedShopRevision != stateRevision)
            return MirrorTestShopRequestResult.ShopStateChanged;

        ServerRefreshPartyBenefits();
        List<string> preservedSoldStock = GetStockBySource(ShopItemSource.PlayerSold);
        if (!TryBuildGeneratedStock(preservedSoldStock, out List<string> generatedStock))
            return MirrorTestShopRequestResult.ShopFull;

        bool usesFreeReroll = usedFreeRerollCount < TotalFreeRerollCount;
        if (!usesFreeReroll)
        {
            requester.ServerSetGold(requester.Gold);
            if (!requester.ServerTrySpendGold(paidRerollGoldCost))
                return MirrorTestShopRequestResult.NotEnoughGold;
        }

        stockSnapshots.Clear();
        foreach (string snapshot in preservedSoldStock)
            stockSnapshots.Add(snapshot);
        foreach (string snapshot in generatedStock)
            stockSnapshots.Add(snapshot);

        if (usesFreeReroll)
            usedFreeRerollCount++;

        rerollSequence++;
        lastServerEvent = usesFreeReroll
            ? $"netId={requester.netId} 무료 리롤"
            : $"netId={requester.netId} 유료 리롤 / {paidRerollGoldCost}골드";
        AdvanceStateRevision();
        return MirrorTestShopRequestResult.Success;
    }

    private bool ValidateRequester(NetworkShopPlayerState_MirrorTest requester)
    {
        return requester != null &&
               requester.Context?.Inventory?.PlayerGrid != null &&
               requester.Context.Wallet != null &&
               requester.InventorySync != null;
    }

    private bool TryBuildGeneratedStock(
        IReadOnlyList<string> preservedStock,
        out List<string> generatedStock)
    {
        generatedStock = new List<string>();
        if (!TryGetShopConfiguration(
                out ItemDatabaseSO itemDatabase,
                out int stockCount,
                out ShopRarityChance[] rarityChances,
                out int gridWidth,
                out int gridHeight))
        {
            return false;
        }

        bool[,] occupied = new bool[gridWidth, gridHeight];
        if (!TryMarkOccupied(preservedStock, occupied))
            return false;

        ShopStockRollService rollService = new();
        System.Random random = new(deterministicSeed + rerollSequence * 7919 + (int)stateRevision);

        for (int i = 0; i < stockCount; i++)
        {
            if (!rollService.TryRoll(itemDatabase, rarityChances, out ItemDefinitionSO definition, random))
                return false;

            ItemInstance itemData = ItemDataCreator.CreateItemData(definition);
            if (itemData == null || !TryFindPlacement(definition, occupied, out int x, out int y, out bool rotated))
                return false;

            InventoryItem item = new(itemData)
            {
                x = x,
                y = y,
                isRotated = rotated,
            };

            MarkOccupied(occupied, x, y, item.CurrentWidth, item.CurrentHeight, true);
            MirrorTestShopItemSnapshot snapshot = new()
            {
                itemSnapshotJson = PlayerInventorySync_MirrorTest.CreateSnapshotJson(item),
                source = ShopItemSource.Generated,
                pricePaidToPlayer = 0,
                gridX = x,
                gridY = y,
                isRotated = rotated,
            };
            generatedStock.Add(JsonUtility.ToJson(snapshot));
        }

        return generatedStock.Count == stockCount;
    }

    private bool TryGetShopConfiguration(
        out ItemDatabaseSO itemDatabase,
        out int stockCount,
        out ShopRarityChance[] rarityChances,
        out int gridWidth,
        out int gridHeight)
    {
        itemDatabase = null;
        stockCount = 0;
        rarityChances = null;
        gridWidth = 0;
        gridHeight = 0;

        ShopStockInitializer initializer = FindInOwningScene<ShopStockInitializer>();
        ShopController controller = FindInOwningScene<ShopController>();

        if (initializer == null || controller?.ShopGrid == null)
        {
            return false;
        }

        itemDatabase = initializer.ItemDatabase;
        stockCount = initializer.InitialStockCount;
        rarityChances = initializer.RarityChances;
        gridWidth = controller.ShopGrid.GridWidth;
        gridHeight = controller.ShopGrid.GridHeight;
        return itemDatabase != null && stockCount > 0 && rarityChances is { Length: > 0 } &&
               gridWidth > 0 && gridHeight > 0;
    }

    /// <summary>
    /// Scene 전환 직후에는 이전 Scene 객체가 파괴 대기 중일 수 있으므로 이 네트워크 상점 상태와
    /// 같은 Scene에 배치된 UI 설정만 선택한다. 전역 Find가 이전 상점 Grid를 집는 경합을 막는다.
    /// </summary>
    private T FindInOwningScene<T>() where T : Component
    {
        T[] candidates = FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (T candidate in candidates)
        {
            if (candidate != null && candidate.gameObject.scene == gameObject.scene)
                return candidate;
        }

        return null;
    }

    private bool TryMarkOccupied(IReadOnlyList<string> snapshots, bool[,] occupied)
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            MirrorTestShopItemSnapshot stock = ParseSnapshot(snapshots[i]);
            ItemInstance itemData = stock != null
                ? PlayerInventorySync_MirrorTest.CreateItemInstance(stock.itemSnapshotJson)
                : null;
            if (itemData?.definition == null)
                return false;

            int width = stock.isRotated ? itemData.definition.itemHeight : itemData.definition.itemWidth;
            int height = stock.isRotated ? itemData.definition.itemWidth : itemData.definition.itemHeight;
            if (!IsAreaFree(occupied, stock.gridX, stock.gridY, width, height))
                return false;

            MarkOccupied(occupied, stock.gridX, stock.gridY, width, height, true);
        }

        return true;
    }

    private bool CanPlaceSharedStock(
        ItemDefinitionSO definition,
        int targetX,
        int targetY,
        bool isRotated)
    {
        if (!TryGetShopConfiguration(out _, out _, out _, out int width, out int height))
            return false;

        bool[,] occupied = new bool[width, height];
        if (!TryMarkOccupied(GetAllStock(), occupied))
            return false;

        int itemWidth = isRotated ? definition.itemHeight : definition.itemWidth;
        int itemHeight = isRotated ? definition.itemWidth : definition.itemHeight;
        return IsAreaFree(occupied, targetX, targetY, itemWidth, itemHeight);
    }

    private static bool TryFindPlacement(
        ItemDefinitionSO definition,
        bool[,] occupied,
        out int foundX,
        out int foundY,
        out bool isRotated)
    {
        if (TryFindPlacement(occupied, definition.itemWidth, definition.itemHeight, out foundX, out foundY))
        {
            isRotated = false;
            return true;
        }

        if (definition.itemWidth != definition.itemHeight &&
            TryFindPlacement(occupied, definition.itemHeight, definition.itemWidth, out foundX, out foundY))
        {
            isRotated = true;
            return true;
        }

        foundX = -1;
        foundY = -1;
        isRotated = false;
        return false;
    }

    private static bool TryFindPlacement(
        bool[,] occupied,
        int width,
        int height,
        out int foundX,
        out int foundY)
    {
        for (int y = 0; y <= occupied.GetLength(1) - height; y++)
        {
            for (int x = 0; x <= occupied.GetLength(0) - width; x++)
            {
                if (!IsAreaFree(occupied, x, y, width, height))
                    continue;

                foundX = x;
                foundY = y;
                return true;
            }
        }

        foundX = -1;
        foundY = -1;
        return false;
    }

    private static bool IsAreaFree(bool[,] occupied, int x, int y, int width, int height)
    {
        if (occupied == null || width <= 0 || height <= 0 || x < 0 || y < 0 ||
            x + width > occupied.GetLength(0) || y + height > occupied.GetLength(1))
        {
            return false;
        }

        for (int cellX = x; cellX < x + width; cellX++)
        {
            for (int cellY = y; cellY < y + height; cellY++)
            {
                if (occupied[cellX, cellY])
                    return false;
            }
        }

        return true;
    }

    private static void MarkOccupied(
        bool[,] occupied,
        int x,
        int y,
        int width,
        int height,
        bool value)
    {
        for (int cellX = x; cellX < x + width; cellX++)
        for (int cellY = y; cellY < y + height; cellY++)
            occupied[cellX, cellY] = value;
    }

    private static InventoryItem FindOwnedItem(
        PlayerContext context,
        string instanceId,
        out EquipSlotType equippedSlot)
    {
        equippedSlot = EquipSlotType.None;
        if (context == null || string.IsNullOrWhiteSpace(instanceId))
            return null;

        foreach (InventoryItem item in context.Inventory.GetAllInventoryItems())
        {
            if (item?.itemData?.instanceId == instanceId)
                return item;
        }

        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in context.Equipment.GetEquippedItems())
        {
            if (pair.Value?.itemData?.instanceId != instanceId)
                continue;

            equippedSlot = pair.Key;
            return pair.Value;
        }

        return null;
    }

    private static bool TryRemoveOwnedItem(
        PlayerContext context,
        InventoryItem item,
        EquipSlotType equippedSlot)
    {
        if (context?.Inventory == null || item == null)
            return false;

        if (context.Inventory.PlayerGrid.ContainsItem(item))
            return context.Inventory.TryRemoveInventoryItem(item) == InventoryRemoveResult.Success;

        if (equippedSlot == EquipSlotType.None || context.Equipment == null)
            return false;

        EquipmentTransactionResult result =
            new EquipmentTransaction(context.Equipment).TryUnequipForTransfer(equippedSlot, item);
        return result.IsSuccess;
    }

    private int FindStockIndex(string instanceId, out MirrorTestShopItemSnapshot snapshot)
    {
        snapshot = null;
        for (int i = 0; i < stockSnapshots.Count; i++)
        {
            MirrorTestShopItemSnapshot candidate = ParseSnapshot(stockSnapshots[i]);
            ItemInstance item = candidate != null
                ? PlayerInventorySync_MirrorTest.CreateItemInstance(candidate.itemSnapshotJson)
                : null;
            if (item?.instanceId != instanceId)
                continue;

            snapshot = candidate;
            return i;
        }

        return -1;
    }

    private List<string> GetStockBySource(ShopItemSource source)
    {
        List<string> result = new();
        for (int i = 0; i < stockSnapshots.Count; i++)
        {
            MirrorTestShopItemSnapshot snapshot = ParseSnapshot(stockSnapshots[i]);
            if (snapshot != null && snapshot.source == source)
                result.Add(stockSnapshots[i]);
        }

        return result;
    }

    private List<string> GetAllStock()
    {
        List<string> result = new(stockSnapshots.Count);
        for (int i = 0; i < stockSnapshots.Count; i++)
            result.Add(stockSnapshots[i]);
        return result;
    }

    private int CountStock(ShopItemSource source)
    {
        int count = 0;
        for (int i = 0; i < stockSnapshots.Count; i++)
        {
            MirrorTestShopItemSnapshot snapshot = ParseSnapshot(stockSnapshots[i]);
            if (snapshot != null && snapshot.source == source)
                count++;
        }
        return count;
    }

    private static MirrorTestShopItemSnapshot ParseSnapshot(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonUtility.FromJson<MirrorTestShopItemSnapshot>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void HandleStockChanged(
        SyncList<string>.Operation operation,
        int index,
        string oldItem,
        string newItem)
    {
        QueueLocalViewRebuild();
    }

    private void HandleStateRevisionChanged(uint oldRevision, uint newRevision)
    {
        QueueLocalViewRebuild();
        StateChanged?.Invoke();
    }

    private void QueueLocalViewRebuild()
    {
        if (NetworkClient.active)
            localViewRebuildQueued = true;
    }

    private void RebuildLocalShopView()
    {
        if (localContext?.Inventory?.PlayerGrid == null ||
            localShopController?.ShopGrid == null ||
            localItemSpawner == null)
        {
            return;
        }

        InventoryGrid shopGrid = localShopController.ShopGrid;
        InventoryGrid playerGrid = localContext.Inventory.PlayerGrid;

        ItemUI[] itemViews = FindObjectsByType<ItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ItemUI itemView in itemViews)
        {
            InventoryItem item = itemView?.Item;
            bool belongsToShopView = itemView != null &&
                (itemView.CurrentGrid == shopGrid ||
                 (itemView.OriginalGrid == shopGrid && !playerGrid.ContainsItem(item)));
            if (belongsToShopView)
                Destroy(itemView.gameObject);
        }

        foreach (InventoryItem item in shopGrid.GetAllItems())
            shopGrid.TryRemoveItem(item);

        ShopStockService localStock = new();
        if (!localShopController.BindStock(localContext.Inventory, localStock))
        {
            Debug.LogError("[NetworkShopState_MirrorTest] 로컬 상점에 PlayerContext를 Bind하지 못했습니다.", this);
            return;
        }

        for (int i = 0; i < stockSnapshots.Count; i++)
        {
            MirrorTestShopItemSnapshot stock = ParseSnapshot(stockSnapshots[i]);
            ItemInstance itemData = stock != null
                ? PlayerInventorySync_MirrorTest.CreateItemInstance(stock.itemSnapshotJson)
                : null;
            if (itemData?.definition == null)
                continue;

            InventoryItem item = new(itemData) { isRotated = stock.isRotated };
            bool placed = shopGrid.TryPlaceItem(item, stock.gridX, stock.gridY);
            bool registered = placed && (stock.source == ShopItemSource.Generated
                ? localStock.RegisterGeneratedItem(item)
                : localStock.RegisterPlayerSoldItem(item, stock.pricePaidToPlayer));
            if (!registered)
            {
                if (placed)
                    shopGrid.TryRemoveItem(item);
                Debug.LogError($"[NetworkShopState_MirrorTest] 공유 재고 화면 적용 실패: {itemData.definition.itemName}", this);
                continue;
            }

            ItemUI itemUI = localItemSpawner.SpawnItemUIAndGet(item, shopGrid);
            ShopItemBadgeView badge = itemUI != null ? itemUI.GetComponent<ShopItemBadgeView>() : null;
            badge?.Apply(stock.source);
        }

    }

    [Server]
    private void AdvanceStateRevision()
    {
        stateRevision++;
        if (stateRevision == 0)
            stateRevision = 1;
    }
}

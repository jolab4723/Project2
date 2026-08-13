using System;
using System.Collections.Generic;
using System.Reflection;
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
/// <para>테스트 복제본 경계: 원본 상점의 private 재고 서비스는 수정하지 않고 화면을 다시 그릴 때만
/// Reflection으로 새 재고 서비스를 주입한다. 정식 전환에서는 원본에 명시적인 Bind API를 추가한 뒤
/// 이 Reflection 연결을 제거한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class NetworkShopState_MirrorTest : NetworkBehaviour
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo StockServiceField =
        typeof(ShopController).GetField("stockService", PrivateInstance);
    private static readonly FieldInfo ItemDatabaseField =
        typeof(ShopStockInitializer).GetField("itemDatabase", PrivateInstance);
    private static readonly FieldInfo InitialStockCountField =
        typeof(ShopStockInitializer).GetField("initialStockCount", PrivateInstance);
    private static readonly FieldInfo RarityChancesField =
        typeof(ShopStockInitializer).GetField("rarityChances", PrivateInstance);

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
        localShopController = FindFirstObjectByType<ShopController>(FindObjectsInactive.Include);
        localItemSpawner = inventoryView.GetComponentInChildren<InventoryItemUISpawner>(true);

        ShopStockInitializer initializer =
            FindFirstObjectByType<ShopStockInitializer>(FindObjectsInactive.Include);
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
        bool alreadyAppliedByHost = ownedItem != null;

        if (alreadyAppliedByHost)
        {
            if (!playerGrid.ContainsItem(ownedItem) ||
                ownedItem.x != targetX || ownedItem.y != targetY || ownedItem.isRotated != isRotated)
            {
                return MirrorTestShopRequestResult.InventoryStateChanged;
            }
        }
        else if (!playerGrid.CanPlaceItem(targetX, targetY, width, height))
        {
            return MirrorTestShopRequestResult.InventoryFull;
        }

        int price = CalculateBuyPrice(itemData.definition.sellPrice, requester.DiscountPercent);
        requester.ServerSetGold(requester.Gold);
        if (!requester.ServerTrySpendGold(price))
            return MirrorTestShopRequestResult.NotEnoughGold;

        if (!alreadyAppliedByHost)
        {
            ownedItem = new InventoryItem(itemData) { isRotated = isRotated };
            InventoryAddResultData addResult = inventory.TryAddItemAt(ownedItem, targetX, targetY);
            if (addResult.Result != InventoryAddResult.Success)
            {
                requester.ServerAddGold(price);
                return MirrorTestShopRequestResult.StateApplyFailed;
            }
        }

        if (!requester.InventorySync.ServerCommitShopItemAdded(ownedItem, requestedInventoryRevision))
        {
            requester.ServerAddGold(price);
            if (!alreadyAppliedByHost)
                inventory.TryRemoveInventoryItem(ownedItem);
            return MirrorTestShopRequestResult.StateApplyFailed;
        }

        stockSnapshots.RemoveAt(stockIndex);
        lastServerEvent = $"netId={requester.netId} 구매: {itemData.definition.itemName} / {price}골드";
        AdvanceStateRevision();
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
        bool alreadyAppliedByHost = ownedItem == null;
        requester.ServerSetGold(requester.Gold);

        if (!alreadyAppliedByHost && !TryRemoveOwnedItem(requester.Context, ownedItem, equippedSlot))
            return MirrorTestShopRequestResult.StateApplyFailed;

        if (!requester.InventorySync.ServerCommitShopItemRemoved(instanceId, requestedInventoryRevision))
            return MirrorTestShopRequestResult.StateApplyFailed;

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

        ShopStockInitializer initializer =
            FindFirstObjectByType<ShopStockInitializer>(FindObjectsInactive.Include);
        ShopController controller =
            FindFirstObjectByType<ShopController>(FindObjectsInactive.Include);

        if (initializer == null || controller?.ShopGrid == null ||
            ItemDatabaseField == null || InitialStockCountField == null || RarityChancesField == null)
        {
            return false;
        }

        itemDatabase = ItemDatabaseField.GetValue(initializer) as ItemDatabaseSO;
        stockCount = InitialStockCountField.GetValue(initializer) is int count ? count : 0;
        rarityChances = RarityChancesField.GetValue(initializer) as ShopRarityChance[];
        gridWidth = controller.ShopGrid.GridWidth;
        gridHeight = controller.ShopGrid.GridHeight;
        return itemDatabase != null && stockCount > 0 && rarityChances is { Length: > 0 } &&
               gridWidth > 0 && gridHeight > 0;
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

    private static int CalculateBuyPrice(int basePrice, float discountPercent)
    {
        float discounted = Mathf.Max(0, basePrice) * (1f - Mathf.Clamp(discountPercent, 0f, 0.95f));
        return Mathf.Max(0, Mathf.CeilToInt(discounted));
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
            localItemSpawner == null ||
            StockServiceField == null)
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
        StockServiceField.SetValue(localShopController, localStock);
        if (!localShopController.BindPlayer(localContext.Inventory))
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

        PlayerInventorySync_MirrorTest inventorySync =
            localContext.GetComponent<PlayerInventorySync_MirrorTest>();
        inventorySync?.TryRestoreGridFromAuthoritativeSnapshots();
    }

    [Server]
    private void AdvanceStateRevision()
    {
        stateRevision++;
        if (stateRevision == 0)
            stateRevision = 1;
    }
}

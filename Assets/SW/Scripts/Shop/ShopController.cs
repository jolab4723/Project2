using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ShopController : MonoBehaviour
{
    public static ShopController Instance { get; private set; }
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private InventoryGrid playerGrid;
    [SerializeField] private InventoryGrid shopGrid;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private InventoryController inventoryController;
    [SerializeField] private InventoryItemUISpawner itemUISpawner;

    [Header("할인 표시")]
    [Tooltip("상점 강화 패시브로 할인이 걸려 있을 때만 켜지는 라벨(ShopSaleLabel).")]
    [SerializeField] private GameObject saleLabel;

    [Tooltip("할인율 숫자를 넣을 텍스트. 비워두면 saleLabel에서 찾는다.")]
    [SerializeField] private TextMeshProUGUI saleLabelText;
    public InventoryGrid ShopGrid => shopGrid;
    public InventoryGrid PlayerGrid => playerGrid;
    public InventoryController BoundPlayer => inventoryController;
    private ShopTradeService tradeService;
    private ShopStockService stockService;

    private void Awake()
    {
        Instance = this;
        stockService ??= new ShopStockService();

        if (inventoryController != null)
            BindPlayer(inventoryController);
        else if (playerWallet != null)
            tradeService = new ShopTradeService(playerWallet, stockService);
    }

    /// <summary>
    /// 상점을 열 때마다(ShopPopup 활성화) 할인 표시를 다시 계산한다.
    /// 캠프 사이에 패시브를 새로 해금하고 돌아올 수 있어서 여는 시점마다 확인해야 한다.
    /// </summary>
    private void OnEnable()
    {
        RefreshSaleLabel();
    }

    /// <summary>
    /// 상점 강화 패시브 할인이 걸려 있을 때만 라벨을 켜고, 실제 할인율을 문구에 넣는다.
    ///
    /// !! 퍼센트 숫자를 씬에 박아두지 않는다 - 패시브 수치(PassiveSkillDatabase의 valuesPerLevel)를
    ///    나중에 조정하면 라벨만 옛 값으로 남아 거짓말을 하게 된다. 항상 ShopPricing에서 읽어 온다.
    /// </summary>
    private void RefreshSaleLabel()
    {
        if (saleLabel == null)
            return;

        float ratio = ShopPricing.DiscountRatio;
        bool hasDiscount = ratio > 0f;

        saleLabel.SetActive(hasDiscount);

        if (!hasDiscount)
            return;

        if (saleLabelText == null)
            saleLabelText = saleLabel.GetComponent<TextMeshProUGUI>()
                            ?? saleLabel.GetComponentInChildren<TextMeshProUGUI>(true);

        if (saleLabelText != null)
            saleLabelText.text = $"- {Mathf.RoundToInt(ratio * 100f)}%";
    }

    public bool BindPlayer(InventoryController owner)
    {
        // 비활성 패널의 Awake 전에도 런타임 플레이어 UI를 연결할 수 있다.
        stockService ??= new ShopStockService();
        if (owner == null ||
            owner.PlayerWallet == null ||
            owner.PlayerGrid == null ||
            stockService == null)
        {
            return false;
        }

        inventoryController = owner;
        playerWallet = owner.PlayerWallet;
        playerGrid = owner.PlayerGrid;

        tradeService = new ShopTradeService(playerWallet, stockService);
        return true;
    }

    /// <summary>공유 상점 화면의 확정 재고를 기존 거래 서비스에 연결한다.</summary>
    internal bool BindStock(InventoryController owner, ShopStockService stock)
    {
        if (stock == null) return false;
        stockService = stock;
        return BindPlayer(owner);
    }

    /// <summary>이 instanceId가 지금 상점 재고에 올라와 있는지. 툴팁이 할인가 표시 여부를 판단할 때 쓴다.</summary>
    public bool IsInStock(string instanceId)
    {
        return stockService != null &&
               !string.IsNullOrWhiteSpace(instanceId) &&
               stockService.TryGetEntry(instanceId, out _);
    }

    /// <summary>재고 출처와 매입가가 필요한 가격 표시용 조회.</summary>
    public bool TryGetStockEntry(string instanceId, out ShopStockEntry entry)
    {
        entry = null;
        return stockService != null && stockService.TryGetEntry(instanceId, out entry);
    }

    public void UnbindPlayer(InventoryController owner)
    {
        if (inventoryController != owner)
            return;

        inventoryController = null;
        playerWallet = null;
        playerGrid = null;
        tradeService = null;
    }

    public void SetLogMessage(string message, bool isWarning = false)
    {
        if (logText != null)
            logText.text = message;
        if (isWarning)
            inventoryController?.ReportSinglePlayerMessage(ChatKind.Warning, message);
    }

    public bool TryAddGeneratedStock(InventoryItem item)
    {
        if (item?.itemData?.definition == null || shopGrid == null || stockService == null || itemUISpawner == null)
        {
            Debug.LogWarning(
                "[ShopController] 기본 판매 상품을 추가할 준비가 되지 않았습니다.");
            return false;
        }

        if (!shopGrid.FindEmptySpace(
            item.CurrentWidth,
            item.CurrentHeight,
            out int x,
            out int y))
        {
            Debug.LogWarning(
                $"[ShopController] 상점에 {item.itemData.definition.itemName}을 " +
                "배치할 공간이 없습니다.");
            return false;
        }

        if (!shopGrid.TryPlaceItem(item, x, y))
        {
            Debug.LogError(
                "[ShopController] 기본 판매 상품을 상점 Grid에 배치하지 못했습니다.");
            return false;
        }

        if (!stockService.RegisterGeneratedItem(item))
        {
            if (!shopGrid.TryRemoveItem(item))
            {
                Debug.LogError(
                    "[ShopController] 재고 등록 실패 후 상점 Grid 복구에도 실패했습니다.");
            }

            return false;
        }

        ItemUI itemUI = itemUISpawner.SpawnItemUIAndGet(item, shopGrid);

        if (itemUI != null)
        {
            RefreshItemBadge(itemUI);
            return true;
        }

        // UI 생성에 실패했으므로 재고와 그리드를 모두 이전 상태로 되돌린다.
        bool stockRemoved = stockService.RemoveStock(item.itemData.instanceId);

        bool gridRemoved = shopGrid.TryRemoveItem(item);

        if (!stockRemoved || !gridRemoved)
        {
            Debug.LogError(
                "[ShopController] 기본 판매 상품 UI 생성 실패 후 " +
                "상점 상태 복구에도 실패했습니다.");
        }

        return false;
    }

    /// <summary>
    /// Generated 재고 전체를 새 상품 묶음으로 교체한다. 새 상품을 모두 넣지 못하면 넣었던 상품을 빼고
    /// 기존 재고를 원래 위치로 되돌린다. 리롤이 실패로 끝났는데 재고 일부만 바뀌어
    /// 무료 횟수나 환불된 골드로 상품을 바꿀 수 있던 문제를 막는다.
    /// </summary>
    public bool TryReplaceGeneratedStock(IReadOnlyList<InventoryItem> newItems)
    {
        if (shopGrid == null || stockService == null || newItems == null)
        {
            Debug.LogError(
                "[ShopController] Generated 재고를 교체할 준비가 되지 않았습니다.");
            return false;
        }

        var generatedEntries =
            stockService.GetEntriesBySource(ShopItemSource.Generated);
        var oldStock = new List<(ShopStockEntry Entry, ItemUI UI, InventoryPlacementSnapshot Placement)>();

        // 변경 전에 Grid와 UI가 모두 같은 아이템을 가리키는지 확인한다.
        foreach (ShopStockEntry entry in generatedEntries)
        {
            InventoryItem item = entry?.Item;
            ItemUI itemUI = item?.itemData != null && shopGrid.ContainsItem(item)
                ? ItemUIFinder.FindInGrid(shopGrid, item)
                : null;

            if (itemUI == null)
            {
                Debug.LogError(
                    "[ShopController] Generated 재고의 Grid 또는 UI 상태가 일치하지 않습니다.");
                return false;
            }

            oldStock.Add((entry, itemUI, InventoryPlacementSnapshot.Capture(shopGrid, item)));
        }

        // 기존 UI는 성공이 확정될 때까지 남겨 두고 Grid와 재고에서만 뺀다.
        int removedCount = 0;
        foreach (var old in oldStock)
        {
            if (!shopGrid.TryRemoveItem(old.Entry.Item))
                break;

            if (!stockService.RemoveStock(old.Entry.InstanceId))
            {
                RestoreGeneratedStock(old.Entry.Item, old.Placement, registerStock: false);
                break;
            }

            removedCount++;
        }

        if (removedCount < oldStock.Count)
        {
            Debug.LogError(
                "[ShopController] 리롤 중 기존 Generated 재고를 제거하지 못해 원래 재고로 되돌립니다.");
            for (int i = 0; i < removedCount; i++)
                RestoreGeneratedStock(oldStock[i].Entry.Item, oldStock[i].Placement, registerStock: true);
            return false;
        }

        var addedItems = new List<InventoryItem>();
        foreach (InventoryItem item in newItems)
        {
            if (!TryAddGeneratedStock(item))
                break;

            addedItems.Add(item);
        }

        if (addedItems.Count == newItems.Count)
        {
            foreach (var old in oldStock)
                Destroy(old.UI.gameObject);
            return true;
        }

        // 일부만 들어갔다면 새 상품을 모두 빼고 기존 재고를 같은 위치·회전으로 복구한다.
        foreach (InventoryItem item in addedItems)
        {
            ItemUI addedUI = ItemUIFinder.FindInGrid(shopGrid, item);
            shopGrid.TryRemoveItem(item);
            stockService.RemoveStock(item.itemData.instanceId);
            if (addedUI != null)
                Destroy(addedUI.gameObject);
        }

        foreach (var old in oldStock)
            RestoreGeneratedStock(old.Entry.Item, old.Placement, registerStock: true);

        return false;
    }

    private void RestoreGeneratedStock(InventoryItem item, InventoryPlacementSnapshot placement, bool registerStock)
    {
        item.isRotated = placement.IsRotated;

        bool restored = shopGrid.TryPlaceItem(item, placement.Rect.X, placement.Rect.Y) &&
                        (!registerStock || stockService.RegisterGeneratedItem(item));
        if (!restored)
        {
            Debug.LogError(
                "[ShopController] 리롤 실패 후 기존 Generated 재고 복구에 실패했습니다.");
        }
    }

    /// <summary>
    /// 상점과 플레이어 인벤토리 사이의 드롭을 처리한다.
    /// 반환값은 거래 성공 여부가 아니라 상점이 해당 드롭을 처리했는지 여부다.
    /// </summary>
    public bool TryHandleTradeDrop(ItemUI itemUI, InventoryGrid fromGrid)
    {
        if (!isActiveAndEnabled)
            return false;

        if (itemUI == null || fromGrid == null)
            return false;

        if (fromGrid == playerGrid)
        {
            if (!IsTradingToShop(fromGrid, itemUI))
                return false;

            Vector2Int cell = itemUI.GetCellFromItemRect(shopGrid);
            bool success = TrySell(itemUI, cell.x, cell.y);

            if (!success)
                RestoreItemAfterFailedTrade(itemUI);

            return true;
        }

        if (fromGrid == shopGrid)
        {
            if (!IsTradingToPlayer(fromGrid, itemUI))
                return false;

            Vector2Int cell = itemUI.GetCellFromItemRect(playerGrid);
            bool success = TryBuy(itemUI, cell.x, cell.y);

            if (!success)
                RestoreItemAfterFailedTrade(itemUI);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 상점 아이템을 지정한 플레이어 인벤토리 위치에 구매한다.
    /// 반환값은 구매 트랜잭션의 성공 여부다.
    /// </summary>
    public bool TryBuy(ItemUI itemUI, int targetX, int targetY)
    {
        InventoryItem item = itemUI?.Item;
        InventoryPlacementSnapshot placement =
            InventoryPlacementSnapshot.FromOriginalState(
                playerGrid,
                item,
                targetX,
                targetY,
                item?.isRotated ?? false);

        return TryBuy(itemUI, placement);
    }

    /// <summary>
    /// 목표 Grid에서 사용할 회전 상태를 거래 서비스에 전달해, 원본 Grid에서 안전하게 제거한 뒤 적용한다.
    /// </summary>
    private bool TryBuy(
        ItemUI itemUI,
        InventoryPlacementSnapshot placement)
    {
        InventoryItem item = itemUI?.Item;

        TradeResult result = tradeService.TryBuy(
            item,
            shopGrid,
            playerGrid,
            placement);

        if (result == TradeResult.Success)
        {
            itemUI.SetGridPosition(
                playerGrid,
                placement.Rect.X,
                placement.Rect.Y);
            RefreshItemBadge(itemUI);
            inventoryController?.NotifyItemOwnershipGained(item);
        }

        ShowTradeMessage(result, item, true);

        return result == TradeResult.Success;
    }

    /// <summary>
    /// 플레이어 아이템을 상점에 판매하고, 요청 위치가 차 있으면 빈 공간을 찾는다.
    /// 반환값은 판매 트랜잭션의 성공 여부다.
    /// </summary>
    public bool TrySell(ItemUI itemUI, int requestedX, int requestedY)
    {
        InventoryItem item = itemUI?.Item;

        if (item?.itemData?.definition == null)
        {
            ShowTradeMessage(
                TradeResult.InvalidItem,
                item,
                false);

            return false;
        }

        if (!TryResolveSellPosition(
                item,
                requestedX,
                requestedY,
                out InventoryPlacementSnapshot placement))
        {
            ShowTradeMessage(
                TradeResult.NoSpace,
                item,
                false);

            return false;
        }

        TradeResult result = tradeService.TrySell(
            item,
            playerGrid,
            shopGrid,
            placement);

        if (result == TradeResult.Success)
        {
            itemUI.SetGridPosition(
                shopGrid,
                placement.Rect.X,
                placement.Rect.Y);
            RefreshItemBadge(itemUI);
            inventoryController?.NotifyItemOwnershipLost(item);
        }

        ShowTradeMessage(result, item, false);

        return result == TradeResult.Success;
    }

    /// <summary>
    /// 상점 아이템의 우클릭 구매를 처리한다.
    /// 반환값은 구매 성공 여부가 아니라 상점이 입력을 처리했는지 여부다.
    /// </summary>
    public bool TryHandleRightClick(ItemUI itemUI)
    {
        if (!isActiveAndEnabled)
            return false;
        if (itemUI == null || itemUI.CurrentGrid != shopGrid)
            return false;

        if (!playerGrid.TryFindEmptySpaceForItem(
                itemUI.Item,
                itemUI.Item.isRotated,
                out InventoryPlacementSnapshot placement))
        {
            ShowTradeMessage(
                TradeResult.NoSpace,
                itemUI.Item,
                true);

            return true;
        }

        TryBuy(itemUI, placement);
        return true;
    }

    public bool IsTradingToShop(InventoryGrid fromGrid, ItemUI itemUI) =>
        IsTradingBetween(fromGrid, playerGrid, itemUI, shopGrid);

    public bool IsTradingToPlayer(InventoryGrid fromGrid, ItemUI itemUI) =>
        IsTradingBetween(fromGrid, shopGrid, itemUI, playerGrid);

    private bool IsTradingBetween(
        InventoryGrid fromGrid,
        InventoryGrid expectedSource,
        ItemUI itemUI,
        InventoryGrid targetGrid)
        => isActiveAndEnabled &&
           fromGrid == expectedSource &&
           IsItemOverGrid(itemUI, targetGrid);

    private bool IsItemOverGrid(ItemUI itemUI, InventoryGrid grid)
    {
        Vector2Int cell = itemUI.GetCellFromItemRect(grid);
        InventoryItem item = itemUI.Item;

        return cell.x + item.CurrentWidth > 0 &&
               cell.y + item.CurrentHeight > 0 &&
               cell.x < grid.GridWidth &&
               cell.y < grid.GridHeight;
    }

    private void ShowTradeMessage(
        TradeResult result,
        InventoryItem item,
        bool isBuying)
    {
        string itemName =
            item?.itemData?.definition != null
                ? item.itemData.definition.itemName
                : "아이템";

        string message = ShopMessageMapper.GetMessage(
            result,
            itemName,
            isBuying);
        if (logText != null) logText.text = message;
        if (result != TradeResult.Success)
            inventoryController?.ReportSinglePlayerMessage(ChatKind.Warning, message);
    }

    /// <summary>
    /// 요청 위치를 우선하고, 사용할 수 없으면 현재 방향과 반대 방향 순서로 상점의 빈자리를 찾는다.
    /// </summary>
    public bool TryResolveSellPosition(
        InventoryItem item,
        int requestedX,
        int requestedY,
        out InventoryPlacementSnapshot placement)
    {
        placement = default;

        if (item?.itemData?.definition == null || shopGrid == null)
            return false;

        // 드롭한 위치가 비어 있으면 해당 위치를 그대로 사용한다.
        if (shopGrid.CanPlaceItem(
                requestedX,
                requestedY,
                item.CurrentWidth,
                item.CurrentHeight))
        {
            placement = InventoryPlacementSnapshot.FromOriginalState(
                shopGrid,
                item,
                requestedX,
                requestedY,
                item.isRotated);

            return placement.IsValid;
        }

        // 현재 방향의 다른 빈자리를 우선하고, 필요한 경우에만 반대 방향까지 확인한다.
        return shopGrid.TryFindEmptySpaceForItem(
            item,
            item.isRotated,
            out placement);
    }
    private void RefreshItemBadge(ItemUI itemUI)
    {
        if (itemUI == null)
            return;

        ShopItemBadgeView badgeView = itemUI.GetComponent<ShopItemBadgeView>();

        if (badgeView == null)
            return;

        string instanceId = itemUI.Item?.itemData?.instanceId;

        if (itemUI.CurrentGrid != shopGrid || stockService == null ||
            !stockService.TryGetEntry(instanceId, out ShopStockEntry entry))
        {
            badgeView.Hide();
            return;
        }

        badgeView.Apply(entry.Source);
    }
    private void RestoreItemAfterFailedTrade(ItemUI itemUI)
    {
        if (itemUI != null &&
            itemUI.TryReturnToOriginalPosition())
        {
            return;
        }

        Debug.LogError(
            "[ShopController] 거래 실패 후 아이템을 원래 위치로 복구하지 못했습니다.");
    }
}

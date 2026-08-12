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
    public InventoryGrid ShopGrid => shopGrid;
    public InventoryGrid PlayerGrid => playerGrid;
    private ShopTradeService tradeService;
    private ShopStockService stockService;

    private void Awake()
    {
        Instance = this;
        stockService = new ShopStockService();

        if (inventoryController != null)
            BindPlayer(inventoryController);
        else if (playerWallet != null)
            tradeService = new ShopTradeService(playerWallet, stockService);
    }

    public bool BindPlayer(InventoryController owner)
    {
        if (owner == null || stockService == null)
            return false;

        inventoryController = owner;
        playerWallet = owner.PlayerWallet;
        playerGrid = owner.PlayerGrid;

        if (playerWallet == null || playerGrid == null)
            return false;

        tradeService = new ShopTradeService(playerWallet, stockService);
        return true;
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

    public bool TryClearGeneratedStock()
    {
        if (shopGrid == null || stockService == null)
        {
            Debug.LogError(
                "[ShopController] Generated 재고를 제거할 준비가 되지 않았습니다.");
            return false;
        }

        var generatedEntries =
            stockService.GetEntriesBySource(ShopItemSource.Generated);

        // 변경 전에 Grid와 UI가 모두 같은 아이템을 가리키는지 확인한다.
        foreach (ShopStockEntry entry in generatedEntries)
        {
            InventoryItem item = entry?.Item;

            if (item?.itemData == null ||
                !shopGrid.ContainsItem(item) ||
                ItemUIFinder.FindInGrid(shopGrid, item) == null)
            {
                Debug.LogError(
                    "[ShopController] Generated 재고의 Grid 또는 UI 상태가 일치하지 않습니다.");
                return false;
            }
        }

        foreach (ShopStockEntry entry in generatedEntries)
        {
            InventoryItem item = entry.Item;
            ItemUI itemUI = ItemUIFinder.FindInGrid(shopGrid, item);
            InventoryPlacementSnapshot placement =
                InventoryPlacementSnapshot.Capture(shopGrid, item);

            if (!shopGrid.TryRemoveItem(item))
            {
                Debug.LogError(
                    "[ShopController] 리롤 중 Generated 아이템을 Grid에서 제거하지 못했습니다.");
                return false;
            }

            if (!stockService.RemoveStock(entry.InstanceId))
            {
                item.isRotated = placement.IsRotated;

                if (!shopGrid.TryPlaceItem(
                        item,
                        placement.Rect.X,
                        placement.Rect.Y))
                {
                    Debug.LogError(
                        "[ShopController] 재고 제거 실패 후 Grid 복구에도 실패했습니다.");
                }

                return false;
            }

            itemUI.gameObject.SetActive(false);
            Destroy(itemUI.gameObject);
        }

        return true;
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
        if (logText == null)
            return;

        string itemName =
            item?.itemData?.definition != null
                ? item.itemData.definition.itemName
                : "아이템";

        logText.text = ShopMessageMapper.GetMessage(
            result,
            itemName,
            isBuying);
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

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
        tradeService = new ShopTradeService(playerWallet, stockService);
    }

    public bool TryAddGeneratedStock(InventoryItem item)
    {
        if (item?.itemData?.definition == null || shopGrid == null || stockService == null || itemUISpawner == null)
        {
            Debug.LogWarning(
                "[ShopController] 기본 판매 상품을 추가할 준비가 되지 않았습니다.");
            return false;
        }

        //if (!TryFindRandomShopSpace(
        //        item.CurrentWidth,
        //        item.CurrentHeight,
        //        out int x,
        //        out int y))
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
            return true;

        // UI 생성에 실패했으므로 재고와 그리드를 모두 이전 상태로 되돌린다.
        bool stockRemoved = stockService.RemoveStock(item.itemData.instanceId);

        bool gridRemoved =shopGrid.TryRemoveItem(item);

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

    public bool TradeItem(ItemUI itemUI, InventoryGrid fromGrid)
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
            bool success = RequestSell(itemUI, cell.x, cell.y);

            if (!success)
                RestoreItemAfterFailedTrade(itemUI);

            return true;
        }

        if (fromGrid == shopGrid)
        {
            if (!IsTradingToPlayer(fromGrid, itemUI))
                return false;
            Vector2Int cell = itemUI.GetCellFromItemRect(playerGrid);
            bool success = RequestBuy(itemUI, cell.x, cell.y);

            if (!success)
                RestoreItemAfterFailedTrade(itemUI);
            return true;
        }

        return false;
    }
    public Vector2Int GetShopCell(ItemUI itemUI)
    {
        return itemUI.GetCellFromItemRect(shopGrid);
    }

    public Vector2Int GetPlayerCell(ItemUI itemUI)
    {
        return itemUI.GetCellFromItemRect(playerGrid);

    }
 
    public bool RequestBuy(ItemUI itemUI, int targetX, int targetY)
    {
        // 팝업 추후 추가 예정

        return ConfirmBuy(itemUI, targetX, targetY);
    }

    public bool ConfirmBuy(ItemUI itemUI, int targetX, int targetY)
    {
        InventoryItem item = itemUI?.Item;

        TradeResult result = tradeService.TryBuy(
            item,
            shopGrid,
            playerGrid,
            targetX,
            targetY);

        if (result == TradeResult.Success)
        {
            itemUI.SetGridPosition(playerGrid, targetX, targetY);
            inventoryController?.NotifyItemOwnershipGained(item);
        }

        ShowTradeMessage(result, item, true);

        return result == TradeResult.Success;
    }

    public void CancelBuy(ItemUI itemUI)
    {

    }
    public bool RequestSell(
                ItemUI itemUI,
                int requestedX,
                int requestedY)
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
                out int resolvedX,
                out int resolvedY))
        {
            ShowTradeMessage(
                TradeResult.NoSpace,
                item,
                false);

            return false;
        }

        return ConfirmSell(
            itemUI,
            resolvedX,
            resolvedY);
    }

    public bool ConfirmSell(ItemUI itemUI, int targetX, int targetY)
    {
        InventoryItem item = itemUI?.Item;

        TradeResult result = tradeService.TrySell(
            item,
            playerGrid,
            shopGrid,
            targetX,
            targetY);

        if (result == TradeResult.Success)
        {
            itemUI.SetGridPosition(shopGrid, targetX, targetY);
            inventoryController?.NotifyItemOwnershipLost(item);
        }

        ShowTradeMessage(result, item, false);

        return result == TradeResult.Success;
    }
    public void CancelSell(ItemUI itemUI)
    {

    }

    public bool TryRightClick(ItemUI itemUI)
    {
        if (!isActiveAndEnabled)
            return false;
        if (itemUI == null || itemUI.CurrentGrid != shopGrid)
            return false;

        if (!playerGrid.FindEmptySpace(
        itemUI.Item.CurrentWidth,
        itemUI.Item.CurrentHeight,
        out int x,
        out int y))
        {
            ShowTradeMessage(
                TradeResult.NoSpace,
                itemUI.Item,
                true);

            return true;
        }

        RequestBuy(itemUI, x, y);
        return true;   
    }

    public bool IsTradingToShop(InventoryGrid fromGrid, ItemUI itemUI)
    {
        if (!isActiveAndEnabled)
            return false;
        if (fromGrid != playerGrid)
            return false;


        return IsItemOverGrid(itemUI, shopGrid);
    }

    public bool IsTradingToPlayer(InventoryGrid fromGrid, ItemUI itemUI)
    {
        if (!isActiveAndEnabled)
            return false;
        if (fromGrid != shopGrid)
            return false;

        return IsItemOverGrid(itemUI, playerGrid);

    }

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

    public bool TryResolveSellPosition(
    InventoryItem item,
    int requestedX,
    int requestedY,
    out int resolvedX,
    out int resolvedY)
    {
        resolvedX = requestedX;
        resolvedY = requestedY;

        if (item?.itemData?.definition == null || shopGrid == null)
            return false;

        // 드롭한 위치가 비어 있으면 해당 위치를 그대로 사용한다.
        if (shopGrid.CanPlaceItem(
                requestedX,
                requestedY,
                item.CurrentWidth,
                item.CurrentHeight))
        {
            return true;
        }

        // 드롭한 위치가 차 있으면 상점의 다른 빈자리를 찾는다.
        return shopGrid.FindEmptySpace(
            item.CurrentWidth,
            item.CurrentHeight,
            out resolvedX,
            out resolvedY);
    }
    private bool TryFindRandomShopSpace(
    int width,
    int height,
    out int foundX,
    out int foundY)
    {
        var candidates = new List<Vector2Int>();

        for (int y = 0; y <= shopGrid.GridHeight - height; y++)
        {
            for (int x = 0; x <= shopGrid.GridWidth - width; x++)
            {
                if (shopGrid.CanPlaceItem(x, y, width, height))
                    candidates.Add(new Vector2Int(x, y));
            }
        }

        if (candidates.Count == 0)
        {
            foundX = -1;
            foundY = -1;
            return false;
        }

        Vector2Int selected =
            candidates[Random.Range(0, candidates.Count)];

        foundX = selected.x;
        foundY = selected.y;
        return true;
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

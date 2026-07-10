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
    public InventoryGrid ShopGrid => shopGrid;
    public InventoryGrid PlayerGrid => playerGrid;
    private ShopTradeService tradeService;

    private void Awake()
    {
        Instance = this;
        tradeService = new ShopTradeService(playerWallet);
    }

    public bool TradeItem(ItemUI itemUI, InventoryGrid fromGrid)
    {
        if (fromGrid == playerGrid)
        {
            if (!IsTradingToShop(fromGrid, itemUI))
                return false;
            Vector2Int cell = itemUI.GetCellFromItemRect(shopGrid);
            bool success = RequestSell(itemUI, cell.x, cell.y);

            if (!success)
                itemUI.ReturnToOriginalPosition();

            return true;
        }

        if (fromGrid == shopGrid)
        {
            if (!IsTradingToPlayer(fromGrid, itemUI))
                return false;
            Vector2Int cell = itemUI.GetCellFromItemRect(playerGrid);
            bool success = RequestBuy(itemUI, cell.x, cell.y);

            if (!success)
                itemUI.ReturnToOriginalPosition();
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
        InventoryItem item = itemUI.Item;

        TradeResult result = tradeService.TryBuy(
            item,
            shopGrid,
            playerGrid,
            targetX,
            targetY
        );

        if (result != TradeResult.Success)
        {
            logText.text = GetTradeMessage(result, item);
            return false;
        }

        itemUI.SetGridPosition(playerGrid, targetX, targetY);
        logText.text = $"{item.itemData.definition.itemName}을 구매했습니다.";
        return true;
    }

    public void CancelBuy(ItemUI itemUI)
    {

    }
    public bool RequestSell (ItemUI itemUI, int targetX, int targetY)
    {
        return ConfirmSell(itemUI, targetX, targetY);
    }

    public bool ConfirmSell(ItemUI itemUI, int targetX, int targetY)
    {
        InventoryItem item = itemUI.Item;

        TradeResult result = tradeService.TrySell(
            item,
            playerGrid,
            shopGrid,
            targetX,
            targetY
        );

        if (result != TradeResult.Success)
        {
            logText.text = GetTradeMessage(result, item);
            return false;
        }

        itemUI.SetGridPosition(shopGrid, targetX, targetY);
        logText.text = $"{item.itemData.definition.itemName}을 판매했습니다.";
        return true;
    }

    public void CancelSell(ItemUI itemUI)
    {

    }

    public bool TryRightClick(ItemUI itemUI)
    {
        if (itemUI.CurrentGrid != shopGrid)
            return false;

        if (!playerGrid.FindEmptySpace(itemUI.Item.CurrentWidth, itemUI.Item.CurrentHeight, out int x, out int y))
        {
            logText.text = "인벤토리에 공간이 없습니다.";
            return true;
        }

        RequestBuy(itemUI, x, y);
        return true;   
    }

    public bool IsTradingToShop(InventoryGrid fromGrid, ItemUI itemUI)
    {
        if (fromGrid != playerGrid)
            return false;


        return IsItemOverGrid(itemUI, shopGrid);
    }

    public bool IsTradingToPlayer(InventoryGrid fromGrid, ItemUI itemUI)
    {
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

    private string GetTradeMessage(TradeResult result, InventoryItem item)
    {
        string itemName = item != null ? item.itemData.definition.itemName : "아이템";

        switch (result)
        {
            case TradeResult.NoSpace:
                return "인벤토리에 공간이 없습니다.";

            case TradeResult.NotEnoughGold:
                return $"골드가 부족해 {itemName} 구매에 실패했습니다.";

            case TradeResult.InvalidItem:
                return "유효하지 않은 아이템입니다.";

            default:
                return "거래에 실패했습니다.";
        }
    }
}

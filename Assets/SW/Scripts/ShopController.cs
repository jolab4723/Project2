using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopController : MonoBehaviour
{
    public static ShopController Instance { get; private set; }
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private InventoryGrid playerGrid;
    [SerializeField] private InventoryGrid shopGrid;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private InventoryController inventoryController;
    [SerializeField] private RectTransform highlightRect;
    [SerializeField] private Image highlightImage;

    public InventoryGrid ShopGrid => shopGrid;
    public InventoryGrid PlayerGrid => playerGrid;



    private void Awake()
    {
        Instance = this;
    }


    public bool TradeItem(ItemUI itemUI, InventoryGrid fromGrid)
    {
        if (fromGrid == playerGrid)
        {
            if (!IsTradingToShop(fromGrid, itemUI))
                return false;
            Vector2Int cell = itemUI.GetCellFromItemRect(shopGrid);
            RequestSell(itemUI, cell.x, cell.y);
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
        
        if (!playerGrid.CanPlaceItem(targetX,targetY,item.CurrentWidth, item.CurrentHeight))
        {
            return false;
        }

        if (!SpendGold(itemUI, item.itemData.definition.sellPrice, item.itemData.definition.itemName))
        {
            return false;
        }

        shopGrid.RemoveItem(itemUI.Item);
        playerGrid.PlaceItem(item, targetX, targetY);
        itemUI.SetGridPosition(playerGrid, targetX, targetY);

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

        if (!shopGrid.CanPlaceItem(targetX, targetY, item.CurrentWidth, item.CurrentHeight))
        {
            itemUI.ReturnToOriginalPosition();
            return false;
        }

        AddGold(item.itemData.definition.sellPrice, item.itemData.definition.itemName);

        shopGrid.PlaceItem(item, targetX, targetY);
        itemUI.SetGridPosition(shopGrid, targetX, targetY);

        return true;
    }

    public void CancelSell(ItemUI itemUI)
    {

    }
    private void AddGold (int amount, string name)
    {
        playerWallet.AddGold(amount);
        logText.text = $"{name}을 판매했습니다.";
        
    }

    private bool SpendGold(ItemUI itemUI, int amount, string name)
    {
        if (!playerWallet.TrySpendGold(amount))
        {
            logText.text = $"골드가 부족해 {name} 구매에 실패했습니다.";
            return false;
        }

        logText.text = $"{name}을 구매했습니다.";
        return true;
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

    public void ShowHighlight(int width, int height, float cellSize, float spacing)
    {
        highlightRect.gameObject.SetActive(true);

        float w = (width * cellSize) + ((width - 1) * spacing);
        float h = (height * cellSize) + ((height - 1) * spacing);
        highlightRect.sizeDelta = new Vector2(w, h);

        highlightRect.SetAsFirstSibling();
    }

    public void MoveHighlight(int gridX, int gridY, bool isValid, float cellSize, float spacing)
    {
        float step = cellSize + spacing;

        highlightRect.anchoredPosition = new Vector2(gridX * step, -gridY * step);
        highlightImage.color = isValid ? new Color(0, 1, 0, 0.6f) : new Color(1, 0, 0, 0.6f);
    }

    public void HideHighlight()
    {
        highlightRect.gameObject.SetActive(false);
    }

    public void SetHighlightActive(bool isActive)
    {
        if (highlightRect.gameObject.activeSelf != isActive)
        {
            highlightRect.gameObject.SetActive(isActive);
        }
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
}

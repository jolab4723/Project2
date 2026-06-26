using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopController : MonoBehaviour
{
    public static ShopController Instance { get; private set; }

    [SerializeField] private InventoryGrid playerGrid;
    [SerializeField] private InventoryGrid shopGrid;
    [SerializeField] private PlayerData playerData;
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
            RequestBuy(itemUI, cell.x, cell.y);
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
        
        if (!(playerData.gold >= item.itemData.buyPrice))
        {
            itemUI.ReturnToOriginalPosition();
            return false;
        }

        if (!playerGrid.CanPlaceItem(targetX,targetY,item.CurrentWidth, item.CurrentHeight))
        {
            itemUI.ReturnToOriginalPosition();
            return false;
        }

        SpendGold(item.itemData.buyPrice, item.itemData.itemName);

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

        AddGold(item.itemData.sellPrice, item.itemData.itemName);

        shopGrid.PlaceItem(item, targetX, targetY);
        itemUI.SetGridPosition(shopGrid, targetX, targetY);

        return true;
    }

    public void CancelSell(ItemUI itemUI)
    {

    }
    private void AddGold (int amount, string name)
    {
        playerData.gold += amount;
        logText.text = $"{name}을 판매했습니다.";
        InventoryController.Instance.RefreshGoldText();
        
    }

    private void SpendGold(int amount, string name)
    {
        playerData.gold -= amount;
        logText.text = $"{name}을 구매했습니다.";
        InventoryController.Instance.RefreshGoldText();
    }

    public bool TryRightClick(ItemUI itemUI)
    {
        if (itemUI.CurrentGrid == shopGrid)
        {
            if (!playerGrid.FindEmptySpace(itemUI.Item.CurrentWidth, itemUI.Item.CurrentHeight, out int x, out int y))
            {
                itemUI.ReturnToOriginalPosition();
                return false;
            }

            shopGrid.RemoveItem(itemUI.Item);
            return RequestBuy(itemUI, x, y);
        }

        return false;
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

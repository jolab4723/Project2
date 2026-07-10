public class ShopTradeService
{
    private readonly PlayerWallet playerWallet;

    public ShopTradeService(PlayerWallet playerWallet)
    {
        this.playerWallet = playerWallet;
    }

    public TradeResult TryBuy(
        InventoryItem item,
        InventoryGrid shopGrid,
        InventoryGrid playerGrid,
        int targetX,
        int targetY)
    {
        if (item == null)
            return TradeResult.InvalidItem;

        if (!playerGrid.CanPlaceItem(targetX, targetY, item.CurrentWidth, item.CurrentHeight))
            return TradeResult.NoSpace;

        int price = item.itemData.definition.sellPrice; // 나중에 buyPrice로 교체 가능

        if (!playerWallet.TrySpendGold(price))
            return TradeResult.NotEnoughGold;

        shopGrid.RemoveItem(item);
        playerGrid.PlaceItem(item, targetX, targetY);

        return TradeResult.Success;
    }

    public TradeResult TrySell(
        InventoryItem item,
        InventoryGrid playerGrid,
        InventoryGrid shopGrid,
        int targetX,
        int targetY)
    {
        if (item == null)
            return TradeResult.InvalidItem;

        if (!shopGrid.CanPlaceItem(targetX, targetY, item.CurrentWidth, item.CurrentHeight))
            return TradeResult.NoSpace;

        int price = item.itemData.definition.sellPrice;

        playerWallet.AddGold(price);

        playerGrid.RemoveItem(item);
        shopGrid.PlaceItem(item, targetX, targetY);

        return TradeResult.Success;
    }
}
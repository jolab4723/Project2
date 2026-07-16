public sealed class ShopStockEntry
{
    public InventoryItem Item { get; }
    public ShopItemSource Source { get; }
    public int PricePaidToPlayer { get; }

    public string InstanceId => Item?.itemData?.instanceId;

    public ShopStockEntry(
        InventoryItem item,
        ShopItemSource source,
        int pricePaidToPlayer = 0)
    {
        Item = item;
        Source = source;
        PricePaidToPlayer = pricePaidToPlayer;
    }
}
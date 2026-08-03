using System.Collections.Generic;
using UnityEngine;

internal sealed class ShopStockService
{
    private readonly Dictionary<string, ShopStockEntry> entriesByInstanceId = new();

    public bool RegisterGeneratedItem(InventoryItem item)
    {
        return TryRegister(item, ShopItemSource.Generated, 0);
    }

    public bool RegisterPlayerSoldItem(InventoryItem item, int pricePaidToPlayer)
    {
        if (pricePaidToPlayer < 0)
            return false;

        return TryRegister(item, ShopItemSource.PlayerSold, pricePaidToPlayer);
    }

    public bool TryGetEntry(string instanceId, out ShopStockEntry entry)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            entry = null;
            return false;
        }

        return entriesByInstanceId.TryGetValue(instanceId, out entry);
    }

    public bool RemoveStock(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        return entriesByInstanceId.Remove(instanceId);
    }

    public List<ShopStockEntry> GetEntriesBySource(ShopItemSource source)
    {
        var result = new List<ShopStockEntry>();

        foreach (ShopStockEntry entry in entriesByInstanceId.Values)
        {
            if (entry != null && entry.Source == source)
                result.Add(entry);
        }

        return result;
    }

    private bool TryRegister(InventoryItem item, ShopItemSource source, int pricePaidToPlayer)
    {
        string instanceId = item?.itemData?.instanceId;

        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        var entry = new ShopStockEntry(item, source, pricePaidToPlayer);

        return entriesByInstanceId.TryAdd(instanceId, entry);
    }
}

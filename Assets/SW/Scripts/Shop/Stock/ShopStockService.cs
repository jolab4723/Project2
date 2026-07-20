using System.Collections.Generic;
using UnityEngine;

public class ShopStockService
{
    private readonly Dictionary<string, ShopStockEntry> entriesByInstanceId = new();

    public int Count => entriesByInstanceId.Count;
    public IEnumerable<ShopStockEntry> Entries =>  entriesByInstanceId.Values;

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

    public bool TryGetEntry(string instanceID, out ShopStockEntry entry)
    {
        if (string.IsNullOrWhiteSpace(instanceID))
        {
            entry = null;
            return false;
        }

        return entriesByInstanceId.TryGetValue(instanceID, out entry);
    }

    public bool RemoveStock(string instanceID)
    {
        if (string.IsNullOrWhiteSpace(instanceID))
            return false;

        return entriesByInstanceId.Remove(instanceID);
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

    public void Restock()
    {
        
    }

    public void Clear()
    {
        entriesByInstanceId.Clear();
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

public struct InventoryAddResultData
{
    public InventoryAddResult Result;
    public InventoryItem Item;
    public int X;
    public int Y;

    public static InventoryAddResultData Success(InventoryItem item, int x, int y)
    {
        return new InventoryAddResultData
        {
            Result = InventoryAddResult.Success,
            Item = item,
            X = x,
            Y = y
        };
    }

    public static InventoryAddResultData Failed(InventoryAddResult result)
    {
        return new InventoryAddResultData
        {
            Result = result
        };
    }
}
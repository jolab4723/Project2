public struct InventoryAddResultData
{
    public InventoryAddResult Result;
    public int X;
    public int Y;

    public static InventoryAddResultData Success(int x, int y)
    {
        return new InventoryAddResultData
        {
            Result = InventoryAddResult.Success,
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

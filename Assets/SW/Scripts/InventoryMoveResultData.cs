public struct InventoryMoveResultData
{
    public InventoryMoveResult Result;

    public InventoryItem MovedItem;
    public int MovedX;
    public int MovedY;

    public InventoryItem SwappedItem;
    public int SwappedX;
    public int SwappedY;

    public static InventoryMoveResultData Success(
        InventoryItem movedItem,
        int movedX,
        int movedY)
    {
        return new InventoryMoveResultData
        {
            Result = InventoryMoveResult.Success,
            MovedItem = movedItem,
            MovedX = movedX,
            MovedY = movedY
        };
    }

    public static InventoryMoveResultData Failed()
    {
        return new InventoryMoveResultData
        {
            Result = InventoryMoveResult.Failed
        };
    }

    public static InventoryMoveResultData Swapped(
    InventoryItem movedItem,
    int movedX,
    int movedY,
    InventoryItem swappedItem,
    int swappedX,
    int swappedY)
    {
        return new InventoryMoveResultData
        {
            Result = InventoryMoveResult.Swapped,
            MovedItem = movedItem,
            MovedX = movedX,
            MovedY = movedY,
            SwappedItem = swappedItem,
            SwappedX = swappedX,
            SwappedY = swappedY
        };
    }

    public static InventoryMoveResultData ReturnedToOriginal(
    InventoryItem movedItem,
    int movedX,
    int movedY)
    {
        return new InventoryMoveResultData
        {
            Result = InventoryMoveResult.ReturnedToOriginal,
            MovedItem = movedItem,
            MovedX = movedX,
            MovedY = movedY
        };
    }

    public static InventoryMoveResultData MovedToEmptySpace(
        InventoryItem movedItem,
        int movedX,
        int movedY)
    {
        return new InventoryMoveResultData
        {
            Result = InventoryMoveResult.MovedToEmptySpace,
            MovedItem = movedItem,
            MovedX = movedX,
            MovedY = movedY
        };
    }
}
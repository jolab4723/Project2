public enum InventoryMoveResult : byte
{
    Success = 0,
    Swapped = 1,
    ReturnedToOriginal = 2,
    MovedToEmptySpace = 3,
    Failed = 4
}

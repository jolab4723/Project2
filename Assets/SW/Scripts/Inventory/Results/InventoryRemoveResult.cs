public enum InventoryDiscardResult : byte
{
    Success = 0,
    InvalidItem = 1,
    InventoryUnavailable = 2,
    NotPlayerInventory = 3,
    EquippedItemNotAllowed = 4,
    RemoveFailed = 5
}
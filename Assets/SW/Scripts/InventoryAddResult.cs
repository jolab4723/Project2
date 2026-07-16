public enum InventoryAddResult : byte
{
    Success = 0,
    InvalidItem = 1,
    NoSpace = 2,
    GridUnavailable = 3,
    PlacementFailed = 4
}
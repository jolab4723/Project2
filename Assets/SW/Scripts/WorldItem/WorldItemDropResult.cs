public enum WorldItemDropResult : byte
{
    Success = 0,
    InvalidItem = 1,
    DropOriginUnavailable = 2,
    PickupPrefabUnavailable = 3,
    SpawnFailed = 4,
    NoAvailablePosition = 5
}
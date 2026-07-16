public static class InventoryMessageMapper
{
    public static string GetMessage(
        InventoryAddResult result,
        string itemName,
        int x,
        int y)
    {
        switch (result)
        {
            case InventoryAddResult.Success:
                return $"{itemName} 아이템을 획득했습니다. 위치 : {x}, {y}";

            case InventoryAddResult.InvalidItem:
                return "유효하지 않은 아이템입니다.";

            case InventoryAddResult.NoSpace:
                return "인벤토리가 꽉 찼습니다!";

            case InventoryAddResult.GridUnavailable:
                return "인벤토리를 사용할 수 없습니다.";

            case InventoryAddResult.PlacementFailed:
                return "인벤토리에 아이템을 배치하지 못했습니다.";

            default:
                return "아이템 획득에 실패했습니다.";
        }
    }
}
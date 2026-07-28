public static class InventoryDiscardMessageMapper
{
    public static string GetMessage(InventoryDiscardResult result, string itemName)
    {
        switch (result)
        {
            case InventoryDiscardResult.Success:
                return $"{itemName}을(를) 삭제했습니다.";

            case InventoryDiscardResult.InvalidItem:
                return "유효하지 않은 아이템입니다.";

            case InventoryDiscardResult.InventoryUnavailable:
                return "인벤토리를 사용할 수 없습니다.";

            case InventoryDiscardResult.NotPlayerInventory:
                return "내 인벤토리의 아이템만 삭제할 수 있습니다.";

            case InventoryDiscardResult.EquippedItemNotAllowed:
                return "장착 중인 아이템은 바로 삭제할 수 없습니다.";

            case InventoryDiscardResult.RemoveFailed:
                return "아이템 삭제에 실패했습니다.";

            default:
                return "아이템을 삭제하지 못했습니다.";
        }
    }
}
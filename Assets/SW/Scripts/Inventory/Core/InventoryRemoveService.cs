public static class InventoryRemoveService
{
    public static InventoryRemoveResult TryRemove(
        InventoryController inventoryController,
        InventoryItem item,
        InventoryGrid originalGrid,
        bool originalWasEquipped)
    {
        if (item?.itemData?.definition == null)
            return InventoryRemoveResult.InvalidItem;

        if (inventoryController == null || inventoryController.PlayerGrid == null)
            return InventoryRemoveResult.InventoryUnavailable;

        if (originalWasEquipped)
            return InventoryRemoveResult.EquippedItemNotAllowed;

        if (originalGrid != inventoryController.PlayerGrid)
            return InventoryRemoveResult.NotPlayerInventory;

        // 일반 버튼 호출처럼 아이템이 아직 Grid에 남아 있는 경우만 제거한다.
        // 현재 드래그 흐름에서는 시작할 때 이미 분리되므로 실행되지 않는다.
        if (originalGrid.ContainsItem(item) && !originalGrid.TryRemoveItem(item))
            return InventoryRemoveResult.RemoveFailed;

        inventoryController.NotifyItemOwnershipLost(item);

        return InventoryRemoveResult.Success;
    }
}

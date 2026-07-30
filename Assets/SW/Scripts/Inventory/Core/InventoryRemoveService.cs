public static class InventoryDiscardService
{
    public static InventoryDiscardResult TryDiscard(
        InventoryController inventoryController,
        InventoryItem item,
        InventoryGrid originalGrid,
        bool originalWasEquipped)
    {
        if (item?.itemData?.definition == null)
            return InventoryDiscardResult.InvalidItem;

        if (inventoryController == null || inventoryController.PlayerGrid == null)
            return InventoryDiscardResult.InventoryUnavailable;

        if (originalWasEquipped)
            return InventoryDiscardResult.EquippedItemNotAllowed;

        if (originalGrid != inventoryController.PlayerGrid)
            return InventoryDiscardResult.NotPlayerInventory;

        // 일반 버튼 호출처럼 아이템이 아직 Grid에 남아 있는 경우만 제거한다.
        // 현재 드래그 흐름에서는 시작할 때 이미 분리되므로 실행되지 않는다.
        if (originalGrid.ContainsItem(item) && !originalGrid.TryRemoveItem(item))
            return InventoryDiscardResult.RemoveFailed;

        inventoryController.NotifyItemOwnershipLost(item);

        return InventoryDiscardResult.Success;
    }
}
using UnityEngine;

public class ItemDropHandler : MonoBehaviour
{
    [SerializeField] private ItemUI itemUI;
    [SerializeField] private ItemEquipHandler equipHandler;

    private void Awake()
    {
        if (itemUI == null)
            itemUI = GetComponent<ItemUI>();

        if (equipHandler == null)
            equipHandler = GetComponent<ItemEquipHandler>();
    }

    public void ResolveDrop()
    {
        ResolveDrop(Vector2.zero, null, default, null);
    }

    public void ResolveDrop(
        Vector2 screenPosition,
        Camera eventCamera,
        InventorySwapPlan previewPlan,
        GameObject target)
    {
        ShopController shop = ShopController.Instance;

        UpgradeDropSlot upgradeDropSlot =
            target != null ? target.GetComponentInParent<UpgradeDropSlot>() : null;

        if (upgradeDropSlot != null)
        {
            // 상점 아이템은 구매 전이므로 강화 대상으로 선택하지 않는다.
            if (shop != null && itemUI.OriginalGrid == shop.ShopGrid)
            {
                itemUI.TryReturnToOriginalPosition();
                return;
            }

            bool restored;

            if (itemUI.OriginalWasEquipped)
            {
                restored = equipHandler.TryHandleDropToEquipSlot(
                    itemUI.CurrentEquipSlot);
            }
            else
            {
                restored = itemUI.TryReturnToOriginalPosition();
            }

            if (!restored)
            {
                Debug.LogError(
                    "[ItemDropHandler] 강화창 드롭 후 원래 위치 복구에 실패했습니다.");
                return;
            }

            upgradeDropSlot.TrySelectItem(itemUI);
            return;
        }
        Vector2Int targetCell = itemUI.GetCellFromItemRect(itemUI.CurrentGrid);
        int targetX = targetCell.x;
        int targetY = targetCell.y;

        EquipSlotUI targetEquipSlot = InventoryController.Instance.hoveredEquipSlot;

        if (shop != null && shop.TradeItem(itemUI, itemUI.OriginalGrid))
            return;

        if (shop != null &&
            itemUI.OriginalGrid == shop.ShopGrid &&
            targetEquipSlot != null)
        {
            itemUI.ReturnToOriginalPosition();
            return;
        }

        if (targetEquipSlot != null)
        {
            equipHandler.TryHandleDropToEquipSlot(targetEquipSlot);
            return;
        }

        if (itemUI.IsEquipped)
        {
            equipHandler.TryHandleDropFromEquipSlotToGrid(targetX, targetY);
            return;
        }

        InventoryMoveResultData result = InventoryMoveService.TryMoveOnGrid(
            itemUI.CurrentGrid,
            itemUI.Item,
            targetX,
            targetY,
            itemUI.OriginalPlacement,
            previewPlan);
        HandleInventoryMoveResult(result);
    }

    private void HandleInventoryMoveResult(InventoryMoveResultData result)
    {
        switch (result.Result)
        {
            case InventoryMoveResult.Success:
            case InventoryMoveResult.ReturnedToOriginal:
            case InventoryMoveResult.MovedToEmptySpace:
                itemUI.SetGridPosition(itemUI.CurrentGrid, result.MovedX, result.MovedY);
                break;
            case InventoryMoveResult.Swapped:
                itemUI.SetGridPositionAnimated(
                    itemUI.CurrentGrid,
                    result.MovedX,
                    result.MovedY);

                ItemUI swappedUI = ItemUIFinder.FindInGrid(itemUI.CurrentGrid, result.SwappedItem);
                if (swappedUI != null)
                {
                    swappedUI.SetGridPositionAnimated(
                        itemUI.CurrentGrid,
                        result.SwappedX,
                        result.SwappedY);
                }
                break;
            case InventoryMoveResult.Failed:
                if (!itemUI.TryReturnToOriginalPosition())
                {
                    Debug.LogError(
                        "[ItemDropHandler] 드롭 실패 후 아이템을 원래 위치에 복구하지 못했습니다.");
                }
                return;
        }
    }
}


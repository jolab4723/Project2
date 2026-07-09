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
        Vector2Int targetCell = itemUI.GetCellFromItemRect(itemUI.CurrentGrid);
        int targetX = targetCell.x;
        int targetY = targetCell.y;

        EquipSlotUI targetEquipSlot = InventoryController.Instance.hoveredEquipSlot;
        ShopController shop = ShopController.Instance;

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
            itemUI.CurrentGrid, itemUI.Item, targetX, targetY, itemUI.OriginalX, itemUI.OriginalY, itemUI.OriginalRotated);
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
                itemUI.SetGridPosition(itemUI.CurrentGrid, result.MovedX, result.MovedY);

                ItemUI swappedUI = ItemUIFinder.FindInGrid(itemUI.CurrentGrid, result.SwappedItem);
                if (swappedUI != null)
                    swappedUI.SetGridPosition(itemUI.CurrentGrid, result.SwappedX, result.SwappedY);
                break;
            case InventoryMoveResult.Failed:
                InventoryController.Instance.PrintLog("?쒕옒洹??꾩쨷 ?몃깽?좊━媛 媛??李??ｌ쓣 ???놁뼱???뚭눼?섏뿀?듬땲??");
                Destroy(itemUI.gameObject);
                return;
        }
    }
}


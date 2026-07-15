using UnityEngine;

public class ItemDropHandler : MonoBehaviour
{
    [SerializeField] private ItemUI itemUI;
    [SerializeField] private ItemEquipHandler equipHandler;

    private WorldItemDropService worldItemDropService;

    private void Awake()
    {
        if (itemUI == null)
            itemUI = GetComponent<ItemUI>();

        if (equipHandler == null)
            equipHandler = GetComponent<ItemEquipHandler>();
    }

    public void Bind(WorldItemDropService service)
    {
        worldItemDropService = service;
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

        if (TryHandleWorldDrop(screenPosition, eventCamera, target))
        {
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

    private bool TryHandleWorldDrop(
    Vector2 screenPosition,
    Camera eventCamera,
    GameObject target)
    {
        // 다른 UI 위에 놓은 경우 월드 드롭으로 처리하지 않는다.
        if (target != null)
            return false;

        // 장비에서 바로 월드로 버리는 기능은 현재 범위에서 제외한다.
        if (itemUI == null ||
            itemUI.OriginalWasEquipped ||
            itemUI.Item?.itemData == null ||
            worldItemDropService == null)
        {
            return false;
        }

        RectTransform gridRect = itemUI.CurrentGrid?.GridRect;

        if (gridRect == null)
            return false;

        // 여전히 인벤토리 그리드 안이면 기존 이동 로직이 처리한다.
        if (RectTransformUtility.RectangleContainsScreenPoint(
                gridRect,
                screenPosition,
                eventCamera))
        {
            return false;
        }

        WorldItemDropResult result =
            worldItemDropService.TryDrop(itemUI.Item.itemData);

        if (result == WorldItemDropResult.Success)
        {
            // 드래그 시작 시 이미 InventoryGrid에서는 제거되었으므로
            // 여기서 다시 TryRemoveItem을 호출하면 안 된다.
            Destroy(itemUI.gameObject);
            return true;
        }

        // 월드 생성 실패 시 인벤토리로 되돌린다.
        if (!itemUI.TryReturnToOriginalPosition())
        {
            Debug.LogError(
                $"[ItemDropHandler] 월드 드롭 실패 후 복구에도 실패했습니다. result={result}");
        }
        else
        {
            Debug.LogWarning(
                $"[ItemDropHandler] 월드 드롭에 실패하여 원래 위치로 복구했습니다. result={result}");
        }

        // 실패해도 이번 드롭 입력은 여기서 처리 완료한다.
        return true;
    }
}


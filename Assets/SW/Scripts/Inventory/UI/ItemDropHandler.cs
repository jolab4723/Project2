using ItemSystem;
using UnityEngine;

public class ItemDropHandler : MonoBehaviour
{
    [SerializeField] private ItemUI itemUI;
    [SerializeField] private ItemEquipHandler equipHandler;

    private WorldItemDropService worldItemDropService;
    private InventoryController inventoryController;
    private bool restorePending;
    private bool recoveryFailureLogged;

    public bool HasPendingRestore => restorePending;

    private void Awake()
    {
        if (itemUI == null)
            itemUI = GetComponent<ItemUI>();

        if (equipHandler == null)
            equipHandler = GetComponent<ItemEquipHandler>();
    }

    private void OnEnable()
    {
        if (restorePending && inventoryController != null)
            TryRestoreOriginalPlacement();
    }

    public void Bind(WorldItemDropService service, InventoryController owner)
    {
        worldItemDropService = service;
        inventoryController = owner;

        if (restorePending)
            TryRestoreOriginalPlacement();
    }

    public void PrepareRestore()
    {
        restorePending = true;
        recoveryFailureLogged = false;
    }

    public void CancelRestore()
    {
        restorePending = false;
        recoveryFailureLogged = false;
    }

    /// <summary>
    /// 강화, 상점, 장비, 월드, 인벤토리 순서로 겹친 드롭 대상을 판별한다.
    /// 모든 분기가 끝난 뒤 모델과 UI가 함께 배치됐는지 확인해 복구 상태를 닫는다.
    /// </summary>
    public void ResolveDrop(
        Vector2 screenPosition,
        Camera eventCamera,
        InventorySwapPlan previewPlan,
        GameObject target)
    {
        try
        {
            ResolveDropInternal(
                screenPosition,
                eventCamera,
                previewPlan,
                target);
        }
        finally
        {
            TryConsumeStablePlacement();
        }
    }

    /// <summary>
    /// 드래그 시작 때 저장한 동일 InventoryItem을 장비 슬롯 또는 Grid에 복구한다.
    /// 모델과 UI가 모두 정상화된 뒤에만 pending을 소비해 중복 배치를 막는다.
    /// </summary>
    public bool TryRestoreOriginalPlacement()
    {
        if (!restorePending)
            return true;

        if (!CanRestoreNow())
            return false;

        if (TryConsumeStablePlacement())
            return true;

        // 모델이 장비에 남아 있는데 시각 슬롯을 찾지 못한 경우 Grid 복구로 중복 소유시키지 않는다.
        if (IsItemOwnedByEquipmentModel())
        {
            LogRecoveryFailureOnce();
            return false;
        }

        bool restored = false;

        if (itemUI.OriginalWasEquipped && equipHandler != null)
        {
            restored = equipHandler.TryRestoreOriginalEquipment(
                itemUI.OriginalEquipSlot);
        }

        // 트랜잭션이 부분적으로 장비 모델을 변경했다면 Grid fallback을 금지한다.
        if (!restored && IsItemOwnedByEquipmentModel())
        {
            LogRecoveryFailureOnce();
            return false;
        }

        if (!restored)
            restored = TryRestoreToGrid();

        if (restored && TryConsumeStablePlacement())
            return true;

        LogRecoveryFailureOnce();
        return false;
    }

    private void ResolveDropInternal(
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
                TryRestoreOriginalPlacement();
                return;
            }

            if (!TryRestoreOriginalPlacement())
                return;

            upgradeDropSlot.TrySelectItem(itemUI);
            return;
        }

        InventoryRemoveDropZone removeDropZone = target != null
            ? target.GetComponentInParent<InventoryRemoveDropZone>()
            : null;

        if (removeDropZone != null)
        {
            HandleDiscard();
            return;
        }

        Vector2Int targetCell = itemUI.GetCellFromItemRect(itemUI.CurrentGrid);
        int targetX = targetCell.x;
        int targetY = targetCell.y;

        EquipSlotUI targetEquipSlot =
            target != null ? target.GetComponentInParent<EquipSlotUI>() : null;

        // 장착 중인 아이템을 상점으로 직접 판매하는 전용 경로.
        // 반드시 일반 ShopController.TryHandleTradeDrop보다 먼저 처리한다.
        if (shop != null &&
            equipHandler != null &&
            itemUI.OriginalWasEquipped &&
            equipHandler.TryHandleSellEquippedItem(shop))
        {
            return;
        }

        if (shop != null && shop.TryHandleTradeDrop(itemUI, itemUI.OriginalGrid))
            return;

        if (shop != null &&
            itemUI.OriginalGrid == shop.ShopGrid &&
            targetEquipSlot != null)
        {
            TryRestoreOriginalPlacement();
            return;
        }

        if (targetEquipSlot != null)
        {
            equipHandler?.TryHandleDropToEquipSlot(targetEquipSlot);
            return;
        }

        if (itemUI.IsEquipped)
        {
            equipHandler?.TryHandleDropFromEquipSlotToGrid(targetX, targetY);
            return;
        }

        if (TryHandleWorldDrop(screenPosition, eventCamera, target))
            return;

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

                ItemUI swappedUI = ItemUIFinder.FindInGrid(
                    itemUI.CurrentGrid,
                    result.SwappedItem);

                if (swappedUI != null)
                {
                    swappedUI.SetGridPositionAnimated(
                        itemUI.CurrentGrid,
                        result.SwappedX,
                        result.SwappedY);
                }
                break;

            case InventoryMoveResult.Failed:
                break;
        }
    }

    private bool TryHandleWorldDrop(
        Vector2 screenPosition,
        Camera eventCamera,
        GameObject target)
    {
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

        if (RectTransformUtility.RectangleContainsScreenPoint(
                gridRect,
                screenPosition,
                eventCamera))
        {
            return false;
        }

        if (itemUI.OriginalGrid == ShopController.Instance?.ShopGrid)
        {
            TryRestoreOriginalPlacement();
            return true;
        }

        WorldItemDropResult result =
            worldItemDropService.TryDrop(itemUI.Item.itemData);

        if (result == WorldItemDropResult.Success)
        {
            MarkPlacementCompleted();
            inventoryController?.NotifyItemOwnershipLost(itemUI.Item);

            // 드래그 시작 시 이미 InventoryGrid에서는 제거되었으므로
            // 여기서 다시 TryRemoveItem을 호출하면 안 된다.
            Destroy(itemUI.gameObject);
            return true;
        }

        if (TryRestoreOriginalPlacement())
        {
            Debug.LogWarning(
                $"[ItemDropHandler] 월드 드롭에 실패하여 원래 위치로 복구했습니다. result={result}");
        }

        return true;
    }

    private void HandleDiscard()
    {
        InventoryItem item = itemUI != null ? itemUI.Item : null;

        InventoryDiscardResult result =
            InventoryDiscardService.TryDiscard(
                inventoryController,
                item,
                itemUI != null ? itemUI.OriginalGrid : null,
                itemUI != null && itemUI.OriginalWasEquipped);

        string itemName =
            item?.itemData?.definition != null
                ? item.itemData.definition.itemName
                : "아이템";

        string message = InventoryDiscardMessageMapper.GetMessage(result, itemName);

        if (inventoryController != null)
            inventoryController.PrintLog(message);
        else
            Debug.LogWarning(message);

        if (result == InventoryDiscardResult.Success)
        {
            MarkPlacementCompleted();
            TooltipManager.Instance?.HideTooltip();
            Destroy(itemUI.gameObject);
            return;
        }

        TryRestoreOriginalPlacement();
    }

    private bool TryRestoreToGrid()
    {
        InventoryGrid originalGrid = itemUI?.OriginalGrid;
        InventoryItem item = itemUI?.Item;

        if (originalGrid == null || item == null)
            return false;

        if (originalGrid.ContainsItem(item))
        {
            itemUI.SetGridPosition(originalGrid, item.x, item.y);
            return true;
        }

        InventoryMoveResultData result = InventoryMoveService.TryRestoreToGrid(
            originalGrid,
            item,
            itemUI.OriginalPlacement);

        if (result.Result != InventoryMoveResult.ReturnedToOriginal &&
            result.Result != InventoryMoveResult.MovedToEmptySpace)
        {
            return false;
        }

        itemUI.SetGridPosition(originalGrid, result.MovedX, result.MovedY);
        return true;
    }

    private bool TryConsumeStablePlacement()
    {
        if (!restorePending)
            return true;

        bool hasGridOwner = TryFindOwningGrid(
            out InventoryGrid owningGrid,
            out bool hasMultipleGridOwners);
        bool hasEquipmentOwner = TryFindEquipmentModelSlot(
            out EquipSlotType equipmentSlotType);

        if (hasMultipleGridOwners ||
            (hasGridOwner && hasEquipmentOwner))
        {
            return false;
        }

        bool synchronized = hasEquipmentOwner
            ? TrySynchronizeEquipmentPlacement(equipmentSlotType)
            : hasGridOwner && TrySynchronizeGridPlacement(owningGrid);

        if (!synchronized)
            return false;

        MarkPlacementCompleted();
        return true;
    }

    private bool TrySynchronizeGridPlacement(InventoryGrid owningGrid)
    {
        InventoryItem item = itemUI?.Item;

        if (owningGrid == null || item == null || !owningGrid.ContainsItem(item))
            return false;

        if (!itemUI.IsGridPlacementVisualized(owningGrid))
            itemUI.SetGridPosition(owningGrid, item.x, item.y);

        return itemUI.IsGridPlacementVisualized(owningGrid);
    }

    private bool TrySynchronizeEquipmentPlacement(EquipSlotType slotType)
    {
        if (inventoryController == null ||
            itemUI?.Item == null ||
            equipHandler == null)
        {
            return false;
        }

        EquipSlotUI targetSlot = null;
        EquipSlotUI[] slots = inventoryController.allEquipSlots;

        if (slots == null)
            return false;

        foreach (EquipSlotUI slot in slots)
        {
            if (slot != null && slot.SlotType == slotType)
            {
                targetSlot = slot;
                break;
            }
        }

        if (targetSlot == null ||
            (targetSlot.EquippedItemUI != null &&
             targetSlot.EquippedItemUI != itemUI))
        {
            return false;
        }

        equipHandler.SetEquipSlotVisual(targetSlot);

        return targetSlot.EquippedItemUI == itemUI &&
               itemUI.CurrentEquipSlot == targetSlot &&
               itemUI.transform.parent == targetSlot.transform;
    }

    private bool TryFindOwningGrid(
        out InventoryGrid owningGrid,
        out bool hasMultipleOwners)
    {
        owningGrid = null;
        hasMultipleOwners = false;

        InventoryGrid[] candidates =
        {
            itemUI?.CurrentGrid,
            itemUI?.OriginalGrid,
            inventoryController?.PlayerGrid,
            ShopController.Instance?.ShopGrid
        };

        foreach (InventoryGrid candidate in candidates)
        {
            if (candidate == null ||
                candidate == owningGrid ||
                !candidate.ContainsItem(itemUI.Item))
            {
                continue;
            }

            if (owningGrid != null)
            {
                hasMultipleOwners = true;
                return true;
            }

            owningGrid = candidate;
        }

        return owningGrid != null;
    }

    private bool IsItemOwnedByEquipmentModel()
    {
        return TryFindEquipmentModelSlot(out _);
    }

    private bool TryFindEquipmentModelSlot(out EquipSlotType slotType)
    {
        slotType = default;

        EquipmentSystem equipmentSystem = inventoryController?.EquipmentSystem;

        if (equipmentSystem == null || itemUI?.Item == null)
            return false;

        foreach (var equippedEntry in equipmentSystem.GetEquippedItems())
        {
            if (ReferenceEquals(equippedEntry.Value, itemUI.Item))
            {
                slotType = equippedEntry.Key;
                return true;
            }
        }

        return false;
    }

    private bool CanRestoreNow()
    {
        return itemUI != null &&
               inventoryController != null &&
               gameObject.scene.isLoaded &&
               inventoryController.gameObject.scene.isLoaded;
    }

    private void MarkPlacementCompleted()
    {
        restorePending = false;
        recoveryFailureLogged = false;
    }

    private void LogRecoveryFailureOnce()
    {
        if (recoveryFailureLogged)
            return;

        recoveryFailureLogged = true;
        Debug.LogError(
            "[ItemDropHandler] 드래그된 아이템을 장비 또는 인벤토리에 복구하지 못했습니다. " +
            "아이템 데이터는 유지되며 다음 UI 활성화 때 다시 복구합니다.",
            this);
    }
}

using System;
using System.Collections;
using ItemSystem;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Mirror 아이템 UI의 입력 의도만 서버에 전달한다. 드래그 중 모델은 ItemUI의 표시용 복사본과 분리된다.
/// 기존 싱글 아이템 Prefab에는 붙이지 않는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemUI), typeof(ItemDragHandler))]
public sealed class NetworkInventoryInput_MirrorTest : MonoBehaviour
{
    [SerializeField] private ItemUI itemUI;
    private PlayerInventorySync_MirrorTest inventorySync;
    private NetworkShopPlayerState_MirrorTest shopPlayer;
    private NetworkShopState_MirrorTest shopState;
    private InventoryGrid playerGrid;
    private uint dragInventoryRevision;
    private uint dragShopRevision;
    private bool queued;

    private void Awake()
    {
        itemUI ??= GetComponent<ItemUI>();
        itemUI.BindExternalInput(HandleInput);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        queued = false;
        // 비활성화 도중 들어오는 입력도 로컬 거래로 바뀌지 않도록 바인딩은 유지한다.
    }

    private void OnDestroy()
    {
        if (itemUI != null) itemUI.BindExternalInput(null);
    }

    private bool TryResolveOwner()
    {
        PlayerContext owner = (NetworkManager.singleton as MirrorTestNetworkManager)?.LocalPlayerContext;
        if (!isActiveAndEnabled || owner?.Inventory?.PlayerGrid == null)
            return false;

        inventorySync = owner.GetComponent<PlayerInventorySync_MirrorTest>();
        shopPlayer = owner.GetComponent<NetworkShopPlayerState_MirrorTest>();
        playerGrid = owner.Inventory.PlayerGrid;
        shopState = shopPlayer?.ResolveShopState();
        return inventorySync != null && inventorySync.isLocalPlayer &&
               inventorySync.PendingRequestCount == 0 &&
               (shopPlayer == null || shopPlayer.PendingRequestCount == 0);
    }

    private ShopController BoundShop => ShopController.Instance != null &&
        ShopController.Instance.BoundPlayer?.PlayerGrid == playerGrid &&
        ShopController.Instance.isActiveAndEnabled ? ShopController.Instance : null;

    private bool HandleInput(ItemExternalInputAction action, PointerEventData eventData)
    {
        if (queued || !TryResolveOwner() || itemUI.DragSourceItem?.itemData?.definition == null)
            return false;

        ShopController shop = BoundShop;
        InventoryGrid source = action == ItemExternalInputAction.EndDrag ? itemUI.OriginalGrid : itemUI.CurrentGrid;
        if (source != playerGrid && (shop == null || source != shop.ShopGrid))
            return false;

        if (action == ItemExternalInputAction.BeginDrag)
        {
            dragInventoryRevision = inventorySync.StateRevision;
            dragShopRevision = shopState != null ? shopState.StateRevision : 0;
            return true;
        }

        if (action == ItemExternalInputAction.RightClick)
        {
            if (itemUI.IsDragPreviewActive) return false;
            dragInventoryRevision = inventorySync.StateRevision;
            dragShopRevision = shopState != null ? shopState.StateRevision : 0;
            return HandleRightClick(shop);
        }

        return itemUI.IsDragPreviewActive && HandleDrop(eventData, shop);
    }

    private bool HandleRightClick(ShopController shop)
    {
        InventoryItem item = itemUI.Item;
        string id = item.itemData.instanceId;
        if (shop != null && itemUI.CurrentGrid == shop.ShopGrid)
        {
            return playerGrid.TryFindEmptySpaceForItem(item, item.isRotated, out InventoryPlacementSnapshot placement) &&
                   Queue(() => shopPlayer.TryRequestBuy(id, placement.Rect.X, placement.Rect.Y, placement.IsRotated, out _), true);
        }

        if (item.isEquipped)
        {
            return itemUI.TryFindUnequipSpace(out int x, out int y, out bool rotated) &&
                   Queue(() => inventorySync.TryRequestEquipmentChange(id, false, EquipSlotType.None, x, y, rotated, out _));
        }

        return EquipSlotRules.TryGetEquipSlot(item.itemData.definition, out EquipSlotType slot) &&
               Queue(() => inventorySync.TryRequestEquipmentChange(id, true, slot, -1, -1, false, out _));
    }

    private bool HandleDrop(PointerEventData eventData, ShopController shop)
    {
        GameObject target = eventData.pointerCurrentRaycast.gameObject;
        InventoryItem preview = itemUI.Item;
        string id = itemUI.DragSourceItem.itemData.instanceId;
        bool fromPlayer = itemUI.OriginalGrid == playerGrid;
        bool wasEquipped = itemUI.OriginalWasEquipped;
        bool rotated = preview.isRotated;

        UpgradeDropSlot upgrade = target != null ? target.GetComponentInParent<UpgradeDropSlot>() : null;
        if (upgrade != null)
            return fromPlayer && Queue(() => upgrade != null && upgrade.TrySelectItem(itemUI));

        if (target != null && target.GetComponentInParent<InventoryRemoveDropZone>() != null)
            return fromPlayer && !wasEquipped && Queue(() => inventorySync.TryRequestRemoveInventoryItem(id, out _));

        if (shop != null && shop.IsTradingToShop(itemUI.OriginalGrid, itemUI))
        {
            Vector2Int cell = itemUI.GetCellFromItemRect(shop.ShopGrid);
            return shop.TryResolveSellPosition(preview, cell.x, cell.y, out InventoryPlacementSnapshot placement) &&
                   Queue(() => shopPlayer.TryRequestSell(id, placement.Rect.X, placement.Rect.Y, placement.IsRotated, out _), true);
        }

        if (shop != null && shop.IsTradingToPlayer(itemUI.OriginalGrid, itemUI))
        {
            Vector2Int cell = itemUI.GetCellFromItemRect(playerGrid);
            return Queue(() => shopPlayer.TryRequestBuy(id, cell.x, cell.y, rotated, out _), true);
        }

        if (!fromPlayer) return false;
        EquipSlotUI equipSlot = target != null ? target.GetComponentInParent<EquipSlotUI>() : null;
        if (equipSlot != null)
        {
            EquipSlotType slot = equipSlot.SlotType;
            return !wasEquipped && Queue(() => inventorySync.TryRequestEquipmentChange(id, true, slot, -1, -1, false, out _));
        }

        bool overGrid = playerGrid.GridRect != null && RectTransformUtility.RectangleContainsScreenPoint(
            playerGrid.GridRect, eventData.position, eventData.pressEventCamera);
        if (overGrid)
        {
            Vector2Int cell = itemUI.GetCellFromItemRect(playerGrid);
            return wasEquipped
                ? Queue(() => inventorySync.TryRequestEquipmentChange(id, false, EquipSlotType.None, cell.x, cell.y, rotated, out _))
                : Queue(() => inventorySync.TryRequestMoveGridItem(id, cell.x, cell.y, rotated, out _));
        }

        // 인벤토리 그리드 밖이면서 유효한 장비/상점/강화/삭제 슬롯이 아니면 필드 드롭(바닥 버리기)으로 판정한다.
        return !wasEquipped && Queue(() => inventorySync.TryRequestDropInventoryItem(id, out _));
    }

    private bool Queue(Func<bool> request, bool trade = false)
    {
        if (trade && (shopPlayer == null || shopState == null)) return false;
        queued = true;
        StartCoroutine(RequestAfterPreviewRestored(request, trade, dragInventoryRevision, dragShopRevision));
        return true;
    }

    private IEnumerator RequestAfterPreviewRestored(Func<bool> request, bool trade, uint inventoryRevision, uint shopRevision)
    {
        // Host Command가 UI를 재생성하기 전에 ItemDragHandler의 finally가 원본 UI를 복구하게 한다.
        yield return null;
        queued = false;
        if (!TryResolveOwner() || itemUI.IsDragPreviewActive || inventorySync.StateRevision != inventoryRevision ||
            (trade && (shopState == null || shopState.StateRevision != shopRevision)))
        {
            Debug.LogWarning("[NetworkInventoryInput_MirrorTest] 입력 중 소유 상태가 갱신되어 요청을 취소했습니다.", this);
            yield break;
        }

        if (!request())
            Debug.LogWarning("[NetworkInventoryInput_MirrorTest] 서버 요청을 시작하지 못했습니다.", this);
    }
}

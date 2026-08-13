using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 기존 장비 드래그와 우클릭 결과를 Mirror 서버에 전달하는 테스트 전용 어댑터다.
/// <para>팀 원본과의 차이: <see cref="ItemEquipHandler"/>와 <see cref="EquipmentTransaction"/>은
/// 장착·해제·교환 계산을 그대로 담당한다. 이 컴포넌트는 계산이 끝난 뒤 아이템의 최종 위치만 읽어
/// <see cref="PlayerInventorySync_MirrorTest"/>에 서버 확정을 요청한다.</para>
/// <para>서버가 승인하면 해당 결과를 유지하고, 거절하면 서버가 보관한 인벤토리·장비 상태로
/// 로컬 모델과 장비 슬롯 화면을 함께 복구한다. 운영 아이템 프리팹과 팀 원본 스크립트에는
/// 네트워크 동작을 추가하지 않으며 <c>ItemPrefab_MirrorTest</c>에서만 참조한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemUI), typeof(ItemDragHandler), typeof(CanvasGroup))]
public sealed class NetworkInventoryEquipmentPlacement_MirrorTest :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerClickHandler,
    IEndDragHandler
{
    [SerializeField] private ItemUI itemUI;
    [SerializeField] private CanvasGroup canvasGroup;

    private PlayerInventorySync_MirrorTest inventorySync;
    private InventoryGrid playerGrid;
    private uint pendingRequestId;
    private InteractionState pointerDownState;
    private float idleAlpha = 1f;
    private bool idleInteractable = true;
    private bool idleBlocksRaycasts = true;

    private readonly struct InteractionState
    {
        public bool IsValid { get; }
        public bool IsEquipped { get; }
        public EquipSlotType SlotType { get; }
        public int GridX { get; }
        public int GridY { get; }
        public bool IsRotated { get; }

        public InteractionState(
            bool isEquipped,
            EquipSlotType slotType,
            int gridX,
            int gridY,
            bool isRotated)
        {
            IsValid = true;
            IsEquipped = isEquipped;
            SlotType = slotType;
            GridX = gridX;
            GridY = gridY;
            IsRotated = isRotated;
        }
    }

    private bool IsPending => pendingRequestId != 0;

    private void Awake()
    {
        itemUI ??= GetComponent<ItemUI>();
        canvasGroup ??= GetComponent<CanvasGroup>();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        pointerDownState = CaptureCurrentState();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right ||
            IsPending ||
            !pointerDownState.IsValid)
        {
            return;
        }

        InteractionState currentState = CaptureCurrentState();
        if (HasEquipmentStateChanged(pointerDownState, currentState))
            RequestCurrentEquipmentState(currentState);

        pointerDownState = default;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (IsPending || itemUI?.Item?.itemData == null)
            return;

        InteractionState beforeDrag = CaptureDragStartState();
        InteractionState currentState = CaptureCurrentState();

        if (HasEquipmentStateChanged(beforeDrag, currentState))
            RequestCurrentEquipmentState(currentState);
    }

    private InteractionState CaptureDragStartState()
    {
        if (itemUI == null || itemUI.Item == null)
            return default;

        if (itemUI.OriginalWasEquipped)
        {
            EquipSlotUI originalSlot = itemUI.OriginalEquipSlot;
            return originalSlot != null
                ? new InteractionState(true, originalSlot.SlotType, -1, -1, false)
                : default;
        }

        InventoryPlacementSnapshot original = itemUI.OriginalPlacement;
        return original.IsValid
            ? new InteractionState(
                false,
                EquipSlotType.None,
                original.Rect.X,
                original.Rect.Y,
                original.IsRotated)
            : default;
    }

    private InteractionState CaptureCurrentState()
    {
        InventoryItem item = itemUI?.Item;
        if (item?.itemData == null)
            return default;

        EquipSlotUI currentSlot = itemUI.CurrentEquipSlot;
        if (item.isEquipped && currentSlot != null)
            return new InteractionState(true, currentSlot.SlotType, -1, -1, false);

        if (!TryResolveInventorySync() ||
            itemUI.CurrentGrid != playerGrid ||
            !playerGrid.ContainsItem(item))
        {
            return default;
        }

        return new InteractionState(
            false,
            EquipSlotType.None,
            item.x,
            item.y,
            item.isRotated);
    }

    private static bool HasEquipmentStateChanged(
        InteractionState before,
        InteractionState after)
    {
        if (!before.IsValid || !after.IsValid || before.IsEquipped == after.IsEquipped)
            return false;

        return true;
    }

    private void RequestCurrentEquipmentState(InteractionState state)
    {
        InventoryItem item = itemUI?.Item;
        if (!state.IsValid ||
            item?.itemData == null ||
            !TryResolveInventorySync())
        {
            RestoreAuthoritativeState();
            return;
        }

        Subscribe();
        if (!inventorySync.TryRequestEquipmentChange(
                item.itemData.instanceId,
                state.IsEquipped,
                state.SlotType,
                state.GridX,
                state.GridY,
                state.IsRotated,
                out uint requestId))
        {
            Unsubscribe();
            RestoreAuthoritativeState();
            Debug.LogWarning(
                "[NetworkInventoryEquipmentPlacement_MirrorTest] 서버 장비 변경 요청을 시작하지 못했습니다.",
                this);
            return;
        }

        pendingRequestId = requestId;
        LockItemView();
    }

    private bool TryResolveInventorySync()
    {
        if (inventorySync != null && playerGrid != null)
            return true;

        MirrorTestNetworkManager networkManager =
            FindFirstObjectByType<MirrorTestNetworkManager>();
        PlayerContext localContext = networkManager?.LocalPlayerContext;
        inventorySync = localContext != null
            ? localContext.GetComponent<PlayerInventorySync_MirrorTest>()
            : null;
        playerGrid = localContext?.Inventory?.PlayerGrid;

        return inventorySync != null && playerGrid != null;
    }

    private void Subscribe()
    {
        inventorySync.RequestCompleted -= HandleRequestCompleted;
        inventorySync.RequestCompleted += HandleRequestCompleted;
    }

    private void Unsubscribe()
    {
        if (inventorySync != null)
            inventorySync.RequestCompleted -= HandleRequestCompleted;
    }

    private void HandleRequestCompleted(MirrorTestInventoryRequestCompleted completed)
    {
        if (completed.RequestId != pendingRequestId ||
            completed.Operation != MirrorTestInventoryOperation.ChangeEquipment)
        {
            return;
        }

        pendingRequestId = 0;
        Unsubscribe();
        UnlockItemView();

        if (completed.Result == MirrorTestInventoryRequestResult.Success)
            return;

        RestoreAuthoritativeState();
        Debug.LogWarning(
            $"[NetworkInventoryEquipmentPlacement_MirrorTest] 서버가 장비 변경을 거절했습니다: {completed.Result}",
            this);
    }

    private void RestoreAuthoritativeState()
    {
        if (inventorySync != null &&
            !inventorySync.TryRestoreGridFromAuthoritativeSnapshots())
        {
            Debug.LogError(
                "[NetworkInventoryEquipmentPlacement_MirrorTest] 서버 확정 상태로 인벤토리와 장비를 복구하지 못했습니다.",
                inventorySync);
        }
    }

    private void LockItemView()
    {
        if (canvasGroup == null)
            return;

        idleAlpha = canvasGroup.alpha;
        idleInteractable = canvasGroup.interactable;
        idleBlocksRaycasts = canvasGroup.blocksRaycasts;
        canvasGroup.alpha = Mathf.Min(idleAlpha, 0.55f);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void UnlockItemView()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = idleAlpha;
        canvasGroup.interactable = idleInteractable;
        canvasGroup.blocksRaycasts = idleBlocksRaycasts;
    }
}

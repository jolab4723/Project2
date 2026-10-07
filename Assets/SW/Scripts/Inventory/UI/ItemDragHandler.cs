using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ItemDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private enum DragState
    {
        Idle,
        Preparing,
        Detached,
        Resolving
    }

    [SerializeField] private ItemUI itemUI;
    [SerializeField] private ItemDragVisual dragVisual;
    [SerializeField] private ItemDragHighlighter dragHighlighter;
    [SerializeField] private ItemDropHandler dropHandler;

    private Vector2 lastPointerPosition;
    private Camera lastEventCamera;
    private GameObject lastPointerTarget;
    private DragState dragState;
    private bool externalDrag;

    public bool IsDragging =>
        dragState == DragState.Detached ||
        dragState == DragState.Resolving;

    private void Awake()
    {
        if (itemUI == null) itemUI = GetComponent<ItemUI>();
        if (dragVisual == null) dragVisual = GetComponent<ItemDragVisual>();
        if (dragHighlighter == null) dragHighlighter = GetComponent<ItemDragHighlighter>();
        if (dropHandler == null) dropHandler = GetComponent<ItemDropHandler>();
    }
    private void Update()
    {
        if (!IsDragging)
            return;

        if (externalDrag && !itemUI.IsDragPreviewActive)
        {
            TryRestoreInterruptedDrag();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            itemUI.RotateDraggingItem();
            dragHighlighter.RefreshHighlight(
                lastPointerPosition,
                lastEventCamera,
                lastPointerTarget);
        }
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (dragState != DragState.Idle ||
            eventData == null ||
            itemUI == null ||
            itemUI.Item?.itemData == null ||
            itemUI.IsDragPreviewActive ||
            itemUI.CurrentGrid == null ||
            dropHandler == null ||
            dragVisual == null ||
            dragHighlighter == null)
        {
            return;
        }

        // 시작 때 정한 경로를 끝까지 유지해 처리기가 해제되어도 로컬 거래로 바뀌지 않게 한다.
        externalDrag = itemUI.HasExternalInput;
        if (externalDrag && !itemUI.TryHandleExternalInput(ItemExternalInputAction.BeginDrag, eventData))
        {
            externalDrag = false;
            return;
        }
        if (!isActiveAndEnabled || itemUI == null)
        {
            externalDrag = false;
            return;
        }

        UpdatePointerContext(eventData);

        TooltipManager.Instance?.HideTooltip();

        bool wasEquipped = itemUI.IsEquipped;

        itemUI.SaveOriginalState();
        dropHandler.PrepareRestore(!externalDrag);
        dragState = DragState.Preparing;

        bool prepared = externalDrag
            ? itemUI.BeginDragPreview()
            : itemUI.TryDetachFromCurrentSlotOrGrid();
        if (!prepared)
        {
            dropHandler.CancelRestore();
            dragState = DragState.Idle;
            externalDrag = false;
            Debug.LogError(
                "[ItemDragHandler] 드래그 시작 전 아이템을 기존 위치에서 분리하지 못했습니다.");
            return;
        }

        dragState = DragState.Detached;

        // 싱글 드래그는 모델을 Grid에서 실제로 뺀다. 배치가 끝날 때까지 저장과 원래 칸 예약에 알린다.
        if (!externalDrag)
            dropHandler.MarkDetachedFromPlayerGrid();

        YJ_CursorManager.Instance?.BeginDragCursor();

        // 분리 메서드 안에서 UI가 비활성화되더라도 모델이 빠진 채 남지 않게 한다.
        if (!isActiveAndEnabled)
        {
            TryRestoreInterruptedDrag();
            return;
        }

        TooltipManager.Instance?.BeginItemDrag();
        itemUI.SetParentToCurrentGrid(true);

        Vector2 localPoint = itemUI.ScreenToCurrentGridLocalPoint(eventData);

        if (wasEquipped)
        {
            itemUI.RestoreGridSettings();
            itemUI.SetAnchoredPosition(
                localPoint + new Vector2(-itemUI.SizeDelta.x / 2f, itemUI.SizeDelta.y / 2f)
            );
        }

        dragVisual.BeginDragVisual();
        dragHighlighter.RefreshHighlight(
            lastPointerPosition,
            lastEventCamera,
            lastPointerTarget);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsDragging || eventData == null)
            return;

        if (externalDrag && !itemUI.IsDragPreviewActive)
        {
            TryRestoreInterruptedDrag();
            return;
        }

        UpdatePointerContext(eventData);
        itemUI.MoveByDelta(eventData.delta);
        dragHighlighter.RefreshHighlight(
            lastPointerPosition,
            lastEventCamera,
            lastPointerTarget);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragState != DragState.Detached)
            return;

        if (eventData == null || (externalDrag && !itemUI.IsDragPreviewActive))
        {
            TryRestoreInterruptedDrag();
            return;
        }

        UpdatePointerContext(eventData);
        dragHighlighter.RefreshHighlight(
            lastPointerPosition,
            lastEventCamera,
            lastPointerTarget);

        dragState = DragState.Resolving;

        try
        {
            if (externalDrag)
                itemUI.TryHandleExternalInput(ItemExternalInputAction.EndDrag, eventData);
            else
            {
                InventorySwapPlan previewPlan = dragHighlighter.CurrentSwapPlan;
                dropHandler.ResolveDrop(
                    lastPointerPosition,
                    lastEventCamera,
                    previewPlan,
                    eventData.pointerCurrentRaycast.gameObject);
            }
        }
        finally
        {
            TryRestoreInterruptedDrag();
        }
    }

    /// <summary>
    /// 팝업 종료로 OnEndDrag가 생략되면 분리 완료된 아이템만 복구한다.
    /// 드롭 처리 중인 아이템은 OnEndDrag의 finally가 담당해 중복 복구하지 않는다.
    /// </summary>
    private void OnDisable()
    {
        if (dragState != DragState.Detached)
            return;

        TryRestoreInterruptedDrag();
    }

    private void TryRestoreInterruptedDrag()
    {
        // 복구 중 부모 변경으로 다시 비활성화돼도 같은 아이템을 중복 복구하지 않는다.
        dragState = DragState.Resolving;
        try
        {
            // 원본 참조를 먼저 돌려놓아 기존 복구가 표시용 복사본을 모델에 추가하지 않게 한다.
            if (externalDrag) itemUI?.EndDragPreview();
            dropHandler?.TryRestoreOriginalPlacement();
        }
        finally
        {
            dragState = DragState.Idle;
            externalDrag = false;
            EndDragVisuals();
        }
    }

    private void EndDragVisuals()
    {
        YJ_CursorManager.Instance?.EndDragCursor();
        TooltipManager.Instance?.EndItemDrag();
        dragHighlighter?.HideActiveHighlight();
        dragVisual?.EndDragVisual();
    }

    private void UpdatePointerContext(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        lastPointerPosition = eventData.position;
        lastEventCamera = eventData.pressEventCamera;
        lastPointerTarget = eventData.pointerCurrentRaycast.gameObject;
    }
}

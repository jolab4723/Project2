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
            itemUI == null ||
            dropHandler == null)
        {
            return;
        }

        UpdatePointerContext(eventData);

        TooltipManager.Instance?.HideTooltip();

        bool wasEquipped = itemUI.IsEquipped;

        itemUI.SaveOriginalState();
        dropHandler.PrepareRestore();
        dragState = DragState.Preparing;

        if (!itemUI.TryDetachFromCurrentSlotOrGrid())
        {
            dropHandler.CancelRestore();
            dragState = DragState.Idle;
            Debug.LogError(
                "[ItemDragHandler] 드래그 시작 전 아이템을 기존 위치에서 분리하지 못했습니다.");
            return;
        }

        dragState = DragState.Detached;
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
        if (!IsDragging)
            return;

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

        UpdatePointerContext(eventData);
        dragHighlighter.RefreshHighlight(
            lastPointerPosition,
            lastEventCamera,
            lastPointerTarget);

        dragState = DragState.Resolving;

        try
        {
            InventorySwapPlan previewPlan = dragHighlighter.CurrentSwapPlan;
            dropHandler.ResolveDrop(
                lastPointerPosition,
                lastEventCamera,
                previewPlan,
                eventData.pointerCurrentRaycast.gameObject);
        }
        finally
        {
            if (dropHandler.HasPendingRestore)
                dropHandler.TryRestoreOriginalPlacement();

            dragState = DragState.Idle;
            EndDragVisuals();
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
        dropHandler?.TryRestoreOriginalPlacement();
        dragState = DragState.Idle;
        EndDragVisuals();
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

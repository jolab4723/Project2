using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ItemDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private ItemUI itemUI;
    [SerializeField] private ItemDragVisual dragVisual;
    [SerializeField] private ItemDragHighlighter dragHighlighter;
    [SerializeField] private ItemDropHandler dropHandler;

    private Vector2 lastPointerPosition;
    private Camera lastEventCamera;

    public bool IsDragging { get; private set; }

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
                lastEventCamera);
        }
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        UpdatePointerContext(eventData);

        TooltipManager.Instance?.HideTooltip();

        bool wasEquipped = itemUI.IsEquipped;

        itemUI.SaveOriginalState();
        if (!itemUI.TryDetachFromCurrentSlotOrGrid())
        {
            Debug.LogError(
                "[ItemDragHandler] 드래그 시작 전 아이템을 기존 위치에서 분리하지 못했습니다.");
            return;
        }

        IsDragging = true;
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
            lastEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsDragging)
            return;

        UpdatePointerContext(eventData);
        itemUI.MoveByDelta(eventData.delta);
        dragHighlighter.RefreshHighlight(
            lastPointerPosition,
            lastEventCamera);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsDragging)
            return;

        UpdatePointerContext(eventData);
        dragHighlighter.RefreshHighlight(
            lastPointerPosition,
            lastEventCamera);

        InventorySwapPlan previewPlan = dragHighlighter.CurrentSwapPlan;
        dropHandler.ResolveDrop(
            lastPointerPosition,
            lastEventCamera,
            previewPlan,
            eventData.pointerCurrentRaycast.gameObject);
        dragHighlighter.HideActiveHighlight();
        dragVisual.EndDragVisual();
        IsDragging = false;
        TooltipManager.Instance?.EndItemDrag();
    }

    /// <summary>
    /// 팝업 종료나 오브젝트 비활성화로 OnEndDrag가 호출되지 않아도
    /// 전역 툴팁 억제 상태와 드래그 시각 효과가 남지 않도록 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (!IsDragging)
            return;

        IsDragging = false;
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
    }
}

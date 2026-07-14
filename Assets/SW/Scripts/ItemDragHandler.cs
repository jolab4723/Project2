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

        TooltipManager.Instance.HideTooltip();

        bool wasEquipped = itemUI.IsEquipped;

        itemUI.SaveOriginalState();
        if (!itemUI.TryDetachFromCurrentSlotOrGrid())
        {
            Debug.LogError(
                "[ItemDragHandler] 드래그 시작 전 아이템을 기존 위치에서 분리하지 못했습니다.");
            return;
        }

        IsDragging = true;
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
    }

    private void UpdatePointerContext(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        lastPointerPosition = eventData.position;
        lastEventCamera = eventData.pressEventCamera;
    }
}
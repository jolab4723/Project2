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
            dragHighlighter.RefreshHighlight();
        }
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        IsDragging = true;

        TooltipManager.Instance.HideTooltip();

        bool wasEquipped = itemUI.IsEquipped;

        itemUI.SaveOriginalState();
        itemUI.DetachFromCurrentSlotOrGrid();
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
        dragHighlighter.RefreshHighlight();
    }

    public void OnDrag(PointerEventData eventData)
    {
        itemUI.MoveByDelta(eventData.delta);
        dragHighlighter.RefreshHighlight();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragHighlighter.HideActiveHighlight();
        dropHandler.ResolveDrop();
        dragVisual.EndDragVisual();
        IsDragging = false;
    }
}
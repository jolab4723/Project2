using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ItemDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private ItemUI itemUI;
    [SerializeField] private ItemDragVisual dragVisual;
    [SerializeField] private ItemDragHighlighter dragHighlighter;
    public bool IsDragging { get; private set; }
    private void Update()
    {
        if (!IsDragging)
            return;

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            itemUI.RotateDraggingItem();
            itemUI.RefreshDragHighlight();
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
            itemUI.RestoreGridVisualSettings();
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
        itemUI.RefreshDragHighlight();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        itemUI.EndDrag(eventData);
        IsDragging = false;
    }
}
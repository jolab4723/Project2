using UnityEngine;
using UnityEngine.EventSystems;

public class YJ_DragCursorHandler : MonoBehaviour,
    IBeginDragHandler,
    IEndDragHandler
{
    [SerializeField] private YJ_CursorManager cursorManager;

    public void OnBeginDrag(PointerEventData eventData)
    {
        cursorManager.SetCursor(CursorType.Drag);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        cursorManager.ResetCursor();
    }

    private void OnDisable()
    {
        // 드래그 도중 비활성화되어 커서가 고정되는 현상 방지
        cursorManager?.ResetCursor();
    }
}
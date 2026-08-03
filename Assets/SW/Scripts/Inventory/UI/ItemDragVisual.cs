using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster))]
public sealed class ItemDragVisual : MonoBehaviour
{
    private Canvas itemCanvas;
    private bool originalOverrideSorting;
    private int originalSortingOrder;

    private CanvasGroup canvasGroup;
    private Image itemIcon;

    private void Awake()
    {
        itemCanvas = GetComponent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();

        originalOverrideSorting = itemCanvas.overrideSorting;
        originalSortingOrder = itemCanvas.sortingOrder;

        itemIcon = GetComponentInChildren<Image>(true);
    }
    public void RaiseForDrag()
    {
        itemCanvas.overrideSorting = true;
        itemCanvas.sortingOrder = 1000;
        transform.SetAsLastSibling();
    }

    public void RestoreSorting()
    {
        itemCanvas.overrideSorting = originalOverrideSorting;
        itemCanvas.sortingOrder = originalSortingOrder;
    }

    public void BeginDragVisual()
    {
        itemIcon.color = new Color(1f, 1f, 1f, 0.8f);
        canvasGroup.blocksRaycasts = false;

        RaiseForDrag();
    }

    public void EndDragVisual()
    {
        itemIcon.color = Color.white;
        canvasGroup.blocksRaycasts = true;

        RestoreSorting();
    }
}

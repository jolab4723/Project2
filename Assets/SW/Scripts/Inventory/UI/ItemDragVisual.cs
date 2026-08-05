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
    [SerializeField] private Image itemIcon;

    private void Awake()
    {
        itemCanvas = GetComponent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();

        originalOverrideSorting = itemCanvas.overrideSorting;
        originalSortingOrder = itemCanvas.sortingOrder;

        // 배경 Image가 추가되어도 드래그 투명도는 실제 아이콘에만 적용한다.
        if (itemIcon == null)
            itemIcon = transform.Find("IconImage")?.GetComponent<Image>();
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
        if (itemIcon != null)
            itemIcon.color = new Color(1f, 1f, 1f, 0.8f);

        canvasGroup.blocksRaycasts = false;

        RaiseForDrag();
    }

    public void EndDragVisual()
    {
        if (itemIcon != null)
            itemIcon.color = Color.white;

        canvasGroup.blocksRaycasts = true;

        RestoreSorting();
    }
}

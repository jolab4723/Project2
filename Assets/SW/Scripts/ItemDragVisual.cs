using UnityEngine;
using UnityEngine.UI;
using static PLAYERTWO.ARPGProject.InventorySerializer;
public class ItemDragVisual : MonoBehaviour
{
    private Canvas itemCanvas;
    private bool originalOverrideSorting;
    private int originalSortingOrder;

    private CanvasGroup canvasGroup;
    private Image itemIcon;


    private void Awake()
    {

        itemCanvas = GetComponent<Canvas>();
        if (itemCanvas == null) itemCanvas = gameObject.AddComponent<Canvas>();

        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        originalOverrideSorting = itemCanvas.overrideSorting;
        originalSortingOrder = itemCanvas.sortingOrder;

        if(itemIcon== null) itemIcon = transform.GetChild(0).GetComponent<Image>();


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

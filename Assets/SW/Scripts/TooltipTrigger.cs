using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private ItemUI itemUI;
    [SerializeField] private ItemDragHandler dragHandler;
    private void Awake()
    {
        if (itemUI == null)
            itemUI = GetComponent<ItemUI>();
    }
    public void Setup(ItemInstance item)
    {
 
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (dragHandler != null && dragHandler.IsDragging)
            return;

        if (itemUI == null || itemUI.Item == null)
            return;

        TooltipManager.Instance.ShowTooltip(itemUI.Item.itemData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.Instance.HideTooltip();
    }
}
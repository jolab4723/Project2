using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private ItemData itemData;

    public void Setup(ItemData data)
    {
        itemData = data;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (itemData == null)
            return;

        TooltipManager.Instance.ShowTooltip(itemData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.Instance.HideTooltip();
    }
}
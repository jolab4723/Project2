using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private ItemInstance itemData;

    public void Setup(ItemInstance data)
    {
        itemData = data;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (itemData == null)
            return;

        TooltipManagerTest.Instance.ShowTooltip(itemData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManagerTest.Instance.HideTooltip();
    }
}
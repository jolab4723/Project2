using UnityEngine;
using UnityEngine.EventSystems;

public class KY_SettingItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string settingName;
    [TextArea]
    public string description;
    public Sprite icon;
    public Sprite background;

    public void OnPointerEnter(PointerEventData eventData)
    {
        KY_SettingDescriptionPanel.Instance.Show(settingName, description, icon, background);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        KY_SettingDescriptionPanel.Instance.Hide();
    }
}
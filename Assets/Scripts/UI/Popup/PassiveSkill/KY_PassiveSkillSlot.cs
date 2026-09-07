using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using TMPro;

public class KY_PassiveSkillSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image iconImage;
    public GameObject activeHighlight;
    [SerializeField] private TMP_Text missingIconLabel;

    private KY_PassiveSkillData myData;

    public event Action<KY_PassiveSkillData> OnSlotClicked;
    public event Action<KY_PassiveSkillData> OnSlotHoverEnter;
    public event Action OnSlotHoverExit;

    public void Render(KY_PassiveSkillData data)
    {
        myData = data;
        iconImage.sprite = data.icon;
        iconImage.enabled = data.icon != null;
        if (missingIconLabel != null)
        {
            missingIconLabel.gameObject.SetActive(data.icon == null);
            missingIconLabel.text = data.skillName;
        }
        activeHighlight.SetActive(data.isActive);
    }

    public void OnPointerClick(PointerEventData eventData) => OnSlotClicked?.Invoke(myData);
    public void OnPointerEnter(PointerEventData eventData) => OnSlotHoverEnter?.Invoke(myData);
    public void OnPointerExit(PointerEventData eventData) => OnSlotHoverExit?.Invoke();
}

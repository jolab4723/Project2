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
    public event Action<KY_PassiveSkillData> OnSlotDecreaseRequested; // SW 수정
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

    /// <summary>좌클릭은 단계 올리기, 우클릭은 단계 내리기를 팝업에 요청한다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (myData == null) return;
        if (eventData.button == PointerEventData.InputButton.Left) OnSlotClicked?.Invoke(myData);
        else if (eventData.button == PointerEventData.InputButton.Right) OnSlotDecreaseRequested?.Invoke(myData);
    }
    public void OnPointerEnter(PointerEventData eventData) => OnSlotHoverEnter?.Invoke(myData);
    public void OnPointerExit(PointerEventData eventData) => OnSlotHoverExit?.Invoke();
}

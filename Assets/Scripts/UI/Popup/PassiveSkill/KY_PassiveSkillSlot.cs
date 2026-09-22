using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using TMPro;

/// <summary>패시브 스킬 한 칸의 아이콘·단계 표시와 좌우 클릭·호버 입력을 전달한다.</summary>
public class KY_PassiveSkillSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image iconImage;
    public GameObject activeHighlight;
    [SerializeField] private TMP_Text missingIconLabel;
    [SerializeField] private TMP_Text levelLabel; // SW 수정: 새 화면의 단계 배지

    private PassiveSkillData myData;

    public event Action<PassiveSkillData> OnSlotClicked;
    public event Action<PassiveSkillData> OnSlotDecreaseRequested; // SW 수정
    public event Action<PassiveSkillData> OnSlotHoverEnter;
    public event Action OnSlotHoverExit;

    public void Render(PassiveSkillData data)
    {
        myData = data;
        iconImage.sprite = data.icon;
        iconImage.enabled = data.icon != null;
        if (missingIconLabel != null)
        {
            missingIconLabel.gameObject.SetActive(data.icon == null);
            missingIconLabel.text = levelLabel != null ? data.DisplayName.Replace(" ", "\n")
                : data.MaxLevel <= 0 ? data.DisplayName : data.LevelLabel;
        }
        if (levelLabel != null) levelLabel.text = data.LevelBadgeText;
        // activeHighlight(OutLine)는 더 이상 여기서 안 건드림 - 현재 선택된 슬롯인지 여부는
        // PassiveSkillPanelUI가 전체 슬롯을 훑어보며 배타적으로 관리한다(2026-09-10).
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

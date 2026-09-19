using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 쿨타임 표시 아이콘 한 칸. BuffIconSlot과 같은 시각 구조(아이콘+라디얼 오버레이+테두리)를
/// 쿨타임 진행 중인 아이템 하나에 맞게 변형한 파생 스크립트.
/// CooldownIconUIContainer가 쿨타임 진행 중인 아이템 수만큼 이 컴포넌트를 인스턴스화해서 값만 채운다.
///
/// 마우스를 올렸을 때의 툴팁도 버프 아이콘과 같은 조립기(BuffTextComposer)와 같은 툴팁(BuffTooltipUI)을
/// 쓴다 - 쿨타임 아이콘에 뜨는 건 결국 발동형 고유효과라, 이름/증감 스탯/설명을 만드는 규칙이 동일하다.
/// </summary>
public class CooldownIconSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image borderImage;

    [Tooltip("Image Type을 Filled/Radial 360으로 설정해서 써야 한다.")]
    [SerializeField] private Image cooldownFillImage;

    [SerializeField] private TextMeshProUGUI remainingText;

    [Tooltip("남은 시간이 이 값(초) 이하로 떨어졌을 때부터 숫자로도 표시한다.")]
    [SerializeField] private float remainingTextThreshold = 9f;

    [SerializeField] private Color borderColor = new Color(0.12f, 0.14f, 0.16f);

    private ItemInstance boundItem;
    private TriggeredBuffUniqueEffectSO boundEffect;

    /// <summary>지금 이 슬롯 위에 마우스가 올라와 있는지(언어 변경 시 툴팁을 다시 그릴지 판단용).</summary>
    private bool hovered;

    /// <summary>이 슬롯에 쿨타임 진행 중인 아이템을 연결한다. 아이콘/테두리색처럼 바인딩 시점에만 바뀌는 값을 채운다.</summary>
    public void Bind(ItemInstance item, TriggeredBuffUniqueEffectSO effect)
    {
        boundItem = item;
        boundEffect = effect;

        if (iconImage != null)
        {
            iconImage.sprite = effect != null ? effect.BuffIcon : null;
            iconImage.enabled = iconImage.sprite != null;
            iconImage.preserveAspect = true;
        }

        if (borderImage != null)
            borderImage.color = borderColor;

        Refresh();
    }

    /// <summary>남은 쿨타임처럼 매 프레임 바뀌는 값만 갱신한다.</summary>
    public void Refresh()
    {
        if (boundEffect == null)
            return;

        float remaining = boundEffect.GetRemainingCooldown(boundItem);
        float duration = boundEffect.cooldownSeconds;

        if (cooldownFillImage != null)
        {
            // 버프의 DurationFill과는 반대 방향 - 쿨타임은 "남은 시간"만큼 덮어서, 준비될수록(남은
            // 시간이 줄수록) 덮개가 걷히는 식으로 보이는 게 다른 ARPG 스킬 쿨타임 표시와 같은 관례다.
            cooldownFillImage.fillAmount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
        }

        if (remainingText != null)
        {
            bool showText = remaining > 0f && remaining <= remainingTextThreshold;
            remainingText.gameObject.SetActive(showText);
            if (showText)
                remainingText.text = CooldownTextFormat.Format(remaining); // 스킬 슬롯과 같은 표기 규칙
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        BuffTooltipUI.Instance?.Hide();
    }

    private void OnEnable()
    {
        // 툴팁이 떠 있는 동안 설정에서 언어를 바꾸면 다음에 다시 올릴 때가 아니라 바로 반영되게 한다.
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= OnLanguageChanged;

        hovered = false;

        // 쿨타임이 끝나 슬롯이 꺼질 때 마우스가 그 위에 있었다면 OnPointerExit이 오지 않고 사라질 수
        // 있어서, 툴팁이 화면에 남는 것을 막기 위해 여기서도 닫는다(BuffIconSlot과 같은 이유).
        BuffTooltipUI.Instance?.Hide();
    }

    private void OnLanguageChanged(GameLanguage _)
    {
        if (hovered)
            ShowTooltip();
    }

    /// <summary>쿨타임은 스택 개념이 없어서 스택 수는 항상 1로 넘긴다(아이콘의 숫자는 남은 초다).</summary>
    private void ShowTooltip()
    {
        if (boundEffect == null || BuffTooltipUI.Instance == null)
            return;

        BuffTooltipUI.Instance.Show(BuffTextComposer.BuildName(boundEffect, 1),
                                    BuffTextComposer.BuildDescription(boundEffect, 1));
    }
}

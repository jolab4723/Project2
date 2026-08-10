using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 쿨타임 표시 아이콘 한 칸. BuffIconSlot과 같은 시각 구조(아이콘+라디얼 오버레이+테두리)를
/// 쿨타임 진행 중인 아이템 하나에 맞게 변형한 파생 스크립트.
/// CooldownIconUIContainer가 쿨타임 진행 중인 아이템 수만큼 이 컴포넌트를 인스턴스화해서 값만 채운다.
/// </summary>
public class CooldownIconSlot : MonoBehaviour
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
                remainingText.text = Mathf.CeilToInt(remaining).ToString();
        }
    }
}

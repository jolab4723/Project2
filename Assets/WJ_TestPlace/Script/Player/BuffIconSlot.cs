using System.Linq;
using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버프 아이콘 UI 한 칸. 아이콘·남은 지속시간(라디얼)·스택 수·디버프 테두리색 표시를 담당한다.
/// BuffIconUIContainer가 활성 버프 하나당 이 컴포넌트를 인스턴스화해서 값만 채운다.
/// </summary>
public class BuffIconSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image borderImage;

    [Tooltip("Image Type을 Filled/Radial 360으로 설정해서 써야 한다.")]
    [SerializeField] private Image durationFillImage;

    [SerializeField] private TextMeshProUGUI stackText;

    [Header("테두리 색상")]
    [SerializeField] private Color buffBorderColor = new Color(0.25f, 0.85f, 0.35f);
    [SerializeField] private Color debuffBorderColor = new Color(0.85f, 0.25f, 0.25f);

    private BuffInstance boundInstance;

    /// <summary>이 슬롯에 버프 인스턴스를 연결한다. 아이콘/테두리색처럼 바인딩 시점에만 바뀌는 값을 채운다.</summary>
    public void Bind(BuffInstance instance)
    {
        boundInstance = instance;
        IBuffSource source = instance?.source;

        if (iconImage != null)
        {
            iconImage.sprite = source?.BuffIcon;
            iconImage.enabled = iconImage.sprite != null;
            // 아이템 아이콘은 인벤토리 칸 비율(itemWidth x itemHeight)에 맞춰 만들어져서 정사각형이 아닌
            // 경우가 흔하다(예: 세로로 긴 무기). 비율 유지 없이 정사각형에 늘리면 그림이 찌그러지고,
            // 빈 여백에 뒤쪽 배경(Border)이 그대로 드러나 보인다. 원본 비율대로 안에 맞춰 넣는다.
            iconImage.preserveAspect = true;
        }

        if (borderImage != null)
            borderImage.color = IsDebuff(source) ? debuffBorderColor : buffBorderColor;

        Refresh();
    }

    /// <summary>남은 지속시간/스택 수처럼 매 프레임 바뀌는 값만 갱신한다. 목록 재구성 없이 매 프레임 호출된다.</summary>
    public void Refresh()
    {
        IBuffSource source = boundInstance?.source;
        if (source == null)
            return;

        if (durationFillImage != null)
        {
            // "남은 시간"이 아니라 "경과 시간"만큼 덮는다. 방금 걸렸을 때(경과 0)는 안 덮여서 아이콘이
            // 선명하게 보이고, 만료가 가까워질수록 서서히 덮여서 경고 역할을 한다.
            // 영구 지속 버프는 애초에 시간 개념이 없으므로 전혀 덮지 않는다(fillAmount 0) - 이전에는
            // 반대로 항상 1로 채워서 대부분의(영구) 버프 아이콘이 흰색 오버레이에 계속 덮여 있었다.
            bool permanent = source.IsPermanent || source.Duration <= 0f;
            durationFillImage.fillAmount = permanent
                ? 0f
                : 1f - Mathf.Clamp01(boundInstance.remainingTime / source.Duration);
        }

        if (stackText != null)
        {
            bool showStack = boundInstance.stackCount > 1;
            stackText.gameObject.SetActive(showStack);
            if (showStack)
                stackText.text = boundInstance.stackCount.ToString();
        }
    }

    /// <summary>스탯 효과 값이 하나라도 음수면 디버프로 취급한다(별도 디버프 플래그가 없어서 값으로 판정).</summary>
    private static bool IsDebuff(IBuffSource source)
    {
        return source?.StatEffects != null && source.StatEffects.Any(effect => effect.value < 0f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IBuffSource source = boundInstance?.source;
        if (source == null || BuffTooltipUI.Instance == null)
            return;

        BuffTooltipUI.Instance.Show(source.BuffDisplayName, source.BuffDescription);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        BuffTooltipUI.Instance?.Hide();
    }

    private void OnDisable()
    {
        // 슬롯이 목록 재구성 등으로 비활성화될 때 마우스가 그 위에 있었다면 OnPointerExit이
        // 호출되지 않고 사라질 수 있어서, 툴팁이 화면에 남는 것을 막기 위해 여기서도 닫는다.
        BuffTooltipUI.Instance?.Hide();
    }
}

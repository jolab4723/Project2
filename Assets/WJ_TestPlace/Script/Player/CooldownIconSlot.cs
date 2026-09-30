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
/// 쓴다 - 발동 버프의 이름/증감 스탯/설명 규칙을 보존한다. SW 수정: 파동·폭발은 효과 라벨 DB의 번역 설명을 쓴다.
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
    // SW 수정: 파동·폭발은 실제 소유자 상태에서 시간을 읽으며 버프 SO의 기존 계약은 유지한다.
    private UniqueEffectSO boundEffect;
    private PlayerItemEffectState ownerEffects;
    private bool hasSuppliedCooldown;
    private float suppliedRemainingCooldown;
    private float suppliedCooldownDuration;

    // 고유효과 대신 거너 아크 레이저(진화1)의 발사 간격을 표시할 때 쓰는 바인딩.
    private GunnerSkillController boundArcLaser;
    private string arcLaserTooltipName;
    private string arcLaserTooltipDescription;

    /// <summary>지금 이 슬롯 위에 마우스가 올라와 있는지(언어 변경 시 툴팁을 다시 그릴지 판단용).</summary>
    private bool hovered;

    /// <summary>SW 수정: 기존 싱글 버프 슬롯의 두 인자 바인딩 계약을 유지한다.</summary>
    public void Bind(ItemInstance item, TriggeredBuffUniqueEffectSO effect)
        => Bind(item, (UniqueEffectSO)effect, null);

    /// <summary>
    /// SW 수정: 싱글의 진행 중인 버프·파동·폭발과 실제 소유자 상태를 연결해 기존 아이콘·테두리·쿨다운을 표시한다.
    /// 숫자로 전달된 쿨다운 표시를 해제하고 기존 효과나 소유자 상태를 읽는 표시 방식으로 초기화한다.
    /// </summary>
    public void Bind(ItemInstance item, UniqueEffectSO effect, PlayerItemEffectState effects)
    {
        hasSuppliedCooldown = false;
        boundItem = item;
        boundEffect = effect;
        ownerEffects = effects;
        boundArcLaser = null;

        if (iconImage != null)
        {
            iconImage.sprite = effect != null ? effect.icon : null;
            iconImage.enabled = iconImage.sprite != null;
            iconImage.preserveAspect = true;
        }

        if (borderImage != null)
            borderImage.color = borderColor;

        Refresh();
    }

    /// <summary>
    /// 원본 툴팁·아이콘은 유지하고 확정된 소유자 쿨다운 값으로 표시한다.
    /// SW 수정: 전달받은 남은 시간과 전체 쿨다운을 0 이상으로 보정해 표시에 사용할 값으로 저장한다.
    /// </summary>
    public void Bind(ItemInstance item, UniqueEffectSO effect, float remaining, float duration)
    {
        Bind(item, effect, (PlayerItemEffectState)null);
        hasSuppliedCooldown = true;
        suppliedRemainingCooldown = Mathf.Max(0f, remaining);
        suppliedCooldownDuration = Mathf.Max(0f, duration);
        Refresh();
    }

    /// <summary>
    /// 이 슬롯에 거너 아크 레이저(진화1)의 발사 간격을 연결한다. 아이콘은 아크 버스터 스킬 아이콘,
    /// 툴팁은 호출 쪽에서 만든 이름/설명을 쓴다. 남은 시간은 매 프레임 컨트롤러에서 직접 읽는다.
    /// SW 수정: 숫자로 전달된 쿨다운 표시를 해제하고 아크 레이저 컨트롤러에서 시간을 읽도록 초기화한다.
    /// </summary>
    public void BindArcLaser(GunnerSkillController controller, Sprite icon, string tooltipName, string tooltipDescription)
    {
        hasSuppliedCooldown = false;
        boundItem = null;
        boundEffect = null;
        boundArcLaser = controller;
        arcLaserTooltipName = tooltipName;
        arcLaserTooltipDescription = tooltipDescription;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;
        }

        if (borderImage != null)
            borderImage.color = borderColor;

        Refresh();
    }

    /// <summary>
    /// SW 수정: 싱글 버프는 기존 SO, 처형 파동·스타 브리처 폭발은 실제 소유자 상태에서 남은 시간을 읽으며 아크 레이저 표시도 유지한다.
    /// 숫자로 전달받은 쿨다운이 있으면 해당 값을 우선 사용해 오버레이와 남은 시간을 갱신한다.
    /// </summary>
    public void Refresh()
    {
        float remaining;
        float duration;

        if (hasSuppliedCooldown)
        {
            remaining = suppliedRemainingCooldown;
            duration = suppliedCooldownDuration;
        }
        else if (boundArcLaser != null)
        {
            if (!boundArcLaser.TryGetArcLaserCooldown(out remaining, out duration, out _))
                remaining = 0f;
        }
        else
        {
            if (boundEffect == null)
                return;

            remaining = boundEffect is TriggeredBuffUniqueEffectSO triggered
                ? triggered.GetRemainingCooldown(boundItem) : ownerEffects?.GetRemainingCooldown(boundItem) ?? 0f;
            duration = boundEffect switch
            {
                TriggeredBuffUniqueEffectSO buff => buff.cooldownSeconds,
                PhaseHarvesterWaveUniqueEffectSO wave => wave.cooldownSeconds,
                StarBreacherExplosionUniqueEffectSO explosion => explosion.cooldownSeconds,
                _ => 0f,
            };
        }

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

    /// <summary>SW 수정: 싱글 버프는 기존 스택 1 툴팁을 사용하고 파동·폭발은 효과 DB의 현재 언어·계수 설명을 표시한다.</summary>
    private void ShowTooltip()
    {
        if (BuffTooltipUI.Instance == null)
            return;

        if (boundArcLaser != null)
        {
            BuffTooltipUI.Instance.Show(arcLaserTooltipName, arcLaserTooltipDescription);
            return;
        }

        if (boundEffect == null)
            return;

        if (boundEffect is IBuffSource source)
        {
            BuffTooltipUI.Instance.Show(BuffTextComposer.BuildName(source, 1),
                BuffTextComposer.BuildDescription(source, 1));
            return;
        }
        UniqueEffectLabelDatabaseSO labels = Resources.Load<UniqueEffectLabelDatabaseSO>(
            "DataFiles/ItemData/3. GeneratedAssets/LabelData/UniqueEffectLabelDatabase");
        string id = boundItem?.definition?.uniqueEffectId;
        string effectName = labels != null && labels.TryGetName(id, out string translatedName)
            ? translatedName : boundEffect.EffectName;
        string description = labels != null ? labels.GetDescription(id, boundEffect.coefficients) : null;
        if (string.IsNullOrWhiteSpace(description)) description = boundEffect.EffectDescription;
        BuffTooltipUI.Instance.Show(effectName, description);
    }
}

using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ItemSystem;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform tooltipRect;
    [Header("패널 / 배경")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image overlayImage;
    [SerializeField] private Image itemImage;

    [Header("이름 / 레어리티 / 분류")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemRarityText;
    [SerializeField] private TextMeshProUGUI itemCategoryText;

    [Header("스탯")]
    [SerializeField] private TextMeshProUGUI mainStatText;
    [SerializeField] private TextMeshProUGUI subStat1Text;
    [SerializeField] private TextMeshProUGUI subStat2Text;
    [SerializeField] private TextMeshProUGUI subStat3Text;
    [SerializeField] private TextMeshProUGUI subStat4Text;
    [SerializeField] private TextMeshProUGUI elementBonusText;

    [Header("고유 효과")]
    [SerializeField] private TextMeshProUGUI uniqueEffectNameText;
    [SerializeField] private TextMeshProUGUI uniqueEffectDescriptionText;

    [Header("가격 / 사이즈")]
    [SerializeField] private TextMeshProUGUI itemPriceText;
    [SerializeField] private TextMeshProUGUI itemSizeText;

    [SerializeField] private Vector2 offset = new Vector2(15f, 15f);

    [Header("섹션 레이아웃 (사이즈 자동 조정)")]
    [SerializeField] private RectTransform section1;
    [Tooltip("스탯 영역. 비활성화된 서브스탯 수만큼 높이가 줄어든다")]
    [SerializeField] private RectTransform section2;
    [Tooltip("고유 효과 영역. 고유 효과가 없으면 비활성화 + 높이 0")]
    [SerializeField] private RectTransform section3;
    [Tooltip("가격 / 사이즈 영역")]
    [SerializeField] private RectTransform section4;
    [Tooltip("배경 이미지 RectTransform")]
    [SerializeField] private RectTransform backgroundRect;
    [Tooltip("섹션 사이 간격")]
    [SerializeField] private float sectionSpacing = 5f;
    [Tooltip("Section1 위쪽 여백")]
    [SerializeField] private float topPadding = 10f;
    [Tooltip("마지막 섹션 아래 여백")]
    [SerializeField] private float bottomPadding = 10f;

    private float section1BaseHeight;
    private float section2BaseHeight;
    private float section3BaseHeight;
    private float section4BaseHeight;

    private TextMeshProUGUI[] subStatTexts;

    private RectTransform ActiveTooltipRect
    {
        get
        {
            if (tooltipRect != null)
                return tooltipRect;

            return tooltipPanel != null ? tooltipPanel.GetComponent<RectTransform>() : null;
        }
    }

    private void Awake()
    {
        Instance = this;
        subStatTexts = new[] { subStat1Text, subStat2Text, subStat3Text, subStat4Text };


        // 씬에 배치된 상태(모든 옵션이 켜져있는 상태)의 섹션 높이를 기준값으로 캐프처
        section1BaseHeight = GetRectHeight(section1);
        section2BaseHeight = GetRectHeight(section2);
        section3BaseHeight = GetRectHeight(section3);
        section4BaseHeight = GetRectHeight(section4);

        HideTooltip(); // 게임 시작 시 무조건 숨김
    }

    private void Update()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf && Mouse.current != null)
        {
            MoveTooltip(Mouse.current.position.ReadValue());
        }
    }

    public void ShowTooltip(ItemInstance itemData)
    {
        if (itemData == null || itemData.definition == null)
        {
            Debug.LogWarning("[TooltipManager] ShowTooltip에 유효하지 않은 itemData가 전달되었습니다.");
            return;
        }

        if (tooltipPanel == null)
        {
            Debug.LogWarning("[TooltipManager] tooltipPanel이 연결되지 않았습니다.");
            return;
        }

        tooltipPanel.SetActive(true);

        var def = itemData.definition;

        ApplyColor(itemData);

        if (itemImage != null)
            itemImage.sprite = def.icon;

        if (itemNameText != null)
            itemNameText.text = itemData.upgradeLevel > 0 ? $"+{itemData.upgradeLevel} {def.itemName}" : def.itemName;

        if (itemRarityText != null)
            itemRarityText.text = BuildRarityText(itemData);

        if (itemCategoryText != null)
            itemCategoryText.text = BuildCategoryText(def);

        if (mainStatText != null)
            mainStatText.text = BuildMainStatText(itemData);

        ApplySubStats(itemData);
        ApplyElementBonus(itemData);
        ApplyUniqueEffect(def);

        if (itemPriceText != null)
            itemPriceText.text = def.sellPrice.ToString("N0");

        if (itemSizeText != null)
            itemSizeText.text = $"{def.itemWidth} X {def.itemHeight}";

        // 섹션 높이 계산 -> 순서대로 배치 -> 배경/테두리/오버레이 크기 반영
        ApplyLayout(def.uniqueEffect != null);

        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel.GetComponent<RectTransform>());

        if (Mouse.current != null)
            MoveTooltip(Mouse.current.position.ReadValue());
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    private void MoveTooltip(Vector2 screenPosition)
    {
        RectTransform targetTooltipRect = ActiveTooltipRect;
        RectTransform targetCanvasRect = canvasRect != null
            ? canvasRect
            : canvas != null ? canvas.transform as RectTransform : null;

        if (targetTooltipRect == null || targetCanvasRect == null)
            return;

        Camera eventCamera = GetCanvasCamera();
        Vector2 targetScreenPosition = screenPosition + offset;

        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            targetTooltipRect.position = new Vector3(
                targetScreenPosition.x,
                targetScreenPosition.y,
                targetTooltipRect.position.z);
        }
        else if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                     targetCanvasRect,
                     targetScreenPosition,
                     eventCamera,
                     out Vector3 worldPoint))
        {
            targetTooltipRect.position = worldPoint;
        }

        ClampTooltipInsideCanvas(targetTooltipRect, targetCanvasRect);
    }

    private Camera GetCanvasCamera()
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }


    // 툴팁이 화면 바깥을 벗어난 경우 벗어나지 않도록 화면 안쪽으로 밀어주는 메서드입니다.
    private void ClampTooltipInsideCanvas(RectTransform targetTooltipRect, RectTransform targetCanvasRect)
    {
        Vector3[] tooltipCorners = new Vector3[4];
        Vector3[] canvasCorners = new Vector3[4];

        targetTooltipRect.GetWorldCorners(tooltipCorners);
        targetCanvasRect.GetWorldCorners(canvasCorners);

        Vector3 correction = Vector3.zero;

        if (tooltipCorners[2].x > canvasCorners[2].x)
            correction.x = canvasCorners[2].x - tooltipCorners[2].x;
        else if (tooltipCorners[0].x < canvasCorners[0].x)
            correction.x = canvasCorners[0].x - tooltipCorners[0].x;

        if (tooltipCorners[2].y > canvasCorners[2].y)
            correction.y = canvasCorners[2].y - tooltipCorners[2].y;
        else if (tooltipCorners[0].y < canvasCorners[0].y)
            correction.y = canvasCorners[0].y - tooltipCorners[0].y;

        targetTooltipRect.position += correction;
    }

    public void ApplyColor(ItemInstance itemData)
    {
        string gradeColorCode = ItemDisplayNames.GradeColorHex.TryGetValue(itemData.definition.rarity, out var code) ? code : "#FFFFFF";

        if (!ColorUtility.TryParseHtmlString(gradeColorCode, out Color gradeColor))
            return;

        if (borderImage != null)
            borderImage.color = gradeColor;

        if (itemRarityText != null)
            itemRarityText.color = gradeColor;

        if (overlayImage != null)
        {
            Color overlayColor = gradeColor;
            overlayColor.a = 0.26f;
            overlayImage.color = overlayColor;
        }
    }

    /// <summary>
    /// "레어리티" 또는 (무기 + 인챈트 속성이 있을 때) "레어리티 | 속성"
    /// </summary>
    private string BuildRarityText(ItemInstance itemData)
    {
        var def = itemData.definition;
        string rarityName = ItemDisplayNames.GradeNames.TryGetValue(def.rarity, out var r) ? r : def.rarity.ToString();

        if (def.category == ItemCategory.Weapon && itemData.rolledElement != ElementType.None)
        {
            string elementName = ItemDisplayNames.ElementNames.TryGetValue(itemData.rolledElement, out var e) ? e : itemData.rolledElement.ToString();
            return $"{rarityName} | {elementName}";
        }

        return rarityName;
    }

    /// <summary>
    /// 무기: "클래스 | 무기종류", 방어구: "방어구 | 부위", 그 외(유물/포션): 카테고리명
    /// </summary>
    private string BuildCategoryText(ItemDefinitionSO def)
    {
        switch (def.category)
        {
            case ItemCategory.Weapon:
                string className = ItemDisplayNames.ClassNames.TryGetValue(def.characterClass, out var c) ? c : def.characterClass.ToString();
                string weaponName = ItemDisplayNames.WeaponNames.TryGetValue(def.weaponType, out var w) ? w : def.weaponType.ToString();
                return $"{className} | {weaponName}";

            case ItemCategory.Armor:
                string armorCategoryName = ItemDisplayNames.CategoryNames.TryGetValue(def.category, out var ac) ? ac : def.category.ToString();
                string armorPartName = ItemDisplayNames.ArmorNames.TryGetValue(def.armorType, out var a) ? a : def.armorType.ToString();
                return $"{armorCategoryName} | {armorPartName}";

            default:
                return ItemDisplayNames.CategoryNames.TryGetValue(def.category, out var t) ? t : def.category.ToString();
        }
    }

    private string BuildMainStatText(ItemInstance itemData)
    {
        var mainOptions = itemData.GetEffectiveMainOptions();
        if (mainOptions.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var opt in mainOptions)
            sb.AppendLine(FormatStat(opt.statType, opt.value));

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// rolledSubStats 중 "슬롯 기반" 서브옵션은 SubStat1~4에, 속성 보너스(항상 마지막에 추가됨)는 별도로 분리.
    /// (ItemDataCreator.Generate: 슬롯 루프 -> ResolveElementalBonus 순서로 채워지므로 마지막 항목이 속성 보너스)
    /// </summary>
    private (List<RolledSubStat> regular, RolledSubStat elemental) SplitSubStats(ItemInstance itemData)
    {
        var def = itemData.definition;
        var all = itemData.rolledSubStats;

        if (def.HasElementalBonusSlot && all.Count > 0)
            return (all.GetRange(0, all.Count - 1), all[all.Count - 1]);

        return (all, null);
    }

    private void ApplySubStats(ItemInstance itemData)
    {
        var (regular, _) = SplitSubStats(itemData);

        for (int i = 0; i < subStatTexts.Length; i++)
        {
            var slot = subStatTexts[i];
            if (slot == null)
                continue;

            if (i < regular.Count)
            {
                slot.gameObject.SetActive(true);
                slot.text = FormatStat(regular[i].statType, regular[i].value);
            }
            else
            {
                slot.gameObject.SetActive(false);
            }
        }
    }

    private void ApplyElementBonus(ItemInstance itemData)
    {
        if (elementBonusText == null)
            return;

        var (_, elemental) = SplitSubStats(itemData);

        if (elemental != null)
        {
            elementBonusText.gameObject.SetActive(true);
            elementBonusText.text = FormatStat(elemental.statType, elemental.value);
        }
        else
        {
            elementBonusText.gameObject.SetActive(false);
        }
    }

    private void ApplyUniqueEffect(ItemDefinitionSO def)
    {
        bool hasEffect = def.uniqueEffect != null;

        if (uniqueEffectNameText != null)
        {
            uniqueEffectNameText.gameObject.SetActive(hasEffect);
            if (hasEffect)
                uniqueEffectNameText.text = def.uniqueEffect.name;
        }

        if (uniqueEffectDescriptionText != null)
        {
            uniqueEffectDescriptionText.gameObject.SetActive(hasEffect);
            if (hasEffect)
                uniqueEffectDescriptionText.text = def.uniqueEffect.EffectDescription;
        }
    }

    /// <summary>
    /// ItemDisplayNames.StatNames는 Percent 계열 이름에 "%"가 이미 포함되어 있어 여기서 별도로 붙이지 않는다.
    /// </summary>
    private string FormatStat(ItemSystem.StatType statType, float value)
    {
        string name = ItemDisplayNames.StatNames.TryGetValue(statType, out var n) ? n : statType.ToString();
        string sign = value >= 0 ? "+" : "";
        return $"{name} {sign}{value:F1}";
    }

    /// <summary>
    /// 섹션별 높이를 계산해 위에서 아래로 순서대로 배치하고(sectionSpacing 간격),
    /// 전체 높이(활성 섹션 합 + 간격 + verticalPadding)를 패널/배경/테두리/오버레이에 반영한다.
    /// </summary>
    private void ApplyLayout(bool hasUniqueEffect)
    {
        // Section2: 비활성화된 서브스탯/속성보너스 높이만큼 감소
        float hidden = 0f;
        foreach (var slot in subStatTexts)
            hidden += GetHiddenHeight(slot);
        hidden += GetHiddenHeight(elementBonusText);

        float h1 = section1BaseHeight;
        float h2 = Mathf.Max(0f, section2BaseHeight - hidden);
        float h3 = hasUniqueEffect ? section3BaseHeight : 0f;
        float h4 = section4BaseHeight;

        // Section3: 고유 효과 없으면 비활성화 (높이 0으로 취급되어 배치에서 빠짐)
        if (section3 != null)
            section3.gameObject.SetActive(hasUniqueEffect);

        SetHeight(section2, h2);

        // 활성 섹션을 위에서부터 순서대로 배치
        float y = topPadding;
        y += PlaceSection(section1, h1, y);
        y += PlaceSection(section2, h2, y);
        y += PlaceSection(section3, h3, y);
        y += PlaceSection(section4, h4, y);

        // 마지막 섹션 뒤에 붙은 간격 하나를 빼고 아래 여백을 더해 전체 높이 확정
        float totalHeight = y - sectionSpacing + bottomPadding;

        SetHeight(tooltipPanel.GetComponent<RectTransform>(), totalHeight);
        AlignBackdrop(backgroundRect, totalHeight);
        AlignBackdrop(borderImage != null ? borderImage.rectTransform : null, totalHeight);
        // overlayImage는 Background의 자식이라 스트레치 앵커로 자동 추적되므로 여기서 건드리지 않음
    }

    /// <summary>
    /// 섹션의 세로 앵커/피벗을 상단으로 통일하고 yOffset 위치에 배치.
    /// 다음 섹션이 시작할 위치를 위해 (높이 + 간격)을 반환하며, 높이가 0이면 배치 없이 0을 반환(간격도 건너뜀).
    /// 가로 앵커/피벗/위치는 씬에 설정된 값을 그대로 유지한다.
    /// </summary>
    private float PlaceSection(RectTransform section, float height, float yOffset)
    {
        if (section == null || height <= 0f)
            return 0f;

        section.anchorMin = new Vector2(section.anchorMin.x, 1f);
        section.anchorMax = new Vector2(section.anchorMax.x, 1f);
        section.pivot = new Vector2(section.pivot.x, 1f);
        section.anchoredPosition = new Vector2(section.anchoredPosition.x, -yOffset);
        return height + sectionSpacing;
    }

    /// <summary>배경/테두리/오버레이를 세로 상단 기준으로 정렬하고 높이를 맞춘다. 가로는 씬 설정 유지.</summary>
    private void AlignBackdrop(RectTransform rect, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(rect.anchorMin.x, 1f);
        rect.anchorMax = new Vector2(rect.anchorMax.x, 1f);
        rect.pivot = new Vector2(rect.pivot.x, 1f);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0f);
        SetHeight(rect, height);
    }

    private static void SetHeight(RectTransform rect, float height)
    {
        if (rect == null)
            return;

        var size = rect.sizeDelta;
        size.y = height;
        rect.sizeDelta = size;
    }

    private static float GetRectHeight(RectTransform rect) => rect != null ? rect.rect.height : 0f;

    /// <summary>
    /// target이 비활성화 상태면 그 오브젝트의 LayoutElement.preferredHeight + 부모 VerticalLayoutGroup.spacing을 반환. 반환.
    /// 활성화 상태면 0 (빼 것 없음).
    /// </summary>
    private float GetHiddenHeight(TextMeshProUGUI target)
    {
        if (target == null || target.gameObject.activeSelf)
            return 0f;

        float height = 0f;

        var layoutElement = target.GetComponent<LayoutElement>();
        if (layoutElement != null && layoutElement.preferredHeight > 0f)
            height += layoutElement.preferredHeight;

        if (target.transform.parent != null)
        {
            var group = target.transform.parent.GetComponent<VerticalLayoutGroup>();
            if (group != null)
                height += group.spacing;
        }

        return height;
    }
}


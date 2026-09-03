using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TooltipUI : MonoBehaviour
{
    private const float FadeDuration = 0.1f;
    private const string ItemLabelResourcePath =
        "DataFiles/ItemData/3. GeneratedAssets/LabelData/ItemLabelDatabase";
    private const string UniqueEffectLabelResourcePath =
        "DataFiles/ItemData/3. GeneratedAssets/LabelData/UniqueEffectLabelDatabase";
    private const string IncreaseColorHex = "#55D66B";
    private const string DecreaseColorHex = "#FF5B5B";
    private const string EqualColorHex = "#9A9A9A";
    [Header("데이터")]
    [Tooltip("비워두면 definition.itemName을 그대로 사용")]
    [SerializeField] private ItemLabelDatabaseSO itemLabels;

    [Tooltip("비워두면 uniqueEffect.EffectName/EffectDescription을 그대로 사용")]
    [SerializeField] private UniqueEffectLabelDatabaseSO uniqueEffectLabels;

    [Header("패널 / 배경")]
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

    [Header("섹션 레이아웃")]
    [SerializeField] private RectTransform section1;

    [Tooltip("스탯 영역. 비활성화된 서브스탯 수만큼 높이가 줄어든다.")]
    [SerializeField] private RectTransform section2;

    [Tooltip("고유 효과가 없으면 비활성화되고 높이가 0이 된다.")]
    [SerializeField] private RectTransform section3;

    [SerializeField] private RectTransform section4;

    [Tooltip("배경 이미지 RectTransform")]
    [SerializeField] private RectTransform backgroundRect;

    [SerializeField] private float sectionSpacing = 5f;
    [SerializeField] private float topPadding = 30f;
    [SerializeField] private float bottomPadding = 10f;

    private TextMeshProUGUI[] subStatTexts;

    private float section1BaseHeight;
    private float section2BaseHeight;
    private float section3BaseHeight;
    private float section4BaseHeight;
    private float uniqueEffectDescriptionBaseHeight;

    private readonly List<CanvasGroup> fadeGroups = new List<CanvasGroup>();
    private Tween fadeTween;
    private float fadeAlpha;
    private bool initialized;

    public RectTransform RootRect => transform as RectTransform;

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        StopFade();
    }

    /// <summary>
    /// 처음 배치된 UI 높이를 기준값으로 저장한다.
    /// Show가 여러 번 호출돼도 최초 한 번만 실행한다.
    /// </summary>
    private void EnsureInitialized()
    {
        if (initialized)
            return;

        InitializeFadeGroups();
        SetFadeAlpha(0f);
        itemLabels ??= Resources.Load<ItemLabelDatabaseSO>(ItemLabelResourcePath);
        uniqueEffectLabels ??=
            Resources.Load<UniqueEffectLabelDatabaseSO>(UniqueEffectLabelResourcePath);

        subStatTexts = new[]
        {
            subStat1Text,
            subStat2Text,
            subStat3Text,
            subStat4Text
        };

        section1BaseHeight = GetRectHeight(section1);
        section2BaseHeight = GetRectHeight(section2);
        section3BaseHeight = GetRectHeight(section3);
        section4BaseHeight = GetRectHeight(section4);

        uniqueEffectDescriptionBaseHeight =
            uniqueEffectDescriptionText != null
                ? GetRectHeight(
                    uniqueEffectDescriptionText.rectTransform)
                : 0f;

        initialized = true;
        itemImage.preserveAspect = true;
    }

    /// <summary>
    /// 비교 정보 없이 일반 아이템 툴팁을 표시한다.
    /// 포션, 유물처럼 장비 최종 스탯 비교가 필요 없는 아이템에 사용한다.
    /// </summary>
    public bool Show(ItemInstance itemData)
    {
        return ShowInternal(itemData, null);
    }

    /// <summary>
    /// 후보 아이템의 원래 메인 옵션을 표시하고,
    /// 비교 결과가 있으면 최종 스탯 증감량만 색상과 함께 덧붙인다.
    /// </summary>
    public bool Show(
        ItemInstance itemData,
        ItemMainStatComparisonResult result)
    {
        return ShowInternal(itemData, result);
    }

    /// <summary>
    /// 공통 아이템 정보를 채우고, 비교 결과가 전달된 경우에만
    /// 아이템 메인 옵션 뒤에 최종 스탯 증감량을 추가하는 내부 표시 함수다.
    /// </summary>
    private bool ShowInternal(
        ItemInstance itemData,
        ItemMainStatComparisonResult result)
    {
        if (itemData == null || itemData.definition == null)
        {
            Debug.LogWarning(
                "[TooltipUI] 유효하지 않은 itemData가 전달되었습니다.");

            return false;
        }

        if (RootRect == null)
        {
            Debug.LogWarning(
                "[TooltipUI] 루트에 RectTransform이 없습니다.");

            return false;
        }

        EnsureInitialized();

        StopFade();
        SetFadeAlpha(0f);
        gameObject.SetActive(true);

        ItemDefinitionSO definition =
            itemData.definition;

        ApplyColor(itemData);

        if (itemImage != null)
            itemImage.sprite = definition.icon;

        if (itemNameText != null)
        {
            string displayName = GetItemName(definition);

            itemNameText.text =
                itemData.upgradeLevel > 0
                    ? $"+{itemData.upgradeLevel} {displayName}"
                    : displayName;
        }

        if (itemRarityText != null)
        {
            itemRarityText.text = BuildRarityText(itemData);
        }

        if (itemCategoryText != null)
        {
            itemCategoryText.text = BuildCategoryText(definition);
        }

        if (mainStatText != null)
        {
            // 증감량에 색상 태그를 사용한다.
            mainStatText.richText = true;

            mainStatText.text = result != null
                ? BuildComparedMainStatText(result)
                : BuildMainStatText(itemData);

        }

        var (regularSubStats, elementalSubStat) = SplitSubStats(itemData);
        ApplySubStats(regularSubStats);
        ApplyElementBonus(elementalSubStat);
        ApplyUniqueEffect(definition);

        if (itemPriceText != null)
        {
            itemPriceText.text = definition.sellPrice.ToString("N0");
        }

        if (itemSizeText != null)
        {
            itemSizeText.text =
                $"{definition.itemWidth} X " +
                $"{definition.itemHeight}";
        }

        ApplyLayout(
            definition.uniqueEffect != null);

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            RootRect);

        FadeTo(1f);

        return true;
    }

    /// <summary>
    /// 표시 중인 툴팁을 짧게 페이드아웃한 뒤 비활성화한다.
    /// </summary>
    public void Hide()
    {
        if (!gameObject.activeSelf)
            return;

        EnsureInitialized();
        FadeTo(0f, () => gameObject.SetActive(false));
    }

    /// <summary>
    /// 루트 Canvas 경계 아래의 실제 시각 자식마다 CanvasGroup을 준비한다.
    /// 현재 World Space Canvas 구성에서는 루트 그룹의 중간 알파가 자식 UI에 반영되지 않아 직접 적용한다.
    /// </summary>
    private void InitializeFadeGroups()
    {
        fadeGroups.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            CanvasGroup group = child.GetComponent<CanvasGroup>();

            if (group == null)
                group = child.gameObject.AddComponent<CanvasGroup>();

            fadeGroups.Add(group);
        }
    }

    /// <summary>
    /// 하나의 DOTween 값으로 모든 시각 자식의 알파를 함께 변경한다.
    /// </summary>
    private void FadeTo(float targetAlpha, TweenCallback onComplete = null)
    {
        StopFade();

        fadeTween = DOTween
            .To(
                () => fadeAlpha,
                SetFadeAlpha,
                targetAlpha,
                FadeDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                fadeTween = null;
                onComplete?.Invoke();
            });
    }

    private void StopFade()
    {
        fadeTween?.Kill();
        fadeTween = null;
    }

    private void SetFadeAlpha(float alpha)
    {
        fadeAlpha = alpha;

        foreach (CanvasGroup group in fadeGroups)
        {
            if (group != null)
                group.alpha = alpha;
        }
    }

    /// <summary>라벨 DB에 itemId가 없는 아이템(예: 구 스킴의 TEST 아이템)은 definition.itemName으로 그대로 폴백한다.</summary>
    private string GetItemName(ItemDefinitionSO definition)
    {
        if (itemLabels != null && itemLabels.TryGetName(definition.itemId, out string name))
            return name;

        return definition.itemName;
    }

    /// <summary>라벨 DB에 없는 고유 효과(예: 번역 전/테스트용)는 effect.EffectName으로 그대로 폴백한다.</summary>
    private string GetUniqueEffectName(UniqueEffectSO effect)
    {
        if (uniqueEffectLabels != null && uniqueEffectLabels.TryGetName(effect.name, out string name))
            return name;

        return effect.EffectName;
    }

    /// <summary>
    /// 라벨 DB의 언어별 설명 템플릿에 coefficients를 대입해 완성 문구를 만든다.
    /// coefficients는 언어와 무관하므로 effect가 이미 들고 있는 값을 그대로 재사용한다.
    /// </summary>
    private string GetUniqueEffectDescription(UniqueEffectSO effect)
    {
        return uniqueEffectLabels != null
            ? uniqueEffectLabels.GetDescription(effect.name, effect.coefficients)
            : effect.EffectDescription;
    }

    private void ApplyColor(ItemInstance itemData)
    {
        string gradeColorCode =
            ItemDisplayNames.GradeColorHex.TryGetValue(
                itemData.definition.rarity,
                out string code)
                ? code
                : "#FFFFFF";

        if (!ColorUtility.TryParseHtmlString(
                gradeColorCode,
                out Color gradeColor))
        {
            return;
        }

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

    private string BuildRarityText(
        ItemInstance itemData)
    {
        ItemDefinitionSO definition =
            itemData.definition;

        string rarityName = GetDisplayName(
            ItemDisplayNames.GradeNames,
            definition.rarity);

        if (definition.category == ItemCategory.Weapon &&
            itemData.rolledElement != ElementType.None)
        {
            string elementName = GetDisplayName(
                ItemDisplayNames.ElementNames,
                itemData.rolledElement);

            return $"{rarityName} | {elementName}";
        }

        return rarityName;
    }

    private string BuildCategoryText(
        ItemDefinitionSO definition)
    {
        switch (definition.category)
        {
            case ItemCategory.Weapon:
                return $"{GetDisplayName(ItemDisplayNames.ClassNames, definition.characterClass)} | " +
                       GetDisplayName(ItemDisplayNames.WeaponNames, definition.weaponType);

            case ItemCategory.Armor:
                return $"{GetDisplayName(ItemDisplayNames.CategoryNames, definition.category)} | " +
                       GetDisplayName(ItemDisplayNames.ArmorNames, definition.armorType);

            default:
                return GetDisplayName(
                    ItemDisplayNames.CategoryNames,
                    definition.category);
        }
    }

    private string BuildMainStatText(
        ItemInstance itemData)
    {
        List<RolledSubStat> mainOptions =
            itemData.GetEffectiveMainOptions();

        if (mainOptions.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();

        foreach (RolledSubStat option in mainOptions)
        {
            if (option == null)
                continue;

            builder.AppendLine(
                FormatStat(
                    option.statType,
                    option.value));
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 후보 아이템의 강화 적용 메인 옵션은 기존 형식으로 유지하고,
    /// 그 뒤에 현재 장비 대비 최종 스탯 증감량을 덧붙인다.
    /// </summary>
    private string BuildComparedMainStatText(
        ItemMainStatComparisonResult comparisonResult)
    {
        if (comparisonResult == null)
            return string.Empty;

        string itemMainStatText =
            BuildMainStatText(comparisonResult.CandidateItem);

        if (comparisonResult.Rows == null ||
            comparisonResult.Rows.Count == 0)
        {
            return itemMainStatText;
        }

        var builder = new StringBuilder(itemMainStatText);
        bool addedDelta = false;

        foreach (MainStatComparisonResult row in comparisonResult.Rows)
        {
            string deltaText = FormatComparisonDelta(row);

            if (string.IsNullOrEmpty(deltaText))
                continue;

            if (builder.Length > 0)
                builder.Append(addedDelta ? "\n" : "  ");

            builder.Append(deltaText);
            addedDelta = true;
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 최종 스탯 증가량은 초록색, 감소량은 빨간색으로 만든다.
    /// 긴 스탯 이름 대신 게임 툴팁에서 자주 사용하는 화살표로 짧게 표시한다.
    /// 동일한 값은 음수와 혼동되지 않도록 회색 긴 대시로 표시한다.
    /// 예: ▲ 6, ▼ 0.15, —
    /// </summary>
    private string FormatComparisonDelta(
        MainStatComparisonResult row)
    {
        if (row == null)
            return string.Empty;

        if (row.Direction == MainStatComparisonDirection.Equal)
            return $"<color={EqualColorHex}>—</color>";

        string colorHex = row.Direction ==
            MainStatComparisonDirection.Increase ? IncreaseColorHex : DecreaseColorHex;

        string numberFormat = row.StatType == StatType.moveSpeedFlat
            ? "F2"
            : "F0";

        string directionSymbol = row.Direction ==
            MainStatComparisonDirection.Increase
                ? "▲"
                : "▼";

        return
            $"<color={colorHex}>{directionSymbol} {Mathf.Abs(row.Delta).ToString(numberFormat)}</color>";
    }

    private (
        List<RolledSubStat> regular,
        RolledSubStat elemental)
        SplitSubStats(ItemInstance itemData)
    {
        ItemDefinitionSO definition =
            itemData.definition;

        List<RolledSubStat> all =
            itemData.rolledSubStats;

        if (definition.HasElementalBonusSlot &&
            all.Count > 0)
        {
            return (
                all.GetRange(0, all.Count - 1),
                all[all.Count - 1]);
        }

        return (all, null);
    }

    private void ApplySubStats(List<RolledSubStat> regularSubStats)
    {
        for (int i = 0;
             i < subStatTexts.Length;
             i++)
        {
            TextMeshProUGUI slot =
                subStatTexts[i];

            if (slot == null)
                continue;

            if (i < regularSubStats.Count)
            {
                slot.gameObject.SetActive(true);

                slot.text =
                    FormatStat(
                        regularSubStats[i].statType,
                        regularSubStats[i].value);
            }
            else
            {
                slot.gameObject.SetActive(false);
            }
        }
    }

    private void ApplyElementBonus(RolledSubStat elementalSubStat)
    {
        if (elementBonusText == null)
            return;

        if (elementalSubStat != null)
        {
            elementBonusText.gameObject.SetActive(true);

            elementBonusText.text = FormatStat(
                elementalSubStat.statType,
                elementalSubStat.value);
        }
        else
        {
            elementBonusText.gameObject.SetActive(false);
        }
    }

    private void ApplyUniqueEffect(
        ItemDefinitionSO definition)
    {
        bool hasEffect =
            definition.uniqueEffect != null;

        if (uniqueEffectNameText != null)
        {
            uniqueEffectNameText.gameObject.SetActive(
                hasEffect);

            if (hasEffect)
            {
                uniqueEffectNameText.text =
                    GetUniqueEffectName(definition.uniqueEffect);
            }
        }

        if (uniqueEffectDescriptionText != null)
        {
            uniqueEffectDescriptionText.gameObject.SetActive(
                hasEffect);

            if (hasEffect)
            {
                uniqueEffectDescriptionText.text =
                    GetUniqueEffectDescription(definition.uniqueEffect);

                uniqueEffectDescriptionText.ForceMeshUpdate();
            }
        }
    }

    private string FormatStat(
        StatType statType,
        float value)
    {
        string statName = GetDisplayName(ItemDisplayNames.StatNames, statType);

        string sign =
            value >= 0f ? "+" : string.Empty;

        return $"{statName} {sign}{value:F1}";
    }

    private static string GetDisplayName<T>(
        IReadOnlyDictionary<T, string> names,
        T value)
    {
        return names.TryGetValue(value, out string displayName)
            ? displayName
            : value.ToString();
    }

    private void ApplyLayout(bool hasUniqueEffect)
    {
        float hiddenHeight = 0f;

        foreach (TextMeshProUGUI slot in subStatTexts)
            hiddenHeight += GetHiddenHeight(slot);

        hiddenHeight += GetHiddenHeight(elementBonusText);

        float section1Height = section1BaseHeight;

        float section2Height = Mathf.Max(0f, section2BaseHeight - hiddenHeight);
        float section3Height = hasUniqueEffect
            ? CalculateUniqueEffectSectionHeight()
            : 0f;

        float section4Height = section4BaseHeight;

        if (section3 != null)
            section3.gameObject.SetActive(hasUniqueEffect);

        SetHeight(section2, section2Height);
        SetHeight(section3, section3Height);

        float currentY = topPadding;

        currentY += PlaceSection(section1, section1Height, currentY);
        currentY += PlaceSection(section2, section2Height, currentY);
        currentY += PlaceSection(section3, section3Height, currentY);
        currentY += PlaceSection(section4, section4Height, currentY);

        float totalHeight = currentY - sectionSpacing + bottomPadding;

        SetHeight(RootRect, totalHeight);

        AlignBackdrop(backgroundRect, totalHeight);

        AlignBackdrop(borderImage != null ? borderImage.rectTransform : null, totalHeight);
    }

    private float CalculateUniqueEffectSectionHeight()
    {
        float descriptionHeight = uniqueEffectDescriptionBaseHeight;

        if (uniqueEffectDescriptionText != null)
        {
            descriptionHeight = Mathf.Clamp(
                uniqueEffectDescriptionText.preferredHeight,
                0f,
                uniqueEffectDescriptionBaseHeight);

            RectTransform descriptionRect =
                uniqueEffectDescriptionText.rectTransform;

            float oldPivotY = descriptionRect.pivot.y;

            float oldHeight = descriptionRect.sizeDelta.y;

            float oldAnchoredY = descriptionRect.anchoredPosition.y;

            float preservedTopY =
                oldAnchoredY +
                (1f - oldPivotY) * oldHeight;

            AlignToTop(descriptionRect, preservedTopY);
            SetHeight(descriptionRect, descriptionHeight);
        }

        float fixedHeight =
            section3BaseHeight -
            uniqueEffectDescriptionBaseHeight;

        return fixedHeight + descriptionHeight;
    }

    private float PlaceSection(
        RectTransform section,
        float height,
        float yOffset)
    {
        if (section == null || height <= 0f)
            return 0f;

        AlignToTop(section, -yOffset);
        return height + sectionSpacing;
    }

    private void AlignBackdrop(
        RectTransform target,
        float height)
    {
        if (target == null)
            return;

        AlignToTop(target, 0f);
        SetHeight(target, height);
    }

    private static void AlignToTop(RectTransform target, float anchoredY)
    {
        target.anchorMin = new Vector2(target.anchorMin.x, 1f);
        target.anchorMax = new Vector2(target.anchorMax.x, 1f);
        target.pivot = new Vector2(target.pivot.x, 1f);
        target.anchoredPosition = new Vector2(target.anchoredPosition.x, anchoredY);
    }

    private static void SetHeight(RectTransform target, float height)
    {
        if (target == null)
            return;

        Vector2 size = target.sizeDelta;

        size.y = height;
        target.sizeDelta = size;
    }

    private static float GetRectHeight(RectTransform target)
    {
        return target != null ? target.rect.height : 0f;
    }

    private float GetHiddenHeight(TextMeshProUGUI target)
    {
        if (target == null || target.gameObject.activeSelf)
            return 0f;

        float height = 0f;

        LayoutElement layoutElement = target.GetComponent<LayoutElement>();

        if (layoutElement != null && layoutElement.preferredHeight > 0f)
            height += layoutElement.preferredHeight;

        if (target.transform.parent != null)
        {
            VerticalLayoutGroup layoutGroup = target.transform.parent.GetComponent<VerticalLayoutGroup>();

            if (layoutGroup != null)
                height += layoutGroup.spacing;
        }

        return height;
    }

}

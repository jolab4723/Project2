using System.Collections.Generic;
using System.Text;
using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TooltipUI : MonoBehaviour
{

    private const string IncreaseColorHex = "#55D66B";
    private const string DecreaseColorHex = "#FF5B5B";
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

    private bool initialized;

    public RectTransform RootRect => transform as RectTransform;

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 처음 배치된 UI 높이를 기준값으로 저장한다.
    /// Show가 여러 번 호출돼도 최초 한 번만 실행한다.
    /// </summary>
    private void EnsureInitialized()
    {
        if (initialized)
            return;

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
    }

    /// <summary>
    /// 비교 정보 없이 일반 아이템 툴팁을 표시한다.
    /// 포션, 유물, 현재 장비 툴팁에서 사용한다.
    /// </summary>
    public bool Show(ItemInstance itemData)
    {
        return Show(itemData, null);
    }

    /// <summary>
    /// 아이템 정보와 선택적인 메인 스탯 비교 결과를 표시한다.
    /// 
    /// comparisonResult가 null이면 일반 메인 스탯을 표시하고,
    /// 값이 있으면 후보 수치 옆에 증감량을 표시한다.
    /// </summary>
    public bool Show(ItemInstance itemData, ItemMainStatComparisonResult result)
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

        gameObject.SetActive(true);

        ItemDefinitionSO definition =
            itemData.definition;

        ApplyColor(itemData);

        if (itemImage != null)
            itemImage.sprite = definition.icon;

        if (itemNameText != null)
        {
            itemNameText.text =
                itemData.upgradeLevel > 0
                    ? $"+{itemData.upgradeLevel} {definition.itemName}"
                    : definition.itemName;
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
                    ? BuildComparedMainStatText(result) : BuildMainStatText(itemData);

        }

        ApplySubStats(itemData);
        ApplyElementBonus(itemData);
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

        return true;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
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

        string rarityName =
            ItemDisplayNames.GradeNames.TryGetValue(
                definition.rarity,
                out string rarity)
                ? rarity
                : definition.rarity.ToString();

        if (definition.category == ItemCategory.Weapon &&
            itemData.rolledElement != ElementType.None)
        {
            string elementName =
                ItemDisplayNames.ElementNames.TryGetValue(
                    itemData.rolledElement,
                    out string element)
                    ? element
                    : itemData.rolledElement.ToString();

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
            {
                string className =
                    ItemDisplayNames.ClassNames.TryGetValue(
                        definition.characterClass,
                        out string characterClass)
                        ? characterClass
                        : definition.characterClass.ToString();

                string weaponName =
                    ItemDisplayNames.WeaponNames.TryGetValue(
                        definition.weaponType,
                        out string weapon)
                        ? weapon
                        : definition.weaponType.ToString();

                return $"{className} | {weaponName}";
            }

            case ItemCategory.Armor:
            {
                string categoryName =
                    ItemDisplayNames.CategoryNames.TryGetValue(
                        definition.category,
                        out string category)
                        ? category
                        : definition.category.ToString();

                string armorName =
                    ItemDisplayNames.ArmorNames.TryGetValue(
                        definition.armorType,
                        out string armor)
                        ? armor
                        : definition.armorType.ToString();

                return $"{categoryName} | {armorName}";
            }

            default:
                return ItemDisplayNames.CategoryNames.TryGetValue(
                    definition.category,
                    out string displayName)
                        ? displayName
                        : definition.category.ToString();
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
    /// 후보 아이템의 메인 스탯 수치와
    /// 현재 장비 대비 증감량을 한 줄씩 생성한다.
    /// </summary>
    private string BuildComparedMainStatText(ItemMainStatComparisonResult comparisonResult)
    {
        if (comparisonResult == null ||
            comparisonResult.Rows == null ||
            comparisonResult.Rows.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (MainStatComparisonResult row in comparisonResult.Rows)
        {
            // 인벤토리 아이템을 장착했을 때의 실제 수치
            builder.Append(FormatStat( row.StatType, row.CandidateValue));

            string deltaText =
                FormatComparisonDelta(row);

            if (!string.IsNullOrEmpty(deltaText))
            {
                builder.Append("  ");
                builder.Append(deltaText);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 증가량은 초록색, 감소량은 빨간색으로 만든다.
    /// 동일한 값은 빈 문자열을 반환해 표시하지 않는다.
    /// </summary>
    private string FormatComparisonDelta(
        MainStatComparisonResult row)
    {
        if (row == null || row.Direction ==  MainStatComparisonDirection.Equal)
            return string.Empty;

        string colorHex = row.Direction ==
            MainStatComparisonDirection.Increase ? IncreaseColorHex : DecreaseColorHex;

        string sign = row.Delta > 0f ? "+" : string.Empty;

        return $"<color={colorHex}>({sign}{row.Delta:F1})</color>";
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

    private void ApplySubStats(
        ItemInstance itemData)
    {
        var (regular, _) =
            SplitSubStats(itemData);

        for (int i = 0;
             i < subStatTexts.Length;
             i++)
        {
            TextMeshProUGUI slot =
                subStatTexts[i];

            if (slot == null)
                continue;

            if (i < regular.Count)
            {
                slot.gameObject.SetActive(true);

                slot.text =
                    FormatStat(
                        regular[i].statType,
                        regular[i].value);
            }
            else
            {
                slot.gameObject.SetActive(false);
            }
        }
    }

    private void ApplyElementBonus(
        ItemInstance itemData)
    {
        if (elementBonusText == null)
            return;

        var (_, elemental) =
            SplitSubStats(itemData);

        if (elemental != null)
        {
            elementBonusText.gameObject.SetActive(true);

            elementBonusText.text =
                FormatStat(
                    elemental.statType,
                    elemental.value);
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
                    definition.uniqueEffect.EffectName;
            }
        }

        if (uniqueEffectDescriptionText != null)
        {
            uniqueEffectDescriptionText.gameObject.SetActive(
                hasEffect);

            if (hasEffect)
            {
                uniqueEffectDescriptionText.text =
                    definition.uniqueEffect.EffectDescription;

                uniqueEffectDescriptionText.ForceMeshUpdate();
            }
        }
    }

    private string FormatStat(
        StatType statType,
        float value)
    {
        string statName =
            ItemDisplayNames.StatNames.TryGetValue(
                statType,
                out string displayName)
                ? displayName
                : statType.ToString();

        string sign =
            value >= 0f ? "+" : string.Empty;

        return $"{statName} {sign}{value:F1}";
    }

    private void ApplyLayout(bool hasUniqueEffect)
    {
        float hiddenHeight = 0f;

        foreach (TextMeshProUGUI slot in subStatTexts)
            hiddenHeight += GetHiddenHeight(slot);

        hiddenHeight += GetHiddenHeight(elementBonusText);

        float section1Height = section1BaseHeight;

        float section2Height =
            Mathf.Max(
                0f,
                section2BaseHeight - hiddenHeight);

        float section3Height = 0f;

        if (hasUniqueEffect)
        {
            section3Height = CalculateUniqueEffectSectionHeight();
        }

        float section4Height = section4BaseHeight;

        if (section3 != null)
        {
            section3.gameObject.SetActive( hasUniqueEffect);
        }

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

        AlignBackdrop(
            borderImage != null
                ? borderImage.rectTransform
                : null,
            totalHeight);
    }

    private float CalculateUniqueEffectSectionHeight()
    {
        float descriptionHeight =
            uniqueEffectDescriptionBaseHeight;

        if (uniqueEffectDescriptionText != null)
        {
            descriptionHeight =
                Mathf.Clamp(
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

            descriptionRect.anchorMin =
                new Vector2(descriptionRect.anchorMin.x, 1f);

            descriptionRect.anchorMax =
                new Vector2(descriptionRect.anchorMax.x, 1f);

            descriptionRect.pivot =
                new Vector2(descriptionRect.pivot.x, 1f);

            descriptionRect.anchoredPosition =
                new Vector2(descriptionRect.anchoredPosition.x, preservedTopY);

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

        section.anchorMin =
            new Vector2(section.anchorMin.x, 1f);

        section.anchorMax =
            new Vector2(section.anchorMax.x, 1f);

        section.pivot =
            new Vector2(section.pivot.x,1f);

        section.anchoredPosition =
            new Vector2(section.anchoredPosition.x, -yOffset);

        return height + sectionSpacing;
    }

    private void AlignBackdrop(
        RectTransform target,
        float height)
    {
        if (target == null)
            return;

        target.anchorMin =
            new Vector2(target.anchorMin.x, 1f);

        target.anchorMax =
            new Vector2(target.anchorMax.x, 1f);

        target.pivot =
            new Vector2(target.pivot.x, 1f);

        target.anchoredPosition =
            new Vector2(target.anchoredPosition.x, 0f);

        SetHeight(target, height);
    }

    private static void SetHeight(
        RectTransform target,
        float height)
    {
        if (target == null)
            return;

        Vector2 size =
            target.sizeDelta;

        size.y = height;
        target.sizeDelta = size;
    }

    private static float GetRectHeight(
        RectTransform target)
    {
        return target != null
            ? target.rect.height
            : 0f;
    }

    private float GetHiddenHeight(
        TextMeshProUGUI target)
    {
        if (target == null ||
            target.gameObject.activeSelf)
        {
            return 0f;
        }

        float height = 0f;

        LayoutElement layoutElement =
            target.GetComponent<LayoutElement>();

        if (layoutElement != null && layoutElement.preferredHeight > 0f)
        {
            height += layoutElement.preferredHeight;
        }

        if (target.transform.parent != null)
        {
            VerticalLayoutGroup layoutGroup =
                target.transform.parent
                    .GetComponent<VerticalLayoutGroup>();

            if (layoutGroup != null)
                height += layoutGroup.spacing;
        }

        return height;
    }
}
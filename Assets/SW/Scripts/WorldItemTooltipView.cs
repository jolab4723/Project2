using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class WorldItemTooltipView : MonoBehaviour
{
    [SerializeField] private GameObject tooltipPanel;

    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemCategoryText;
    [SerializeField] private TextMeshProUGUI itemRarityText;

    [SerializeField] private Image itemImage;
    [SerializeField] private Image borderImage;

    [SerializeField] private RectTransform tooltipRect;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;

    [SerializeField]
    private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);

    private Transform currentTarget;

    private void Awake()
    {
        Hide();
    }
    public void Show(ItemInstance item, Transform target)
    {
        if (item?.definition == null || target == null)
        {
            Hide();
            return;
        }

        currentTarget = target;

        ItemDefinitionSO definition = item.definition;

        itemImage.sprite = definition.icon;
        itemImage.enabled = definition.icon != null;
        itemImage.preserveAspect = true;

        itemNameText.text = item.upgradeLevel > 0
            ? $"+{item.upgradeLevel} {definition.itemName}"
            : definition.itemName;

        itemCategoryText.text =
            ItemDisplayNames.CategoryNames.TryGetValue(
                definition.category,
                out string categoryName)
                ? categoryName
                : definition.category.ToString();

        itemRarityText.text =
            ItemDisplayNames.GradeNames.TryGetValue(
                definition.rarity,
                out string rarityName)
                ? rarityName
                : definition.rarity.ToString();

        ApplyRarityColor(definition.rarity);

        tooltipPanel.SetActive(true);
        UpdatePosition();
    }

    private void ApplyRarityColor(ItemRarity rarity)
    {
        string colorHex =
            ItemDisplayNames.GradeColorHex.TryGetValue(
                rarity,
                out string mappedColor)
                ? mappedColor
                : "#FFFFFF";

        if (!ColorUtility.TryParseHtmlString(colorHex, out Color color))
            color = Color.white;

        if (itemRarityText != null)
            itemRarityText.color = color;

        if (borderImage != null)
            borderImage.color = color;
    }

    private void LateUpdate()
    {
        if (currentTarget == null)
        {
            Hide();
            return;
        }
          
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null || tooltipRect == null)
            return;

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(
            currentTarget.position + worldOffset);

        if (screenPosition.z <= 0f)
            return;

        tooltipRect.position = screenPosition;
    }

    public void Hide()
    {
        currentTarget = null;

        if (itemImage != null)
        {
            itemImage.sprite = null;
            itemImage.enabled = false;
        }

        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }
}


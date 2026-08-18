using ItemSystem;
using TMPro;
using UnityEngine;


public class WorldItemTooltipView : MonoBehaviour
{
    [SerializeField] private GameObject tooltipPanel;

    [Tooltip("비워두면 definition.itemName을 그대로 사용")]
    [SerializeField] private ItemLabelDatabaseSO itemLabels;

    [SerializeField] private TextMeshProUGUI itemNameText;

    [SerializeField] private RectTransform tooltipRect;
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

        ItemDefinitionSO definition = item.definition;
        itemNameText.text = GetItemName(definition);
        ApplyRarityColor(definition.rarity);

        tooltipPanel.SetActive(true);
        currentTarget = target;
        UpdatePosition();
    }

    /// <summary>라벨 DB에 itemId가 없는 아이템(예: 구 스킴의 TEST 아이템)은 definition.itemName으로 그대로 폴백한다.</summary>
    private string GetItemName(ItemDefinitionSO definition)
    {
        if (itemLabels != null && itemLabels.TryGetName(definition.itemId, out string name))
            return name;

        return definition.itemName;
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

        if (itemNameText != null)
            itemNameText.color = color;

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
        Transform target = currentTarget;
        if (target == null)
        {
            Hide();
            return;
        }

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null || tooltipRect == null)
            return;

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(
            target.position + worldOffset);

        if (screenPosition.z <= 0f)
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);

            return;
        }

        if (tooltipPanel != null && !tooltipPanel.activeSelf)
            tooltipPanel.SetActive(true);

        tooltipRect.position = screenPosition;
    }

    public void Hide()
    {
        currentTarget = null;

        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }
}


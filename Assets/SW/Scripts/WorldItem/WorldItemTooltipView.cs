using DG.Tweening;
using ItemSystem;
using TMPro;
using UnityEngine;


public class WorldItemTooltipView : MonoBehaviour
{
    private const float FadeDuration = 0.1f;

    [SerializeField] private GameObject tooltipPanel;

    [Tooltip("비워두면 definition.itemName을 그대로 사용")]
    [SerializeField] private ItemLabelDatabaseSO itemLabels;

    [SerializeField] private TextMeshProUGUI itemNameText;

    [SerializeField] private RectTransform tooltipRect;
    [SerializeField] private Camera worldCamera;

    [SerializeField]
    private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);

    private Transform currentTarget;
    private CanvasGroup canvasGroup;
    private Canvas tooltipCanvas;

    private void Awake()
    {
        if (tooltipPanel == null)
            return;

        canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = tooltipPanel.AddComponent<CanvasGroup>();

        tooltipCanvas = tooltipPanel.GetComponentInParent<Canvas>()?.rootCanvas;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0f;
        tooltipPanel.SetActive(false);
    }

    private void OnDisable()
    {
        canvasGroup?.DOKill();
    }

    /// <summary>
    /// 월드 아이템의 간소화된 이름 툴팁을 표시한다.
    /// </summary>
    public void Show(ItemInstance item, Transform target)
    {
        if (item?.definition == null || target == null)
        {
            Hide();
            return;
        }

        bool wasActive = tooltipPanel.activeSelf;
        ItemDefinitionSO definition = item.definition;
        if (itemNameText != null)
            itemNameText.text = GetItemName(definition);

        ApplyRarityColor(definition.rarity);

        currentTarget = target;
        tooltipPanel.SetActive(true);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        UpdatePosition();

        canvasGroup.DOKill();
        if (!wasActive)
            canvasGroup.alpha = 0f;

        canvasGroup
            .DOFade(1f, FadeDuration)
            .SetUpdate(true)
            .SetLink(tooltipPanel);
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
        if (currentTarget != null)
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

        if (tooltipCanvas == null)
            return;

        var canvasRect = (RectTransform)tooltipCanvas.transform;
        Camera uiCamera = tooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : tooltipCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect, screenPosition, uiCamera, out Vector3 point))
            return;

        tooltipRect.position = point;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, tooltipRect);
        Rect area = canvasRect.rect;
        float x = Mathf.Clamp(bounds.center.x, area.xMin + bounds.extents.x,
            Mathf.Max(area.xMin + bounds.extents.x, area.xMax - bounds.extents.x));
        float y = Mathf.Clamp(bounds.center.y, area.yMin + bounds.extents.y,
            Mathf.Max(area.yMin + bounds.extents.y, area.yMax - bounds.extents.y));
        tooltipRect.position += canvasRect.TransformVector(
            new Vector3(x - bounds.center.x, y - bounds.center.y, 0f));
    }

    /// <summary>
    /// 월드 아이템 툴팁을 짧게 페이드아웃한 뒤 비활성화한다.
    /// </summary>
    public void Hide()
    {
        currentTarget = null;

        if (tooltipPanel == null || canvasGroup == null || !tooltipPanel.activeSelf)
            return;

        canvasGroup.DOKill();
        canvasGroup
            .DOFade(0f, FadeDuration)
            .SetUpdate(true)
            .SetLink(tooltipPanel)
            .OnComplete(() => tooltipPanel.SetActive(false));
    }
}


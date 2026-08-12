using UnityEngine;
using UnityEngine.UI;

public class GridHighlightUI : MonoBehaviour
{
    public RectTransform highlightRect;
    public Image highlightImage;
    [SerializeField] private RectTransform secondaryHighlightRect;
    [SerializeField] private Image secondaryHighlightImage;

    private static readonly Color MoveColor = new Color(0.34f, 0.84f, 0.64f, 0.60f);
    private static readonly Color InvalidColor = new Color(1f, 0.42f, 0.42f, 0.62f);
    private static readonly Color SwapMovingColor = new Color(0.36f, 0.78f, 1f, 0.60f);
    private static readonly Color SwapOtherColor = new Color(1f, 0.82f, 0.40f, 0.62f);
    private const float FillAlphaMultiplier = 0.25f;

    public void ShowMovePreview(
        InventoryCellRect rect,
        float cellSize,
        float spacing)
    {
        ShowRect(
            highlightRect,
            highlightImage,
            rect.X,
            rect.Y,
            rect.Width,
            rect.Height,
            MoveColor,
            cellSize,
            spacing);
        HideSecondaryHighlight();
    }

    public void ShowInvalidPreview(
        InventoryCellRect rect,
        float cellSize,
        float spacing)
    {
        ShowRect(
            highlightRect,
            highlightImage,
            rect.X,
            rect.Y,
            rect.Width,
            rect.Height,
            InvalidColor,
            cellSize,
            spacing);
        HideSecondaryHighlight();
    }

    public void ShowSwapPreview(
        InventorySwapPlan plan,
        float cellSize,
        float spacing)
    {
        EnsureSecondaryHighlight();

        ShowRect(
            highlightRect,
            highlightImage,
            plan.MovingTo.X,
            plan.MovingTo.Y,
            plan.MovingTo.Width,
            plan.MovingTo.Height,
            SwapMovingColor,
            cellSize,
            spacing);

        ShowRect(
            secondaryHighlightRect,
            secondaryHighlightImage,
            plan.OtherTo.X,
            plan.OtherTo.Y,
            plan.OtherTo.Width,
            plan.OtherTo.Height,
            SwapOtherColor,
            cellSize,
            spacing);
    }

    public void HideHighlight()
    {
        if (highlightRect != null)
            highlightRect.gameObject.SetActive(false);

        HideSecondaryHighlight();
    }

    private void EnsureSecondaryHighlight()
    {
        if (secondaryHighlightRect != null && secondaryHighlightImage != null)
            return;

        if (highlightRect == null)
            return;

        secondaryHighlightRect = Instantiate(
            highlightRect,
            highlightRect.parent);
        secondaryHighlightRect.name = $"{highlightRect.name}_SwapOther";

        secondaryHighlightImage = secondaryHighlightRect.GetComponent<Image>();
        if (secondaryHighlightImage == null)
            secondaryHighlightImage = secondaryHighlightRect.GetComponentInChildren<Image>();

        if (secondaryHighlightImage != null)
            secondaryHighlightImage.raycastTarget = false;

        secondaryHighlightRect.gameObject.SetActive(false);
    }

    private void HideSecondaryHighlight()
    {
        if (secondaryHighlightRect != null)
            secondaryHighlightRect.gameObject.SetActive(false);
    }

    private static void ShowRect(
        RectTransform rect,
        Image image,
        int gridX,
        int gridY,
        int width,
        int height,
        Color color,
        float cellSize,
        float spacing)
    {
        if (rect == null || image == null)
            return;

        float rectWidth = (width * cellSize) + ((width - 1) * spacing);
        float rectHeight = (height * cellSize) + ((height - 1) * spacing);

        rect.gameObject.SetActive(true);
        rect.sizeDelta = new Vector2(rectWidth, rectHeight);
        rect.anchoredPosition = new Vector2(
            gridX * (cellSize + spacing),
            -gridY * (cellSize + spacing));
        image.color = color;
        image.raycastTarget = false;

        Transform fillTransform = rect.Find("HighlightFill");
        if (fillTransform != null && fillTransform.TryGetComponent(out Image fillImage))
        {
            fillImage.color = new Color(
                color.r,
                color.g,
                color.b,
                color.a * FillAlphaMultiplier);
            fillImage.raycastTarget = false;
        }

        rect.SetAsLastSibling();
    }

}

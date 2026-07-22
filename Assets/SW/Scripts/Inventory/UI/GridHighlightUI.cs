using UnityEngine;
using UnityEngine.UI;

public class GridHighlightUI : MonoBehaviour
{
    public RectTransform highlightRect;
    public Image highlightImage;
    [SerializeField] private RectTransform secondaryHighlightRect;
    [SerializeField] private Image secondaryHighlightImage;

    private static readonly Color MoveColor = new Color(0f, 1f, 0f, 0.75f);
    private static readonly Color InvalidColor = new Color(1f, 0f, 0f, 0.65f);
    private static readonly Color SwapMovingColor = new Color(0f, 0.75f, 1f, 0.75f);
    private static readonly Color SwapOtherColor = new Color(1f, 0.85f, 0f, 0.75f);

    public void ShowHighlight(int width, int height, float cellSize, float spacing)
    {
        ShowRect(
            highlightRect,
            highlightImage,
            0,
            0,
            width,
            height,
            MoveColor,
            cellSize,
            spacing);
    }

    public void MoveHighlight(int gridX, int gridY, bool isValid, float cellSize, float spacing)
    {
        MoveRect(
            highlightRect,
            highlightImage,
            gridX,
            gridY,
            isValid ? MoveColor : InvalidColor,
            cellSize,
            spacing);
        HideSecondaryHighlight();
    }

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

    public void SetHighlightActive(bool isActive)
    {
        if (highlightRect != null && highlightRect.gameObject.activeSelf != isActive)
        {
            highlightRect.gameObject.SetActive(isActive);
        }

        if (!isActive)
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
        rect.SetAsLastSibling();
    }

    private static void MoveRect(
        RectTransform rect,
        Image image,
        int gridX,
        int gridY,
        Color color,
        float cellSize,
        float spacing)
    {
        if (rect == null || image == null)
            return;

        float step = cellSize + spacing;
        rect.anchoredPosition = new Vector2(gridX * step, -gridY * step);
        image.color = color;
        image.raycastTarget = false;
        rect.SetAsLastSibling();
    }
}

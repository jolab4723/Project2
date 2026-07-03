using UnityEngine;
using UnityEngine.UI;

public class GridHighlightUI : MonoBehaviour
{

    public RectTransform highlightRect;
    public Image highlightImage;


    public void ShowHighlight(int width, int height, float cellSize, float spacing)
    {
        highlightRect.gameObject.SetActive(true);

        float w = (width * cellSize) + ((width - 1) * spacing);
        float h = (height * cellSize) + ((height - 1) * spacing);
        highlightRect.sizeDelta = new Vector2(w, h);

        highlightRect.SetAsFirstSibling();
    }

    public void MoveHighlight(int gridX, int gridY, bool isValid, float cellSize, float spacing)
    {
        float step = cellSize + spacing;

        highlightRect.anchoredPosition = new Vector2(gridX * step, -gridY * step);
        highlightImage.color = isValid ? new Color(0, 1, 0, 0.6f) : new Color(1, 0, 0, 0.6f);
    }

    public void HideHighlight()
    {
        highlightRect.gameObject.SetActive(false);
    }

    public void SetHighlightActive(bool isActive)
    {
        if (highlightRect.gameObject.activeSelf != isActive)
        {
            highlightRect.gameObject.SetActive(isActive);
        }
    }
}

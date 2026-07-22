using UnityEngine;

public class WBH_PlayerIndicator : MonoBehaviour
{
    private Renderer indicatorRenderer;
    private Material material;

    private void Awake()
    {
        indicatorRenderer = GetComponent<Renderer>();
        material = indicatorRenderer.material;
    }

    public void SetColor(Color color)
    {
        material.color = color;
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KY_ElementRow : MonoBehaviour
{
    public TextMeshProUGUI totalValueText;

    [SerializeField] private Image backgroundImage;
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.3f;

    public void SetData(KY_StatTypeData data)
    {
        totalValueText.text = data.Total.ToString("0.##");
    }

    public void SetElementActive(bool isActive)
    {
        Color c = backgroundImage.color;
        c.a = isActive ? 1f : dimAlpha;
        backgroundImage.color = c;
    }
}
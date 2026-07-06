using UnityEngine;
using TMPro;

public class KY_StatRow : MonoBehaviour
{
    public TextMeshProUGUI totalValueText;
    public TextMeshProUGUI detailValueText;

    private Color baseColor;
    private Color equipColor;
    private Color buffColor;

    void Awake()
    {
        ColorUtility.TryParseHtmlString("#FFFFFF", out baseColor);
        ColorUtility.TryParseHtmlString("#FFD700", out equipColor);
        ColorUtility.TryParseHtmlString("#00FF99", out buffColor);
    }

    public void UpdateMode(KY_StatTypeData data, bool isDetailed)
    {
        totalValueText.text = data.Total.ToString();

        detailValueText.gameObject.SetActive(isDetailed);

        if (isDetailed)
        {
            detailValueText.text =
                $"(<color=#{ColorUtility.ToHtmlStringRGB(baseColor)}>{data.baseValue}</color>" +
                $" + <color=#{ColorUtility.ToHtmlStringRGB(equipColor)}>{data.equipValue}</color>" +
                $" + <color=#{ColorUtility.ToHtmlStringRGB(buffColor)}>{data.buffValue}</color>)";
        }
    }
}
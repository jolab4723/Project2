using UnityEngine;
using TMPro;

public class KY_StatRow : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI totalValueText;
    public TextMeshProUGUI detailValueText;

    private const string ValueFormat = "0.##";

    private Color baseColor;
    private Color equipColor;
    private Color buffColor;

    void Awake()
    {
        ColorUtility.TryParseHtmlString("#FFFFFF", out baseColor);
        ColorUtility.TryParseHtmlString("#FFD700", out equipColor);
        ColorUtility.TryParseHtmlString("#00FF99", out buffColor);
    }

    /// <summary>이 행이 어떤 스탯인지 나타내는 이름 텍스트를 설정한다. 값이 바뀌지 않는 한 한 번만 호출하면 된다.</summary>
    public void SetLabel(string label)
    {
        if (nameText != null)
            nameText.text = label;
    }

    public void UpdateMode(KY_StatTypeData data, bool isDetailed)
    {
        totalValueText.text = data.Total.ToString(ValueFormat);

        detailValueText.gameObject.SetActive(isDetailed);

        if (isDetailed)
        {
            detailValueText.text =
                $"(<color=#{ColorUtility.ToHtmlStringRGB(baseColor)}>{data.baseValue.ToString(ValueFormat)}</color>" +
                $" + <color=#{ColorUtility.ToHtmlStringRGB(equipColor)}>{data.equipValue.ToString(ValueFormat)}</color>" +
                $" + <color=#{ColorUtility.ToHtmlStringRGB(buffColor)}>{data.buffValue.ToString(ValueFormat)}</color>)";
        }
    }
}
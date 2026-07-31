using UnityEngine;
using TMPro;

public class KY_ElementRow : MonoBehaviour
{
    public TextMeshProUGUI totalValueText;

    public void SetData(KY_StatTypeData data)
    {
        totalValueText.text = data.Total.ToString("0.##");
    }
}
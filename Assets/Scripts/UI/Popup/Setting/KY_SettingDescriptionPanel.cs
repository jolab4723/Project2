using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>현재 호버한 설정 항목의 설명 텍스트를 표시하는 공용 패널이다.</summary>
public class KY_SettingDescriptionPanel : MonoBehaviour
{
    public static KY_SettingDescriptionPanel Instance;

    public TextMeshProUGUI descriptionText;

    void Awake()
    {
        Instance = this;
    }

    public void Show(string description)
    {
        gameObject.SetActive(true);
        descriptionText.text = description;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}

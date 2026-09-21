using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
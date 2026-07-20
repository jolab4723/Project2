using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KY_SettingDescriptionPanel : MonoBehaviour
{
    public static KY_SettingDescriptionPanel Instance;

    public Image icon;
    public Image background;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    void Awake()
    {
        Instance = this;
    }

    public void Show(string name, string description, Sprite iconSprite, Sprite bgSprite)
    {
        gameObject.SetActive(true);
        nameText.text = name;
        descriptionText.text = description;

        if (iconSprite != null)
            icon.sprite = iconSprite;

        if (bgSprite != null)
            background.sprite = bgSprite;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KY_SettingsTab : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI label;
    public Image background;

    private Color activeTextColor;
    private Color inactiveTextColor;
    private Color activeBgColor;
    private Color inactiveBgColor;

    private Toggle toggle;

    void Awake()
    {
        ColorUtility.TryParseHtmlString("#000000", out activeTextColor);
        ColorUtility.TryParseHtmlString("#FFFFFF", out inactiveTextColor);
        ColorUtility.TryParseHtmlString("#FFFFFF", out activeBgColor);
        ColorUtility.TryParseHtmlString("#7D7D7D", out inactiveBgColor);

        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    void OnToggleChanged(bool isOn)
    {
        panel.SetActive(isOn);
        label.color = isOn ? activeTextColor : inactiveTextColor;
        background.color = isOn ? activeBgColor : inactiveBgColor;
    }
}
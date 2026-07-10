using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KY_SettingsTab : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI label;

    public Color activeTextColor = Color.black;
    public Color inactiveTextColor = Color.white;
    public Color activeBgColor = Color.white;
    public Color inactiveBgColor = Color.gray;

    private Toggle toggle;

    void Awake()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    void OnToggleChanged(bool isOn)
    {
        if (panel != null)
            panel.SetActive(isOn);

        label.color = isOn ? activeTextColor : inactiveTextColor;
        label.fontStyle = isOn ? FontStyles.Bold : FontStyles.Normal;
    }

    public void RefreshVisual()
{
    bool isOn = toggle.isOn;
    label.color = isOn ? activeTextColor : inactiveTextColor;
    label.fontStyle = isOn ? FontStyles.Bold : FontStyles.Normal;
}
}
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

    // 패널의 씬 저장 상태가 토글 값과 어긋나 있어도(예: "게임 플레이" 패널이 active=True로 저장된 채
    // 토글은 isOn=False인 경우) OnToggleChanged가 실제로 값이 "바뀌는" 이벤트를 겪지 않는 한 방치되던
    // 문제 - 팝업을 열 때마다 항상 패널 활성 상태를 토글 값으로 강제 동기화한다.
    if (panel != null)
        panel.SetActive(isOn);

    label.color = isOn ? activeTextColor : inactiveTextColor;
    label.fontStyle = isOn ? FontStyles.Bold : FontStyles.Normal;
}
}
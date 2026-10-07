using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>설정 탭 토글의 선택 상태에 맞춰 연결된 패널과 탭 색상을 갱신한다.</summary>
public class KY_SettingsTab : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI label;

    public Color activeTextColor = Color.black;
    public Color inactiveTextColor = Color.white;
    public Color activeBgColor = Color.white;
    public Color inactiveBgColor = Color.gray;

    // WJ 이우진 추가(2026-10-06): 탭마다 줄 수가 달라(조작 탭은 메뉴 단축키까지 11줄) 스크롤 내용 높이를
    // 열린 탭의 마지막 줄에 맞춘다. 예전처럼 300 고정이면 화면 아래로 넘친 줄을 스크롤로도 볼 수 없다.
    [Tooltip("탭 내용의 마지막 줄 아래에 둘 여백")]
    [SerializeField] private float contentBottomPadding = 50f;

    private Toggle toggle;
    private readonly Vector3[] cornerBuffer = new Vector3[4];

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

        if (isOn)
            FitScrollContent();
    }

    /// <summary>
    /// WJ 이우진 추가(2026-10-06): 패널의 부모(스크롤 Content) 높이를 패널 안 가장 아래 UI까지로 맞추고 맨 위로 되돌린다.
    /// 화면 안에 다 들어가는 탭은 스크롤이 생기지 않아 기존 표시와 같다.
    /// </summary>
    private void FitScrollContent()
    {
        if (panel == null || !(panel.transform.parent is RectTransform content))
            return;

        float lowest = 0f;
        foreach (RectTransform rect in panel.GetComponentsInChildren<RectTransform>(false))
        {
            rect.GetWorldCorners(cornerBuffer);
            for (int i = 0; i < cornerBuffer.Length; i++)
                lowest = Mathf.Min(lowest, content.InverseTransformPoint(cornerBuffer[i]).y);
        }

        // Content의 기준점이 위쪽(pivot y = 1)이라 맨 위가 0, 아래로 갈수록 음수다.
        float topOffset = content.rect.height * (1f - content.pivot.y);
        float height = topOffset - lowest + contentBottomPadding;
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        ScrollRect scroll = content.GetComponentInParent<ScrollRect>();
        if (scroll != null)
            scroll.verticalNormalizedPosition = 1f;
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

    if (isOn)
        FitScrollContent();
}
}

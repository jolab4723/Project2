using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택형 설정 토글의 현재 선택 여부를 텍스트 색으로 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class KY_ToggleStateTextColor : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color unselectedColor = new(0.78f, 0.80f, 0.83f, 1f);

    private void Awake()
    {
        toggle ??= GetComponent<Toggle>();
        label ??= GetComponentInChildren<TMP_Text>(true);

        if (toggle != null)
            toggle.onValueChanged.AddListener(Refresh);

        Refresh(toggle != null && toggle.isOn);
    }

    private void OnEnable()
    {
        Refresh(toggle != null && toggle.isOn);
    }

    private void OnDestroy()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(Refresh);
    }

    private void Refresh(bool isSelected)
    {
        if (label != null)
            label.color = isSelected ? selectedColor : unselectedColor;
    }
}

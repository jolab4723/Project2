using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class SwTestPopupToggleButton : MonoBehaviour
{
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private GameObject targetPopup;
    [SerializeField] private GameObject otherPopup;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        // 복제한 버튼에 남아 있는 기존 테스트 이벤트를 제거하고
        // 이 버튼이 담당하는 팝업 토글만 연결한다.
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(TogglePopup);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(TogglePopup);
    }

    public void TogglePopup()
    {
        if (windowRoot == null || targetPopup == null)
        {
            Debug.LogWarning(
                "[SwTestPopupToggleButton] WindowRoot 또는 TargetPopup이 연결되지 않았습니다.",
                this);
            return;
        }

        bool shouldOpen =
            !windowRoot.activeSelf ||
            !targetPopup.activeSelf;

        if (otherPopup != null)
            otherPopup.SetActive(false);

        windowRoot.SetActive(true);
        targetPopup.SetActive(shouldOpen);
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 강화 테스트 창을 버튼으로 열고 닫기 위한 테스트 전용 컴포넌트입니다.
/// </summary>
public sealed class UpgradePanelToggle : MonoBehaviour
{
    [SerializeField] private Button toggleButton;
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private bool hideOnAwake = true;

    private void Awake()
    {
        if (toggleButton == null)
            toggleButton = GetComponent<Button>();

        if (hideOnAwake && upgradePanel != null)
            upgradePanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (toggleButton == null)
            toggleButton = GetComponent<Button>();

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleUpgradePanel);
    }

    private void OnDisable()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(ToggleUpgradePanel);
    }

    public void ToggleUpgradePanel()
    {
        if (upgradePanel == null)
        {
            Debug.LogWarning("[UpgradePanelToggle] 강화 패널이 연결되지 않았습니다.");
            return;
        }

        upgradePanel.SetActive(!upgradePanel.activeSelf);
    }
}

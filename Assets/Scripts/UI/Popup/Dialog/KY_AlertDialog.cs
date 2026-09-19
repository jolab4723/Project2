using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 확인(Confirm) 버튼 하나짜리 알림 다이얼로그.
/// KY_AlertData를 받아 표시하고, 버튼 클릭 시 콜백 실행 후 스스로 닫힘.
/// </summary>
public class KY_AlertDialog : KY_PopupBase
{
    [Header("Text")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text warningText;

    [Header("Button")]
    [SerializeField] private Button confirmButton;

    [Header("Effect")]
    [SerializeField] private KY_CurtainEffect curtainEffect;

    private KY_AlertData currentData;

    private void Awake()
    {
        confirmButton.onClick.AddListener(OnConfirmClicked);
        gameObject.SetActive(false);
    }

    public void Show(KY_AlertData data)
    {
        currentData = data;

        messageText.text = data.message;

        bool hasWarning = !string.IsNullOrEmpty(data.warningText);
        warningText.gameObject.SetActive(hasWarning);
        if (hasWarning)
            warningText.text = data.warningText;

        Open();
        curtainEffect?.Open();
    }

    private void OnConfirmClicked()
    {
        currentData.onConfirm?.Invoke();
        Close();
    }
}

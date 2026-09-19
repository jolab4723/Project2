using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 예/아니오 확인 다이얼로그.
// 버튼 클릭 시 콜백 실행 후 스스로 닫힘.
public class KY_ConfirmDialog : KY_PopupBase
{
    [Header("Text")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text warningText; // 프리팹에서 미리 빨간색으로 세팅해둠

    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private KY_DialogData currentData;

    private void Awake()
    {
        if (yesButton != null)
            yesButton.onClick.AddListener(OnYesClicked);
        if (noButton != null)
            noButton.onClick.AddListener(OnNoClicked);}

    public void Show(KY_DialogData data)
    {
        currentData = data;

        if (messageText != null)
            messageText.text = data.message;

        // 경고 문구 없으면 오브젝트 자체를 꺼서 레이아웃도 자연스럽게 줄어들게
        bool hasWarning = !string.IsNullOrEmpty(data.warningText);
        if (warningText != null)
        {
            warningText.gameObject.SetActive(hasWarning);
            if (hasWarning)
                warningText.text = data.warningText;
        }

        Open();
        GetComponent<KY_CurtainEffect>()?.Open();
    }

    private void OnYesClicked()
    {
        currentData.onYes?.Invoke();
        CloseThroughManager();
    }

    private void OnNoClicked()
    {
        currentData.onNo?.Invoke();
        CloseThroughManager();
    }

    private void CloseThroughManager()
    {
        if (KY_PopupManager.Instance != null)
            KY_PopupManager.Instance.Hide();
        else
            Close();
    }
}

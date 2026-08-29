using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 예/아니오 확인 다이얼로그.
// 버튼 클릭 시 콜백 실행 후 스스로 닫힘.
public class KY_ConfirmDialog : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text warningText; // 프리팹에서 미리 빨간색으로 세팅해둠

    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private KY_DialogData currentData;

    private void Awake()
    {
        yesButton.onClick.AddListener(OnYesClicked);
        noButton.onClick.AddListener(OnNoClicked);
        gameObject.SetActive(false); // 평소엔 꺼둔 상태로 시작
    }

    public void Show(KY_DialogData data)
    {
        currentData = data;

        titleText.text = data.title;
        messageText.text = data.message;

        // 경고 문구 없으면 오브젝트 자체를 꺼서 레이아웃도 자연스럽게 줄어들게
        bool hasWarning = !string.IsNullOrEmpty(data.warningText);
        warningText.gameObject.SetActive(hasWarning);
        if (hasWarning)
            warningText.text = data.warningText;

        gameObject.SetActive(true);
    }

    private void OnYesClicked()
    {
        currentData.onYes?.Invoke();
        Hide();
    }

    private void OnNoClicked()
    {
        currentData.onNo?.Invoke();
        Hide();
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}
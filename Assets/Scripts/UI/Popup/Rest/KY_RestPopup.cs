using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 휴식 팝업의 표시와 버튼 흐름을 담당한다.
/// 실제 체력·포션·크레딧 처리는 외부 시스템에서 계산한 값을 SetRecoveryPreview로 주입한다.
/// </summary>
public class KY_RestPopup : MonoBehaviour
{
    /// <summary>확인 버튼이 비용 검사를 통과해 실제로 눌렸을 때 발생. 실제 체력/포션/크레딧 처리는
    /// 외부(회복 NPC 연결 스크립트)가 구독해서 수행한다 - 이 팝업은 표시와 버튼 흐름만 담당한다.</summary>
    public event System.Action OnConfirmed;

    [Header("표시 텍스트")]
    [SerializeField] private TMP_Text healthRecoveryText;
    [SerializeField] private TMP_Text potionRecoveryText;
    [SerializeField] private TMP_Text costText;

    [Header("완료 알림(팝업이 닫히며 나오는 것이기에 팝업 외부에서 연결)")]
    [Tooltip("팝업 바깥에서 완료 문구와 배경을 함께 표시할 패널")]
    [SerializeField] private GameObject completionMessagePanel;
    [SerializeField] private TMP_Text completionMessageText;
    [Min(0f)] [SerializeField] private float completionMessageDuration = 2.5f;

    [Header("버튼")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("번역 (비워두면 Resources에서 공용 DB를 자동으로 찾아 쓴다)")]
    [Tooltip("비용/완료 메시지처럼 값이 들어가는 문구의 다국어 DB. 고정 문구는 각 텍스트의 UILabelText가 담당한다.")]
    [SerializeField] private UILabelDatabaseSO uiLabels;

    private const string UiLabelResourcePath = "DataFiles/UIData/3. GeneratedAssets/UILabelDatabase";

    private int healthAmount;
    private int potionAmount;
    private int cost;
    private int currentCredits;
    private Coroutine completionMessageRoutine;

    private void Awake()
    {
        if (uiLabels == null)
            uiLabels = Resources.Load<UILabelDatabaseSO>(UiLabelResourcePath);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(HandleConfirmClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(HandleCancelClicked);

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;

        HideCompletionMessage();
    }

    private void OnDestroy()
    {
        if (completionMessageRoutine != null)
            StopCoroutine(completionMessageRoutine);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(HandleConfirmClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(HandleCancelClicked);

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(GameLanguage _) => RefreshView();

    /// <summary>uiLabels에서 key 문구를 가져오되, DB가 없거나 매칭 실패면 한국어 폴백을 쓴다.</summary>
    private string L(string key, string korFallback)
    {
        if (uiLabels == null) return korFallback;
        string value = uiLabels.GetLabel(key);
        return string.IsNullOrEmpty(value) || value == key ? korFallback : value;
    }

    private void Update()
    {
        if (gameObject.activeSelf && Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HandleCancelClicked();
        }
    }

    /// <summary>팝업을 열고 현재 회복 미리보기 값을 표시한다.</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        HideCompletionMessage();
        RefreshView();
    }

    /// <summary>팝업을 닫는다.</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>외부 회복 시스템이 계산한 미리보기 값을 주입한다.</summary>
    public void SetRecoveryPreview(int health, int potions, int requiredCost, int credits)
    {
        healthAmount = Mathf.Max(0, health);
        potionAmount = Mathf.Max(0, potions);
        cost = Mathf.Max(0, requiredCost);
        currentCredits = Mathf.Max(0, credits);
        RefreshView();
    }

    private void RefreshView()
    {
        // "체력 회복"/"포션 충전" 같은 고정 문구는 각 슬롯의 라벨 텍스트(UILabelText)가 담당하므로
        // 여기서는 값만 넣는다 - 예전엔 값 텍스트에 문구까지 같이 넣어서 라벨이 두 번 보였다.
        if (healthRecoveryText != null)
            healthRecoveryText.text = $"+{healthAmount}";

        if (potionRecoveryText != null)
            potionRecoveryText.text = $"+{potionAmount}";

        if (costText != null)
            costText.text = string.Format(L("rest_ui.cost_format", "필요 크레딧 : {0}"), cost);

        if (confirmButton != null)
            confirmButton.interactable = currentCredits >= cost;
    }

    private void HandleConfirmClicked()
    {
        if (currentCredits < cost)
            return;

        if (completionMessageText != null)
            completionMessageText.text = string.Format(
                L("rest_ui.complete_message", "{0} 크레딧을 소비하여 체력 {1}, 포션 {2}개를 채웠습니다."),
                cost, healthAmount, potionAmount);

        if (completionMessagePanel != null)
            completionMessagePanel.SetActive(true);

<<<<<<< Updated upstream
        if (completionMessageRoutine != null)
            StopCoroutine(completionMessageRoutine);

        completionMessageRoutine = StartCoroutine(HideCompletionMessageAfterDelay());
=======
        // 완료 메시지가 뜬 채로 팝업이 안 닫히므로, 다시 눌러서 중복 적용되지 않도록 막는다.
        if (confirmButton != null)
            confirmButton.interactable = false;

        OnConfirmed?.Invoke();
>>>>>>> Stashed changes
    }

    private void HandleCancelClicked()
    {
        Close();
    }

    private void HideCompletionMessage()
    {
        if (completionMessageRoutine != null)
        {
            StopCoroutine(completionMessageRoutine);
            completionMessageRoutine = null;
        }

        if (completionMessagePanel != null)
            completionMessagePanel.SetActive(false);
    }

    private IEnumerator HideCompletionMessageAfterDelay()
    {
        yield return new WaitForSecondsRealtime(completionMessageDuration);
        completionMessageRoutine = null;
        HideCompletionMessage();
    }
}

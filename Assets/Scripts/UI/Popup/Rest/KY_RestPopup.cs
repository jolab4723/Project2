using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 휴식 팝업의 표시와 버튼 흐름을 담당한다.
/// 실제 체력·포션·크레딧 처리는 외부 시스템에서 계산한 값을 SetRecoveryPreview로 주입한다.
///
/// !! KY_PopupManager에 PopupType.Rest로 등록해서 여닫는다. 예전엔 매니저 밖에서 직접 SetActive하고
///    ESC도 이 클래스가 따로 폴링했는데, 그러면 매니저는 이 팝업이 열린 걸 모른 채 같은 ESC를
///    "열린 팝업 없음"으로 처리해 **일시정지를 같이 열어버렸다.** 이제 여닫기를 전부 매니저로 보내서
///    ESC 처리(Hide)와 배경 입력 차단(SetBackgroundInput)을 다른 팝업과 동일하게 탄다.
/// </summary>
public class KY_RestPopup : KY_PopupBase
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
    [Min(0f)] [SerializeField] private float completionMessageDuration = 1f;

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
    private Tween completionMessageTween;

    /// <summary>완료 알림 패널의 커튼 연출. 이걸 열어주지 않으면 패널을 켜도 세로로 접힌 채(스케일 0) 안 보인다.</summary>
    private KY_CurtainEffect completionCurtain;

    private void Awake()
    {
        if (uiLabels == null)
            uiLabels = Resources.Load<UILabelDatabaseSO>(UiLabelResourcePath);

        if (completionMessagePanel != null)
            completionCurtain = completionMessagePanel.GetComponent<KY_CurtainEffect>();

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
        completionMessageTween?.Kill();

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

    /// <summary>
    /// 팝업을 열고 현재 회복 미리보기 값을 표시한다.
    /// 직접 부르지 말고 KY_PopupManager.Show(PopupType.Rest)를 쓴다 - 매니저 스택에 올라가야
    /// ESC와 배경 입력 차단이 정상 동작한다.
    /// </summary>
    public override void Open()
    {
        base.Open();
        HideCompletionMessage();
        RefreshView();
    }

    /// <summary>
    /// 팝업을 닫는다. 매니저가 Hide()에서 호출한다.
    /// 이 클래스에서 스스로 닫아야 할 때는 Close()가 아니라 CloseThroughManager()를 쓴다 -
    /// Close()만 부르면 화면에선 사라지는데 매니저 스택에는 남아 배경 입력이 잠긴 채로 굳는다.
    /// </summary>
    public override void Close()
    {
        base.Close();
    }

    /// <summary>매니저 스택까지 정리하면서 닫는다. 매니저가 없으면 예전처럼 직접 닫는다.</summary>
    private void CloseThroughManager()
    {
        if (KY_PopupManager.Instance != null)
            KY_PopupManager.Instance.Hide();
        else
            Close();
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

        ShowCompletionMessage();
        OnConfirmed?.Invoke();
        CloseThroughManager();
    }

    /// <summary>
    /// 완료 알림을 띄운다. 알림 패널은 팝업 바깥에 있어서 팝업을 닫아도 남는다.
    /// !! 자동 숨김을 코루틴으로 돌리면 팝업이 닫히는 순간(SetActive(false)) 같이 죽어서 알림이 영원히 남는다.
    ///    그래서 팝업 활성 상태와 무관하게 도는 DOTween 지연 호출을 쓴다.
    /// </summary>
    private void ShowCompletionMessage()
    {
        if (completionMessageText != null)
            completionMessageText.text = string.Format(
                L("rest_ui.complete_message", "{0} 크레딧을 소비하여 체력 {1}, 포션 {2}개를 채웠습니다."),
                cost, healthAmount, potionAmount);

        if (completionMessagePanel == null)
            return;

        completionMessagePanel.SetActive(true);
        completionCurtain?.Open();

        completionMessageTween?.Kill();
        completionMessageTween = DOVirtual.DelayedCall(completionMessageDuration, HideCompletionMessage, true);
    }

    private void HandleCancelClicked()
    {
        CloseThroughManager();
    }

    private void HideCompletionMessage()
    {
        completionMessageTween?.Kill();
        completionMessageTween = null;

        if (completionMessagePanel != null)
            completionMessagePanel.SetActive(false);
    }
}

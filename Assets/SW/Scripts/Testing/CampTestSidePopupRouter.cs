using DG.Tweening;
using UnityEngine;

/// <summary>
/// 캠프 통합 테스트에서 구형 인벤토리를 제외하고 상태·스킬·퀘스트 팝업만 전환한다.
/// 인벤토리·상점·강화와 ESC 처리는 기존 InventoryPartView가 계속 담당한다.
/// </summary>
public sealed class CampTestSidePopupRouter : MonoBehaviour
{
    [SerializeField] private KY_PopupBase statusPopup;
    [SerializeField] private KY_PopupBase skillPopup;
    [SerializeField] private KY_PopupBase questPopup;
    [SerializeField] private KY_PopupBase pausePopup;
    [SerializeField] private KY_PopupManager popupManager;
    [SerializeField] private InventoryPartView inventoryPartView;

    private KY_PopupBase currentPopup;
    private GameInputActions inputActions;

    private void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
    }

    private void OnEnable()
    {
        InitializePopupManager();

        inputActions ??= new GameInputActions();
        inputActions.Enable();

        KY_GameEvents.OnStatusRequested += HandleStatusRequested;
        KY_GameEvents.OnSkillRequested += HandleSkillRequested;
        KY_GameEvents.OnQuestRequested += HandleQuestRequested;
        KY_GameEvents.OnEscPressed += HandleEscape;
    }

    private void OnDisable()
    {
        inputActions?.Disable();

        KY_GameEvents.OnStatusRequested -= HandleStatusRequested;
        KY_GameEvents.OnSkillRequested -= HandleSkillRequested;
        KY_GameEvents.OnQuestRequested -= HandleQuestRequested;
        KY_GameEvents.OnEscPressed -= HandleEscape;
        currentPopup = null;
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void Update()
    {
        if (inputActions == null)
            return;

        if (inputActions.Player.Pause.triggered)
            HandlePauseInput();

        if (inputActions.Player.OpenInventory.triggered)
            KY_GameEvents.InventoryRequested();

        if (inputActions.Player.OpenSkill.triggered)
            KY_GameEvents.SkillRequested();

        if (inputActions.Player.OpenStatus.triggered)
            KY_GameEvents.StatusRequested();

        if (inputActions.Player.OpenQuest.triggered)
            KY_GameEvents.QuestRequested();
    }

    private void HandleStatusRequested() => Toggle(statusPopup);

    private void HandleSkillRequested() => Toggle(skillPopup);

    private void HandleQuestRequested() => Toggle(questPopup);

    private void HandlePauseInput()
    {
        if (pausePopup != null && pausePopup.gameObject.activeSelf)
        {
            popupManager?.Hide();
            return;
        }

        if (currentPopup != null ||
            (inventoryPartView != null && inventoryPartView.HasOpenWindow))
        {
            KY_GameEvents.EscPressed();
            return;
        }

        ShowPausePopup();
    }

    private void InitializePopupManager()
    {
        if (popupManager == null || popupManager.enabled)
            return;

        // 이 테스트 씬은 KY 이벤트 중복 처리를 피하려고 PopupManager를 비활성화해 둔다.
        // 한 번만 활성/비활성 전환해 Awake 초기화는 실행하되 이벤트 구독은 남기지 않는다.
        popupManager.enabled = true;
        popupManager.enabled = false;
    }

    private void ShowPausePopup()
    {
        if (popupManager == null || pausePopup == null)
            return;

        popupManager.Show(PopupType.Pause);

        // PausePopup이 즉시 timeScale을 0으로 만들기 때문에 방금 생성된
        // 슬라이드 트윈만 unscaled time으로 전환한다.
        KY_SlideAnimator slideAnimator = pausePopup.GetComponentInChildren<KY_SlideAnimator>(true);
        if (slideAnimator == null)
            return;

        RectTransform target = slideAnimator.GetComponent<RectTransform>();
        var tweens = DOTween.TweensByTarget(target, true);

        if (tweens == null)
            return;

        foreach (Tween tween in tweens)
            tween.SetUpdate(true);
    }

    private void HandleEscape()
    {
        if (currentPopup != null)
            CloseCurrent();
    }

    private void Toggle(KY_PopupBase popup)
    {
        if (popup == null)
            return;

        if (currentPopup == popup)
        {
            CloseCurrent();
            return;
        }

        if (currentPopup != null)
            currentPopup.Close();

        currentPopup = popup;
        currentPopup.Open();
        KY_GameEvents.SidePopupOpened();
    }

    private void CloseCurrent()
    {
        currentPopup.Close();
        currentPopup = null;
        KY_GameEvents.SidePopupClosed();
    }
}

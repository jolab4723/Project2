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

    private void OnEnable()
    {
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

        popupManager?.Show(PopupType.Pause);
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

using UnityEngine;
using UnityEngine.InputSystem;

public class KY_UIInputManager : MonoBehaviour
{
    private GameInputActions inputActions;
    private InventoryPartView campInventoryView;

    void Awake()
    {
        inputActions = new GameInputActions();
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    void OnDestroy()
    {
        inputActions.Dispose();
    }

    void Update()
    {
        if (inputActions.Player.Pause.triggered)
        {
            if (!CloseCampInventoryIfOpen() && !CloseQuestOfferIfOpen())
                KY_GameEvents.EscPressed();
        }

        if (inputActions.Player.OpenInventory.triggered)
        {
            InventoryPartView inventoryView = GetCampInventoryView();

            if (inventoryView != null)
                inventoryView.ToggleInventory();
            else
                KY_GameEvents.InventoryRequested();
        }

        if (inputActions.Player.OpenSkill.triggered)
        {
            CloseCampInventoryIfOpen();
            KY_GameEvents.SkillRequested();
        }

        if (inputActions.Player.OpenStatus.triggered)
        {
            CloseCampInventoryIfOpen();
            KY_GameEvents.StatusRequested();
        }

        if (inputActions.Player.OpenQuest.triggered)
        {
            CloseCampInventoryIfOpen();
            KY_GameEvents.QuestRequested();
        }
    }

    private InventoryPartView GetCampInventoryView()
    {
        if (campInventoryView == null)
            campInventoryView = FindFirstObjectByType<InventoryPartView>();

        return campInventoryView;
    }

    private bool CloseCampInventoryIfOpen()
    {
        InventoryPartView inventoryView = GetCampInventoryView();

        if (inventoryView == null || !inventoryView.HasOpenWindow)
            return false;

        inventoryView.CloseAll();
        return true;
    }

    // 이우진님의 QuestOfferUI(의뢰 제시 팝업)가 열려있으면 ESC로 이걸 먼저 닫고, 이벤트 자체를
    // 발생시키지 않는다 - 안 그러면 KY_PopupManager가 이 팝업을 몰라서 일시정지도 같이 열어버린다.
    private bool CloseQuestOfferIfOpen()
    {
        if (QuestOfferUI.Instance == null || !QuestOfferUI.Instance.IsShowing)
            return false;

        QuestOfferUI.Instance.Hide();
        return true;
    }
}

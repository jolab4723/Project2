using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>UI 단축키 입력을 받아 열린 팝업을 닫거나 HUD 팝업 요청을 전달한다.</summary>
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
            {
                // 캠프 인벤토리는 KY_PopupManager를 거치지 않고 자기가 직접 창을 켜고 끈다.
                // 그래서 매니저는 인벤토리가 열린 걸 모르고 currentSidePopup을 그대로 들고 있어서,
                // 스테이터스를 열어둔 채 인벤토리를 열면 **둘 다 떠 있는** 상태가 됐다.
                // 스테이터스/스킬/퀘스트 키가 CloseCampInventoryIfOpen()으로 반대 방향을 막고 있으니
                // 여기서도 대칭으로, 새로 열 때만 다른 사이드 팝업을 닫아준다.
                if (!inventoryView.HasOpenWindow)
                    KY_PopupManager.Instance?.HideSidePopup();

                inventoryView.ToggleInventory();
            }
            else
            {
                KY_GameEvents.InventoryRequested();
            }
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

        if (inputActions.Player.OpenBuff.triggered)
        {
            CloseCampInventoryIfOpen();
            KY_GameEvents.BuffRequested();
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

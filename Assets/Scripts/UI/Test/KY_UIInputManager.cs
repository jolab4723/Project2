using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// UI 단축키 입력을 받아 열린 팝업을 닫거나 HUD 팝업 요청을 전달한다.
/// SW 수정: 플레이어 입력보다 먼저 공용 입력 액션으로 로컬 플레이어의 메뉴를 처리한다.
/// </summary>
[DefaultExecutionOrder(-200)]
public class KY_UIInputManager : MonoBehaviour
{
    private GameInputActions inputActions;
    private InventoryPartView campInventoryView;
    private PlayerContext playerContext;
    private KY_StatusPopup statusPopup;
    private KY_PopupManager popupManager;
    private Func<bool> consumesInput;
    private bool networkInputBound;

    /// <summary>
    /// SW 수정: 키 바인딩 서비스의 공용 입력 액션을 사용해 UI에도 같은 키 설정을 적용한다.
    /// </summary>
    void OnEnable()
    {
        inputActions = KeyBindingService.InputActions;
    }

    /// <summary>
    /// 현재 로컬 플레이어와 메뉴를 연결한다. 플레이어가 없으면 ESC 외의 메뉴 입력을 기다린다.
    /// SW 수정: 로컬 플레이어의 메뉴 참조와 채팅의 입력 소비 조건을 함께 연결한다.
    /// </summary>
    public void Bind(
        PlayerContext context,
        InventoryPartView inventoryView,
        KY_StatusPopup status,
        KY_PopupManager popups,
        Func<bool> inputConsumed)
    {
        playerContext = context;
        campInventoryView = inventoryView;
        statusPopup = status;
        popupManager = popups;
        consumesInput = inputConsumed;
        networkInputBound = true;
    }

    /// <summary>
    /// 지정한 입력 소비 경계의 연결만 해제하고 기존 싱글 메뉴 입력으로 돌아간다.
    /// SW 수정: 연결한 입력 소비 조건이 일치할 때만 메뉴 참조를 해제한다.
    /// </summary>
    public void Unbind(Func<bool> inputConsumed)
    {
        if (consumesInput != inputConsumed)
            return;

        playerContext = null;
        campInventoryView = null;
        statusPopup = null;
        popupManager = null;
        consumesInput = null;
        networkInputBound = false;
    }

    /// <summary>
    /// SW 수정: 채팅 입력 소비와 로컬 플레이어 준비 상태를 확인하고,
    /// ESC를 우선 처리해 한 프레임에 하나의 메뉴 단축키만 실행한다.
    /// </summary>
    void Update()
    {
        // 채팅을 닫은 ESC도 같은 프레임에는 메뉴 입력으로 다시 사용하지 않는다.
        if (inputActions == null || consumesInput?.Invoke() == true)
            return;

        KY_PopupManager popups = GetPopupManager();
        if (inputActions.Player.Pause.triggered)
        {
            if ((popups != null && popups.HasOpenModalPopup) ||
                (!CloseCampInventoryIfOpen() && !CloseQuestOfferIfOpen() && !CloseStatusPopupIfOpen()))
                KY_GameEvents.EscPressed();

            // ESC는 모달을 닫는 입력이므로, 같은 프레임에 다른 단축키를 이어서 처리하지 않는다.
            return;
        }

        // 퀘스트 상세/휴식/확인창 같은 일반 모달이 열린 동안에는 그 창의 닫기(ESC)만 허용한다.
        // 사이드 팝업은 닫지 않고 가린 채 유지되므로, 상세 창을 닫으면 원래 퀘스트 팝업으로 돌아온다.
        if (popups != null && popups.HasOpenModalPopup)
            return;

        if ((networkInputBound || MirrorNetworkManager.OwnsGameplay) && playerContext == null)
            return;

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
                {
                    popups?.HideSidePopup();
                    CloseStatusPopupIfOpen();
                }

                inventoryView.ToggleInventory();
            }
            else
            {
                KY_GameEvents.InventoryRequested();
            }
            return;
        }

        if (inputActions.Player.OpenSkill.triggered)
        {
            CloseCampInventoryIfOpen();
            CloseStatusPopupIfOpen();
            KY_GameEvents.SkillRequested();
            return;
        }

        if (inputActions.Player.OpenStatus.triggered)
        {
            CloseCampInventoryIfOpen();
            if (networkInputBound && statusPopup != null)
            {
                popups?.HideSidePopup();
                if (statusPopup.IsOpen) statusPopup.Close(); else statusPopup.Open();
            }
            else
            {
                KY_GameEvents.StatusRequested();
            }
            return;
        }

        if (inputActions.Player.OpenQuest.triggered)
        {
            CloseCampInventoryIfOpen();
            CloseStatusPopupIfOpen();
            KY_GameEvents.QuestRequested();
            return;
        }

        if (inputActions.Player.OpenBuff.triggered)
        {
            CloseCampInventoryIfOpen();
            CloseStatusPopupIfOpen();
            KY_GameEvents.BuffRequested();
            return;
        }

        // 기존 강화 단축키 U는 공용 InputActions에 없으므로 현재 키 입력을 그대로 사용한다.
        if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
        {
            InventoryPartView inventoryView = GetCampInventoryView();
            if (inventoryView == null)
                return;

            popups?.HideSidePopup();
            CloseStatusPopupIfOpen();
            inventoryView.OpenUpgrade();
        }
    }

    /// <summary>
    /// SW 수정: 네트워크 메뉴는 연결된 팝업 매니저를 사용하고, 싱글 메뉴는 기존 공용 인스턴스를 사용한다.
    /// </summary>
    private KY_PopupManager GetPopupManager() =>
        networkInputBound ? popupManager : KY_PopupManager.Instance;

    /// <summary>
    /// SW 수정: 네트워크 입력이 연결됐으면 주입된 캠프 인벤토리만 사용하고, 싱글 입력에서만 씬의 인벤토리를 찾는다.
    /// </summary>
    private InventoryPartView GetCampInventoryView()
    {
        if (!networkInputBound && campInventoryView == null)
            campInventoryView = FindFirstObjectByType<InventoryPartView>();

        return campInventoryView;
    }

    /// <summary>
    /// SW 수정: 연결된 스테이터스 팝업이 열려 있으면 닫고, ESC 처리에 닫기 성공 여부를 반환한다.
    /// </summary>
    private bool CloseStatusPopupIfOpen()
    {
        if (statusPopup == null || !statusPopup.IsOpen)
            return false;

        statusPopup.Close();
        return true;
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

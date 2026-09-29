using Mirror;
using UnityEngine;

/// <summary>
/// 스테이지 선택처럼 플레이어·HUD가 없는 씬에서 ESC(일시정지) 입력만 받아 KY_PopupManager에 전달한다.
///
/// KY_UIInputManager를 그대로 쓰지 않는 이유: 인벤토리·스테이터스·스킬·퀘스트·버프 단축키까지 받아서
/// 플레이어가 없는 씬에서 내용 없는 사이드 팝업이 열린다. 여기서는 일시정지(정산·포기·설정·저장 후 종료)만 연다.
/// 멀티 세션에서는 일시정지의 종료 버튼이 세션 흐름(MirrorLocalPlayerUIBinder)과 연결되지 않으므로 동작하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class StageSelectPauseInput : MonoBehaviour
{
    private GameInputActions inputActions;

    private void Awake()
    {
        inputActions = new GameInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void OnDestroy()
    {
        inputActions.Dispose();
    }

    private void Update()
    {
        if (!inputActions.Player.Pause.triggered)
            return;
        if (NetworkClient.active || NetworkServer.active)
            return;

        // 일시정지가 열려 있으면 매니저가 최상단 팝업을 닫고, 없으면 일시정지를 연다.
        KY_GameEvents.EscPressed();
    }
}

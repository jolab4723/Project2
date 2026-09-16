using System;
using UnityEngine;

/// <summary>같은 Canvas 안에서 캐릭터 선택 패널과 멀티플레이 로비 패널을 전환한다.</summary>
public class KY_LobbyFlowController : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject characterSelectionPanel;
    [SerializeField] private GameObject multiplayerLobbyPanel;
    [Header("화면 컨트롤러")]
    [SerializeField] private KY_CharacterSelectionController characterSelectionController;
    [SerializeField] private KY_MultiplayerLobbyController multiplayerLobbyController;

    private KY_CharacterId? selectedCharacterId;
    private bool usesExternalFlow;

    /// <summary>외부 연결 흐름에 방 나가기를 요청한다.</summary>
    public event Action LeaveRequested;

    /// <summary>기본 선택 화면 열기를 억제하고 외부 연결 흐름이 화면을 열도록 준비한다.</summary>
    public void ConfigureExternalFlow()
    {
        usesExternalFlow = true;
        HidePanels();
    }

    /// <summary>참가자 상태를 변경하지 않고 로비 패널만 표시한다.</summary>
    public void ShowLobby()
    {
        SetPanelActive(characterSelectionPanel, false);
        SetPanelActive(multiplayerLobbyPanel, true);
    }

    /// <summary>캐릭터 선택과 로비 패널을 모두 숨긴다.</summary>
    public void HidePanels()
    {
        SetPanelActive(characterSelectionPanel, false);
        SetPanelActive(multiplayerLobbyPanel, false);
    }

    /// <summary>선택 화면과 멀티플레이 로비의 전환 이벤트를 등록한다.</summary>
    private void Awake()
    {
        if (characterSelectionController != null)
            characterSelectionController.SelectionConfirmed += ShowMultiplayerLobby;
        if (multiplayerLobbyController != null)
        {
            multiplayerLobbyController.ChangeCharacterRequested += ShowCharacterSelection;
            multiplayerLobbyController.LeaveLobbyRequested += HandleLeaveLobby;
        }
    }

    /// <summary>처음에는 캐릭터 선택 화면을 표시한다.</summary>
    private void Start()
    {
        if (!usesExternalFlow) ShowCharacterSelection();
        YJ_BgmPlayer.Instance.Play(YJ_BgmPlayer.YJ_BgmType.LobbyBgm);
    }

    /// <summary>캐릭터 선택 패널을 열고 이전에 선택한 캐릭터를 유지한다.</summary>
    public void ShowCharacterSelection()
    {
        SetPanelActive(multiplayerLobbyPanel, false);
        SetPanelActive(characterSelectionPanel, true);
        characterSelectionController?.ShowSelection(selectedCharacterId);
    }

    /// <summary>선택한 캐릭터를 저장하고 멀티플레이 로비 패널을 연다.</summary>
    private void ShowMultiplayerLobby(KY_CharacterId characterId)
    {
        selectedCharacterId = characterId;
        ShowLobby();
        multiplayerLobbyController?.OpenForLocalPlayer(characterId);
    }

    /// <summary>방 나가기는 실제 네트워크 연결 해제 구현 전까지 외부 연결 지점으로 남긴다.</summary>
    private void HandleLeaveLobby()
    {
        if (usesExternalFlow)
        {
            LeaveRequested?.Invoke();
            return;
        }
        multiplayerLobbyController?.SetPlayers(null);
        ShowCharacterSelection();
        YJ_BgmPlayer.Instance.Stop();
        Debug.Log("[KY_LobbyFlowController] 로컬 로비 미리보기를 나왔습니다. 실제 네트워크 연결 해제는 추후 연결합니다.", this);
    }

    /// <summary>선택적으로 연결한 패널을 안전하게 켜거나 끈다.</summary>
    private static void SetPanelActive(GameObject panel, bool isActive)
    {
        if (panel != null) panel.SetActive(isActive);
    }

    /// <summary>등록한 화면 전환 이벤트를 해제한다.</summary>
    private void OnDestroy()
    {
        if (characterSelectionController != null)
            characterSelectionController.SelectionConfirmed -= ShowMultiplayerLobby;
        if (multiplayerLobbyController != null)
        {
            multiplayerLobbyController.ChangeCharacterRequested -= ShowCharacterSelection;
            multiplayerLobbyController.LeaveLobbyRequested -= HandleLeaveLobby;
        }
    }
}

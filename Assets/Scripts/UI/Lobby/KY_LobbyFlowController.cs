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
    private void Start() => ShowCharacterSelection();

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
        SetPanelActive(characterSelectionPanel, false);
        SetPanelActive(multiplayerLobbyPanel, true);
        multiplayerLobbyController?.OpenForLocalPlayer(characterId);
    }

    /// <summary>방 나가기는 실제 네트워크 연결 해제 구현 전까지 외부 연결 지점으로 남긴다.</summary>
    private void HandleLeaveLobby()
    {
        Debug.Log("[KY_LobbyFlowController] 방 나가기 요청을 받았습니다. 네트워크 연결 해제 후 타이틀 전환을 연결하세요.", this);
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

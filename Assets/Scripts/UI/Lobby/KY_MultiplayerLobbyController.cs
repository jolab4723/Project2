using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 최대 4명의 캐릭터 선택, 준비 상태, 로비 슬롯과 시작 가능 여부를 표시한다.
/// 네트워크 동기화는 이후 외부에서 SetPlayers로 전달받아 연결한다.
/// </summary>
public class KY_MultiplayerLobbyController : MonoBehaviour
{
    private const int MaxPlayerCount = 4;

    [Header("로컬 플레이어")]
    [SerializeField] private string localPlayerId = "local";
    [SerializeField] private string localPlayerName = "Player";
    [Header("미리보기 참여자")]
    [SerializeField] private List<KY_LobbyPlayerData> previewPlayers = new List<KY_LobbyPlayerData>();
    [Header("로비 표시")]
    [SerializeField] private KY_LobbyPlayerSlot[] playerSlots;
    [SerializeField] private KY_LobbyLineupView lineupView;
    [Header("버튼")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Button changeCharacterButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Button passiveSkillButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private TMP_Text readyButtonText;
    [Header("팝업")]
    [SerializeField] private KY_PopupManager popupManager;

    /// <summary>캐릭터를 다시 선택하려고 할 때 호출한다.</summary>
    public event Action ChangeCharacterRequested;
    /// <summary>방 나가기 처리를 외부 흐름에 요청할 때 호출한다.</summary>
    public event Action LeaveLobbyRequested;
    /// <summary>모든 참여자가 준비되어 게임 시작을 요청할 때 호출한다.</summary>
    public event Action GameStartRequested;

    /// <summary>로비 버튼의 이벤트를 등록한다.</summary>
    private void Awake()
    {
        readyButton?.onClick.AddListener(ToggleLocalReady);
        changeCharacterButton?.onClick.AddListener(RequestCharacterChange);
        leaveLobbyButton?.onClick.AddListener(RequestLeaveLobby);
        gameStartButton?.onClick.AddListener(RequestGameStart);
        passiveSkillButton?.onClick.AddListener(OpenPassiveSkill);
        optionsButton?.onClick.AddListener(OpenOptions);
    }

    /// <summary>로컬 플레이어가 캐릭터를 확정해 로비에 들어왔을 때 상태를 초기화해 표시한다.</summary>
    public void OpenForLocalPlayer(KY_CharacterId characterId)
    {
        KY_LobbyPlayerData localPlayer = GetOrCreateLocalPlayer();
        if (localPlayer == null)
            return;

        localPlayer.selectedCharacterId = characterId;
        localPlayer.readyState = KY_LobbyReadyState.NotReady;
        RefreshView();
    }

    /// <summary>외부 소스에서 받은 플레이어 목록을 UI 표시용으로 반영한다.</summary>
    public void SetPlayers(IReadOnlyList<KY_LobbyPlayerData> players)
    {
        previewPlayers.Clear();
        if (players != null)
            foreach (KY_LobbyPlayerData player in players)
            {
                if (player == null || previewPlayers.Count >= MaxPlayerCount)
                    continue;

                previewPlayers.Add(ClonePlayer(player));
            }
        RefreshView();
    }

    /// <summary>로컬 플레이어의 준비 상태를 준비/해제 사이로 전환한다.</summary>
    private void ToggleLocalReady()
    {
        KY_LobbyPlayerData localPlayer = GetOrCreateLocalPlayer();
        if (localPlayer == null)
            return;

        localPlayer.readyState = localPlayer.readyState == KY_LobbyReadyState.Ready ? KY_LobbyReadyState.NotReady : KY_LobbyReadyState.Ready;
        RefreshView();
    }
    /// <summary>캐릭터 변경 전 준비 상태를 해제하고 선택 화면 전환을 요청한다.</summary>
    private void RequestCharacterChange()
    {
        KY_LobbyPlayerData localPlayer = GetOrCreateLocalPlayer();
        if (localPlayer == null)
            return;

        localPlayer.readyState = KY_LobbyReadyState.NotReady;
        RefreshView();
        ChangeCharacterRequested?.Invoke();
    }
    /// <summary>방 나가기 처리를 상위 흐름에 요청한다.</summary>
    private void RequestLeaveLobby() => LeaveLobbyRequested?.Invoke();
    /// <summary>모두 준비된 상태에서만 게임 시작 요청을 전달한다.</summary>
    private void RequestGameStart()
    {
        if (!CanStartGame()) { Debug.LogWarning("[KY_MultiplayerLobbyController] 모든 참여자가 READY 상태여야 게임을 시작할 수 있습니다.", this); return; }
        GameStartRequested?.Invoke();
    }
    /// <summary>패시브 스킬 팝업을 연다.</summary>
    private void OpenPassiveSkill() => popupManager?.Show(PopupType.PassiveSkill);
    /// <summary>옵션 팝업을 연다.</summary>
    private void OpenOptions() => popupManager?.Show(PopupType.Settings);

    /// <summary>최대 4개 슬롯, 3D 라인업, 버튼 상태를 현재 데이터로 다시 표시한다.</summary>
    private void RefreshView()
    {
        if (playerSlots != null)
            for (int index = 0; index < playerSlots.Length; index++)
            {
                KY_LobbyPlayerSlot slot = playerSlots[index];
                if (slot == null) continue;
                if (index < previewPlayers.Count)
                {
                    KY_LobbyPlayerData player = previewPlayers[index];
                    slot.ShowPlayer(player, player.playerId == localPlayerId);
                }
                else slot.ShowEmpty();
            }
        lineupView?.ShowPlayers(previewPlayers);
        KY_LobbyPlayerData localPlayer = FindLocalPlayer();
        if (readyButtonText != null) readyButtonText.text = localPlayer != null && localPlayer.readyState == KY_LobbyReadyState.Ready ? "READY CANCEL" : "READY";
        if (gameStartButton != null) gameStartButton.interactable = CanStartGame();
    }

    /// <summary>호스트이며 모든 실제 참여자가 준비되었는지 확인한다.</summary>
    private bool CanStartGame()
    {
        KY_LobbyPlayerData localPlayer = FindLocalPlayer();
        if (localPlayer == null || !localPlayer.isHost || previewPlayers.Count == 0) return false;
        foreach (KY_LobbyPlayerData player in previewPlayers)
            if (player == null || player.readyState != KY_LobbyReadyState.Ready) return false;
        return true;
    }
    /// <summary>로컬 플레이어 데이터를 찾고 없으면 호스트인 기본 데이터를 만든다.</summary>
    private KY_LobbyPlayerData GetOrCreateLocalPlayer()
    {
        KY_LobbyPlayerData localPlayer = FindLocalPlayer();
        if (localPlayer != null) return localPlayer;

        if (previewPlayers.Count >= MaxPlayerCount)
        {
            Debug.LogError("[KY_MultiplayerLobbyController] 로컬 플레이어를 추가할 빈 로비 슬롯이 없습니다.", this);
            return null;
        }
        localPlayer = new KY_LobbyPlayerData { playerId = localPlayerId, displayName = localPlayerName, isHost = true };
        previewPlayers.Insert(0, localPlayer);
        return localPlayer;
    }
    /// <summary>현재 목록에서 로컬 플레이어 데이터를 찾는다.</summary>
    private KY_LobbyPlayerData FindLocalPlayer() => previewPlayers.Find(player => player != null && player.playerId == localPlayerId);
    /// <summary>외부 데이터의 참조를 보관하지 않도록 UI용 복사본을 만든다.</summary>
    private static KY_LobbyPlayerData ClonePlayer(KY_LobbyPlayerData player) => new KY_LobbyPlayerData
    {
        playerId = player.playerId, displayName = player.displayName, selectedCharacterId = player.selectedCharacterId,
        readyState = player.readyState, isHost = player.isHost
    };
    /// <summary>등록한 로비 버튼의 이벤트를 해제한다.</summary>
    private void OnDestroy()
    {
        readyButton?.onClick.RemoveListener(ToggleLocalReady);
        changeCharacterButton?.onClick.RemoveListener(RequestCharacterChange);
        leaveLobbyButton?.onClick.RemoveListener(RequestLeaveLobby);
        gameStartButton?.onClick.RemoveListener(RequestGameStart);
        passiveSkillButton?.onClick.RemoveListener(OpenPassiveSkill);
        optionsButton?.onClick.RemoveListener(OpenOptions);
    }
}

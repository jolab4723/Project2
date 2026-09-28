using ItemSystem;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>KY 로비 버튼을 세션 요청으로 연결하고 서버 명부를 고정 슬롯에 표시한다. 참가 상태를 소유하지 않는다.</summary>
[DefaultExecutionOrder(-50)]
public sealed class MirrorLobbyBridge : MonoBehaviour
{
    [SerializeField] private KY_LobbyFlowController flow;
    [SerializeField] private KY_MultiplayerLobbyController lobby;
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private TMP_InputField displayNameInput;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button serverButton;
    [SerializeField] private Button reconnectButton;
    [SerializeField] private PassiveSkillPanelUI passivePopup;
    private MirrorNetworkManager manager;
    private string displayedParticipantId;
    private bool? passiveChangesAllowed;
    private KY_PausePopup pausePopup;
    private UILabelDatabaseSO uiLabels;
    private YJ_LanguageManager languageManager;
    private string statusMessage = string.Empty;

    private void Awake()
    {
        uiLabels = Resources.Load<UILabelDatabaseSO>(SessionUIMessageLocalizer.DatabasePath);
        Core.SettingManager.Instance?.Activate();
        Core.DataManager.Instance?.LoadPassiveData();
        flow.ConfigureExternalFlow();
        lobby.ConfigureExternalState(null, string.Empty);
        lobby.SetPlayers(null);
        hostButton.onClick.AddListener(StartHost);
        joinButton.onClick.AddListener(StartClient);
        serverButton.onClick.AddListener(StartServer);
        reconnectButton.onClick.AddListener(Reconnect);
        lobby.ReadyChangeRequested += ChangeReady;
        lobby.CharacterChangeRequested += ChangeCharacter;
        lobby.GameStartRequested += StartRun;
        flow.LeaveRequested += Leave;
    }

    private void OnEnable()
    {
        languageManager = YJ_LanguageManager.Instance;
        if (languageManager != null) languageManager.LanguageChanged += RefreshLanguage;
        RefreshLanguage(default);
    }

    private void OnDisable()
    {
        if (languageManager != null) languageManager.LanguageChanged -= RefreshLanguage;
    }

    private void RefreshLanguage(GameLanguage _)
    {
        if (statusText != null)
            statusText.text = SessionUIMessageLocalizer.GetMessage(uiLabels, statusMessage);
    }

    private void Update()
    {
        bool canConnect = manager != null && !NetworkClient.active && !NetworkServer.active;
        hostButton.interactable = joinButton.interactable = serverButton.interactable = reconnectButton.interactable = canConnect;
        addressInput.interactable = displayNameInput.interactable = canConnect;
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            KY_GameEvents.EscPressed();
        }
    }

    private void Start()
    {
        foreach (var popup in FindObjectsByType<KY_PausePopup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (popup.gameObject.scene == gameObject.scene) { pausePopup = popup; break; }
        manager = NetworkManager.singleton as MirrorNetworkManager;
        if (manager == null)
        {
            SetStatus("세션 연결 설정을 찾을 수 없습니다.");
            return;
        }
        manager.LobbyStateChanged += RefreshLobby;
        manager.AdmissionStatusChanged += SetStatus;
        RefreshLobby();
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.LobbyStateChanged -= RefreshLobby;
            manager.AdmissionStatusChanged -= SetStatus;
        }
        lobby.ReadyChangeRequested -= ChangeReady;
        lobby.CharacterChangeRequested -= ChangeCharacter;
        lobby.GameStartRequested -= StartRun;
        flow.LeaveRequested -= Leave;
    }

    private bool PrepareConnection()
    {
        if (manager == null || NetworkClient.active || NetworkServer.active) return false;
        string nickname = displayNameInput.text.Trim();
        string address = addressInput.text.Trim();
        if (nickname.Length == 0 || nickname.Length > 24 || nickname.IndexOfAny(new[] { '<', '>', '\n', '\r', '\t' }) >= 0)
        {
            SetStatus("닉네임을 1~24자로 입력해 주세요. 태그와 줄바꿈은 사용할 수 없습니다.");
            displayNameInput.Select();
            return false;
        }
        if (System.Uri.CheckHostName(address) == System.UriHostNameType.Unknown)
        {
            SetStatus("서버 IP 또는 호스트명을 입력해 주세요. 포트 번호는 붙이지 않습니다.");
            addressInput.Select();
            return false;
        }
        manager.ClientDisplayName = nickname;
        manager.networkAddress = address;
        manager.RequestedReconnectProfile = null;
        SetStatus("연결 중…");
        return true;
    }

    private void StartHost() { if (PrepareConnection()) manager.StartHost(); }
    private void StartClient() { if (PrepareConnection()) manager.StartClient(); }
    private void StartServer()
    {
        if (!PrepareConnection()) return;
        manager.StartServer();
        SetStatus("전용 서버 실행 중 · 참가자 대기");
    }

    private void Reconnect()
    {
        if (!PrepareConnection()) return;
        MirrorReconnectProfile profile = MirrorReconnectProfile.Load(out string reason);
        if (profile == null)
        {
            SetStatus(reason ?? "이 프로필에 저장된 최근 세션이 없습니다.");
            return;
        }
        manager.RequestedReconnectProfile = profile;
        manager.networkAddress = profile.ServerAddress;
        manager.StartClient();
    }

    private void ChangeReady(bool ready)
    {
        if (manager == null) return;
        if (ready) SetPassiveChangesAllowed(false);
        if (!manager.RequestLobbyChange(MirrorLobbyOperation.Ready, ready: ready)) RefreshLobby();
    }
    private void ChangeCharacter(KY_CharacterId character) => manager?.RequestLobbyChange(
        MirrorLobbyOperation.Character,
        character == KY_CharacterId.Gunner ? CharacterClass.Gunner : CharacterClass.Fighter);
    private void StartRun() => manager?.RequestStartSession();
    private void Leave() => manager?.RequestLeaveSession();

    /// <summary>연결을 종료한 로비에서 세션 소유자를 정리하고 기존 타이틀로 돌아갑니다.</summary>
    public void ReturnToTitle()
    {
        if (NetworkClient.active || NetworkServer.active) return;
        var loader = Core.SceneLoader.Instance;
        if (loader != null) loader.LoadScene("TitleScene");
    }
    private void SetStatus(string message)
    {
        statusMessage = message ?? string.Empty;
        RefreshLanguage(default);
    }

    private void RefreshLobby()
    {
        MirrorLocalPlayerUIBinder.ConfigurePauseMenu(pausePopup, manager);
        bool admitted = !string.IsNullOrEmpty(manager.LocalParticipantId);
        connectionPanel.SetActive(!admitted);
        if (!admitted)
        {
            SetPassiveChangesAllowed(true);
            displayedParticipantId = null;
            flow.HidePanels();
            return;
        }
        lobby.ConfigureExternalState(manager.LocalParticipantId, manager.ClientDisplayName);
        var slots = new KY_LobbyPlayerData[MirrorSessionRoster.MaxMembers];
        MirrorLobbySnapshot snapshot = manager.ClientLobby;
        MirrorLobbyMember local = default;
        if (snapshot.Members != null)
        {
            foreach (MirrorLobbyMember member in snapshot.Members)
            {
                if (member.ParticipantId == manager.LocalParticipantId) local = member;
                if (member.Slot < 0 || member.Slot >= slots.Length || member.HasForfeited) continue;
                slots[member.Slot] = new KY_LobbyPlayerData
                {
                    playerId = member.ParticipantId, displayName = member.DisplayName,
                    selectedCharacterId = member.CharacterClass == CharacterClass.Gunner ? KY_CharacterId.Gunner : KY_CharacterId.Fighter,
                    readyState = GetReadyState(member), isHost = member.IsLeader
                };
            }
        }
        SetPassiveChangesAllowed(!snapshot.RunStarted && !local.IsReady);
        lobby.SetPlayers(slots);
        if (snapshot.RunStarted) flow.HidePanels();
        else if (displayedParticipantId != manager.LocalParticipantId)
        {
            displayedParticipantId = manager.LocalParticipantId;
            if (local.HasCharacterChoice) flow.ShowLobby();
            else flow.ShowCharacterSelection();
        }
    }

    /// <summary>준비 시 전송한 패시브와 출발 시 적용할 값이 달라지지 않도록 준비 후에는 변경을 잠근다.</summary>
    private void SetPassiveChangesAllowed(bool allowed)
    {
        if (passivePopup == null || passiveChangesAllowed == allowed) return;
        passiveChangesAllowed = allowed;
        passivePopup.SetChangesAllowed(allowed, "준비를 취소한 뒤 패시브를 변경할 수 있습니다.");
    }

    private static KY_LobbyReadyState GetReadyState(MirrorLobbyMember member)
    {
        if (!member.HasCharacterChoice) return KY_LobbyReadyState.Selecting;
        return member.IsReady ? KY_LobbyReadyState.Ready : KY_LobbyReadyState.NotReady;
    }
}

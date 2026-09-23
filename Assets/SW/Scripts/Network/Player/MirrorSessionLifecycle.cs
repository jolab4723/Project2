using System;
using System.Collections;
using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum MirrorLobbyOperation : byte { Character, Ready, Start, Leave, ReturnToLobby }

public struct MirrorLobbyRequest : NetworkMessage
{
    public MirrorLobbyOperation Operation;
    public CharacterClass CharacterClass;
    public bool Ready;
    public string PassiveProfileJson;
}

/// <summary>클라이언트 표시용 명부 항목. 참가 자격을 증명하는 비밀은 포함하지 않는다.</summary>
public struct MirrorLobbyMember
{
    public string ParticipantId;
    public int Slot;
    public string DisplayName;
    public CharacterClass CharacterClass;
    public bool HasCharacterChoice;
    public bool IsReady;
    public bool IsLeader;
    public bool IsConnected;
    public bool HasForfeited;
}

public struct MirrorLobbySnapshot : NetworkMessage
{
    public bool RunStarted;
    public MirrorLobbyMember[] Members;
}

public struct MirrorSessionFeedback : NetworkMessage { public string Reason; }

/// <summary>기존 NetworkManager의 참가 명부·로비 요청·재접속 수명주기 구현이다.</summary>
public sealed partial class MirrorNetworkManager
{
    public const string SessionLobbyScene = "Assets/SW/Scenes/Network/Lobby.unity";
    [SerializeField] private GameObject gunnerPlayerPrefab;
    private bool pausedForAbsentParty;
    private float timeScaleBeforePartyPause = 1;
    private MirrorLobbySnapshot clientLobby;

    public MirrorSessionRoster ServerRoster { get; } = new();
    public string ClientDisplayName { get; set; } = "Player";
    public string LocalParticipantId { get; private set; }
    public string LocalSessionId { get; private set; }
    public MirrorReconnectProfile RequestedReconnectProfile { get; set; }
    public MirrorLobbySnapshot ClientLobby => clientLobby;
    public event Action LobbyStateChanged;
    public event Action<string> AdmissionStatusChanged;

    /// <summary>인증 컴포넌트가 확정한 로컬 참가자 ID만 보관한다.</summary>
    internal void AcceptLocalParticipant(string participantId, string sessionId)
    {
        LocalParticipantId = participantId;
        LocalSessionId = sessionId;
    }

    internal void SetAdmissionStatus(string message)
    {
        compatibilityStatusMessage = message ?? string.Empty;
        AdmissionStatusChanged?.Invoke(compatibilityStatusMessage);
    }

    /// <summary>로비 UI 의도를 서버에 전달한다. UI는 응답 명부가 도착할 때까지 확정 상태를 바꾸지 않는다.</summary>
    public bool RequestLobbyChange(MirrorLobbyOperation operation,
        CharacterClass characterClass = CharacterClass.Fighter, bool ready = false)
    {
        if (!NetworkClient.isConnected || !clientCompatibilityConfirmed) return false;
        string passiveJson = null;
        if (operation == MirrorLobbyOperation.Ready && ready)
        {
            try
            {
                passiveJson = MirrorPassiveProfile.ReadLocalPayload();
            }
            catch (Exception exception) when (exception is System.IO.IOException ||
                exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                SetAdmissionStatus("저장된 패시브 프로필을 읽지 못했습니다.");
                return false;
            }
        }
        NetworkClient.Send(new MirrorLobbyRequest
        {
            Operation = operation, CharacterClass = characterClass, Ready = ready,
            PassiveProfileJson = passiveJson
        });
        return true;
    }

    /// <summary>명시적으로 참여를 종료한다. 전송 끊김은 이 메서드를 호출하지 않아 재접속 자격을 유지한다.</summary>
    public void RequestLeaveSession()
    {
        if (NetworkServer.active && serverResultFinalized)
        {
            foreach (var member in ServerRoster.ConnectedMembers)
                if (member.RuntimeContext?.GetComponent<NetworkShopPlayerState>()?.ServerTransferRunCreditsToOwner() == false)
                {
                    SetAdmissionStatus("참가자의 결과 저장을 기다리고 있습니다. 저장 후 세션을 종료해 주세요.");
                    return;
                }
        }
        MirrorReconnectProfile.Clear(out _);
        RequestedReconnectProfile = null;
        if (mode == NetworkManagerMode.Host)
        {
            StopHost();
            return;
        }
        if (RequestLobbyChange(MirrorLobbyOperation.Leave)) return;
        if (NetworkClient.active) StopClient();
    }

    private void StartServerMembership()
    {
        ServerRoster.Reset();
        NetworkServer.RegisterHandler<MirrorLobbyRequest>(HandleLobbyRequest);
    }

    private void StartClientMembership()
    {
        clientLobby = default;
        LocalParticipantId = null;
        NetworkClient.RegisterHandler<MirrorLobbySnapshot>(snapshot =>
        {
            if (!snapshot.RunStarted)
                ResetRunSnapshot();
            clientLobby = snapshot;
            LobbyStateChanged?.Invoke();
        });
        NetworkClient.RegisterHandler<MirrorSessionFeedback>(message => SetAdmissionStatus(message.Reason));
    }

    private void HandleLobbyRequest(NetworkConnectionToClient connection, MirrorLobbyRequest request)
    {
        string reason = null;
        bool accepted;
        switch (request.Operation)
        {
            case MirrorLobbyOperation.Character:
                accepted = ServerRoster.TrySetCharacter(connection.connectionId, request.CharacterClass, out reason);
                break;
            case MirrorLobbyOperation.Ready:
                accepted = TrySetReadyWithPassive(connection.connectionId, request, out reason);
                break;
            case MirrorLobbyOperation.Start:
                if (sessionSceneChangeRequested || NetworkServer.isLoadingScene ||
                    SceneManager.GetActiveScene().path != SessionLobbyScene)
                {
                    accepted = false;
                    reason = "로비 이동이 끝난 뒤 출발할 수 있습니다.";
                    break;
                }
                if (gunnerPlayerPrefab == null || playerPrefab == null)
                {
                    accepted = false;
                    reason = "두 클래스의 네트워크 프리팹 연결을 확인하세요.";
                    break;
                }
                accepted = ServerRoster.TryStartRun(connection.connectionId, out reason);
                if (accepted)
                {
                    sessionSceneChangeRequested = true;
                    pendingSessionRoute = MirrorSessionRoute.StageSelect;
                    BroadcastLobby();
                    ServerChangeScene(SessionCampScene);
                }
                break;
            case MirrorLobbyOperation.Leave:
                if (serverResultFinalized && ServerRoster.FindByConnection(connection.connectionId)?.RuntimeContext?
                    .GetComponent<NetworkShopPlayerState>()?.ServerTransferRunCreditsToOwner() == false)
                {
                    accepted = false;
                    reason = "결과 저장을 기다리고 있습니다. 잠시 후 다시 시도해 주세요.";
                    break;
                }
                MirrorSessionRoster.Member leaving = ServerRoster.Leave(connection.connectionId);
                DestroyRetainedPlayer(leaving);
                connection.Disconnect();
                accepted = leaving != null;
                break;
            case MirrorLobbyOperation.ReturnToLobby:
                accepted = TryReturnCompletedRunToLobby(connection, out reason);
                if (accepted) return;
                break;
            default:
                accepted = false;
                reason = "지원하지 않는 로비 요청입니다.";
                break;
        }
        if (!accepted) connection.Send(new MirrorSessionFeedback { Reason = reason });
        BroadcastLobby();
    }

    /// <summary>준비할 때 최신 패시브를 검증한다. 검증 또는 준비 요청이 실패하면 기존 프로필을 유지한다.</summary>
    private bool TrySetReadyWithPassive(int connectionId, MirrorLobbyRequest request, out string reason)
    {
        MirrorPassiveProfile passive = null;
        if (request.Ready && !MirrorPassiveProfile.TryValidate(request.PassiveProfileJson,
            GetComponent<MirrorSessionAuthenticator>()?.PassiveDatabase, out passive, out reason))
            return false;
        if (!ServerRoster.TrySetReady(connectionId, request.Ready, out reason)) return false;
        if (request.Ready) ServerRoster.FindByConnection(connectionId).PassiveProfile = passive;
        return true;
    }

    /// <summary>
    /// 결과 화면에서 방장이 파티의 로비 복귀를 요청한다.
    /// 서버는 마지막 보스 완료·리더·씬 전환 상태를 다시 검사한다.
    /// </summary>
    public bool RequestReturnToLobby()
    {
        return CanLocalClientControlSession && HasLocalRunResult && CurrentSessionRoute == MirrorSessionRoute.Result &&
               RequestLobbyChange(MirrorLobbyOperation.ReturnToLobby);
    }

    private bool TryReturnCompletedRunToLobby(NetworkConnectionToClient connection, out string reason)
    {
        reason = null;
        if (!CanConnectionControlSession(connection, "결과 화면 로비 복귀") ||
            !ServerRoster.RunStarted || !serverResultFinalized ||
            CurrentSessionRoute != MirrorSessionRoute.Result ||
            sessionSceneChangeRequested || NetworkServer.isLoadingScene)
        {
            reason = "서버가 결과를 확정한 뒤 방장만 파티를 로비로 이동할 수 있습니다.";
            return false;
        }

        foreach (MirrorSessionRoster.Member member in ServerRoster.Members)
        {
            if (member.HasForfeited) continue;
            NetworkShopPlayerState shopState =
                member.RuntimeContext?.GetComponent<NetworkShopPlayerState>();
            if (shopState != null && !shopState.ServerTransferRunCreditsToOwner())
            {
                reason = $"{member.DisplayName}의 결과 저장을 기다리고 있습니다. 잠시 후 다시 시도해 주세요.";
                return false;
            }
        }
        foreach (MirrorSessionRoster.Member member in ServerRoster.Members)
        {
            DestroyRetainedPlayer(member);
        }
        ServerRoster.ReturnToLobby();
        ResetRunSnapshot();
        SetPartyAbsentPause(false);
        sessionSceneChangeRequested = true;
        pendingSessionRoute = MirrorSessionRoute.Unknown;
        BroadcastLobby();
        Debug.Log("[MirrorNetworkManager] 보스 결과에서 로비로 복귀: 런 상태 정리, 연결 참가자 유지, 준비 해제");
        ServerChangeScene(SessionLobbyScene);
        return true;
    }

    private void BroadcastLobby()
    {
        sessionLeaderConnectionId = -1;
        var members = new MirrorLobbyMember[ServerRoster.Members.Count];
        for (int index = 0; index < members.Length; index++)
        {
            MirrorSessionRoster.Member member = ServerRoster.Members[index];
            if (member.IsLeader) sessionLeaderConnectionId = member.ConnectionId;
            members[index] = new MirrorLobbyMember
            {
                ParticipantId = member.ParticipantId, Slot = member.Slot, DisplayName = member.DisplayName,
                CharacterClass = member.CharacterClass, HasCharacterChoice = member.HasCharacterChoice,
                IsReady = member.IsReady, IsLeader = member.IsLeader,
                IsConnected = member.ConnectionId >= 0, HasForfeited = member.HasForfeited
            };
        }
        var snapshot = new MirrorLobbySnapshot { RunStarted = ServerRoster.RunStarted, Members = members };
        foreach (MirrorSessionRoster.Member member in ServerRoster.ConnectedMembers)
            if (NetworkServer.connections.TryGetValue(member.ConnectionId, out NetworkConnectionToClient connection))
                connection.Send(snapshot);
        BroadcastSessionLeadership();
    }

    /// <summary>씬 Ready 뒤에만 클래스별 캐릭터를 한 번 생성하거나 기존 서버 캐릭터에 새 연결을 붙인다.</summary>
    private void AttachReadyParticipant(NetworkConnectionToClient connection)
    {
        MirrorSessionRoster.Member member = ServerRoster.FindByConnection(connection.connectionId);
        if (!ServerRoster.RunStarted || member == null || !connection.isReady || connection.identity != null) return;
        PlayerContext context = member.RuntimeContext;
        bool created = context == null;
        Transform start = GetParticipantStartPosition(member.Slot);
        if (context == null)
        {
            GameObject prefab = member.CharacterClass == CharacterClass.Gunner ? gunnerPlayerPrefab : playerPrefab;
            if (prefab == null)
            {
                SetAdmissionStatus("플레이어 프리팹을 찾을 수 없습니다.");
                connection.Disconnect();
                return;
            }
            GameObject player = start != null
                ? Instantiate(prefab, start.position, start.rotation)
                : Instantiate(prefab);
            context = player.GetComponent<PlayerContext>();
            if (context == null || player.GetComponent<MirrorSpawnedPlayerBinder>()?.IsConfigured != true)
            {
                Destroy(player);
                SetAdmissionStatus("플레이어의 공통 상태 또는 필수 네트워크 참조가 비어 있습니다.");
                connection.Disconnect();
                return;
            }
            member.RuntimeContext = context;
            context.Equipment.SetActiveCharacterClass(member.CharacterClass);
        }
        MirrorSpawnedPlayerBinder binder = context.GetComponent<MirrorSpawnedPlayerBinder>();
        if (binder == null || !binder.IsConfigured)
        {
            SetAdmissionStatus("보존된 플레이어의 필수 네트워크 구성을 확인할 수 없습니다.");
            connection.Disconnect();
            return;
        }
        binder.ServerSetDisplayIdentity(member);
        context.GetComponent<PlayerInventorySync>().ServerResetOwnerRequests();
        context.GetComponent<NetworkShopPlayerState>().ServerResetOwnerRequests();
        if (start != null) binder.ServerPlaceAtSceneStart(start.position, start.rotation);
        if (!created) context.GetComponent<PlayerNetworkTransform>().ServerResetOwnerReceiveState();
        if (!NetworkServer.AddPlayerForConnection(connection, context.gameObject))
        {
            connection.Disconnect();
            return;
        }
        binder.ServerSetTemporarilyAbsent(false);
        RegisterServerPlayer(context);
        if (created) context.GetComponent<NetworkShopPlayerState>().ServerApplyPassiveProfile(member.PassiveProfile);
        binder.ServerConfirmSceneStart(connection);
        SendRunSnapshot(connection);
    }

    /// <summary>직접 AddPlayer 메시지로 로비 준비·클래스 선택 검사를 우회할 수 없게 한다.</summary>
    public override void OnServerAddPlayer(NetworkConnectionToClient connection)
    {
        AttachReadyParticipant(connection);
    }

    private void DestroyRetainedPlayer(MirrorSessionRoster.Member member)
    {
        if (member?.RuntimeContext == null) return;
        PlayerContext context = member.RuntimeContext;
        NetworkIdentity identity = context.GetComponent<NetworkIdentity>();
        NetworkConnectionToClient owner = identity.connectionToClient;
        UnregisterServerPlayer(context);
        member.RuntimeContext = null;
        if (owner != null && owner.identity == identity)
            NetworkServer.RemovePlayerForConnection(owner, RemovePlayerOptions.Destroy);
        else
            NetworkServer.Destroy(context.gameObject);
    }

    private void TryStartCombatWhenPartyReady()
    {
        if (CurrentSessionRoute == MirrorSessionRoute.Combat && IsPartyGameplayReady && PrepareUnknownBattle())
            TryStartCombatSession(-1);
    }

    public override void Update()
    {
        base.Update();
        if (!NetworkServer.active || !ServerRoster.RunStarted) return;
        double now = Time.realtimeSinceStartupAsDouble;
        List<MirrorSessionRoster.Member> expired = ServerRoster.ExpireReservations(now);
        foreach (MirrorSessionRoster.Member member in expired) DestroyRetainedPlayer(member);
        if (expired.Count > 0) BroadcastLobby();
        bool anyoneConnected = false;
        bool anyoneReady = false;
        foreach (MirrorSessionRoster.Member member in ServerRoster.ConnectedMembers)
        {
            anyoneConnected = true;
            if (NetworkServer.connections.TryGetValue(member.ConnectionId, out NetworkConnectionToClient connection) &&
                connection.isReady && connection.identity != null && member.RuntimeContext != null &&
                !member.RuntimeContext.GetComponent<MirrorSpawnedPlayerBinder>().IsTemporarilyAbsent)
                anyoneReady = true;
        }
        SetPartyAbsentPause(!anyoneReady && (anyoneConnected || ServerRoster.HasReconnectReservations(now)));
        if (anyoneReady) RetryQuestRewards();
        if (serverResultFinalized)
            foreach (var member in ServerRoster.ConnectedMembers)
                member.RuntimeContext?.GetComponent<NetworkShopPlayerState>()?.ServerTransferRunCreditsToOwner();
        if (anyoneReady) CheckPartyDefeat();
        if (!anyoneConnected && !ServerRoster.HasReconnectReservations(now))
        {
            foreach (MirrorSessionRoster.Member member in ServerRoster.Members) DestroyRetainedPlayer(member);
            ServerRoster.Reset();
            ResetRunSnapshot();
            ServerChangeScene(SessionLobbyScene);
        }
    }

    private void SetPartyAbsentPause(bool pause)
    {
        if (pause == pausedForAbsentParty) return;
        pausedForAbsentParty = pause;
        if (pause)
        {
            timeScaleBeforePartyPause = Time.timeScale;
            Time.timeScale = 0;
        }
        else Time.timeScale = timeScaleBeforePartyPause;
    }
}

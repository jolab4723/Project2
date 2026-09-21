using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>플레이어 생성 전에 빌드와 참가 자격을 검사한다. 비밀 토큰은 소유 연결에만 응답한다.</summary>
[DisallowMultipleComponent]
public sealed class MirrorSessionAuthenticator_MirrorTest : NetworkAuthenticator
{
    [SerializeField] private PassiveSkillDatabaseSO passiveDatabase;
    public PassiveSkillDatabaseSO PassiveDatabase => passiveDatabase;
    public struct AdmissionRequest : NetworkMessage
    {
        public int Version;
        public string DisplayName;
        public string SessionId;
        public string ReconnectToken;
        public string PassiveProfileJson;
    }

    public struct AdmissionResponse : NetworkMessage
    {
        public bool Accepted;
        public string Reason;
        public string SessionId;
        public string ParticipantId;
        public string ReconnectToken;
    }

    private readonly HashSet<NetworkConnectionToClient> pending = new();
    private MirrorTestNetworkManager manager;
    private Coroutine clientTimeout;

    public override void OnStartServer()
    {
        manager = GetComponent<MirrorTestNetworkManager>();
        NetworkServer.RegisterHandler<AdmissionRequest>(HandleAdmission, false);
    }

    public override void OnStopServer()
    {
        NetworkServer.UnregisterHandler<AdmissionRequest>();
        pending.Clear();
        StopAllCoroutines();
    }

    /// <summary>전송 연결은 최대 10초만 인증을 기다린다. 게임 일시 정지 중에도 제한은 흐른다.</summary>
    public override void OnServerAuthenticate(NetworkConnectionToClient connection)
    {
        pending.Add(connection);
        StartCoroutine(ExpireAdmission(connection));
    }

    private IEnumerator ExpireAdmission(NetworkConnectionToClient connection)
    {
        yield return new WaitForSecondsRealtime(10);
        if (pending.Remove(connection)) ServerReject(connection);
    }

    private void HandleAdmission(NetworkConnectionToClient connection, AdmissionRequest request)
    {
        if (!pending.Remove(connection) || connection.isAuthenticated) return;
        MirrorSessionRoster_MirrorTest.Member member = null;
        string reason = null;
        bool accepted = request.Version == MirrorTestNetworkManager.CompatibilityVersion;
        if (!accepted) reason = "서버와 클라이언트의 빌드 버전이 다릅니다.";
        else if (string.IsNullOrEmpty(request.SessionId) && string.IsNullOrEmpty(request.ReconnectToken))
        {
            accepted = MirrorPassiveProfile_MirrorTest.TryValidate(request.PassiveProfileJson, passiveDatabase,
                out MirrorPassiveProfile_MirrorTest passive, out reason);
            if (accepted)
            {
                accepted = manager.ServerRoster.TryJoin(connection.connectionId, request.DisplayName, out member, out reason);
                if (accepted) member.PassiveProfile = passive;
            }
        }
        else
            accepted = manager.ServerRoster.TryResume(connection.connectionId, request.SessionId,
                request.ReconnectToken, Time.realtimeSinceStartupAsDouble, out member, out reason);

        connection.Send(new AdmissionResponse
        {
            Accepted = accepted,
            Reason = reason,
            SessionId = accepted ? manager.ServerRoster.SessionId : null,
            ParticipantId = member?.ParticipantId,
            ReconnectToken = member?.ReconnectToken
        });
        if (accepted)
        {
            connection.authenticationData = member.ParticipantId;
            ServerAccept(connection);
        }
        else StartCoroutine(RejectAfterReply(connection));
    }

    private IEnumerator RejectAfterReply(NetworkConnectionToClient connection)
    {
        yield return new WaitForSecondsRealtime(0.25f);
        ServerReject(connection);
    }

    public override void OnStartClient()
    {
        manager = GetComponent<MirrorTestNetworkManager>();
        NetworkClient.RegisterHandler<AdmissionResponse>(HandleAdmissionResponse, false);
    }

    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler<AdmissionResponse>();
        if (clientTimeout != null) StopCoroutine(clientTimeout);
        clientTimeout = null;
    }

    /// <summary>새 참가 또는 사용자가 선택한 최근 세션 복귀 자격을 보낸다.</summary>
    public override void OnClientAuthenticate()
    {
        MirrorReconnectProfile_MirrorTest profile = manager.RequestedReconnectProfile;
        string passiveJson;
        try
        {
            passiveJson = profile == null ?
                MirrorSmokeConfiguration_MirrorTest.PassiveFixtureJson() ?? MirrorPassiveProfile_MirrorTest.ReadLocalPayload() : null;
        }
        catch (System.Exception exception) when (exception is System.IO.IOException ||
            exception is System.UnauthorizedAccessException || exception is System.ArgumentException)
        {
            manager.SetAdmissionStatus("저장된 패시브 프로필을 읽지 못했습니다.");
            ClientReject();
            return;
        }
        NetworkClient.Send(new AdmissionRequest
        {
            Version = MirrorTestNetworkManager.CompatibilityVersion,
            DisplayName = manager.ClientDisplayName,
            SessionId = profile?.SessionId,
            ReconnectToken = profile?.ReconnectToken,
            PassiveProfileJson = passiveJson
        });
        clientTimeout = StartCoroutine(ExpireClientAdmission());
    }

    private IEnumerator ExpireClientAdmission()
    {
        yield return new WaitForSecondsRealtime(12);
        clientTimeout = null;
        if (NetworkClient.connection != null && !NetworkClient.connection.isAuthenticated)
        {
            manager.SetAdmissionStatus("서버 참가 승인 시간이 초과되었습니다.");
            ClientReject();
        }
    }

    private void HandleAdmissionResponse(AdmissionResponse response)
    {
        if (clientTimeout != null) StopCoroutine(clientTimeout);
        clientTimeout = null;
        if (!response.Accepted)
        {
            manager.SetAdmissionStatus(response.Reason);
            ClientReject();
            return;
        }
        manager.AcceptLocalParticipant(response.ParticipantId);
        var profile = new MirrorReconnectProfile_MirrorTest
        {
            ServerAddress = manager.networkAddress,
            SessionId = response.SessionId,
            ParticipantId = response.ParticipantId,
            ReconnectToken = response.ReconnectToken
        };
        if (!MirrorReconnectProfile_MirrorTest.Save(profile, out string reason))
            Debug.LogWarning(reason);
        manager.SetAdmissionStatus("참가 승인 완료");
        ClientAccept();
    }
}

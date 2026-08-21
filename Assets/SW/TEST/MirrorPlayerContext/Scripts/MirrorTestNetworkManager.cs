using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct MirrorCompatibilityRequestMessage : NetworkMessage
{
    public int Version;
}

public struct MirrorCompatibilityResponseMessage : NetworkMessage
{
    public bool Accepted;
    public int ServerVersion;
}

public enum MirrorSessionRoute : byte
{
    Unknown,
    StageSelect,
    Camp,
    Event,
    Combat,
}

public struct MirrorSessionRouteRequestMessage : NetworkMessage
{
    public MirrorSessionRoute Route;
}

public struct MirrorStageNodeSelectionRequestMessage : NetworkMessage
{
    public string NodeId;
}

public struct MirrorUnknownStageChoiceRequestMessage : NetworkMessage
{
    public byte ChoiceIndex;
}

public struct MirrorSessionRunSnapshotMessage : NetworkMessage
{
    public uint Revision;
    public string SnapshotJson;
}

public struct MirrorSessionLeadershipMessage : NetworkMessage
{
    public bool IsLeader;
}

/// <summary>
/// Mirror 테스트의 전역 연결 수명주기를 담당한다.
/// <para>6-A 이전 기능인 로컬·서버 PlayerContext 등록과 빈 전용 서버 세션 재시작을 유지한다.</para>
/// <para>6-A에서는 플레이어를 생성하기 전에 Client와 Server의 네트워크 호환 버전을 확인하고,
/// 호환 확인이 끝난 연결만 전투 세션 시작 요청을 보낼 수 있게 한다.</para>
/// <para>6-B에서는 전환 전용 Camp에서 받은 최초 시작 요청으로 모든 Client를 Stage1으로 옮기고,
/// Scene보다 오래 살아 있는 PlayerContext와 새 Scene의 로컬 UI를 다시 연결한다.</para>
/// <para>6-B 재접속 보완: 서버 Scene을 불러오는 동안 호환 응답이 먼저 처리돼도 Player 생성을 재촉하지 않고,
/// Mirror의 Scene 완료 경로가 Ready와 AddPlayer를 한 번만 처리하게 한다.</para>
/// <para>6-C에서는 팀원 원본 StageSelect를 수정하지 않고 SW 테스트 Scene으로 복제하여,
/// 실제 노드 선택을 서버 권한 Camp 또는 전투 이동과 선택 Scene 복귀에 연결한다.</para>
/// <para>7-2에서는 Host의 로컬 Client 또는 전용 서버의 첫 호환 Client를 세션 방장으로 정하고,
/// 방장만 전체 파티의 Scene 이동과 세션 시작을 요청하게 한다.</para>
/// <para>7-3에서는 방장이 nodeId만 서버에 보내고, 서버가 런 Snapshot의 진행도와 연결 그래프를 검증한 뒤
/// pending/cleared/revision을 갱신해 다음 노드 가용성과 재접속 복원을 하나의 원본으로 유지한다.</para>
/// </summary>
public sealed class MirrorTestNetworkManager : NetworkManager
{
    // ponytail: 현재는 수동 호환 버전 하나면 충분하다. 네트워크 DTO·SyncVar 순서가 바뀔 때만
    // 이 값을 올리며, 빌드가 잦아 수동 갱신 누락이 실제로 반복될 때 Git 해시 자동 생성을 검토한다.
    public const int CompatibilityVersion = 2026082102;
    internal const int InitialRunSeed = 382597156;

    public const string SessionCampScene =
        "Assets/SW/TEST/MirrorCombat/Scenes/StageSelect_MirrorSessionTest.unity";
    public const string SessionCampGameplayScene =
        "Assets/SW/TEST/MirrorCombat/Scenes/Act1_Camp_MirrorSessionTest.unity";
    public const string SessionCombatScene =
        "Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage1_MirrorSessionTest.unity";
    public const string SessionUnknownScene =
        "Assets/SW/TEST/MirrorCombat/Scenes/Unknown_Stage_MirrorSessionTest.unity";

    private const float CompatibilityTimeoutSeconds = 5f;
    private const float RejectionDeliveryDelaySeconds = 0.2f;
    private const string UnknownStageDatabaseResourcePath =
        "DataFiles/UnknownStageData/3. GeneratedAssets/AllUnknownStages";

    [SerializeField, Min(0f)] private float emptySessionResetDelay = 15f;

    private readonly HashSet<PlayerContext> serverPlayerContexts = new();
    private readonly HashSet<int> compatibleConnectionIds = new();
    private Coroutine emptySessionResetRoutine;
    private Coroutine clientCompatibilityTimeoutRoutine;
    private bool sessionHasStarted;
    private bool isEndingEmptySession;
    private bool clientCompatibilityConfirmed;
    private bool clientIsSessionLeader;
    private bool sessionSceneChangeRequested;
    private int sessionLeaderConnectionId = -1;
    private uint runSnapshotRevision;
    private string runSnapshotJson = string.Empty;
    private MirrorSessionRoute pendingSessionRoute = MirrorSessionRoute.StageSelect;
    private string compatibilityStatusMessage = "서버 연결 전";

    public PlayerContext LocalPlayerContext { get; private set; }
    public event Action<PlayerContext> LocalPlayerContextChanged;
    public event Action<uint> RunSnapshotChanged;
    public event Action<bool> ClientSessionLeaderChanged;
    public IReadOnlyCollection<PlayerContext> ServerPlayerContexts => serverPlayerContexts;
    public bool ClientCompatibilityConfirmed => clientCompatibilityConfirmed;
    public bool ClientIsSessionLeader => clientIsSessionLeader;
    public int ServerSessionLeaderConnectionId => sessionLeaderConnectionId;
    public bool CanLocalClientControlSession =>
        NetworkClient.active &&
        NetworkClient.ready &&
        clientCompatibilityConfirmed &&
        clientIsSessionLeader &&
        NetworkClient.localPlayer != null;
    public uint RunSnapshotRevision => runSnapshotRevision;
    public bool HasRunSnapshot => !string.IsNullOrEmpty(runSnapshotJson);
    public string CompatibilityStatusMessage => compatibilityStatusMessage;
    public bool IsSessionSelectionActive =>
        SceneManager.GetActiveScene().path == SessionCampScene;
    public MirrorSessionRoute CurrentSessionRoute =>
        GetRouteForScene(SceneManager.GetActiveScene().path);

    /// <summary>
    /// StageSelect 복귀로 같은 Scene이 다시 로드될 때는 기존 세션 Manager가 이미
    /// DontDestroyOnLoad에 남아 있다. Mirror 기본 초기화보다 먼저 Scene 복제본을 제거해
    /// 같은 책임의 Manager를 한 프레임도 중복 초기화하지 않는다.
    /// </summary>
    public override void Awake()
    {
        if (NetworkManager.singleton != null && NetworkManager.singleton != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        base.Awake();

        UnityEngine.EventSystems.EventSystem sessionEventSystem =
            GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true);
        if (sessionEventSystem != null && !sessionEventSystem.gameObject.activeSelf)
            sessionEventSystem.gameObject.SetActive(true);
    }

    /// <summary>
    /// 일반 Client/Host 빌드의 시작 방식은 그대로 유지하고, 전용 서버 빌드에서만
    /// Mirror가 화면 없는 서버를 자동으로 시작하도록 설정합니다.
    /// </summary>
    public override void Start()
    {
#if UNITY_SERVER
        headlessStartMode = HeadlessStartOptions.AutoStartServer;
#endif
        base.Start();
    }

    internal void RegisterLocalPlayer(PlayerContext context)
    {
        // Scene 전환 중 원격 복제본의 생명주기 콜백이 섞여 들어와도 UI 소유자가 바뀌지 않도록
        // Mirror가 확정한 실제 localPlayer와 같은 NetworkIdentity만 로컬 Context로 등록한다.
        PlayerContext mirrorLocalContext = ResolveMirrorLocalPlayerContext();
        if (context == null || mirrorLocalContext != context || LocalPlayerContext == context)
            return;

        LocalPlayerContext = context;
        LocalPlayerContextChanged?.Invoke(context);
    }

    internal void UnregisterLocalPlayer(PlayerContext context)
    {
        if (LocalPlayerContext != context)
            return;

        LocalPlayerContext = null;
        LocalPlayerContextChanged?.Invoke(null);
    }

    internal void RegisterServerPlayer(PlayerContext context)
    {
        if (context == null || !serverPlayerContexts.Add(context))
            return;

        NetworkConnectionToClient ownerConnection = context
            .GetComponent<NetworkIdentity>()
            ?.connectionToClient;
        if (sessionLeaderConnectionId < 0 &&
            ownerConnection != null &&
            compatibleConnectionIds.Contains(ownerConnection.connectionId))
        {
            sessionLeaderConnectionId = ownerConnection.connectionId;
            BroadcastSessionLeadership();
            Debug.Log(
                $"[MirrorTestNetworkManager] 세션 방장 지정: " +
                $"connectionId={sessionLeaderConnectionId} | " +
                $"형태={(NetworkClient.active ? "Host" : "전용 서버")}");
        }

        sessionHasStarted = true;
        CancelEmptySessionReset();
        FindFirstObjectByType<NetworkShopState_MirrorTest>()?.ServerRefreshPartyBenefits();
    }

    internal void UnregisterServerPlayer(PlayerContext context)
    {
        if (context == null || !serverPlayerContexts.Remove(context))
            return;

        FindFirstObjectByType<NetworkShopState_MirrorTest>()?.ServerRefreshPartyBenefits();
        TryScheduleEmptySessionReset();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<MirrorCompatibilityRequestMessage>(
            HandleServerCompatibilityRequest,
            false);
        NetworkServer.RegisterHandler<MirrorSessionRouteRequestMessage>(
            HandleServerSessionRouteRequest);
        NetworkServer.RegisterHandler<MirrorStageNodeSelectionRequestMessage>(
            HandleServerStageNodeSelectionRequest);
        NetworkServer.RegisterHandler<MirrorUnknownStageChoiceRequestMessage>(
            HandleServerUnknownStageChoiceRequest);
        ResetRunSnapshot();
        sessionHasStarted = false;
        isEndingEmptySession = false;
        emptySessionResetRoutine = null;
        sessionSceneChangeRequested = false;
        pendingSessionRoute = MirrorSessionRoute.StageSelect;
        sessionLeaderConnectionId = -1;
        compatibleConnectionIds.Clear();
        Debug.Log(
            $"[MirrorTestNetworkManager] 서버 시작: UDP 포트 {GetServerPort()} | " +
            $"호환 버전 {CompatibilityVersion}");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkClient.RegisterHandler<MirrorCompatibilityResponseMessage>(
            HandleClientCompatibilityResponse,
            false);
        NetworkClient.RegisterHandler<MirrorSessionRunSnapshotMessage>(
            HandleClientRunSnapshot,
            false);
        NetworkClient.RegisterHandler<MirrorSessionLeadershipMessage>(
            HandleClientSessionLeadership,
            false);
        if (!NetworkServer.active)
            ResetRunSnapshot();
        clientCompatibilityConfirmed = false;
        SetClientSessionLeader(false);
        compatibilityStatusMessage = "서버 연결 대기 중";
    }

    /// <summary>
    /// Mirror의 기본 <c>OnClientConnect</c>는 즉시 Ready와 AddPlayer를 보낸다.
    /// 여기서는 먼저 호환 버전을 보내고, 서버 승인 응답을 받은 뒤에만 기본 구현을 호출해
    /// 서로 다른 SyncVar·NetworkMessage 구조의 빌드가 플레이어를 생성하지 못하게 한다.
    /// </summary>
    public override void OnClientConnect()
    {
        clientCompatibilityConfirmed = false;
        SetClientSessionLeader(false);
        compatibilityStatusMessage =
            $"호환 버전 확인 중: {CompatibilityVersion}";

        NetworkClient.Send(new MirrorCompatibilityRequestMessage
        {
            Version = CompatibilityVersion,
        });

        if (clientCompatibilityTimeoutRoutine != null)
            StopCoroutine(clientCompatibilityTimeoutRoutine);
        clientCompatibilityTimeoutRoutine =
            StartCoroutine(StopUnverifiedClientAfterTimeout());
    }

    public override void OnServerConnect(NetworkConnectionToClient connection)
    {
        base.OnServerConnect(connection);
        StartCoroutine(DisconnectUnverifiedConnectionAfterTimeout(connection));
        Debug.Log(
            $"[MirrorTestNetworkManager] Client 전송 연결: connectionId={connection.connectionId} | " +
            "플레이어 생성 전 호환 버전 확인 대기");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient connection)
    {
        bool leaderDisconnected = sessionLeaderConnectionId == connection.connectionId;
        compatibleConnectionIds.Remove(connection.connectionId);
        Debug.Log($"[MirrorTestNetworkManager] Client 접속 종료: connectionId={connection.connectionId}");
        base.OnServerDisconnect(connection);

        if (leaderDisconnected)
        {
            sessionLeaderConnectionId = SelectLowestReadyConnectionId();
            BroadcastSessionLeadership();
            Debug.Log(
                $"[MirrorTestNetworkManager] 세션 방장 승계: " +
                $"connectionId={sessionLeaderConnectionId}");
        }

        TryScheduleEmptySessionReset();
    }

    public override void OnClientDisconnect()
    {
        CancelClientCompatibilityTimeout();
        clientCompatibilityConfirmed = false;
        SetClientSessionLeader(false);
        base.OnClientDisconnect();
    }

    public override void OnStopClient()
    {
        CancelClientCompatibilityTimeout();
        NetworkClient.UnregisterHandler<MirrorCompatibilityResponseMessage>();
        NetworkClient.UnregisterHandler<MirrorSessionRunSnapshotMessage>();
        NetworkClient.UnregisterHandler<MirrorSessionLeadershipMessage>();
        clientCompatibilityConfirmed = false;
        SetClientSessionLeader(false);
        base.OnStopClient();

        if (!NetworkServer.active)
            ResetRunSnapshot();

        if (LocalPlayerContext != null)
        {
            LocalPlayerContext = null;
            LocalPlayerContextChanged?.Invoke(null);
        }
    }

    public override void OnStopServer()
    {
        CancelEmptySessionReset();
        NetworkServer.UnregisterHandler<MirrorCompatibilityRequestMessage>();
        NetworkServer.UnregisterHandler<MirrorSessionRouteRequestMessage>();
        NetworkServer.UnregisterHandler<MirrorStageNodeSelectionRequestMessage>();
        NetworkServer.UnregisterHandler<MirrorUnknownStageChoiceRequestMessage>();

        compatibleConnectionIds.Clear();
        sessionHasStarted = false;
        isEndingEmptySession = false;
        sessionSceneChangeRequested = false;
        pendingSessionRoute = MirrorSessionRoute.StageSelect;
        sessionLeaderConnectionId = -1;
        serverPlayerContexts.Clear();
        ResetRunSnapshot();
        Debug.Log("[MirrorTestNetworkManager] 서버 종료");
        base.OnStopServer();
    }

    /// <summary>
    /// 로컬 Client가 대기 중인 전투 세션의 시작을 서버에 요청한다.
    /// Host의 로컬 Client 또는 전용 서버가 정한 방장 Client만 요청할 수 있다.
    /// 서버가 이미 시작했으면 요청은 무변경으로 끝난다.
    /// </summary>
    public bool RequestStartSession()
    {
        return RequestSessionRoute(MirrorSessionRoute.Combat);
    }

    /// <summary>
    /// 플레이 Scene의 선택 화면 복귀 또는 이미 pending인 스테이지 재진입을 서버에 요청한다.
    /// 최초 스테이지 진입은 이 범용 경로가 아니라 RequestStageNodeSelection을 거쳐야 한다.
    /// </summary>
    public bool RequestSessionRoute(MirrorSessionRoute route)
    {
        if (!CanLocalClientControlSession)
            return false;

        NetworkClient.Send(new MirrorSessionRouteRequestMessage
        {
            Route = route,
        });
        return true;
    }

    /// <summary>
    /// StageSelect에서 선택한 노드 ID만 서버에 전달한다.
    /// 노드 종류, 이동 Scene, 현재 진행 가능 여부는 클라이언트 값을 신뢰하지 않고 서버 Snapshot으로 판정한다.
    /// </summary>
    public bool RequestStageNodeSelection(string nodeId)
    {
        if (!CanLocalClientControlSession ||
            !IsSessionSelectionActive ||
            string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        NetworkClient.Send(new MirrorStageNodeSelectionRequestMessage
        {
            NodeId = nodeId,
        });
        return true;
    }

    /// <summary>
    /// StageSelect에 복원된 pending 노드의 서버 확정 타입으로 재진입을 요청한다.
    /// Client가 임의 Scene을 고르지 않고 기존 pending 검증과 Route 요청 경계를 그대로 재사용한다.
    /// </summary>
    public bool RequestPendingStageReentry()
    {
        if (!CanLocalClientControlSession ||
            CurrentSessionRoute != MirrorSessionRoute.StageSelect ||
            !TryGetPendingStageNode(out StageNodeSaveData pendingNode))
        {
            return false;
        }

        return RequestSessionRoute(GetRouteForStageNodeType(pendingNode.type));
    }

    /// <summary>
    /// 미지 Scene에서 방장이 고른 선택지 번호만 서버에 전달한다.
    /// 이벤트 ID와 실제 선택지 범위, pending 완료 여부는 서버 Snapshot과 데이터베이스로 판정한다.
    /// </summary>
    public bool RequestUnknownStageChoice(int choiceIndex)
    {
        if (!CanLocalClientControlSession ||
            CurrentSessionRoute != MirrorSessionRoute.Event ||
            choiceIndex < 0 ||
            choiceIndex > 2)
        {
            return false;
        }

        NetworkClient.Send(new MirrorUnknownStageChoiceRequestMessage
        {
            ChoiceIndex = (byte)choiceIndex,
        });
        return true;
    }

    /// <summary>
    /// 서버가 소유하는 Act 진행 Snapshot을 한 번 갱신하고 현재 접속자에게 배포한다.
    /// StageMapSaveData JSON 하나가 원본이며 Scene별 복제 상태는 만들지 않는다.
    /// </summary>
    internal bool ServerPublishRunSnapshot(StageMapSaveData snapshot)
    {
        if (!NetworkServer.active || !IsRunSnapshotValid(snapshot))
        {
            Debug.LogError("[MirrorTestNetworkManager] 유효하지 않은 Run Snapshot 배포 요청을 거부했습니다.");
            return false;
        }

        string snapshotJson = JsonUtility.ToJson(snapshot);
        if (snapshotJson == runSnapshotJson)
            return true;

        runSnapshotJson = snapshotJson;
        runSnapshotRevision = runSnapshotRevision == uint.MaxValue
            ? 1u
            : runSnapshotRevision + 1u;

        foreach (int connectionId in compatibleConnectionIds)
        {
            if (NetworkServer.connections.TryGetValue(
                    connectionId,
                    out NetworkConnectionToClient connection))
            {
                SendRunSnapshot(connection);
            }
        }

        RunSnapshotChanged?.Invoke(runSnapshotRevision);
        Debug.Log(
            $"[MirrorTestNetworkManager] 서버 Run Snapshot 갱신: " +
            $"revision={runSnapshotRevision}, seed={snapshot.mapSeed}, nodes={snapshot.nodes.Count}");
        return true;
    }

    /// <summary>
    /// 서버 원본 또는 서버가 복제한 로컬 Snapshot의 독립 복사본을 반환한다.
    /// 호출자가 반환값을 바꿔도 Manager의 JSON 원본은 변하지 않는다.
    /// </summary>
    public bool TryGetRunSnapshot(out StageMapSaveData snapshot)
    {
        return TryDeserializeRunSnapshot(runSnapshotJson, out snapshot);
    }

    /// <summary>
    /// 현재 서버 Run Snapshot이 가리키는 진행 중 노드를 반환한다.
    /// 전투 Scene의 런타임 어댑터가 노드 종류를 추측하지 않고 같은 서버 원본을 재사용하는 경계다.
    /// </summary>
    public bool TryGetPendingStageNode(out StageNodeSaveData pendingNode)
    {
        pendingNode = null;
        return TryGetRunSnapshot(out StageMapSaveData snapshot) &&
               !string.IsNullOrEmpty(snapshot.pendingNodeId) &&
               TryFindSelectableStageNode(
                   snapshot,
                   snapshot.pendingNodeId,
                   out pendingNode,
                   out _);
    }

    /// <summary>
    /// 전투·캠프의 서버 판정 지점이 호출하는 7-3 완료 경계다.
    /// 현재 pending 노드만 클리어하고 Snapshot을 먼저 배포한 뒤 파티를 StageSelect로 복귀시킨다.
    /// 7-4의 정식 웨이브 포탈은 이 메서드만 호출하면 된다.
    /// </summary>
    [Server]
    public bool ServerTryCompletePendingStageAndReturnToSelection()
    {
        if (sessionSceneChangeRequested || NetworkServer.isLoadingScene)
            return false;

        if (!TryGetRunSnapshot(out StageMapSaveData snapshot))
        {
            Debug.LogWarning("[MirrorTestNetworkManager] 완료할 Run Snapshot이 없습니다.");
            return false;
        }

        if (!TryCompletePendingStageNode(
                snapshot,
                out StageNodeSaveData completedNode,
                out string error))
        {
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] pending 노드 완료 거부: {error}");
            return false;
        }

        MirrorSessionRoute currentRoute = GetRouteForScene(SceneManager.GetActiveScene().path);
        MirrorSessionRoute expectedRoute = GetRouteForStageNodeType(completedNode.type);
        if (currentRoute != expectedRoute ||
            !CanChangeSessionRoute(currentRoute, MirrorSessionRoute.StageSelect))
        {
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] pending 노드와 현재 Scene이 일치하지 않아 완료를 거부했습니다. " +
                $"node={completedNode.id}, expected={expectedRoute}, current={currentRoute}");
            return false;
        }

        if (!ServerPublishRunSnapshot(snapshot))
            return false;

        pendingSessionRoute = MirrorSessionRoute.StageSelect;
        sessionSceneChangeRequested = true;
        Debug.Log(
            $"[MirrorTestNetworkManager] 스테이지 클리어 및 선택 화면 복귀: " +
            $"node={completedNode.id}, floor={completedNode.floor}, revision={runSnapshotRevision}");
        ServerChangeScene(SessionCampScene);
        return true;
    }

    [Server]
    public bool ServerTryCompletePendingBossWithoutSceneChange()
    {
        if (sessionSceneChangeRequested || NetworkServer.isLoadingScene)
            return false;

        if (!TryGetRunSnapshot(out StageMapSaveData snapshot))
        {
            Debug.LogWarning("[MirrorTestNetworkManager] No run snapshot exists to complete.");
            return false;
        }

        if (!TryCompletePendingStageNode(
                snapshot,
                out StageNodeSaveData completedNode,
                out string error))
        {
            Debug.LogWarning($"[MirrorTestNetworkManager] Pending boss completion rejected: {error}");
            return false;
        }

        MirrorSessionRoute currentRoute = GetRouteForScene(SceneManager.GetActiveScene().path);
        MirrorSessionRoute expectedRoute = GetRouteForStageNodeType(completedNode.type);
        if (completedNode.type != StageNodeType.Boss || currentRoute != expectedRoute)
        {
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] Final clear requires the pending boss scene: " +
                $"node={completedNode.id}, type={completedNode.type}, " +
                $"expected={expectedRoute}, current={currentRoute}");
            return false;
        }

        if (!ServerPublishRunSnapshot(snapshot))
            return false;

        Debug.Log(
            $"[MirrorTestNetworkManager] Final boss node completed without scene change: " +
            $"node={completedNode.id}, floor={completedNode.floor}, revision={runSnapshotRevision}");
        return true;
    }

    private void HandleClientRunSnapshot(MirrorSessionRunSnapshotMessage message)
    {
        if (message.Revision == 0 ||
            string.IsNullOrEmpty(message.SnapshotJson) ||
            message.Revision < runSnapshotRevision ||
            (message.Revision == runSnapshotRevision &&
             message.SnapshotJson == runSnapshotJson))
        {
            return;
        }

        if (message.Revision == runSnapshotRevision ||
            !TryDeserializeRunSnapshot(message.SnapshotJson, out StageMapSaveData snapshot))
        {
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] 유효하지 않거나 충돌하는 Run Snapshot을 무시했습니다. " +
                $"local={runSnapshotRevision}, received={message.Revision}");
            return;
        }

        runSnapshotRevision = message.Revision;
        runSnapshotJson = message.SnapshotJson;
        RunSnapshotChanged?.Invoke(runSnapshotRevision);
        Debug.Log(
            $"[MirrorTestNetworkManager] 서버 Run Snapshot 적용: " +
            $"revision={runSnapshotRevision}, seed={snapshot.mapSeed}, nodes={snapshot.nodes.Count}");
    }

    private void SendRunSnapshot(NetworkConnectionToClient connection)
    {
        if (connection == null || !HasRunSnapshot || runSnapshotRevision == 0)
            return;

        connection.Send(new MirrorSessionRunSnapshotMessage
        {
            Revision = runSnapshotRevision,
            SnapshotJson = runSnapshotJson,
        });
    }

    private static bool TryDeserializeRunSnapshot(
        string snapshotJson,
        out StageMapSaveData snapshot)
    {
        snapshot = null;
        if (string.IsNullOrEmpty(snapshotJson))
            return false;

        try
        {
            snapshot = JsonUtility.FromJson<StageMapSaveData>(snapshotJson);
            return IsRunSnapshotValid(snapshot);
        }
        catch (Exception)
        {
            snapshot = null;
            return false;
        }
    }

    private static bool IsRunSnapshotValid(StageMapSaveData snapshot)
    {
        return snapshot != null &&
               snapshot.mapSeed != 0 &&
               snapshot.nodes != null &&
               snapshot.nodes.Count > 0;
    }

    private static bool TryBeginStageNode(
        StageMapSaveData snapshot,
        string nodeId,
        out StageNodeSaveData selectedNode,
        out string error)
    {
        selectedNode = null;
        if (snapshot == null)
        {
            error = "Run Snapshot이 없습니다.";
            return false;
        }

        if (!string.IsNullOrEmpty(snapshot.pendingNodeId))
        {
            error = $"이미 진행 중인 노드가 있습니다: {snapshot.pendingNodeId}";
            return false;
        }

        if (!TryFindSelectableStageNode(snapshot, nodeId, out selectedNode, out error))
            return false;

        snapshot.pendingNodeId = selectedNode.id;
        return true;
    }

    private static bool TryCompletePendingStageNode(
        StageMapSaveData snapshot,
        out StageNodeSaveData completedNode,
        out string error)
    {
        completedNode = null;
        if (snapshot == null || string.IsNullOrEmpty(snapshot.pendingNodeId))
        {
            error = "완료할 pending 노드가 없습니다.";
            return false;
        }

        if (!TryFindSelectableStageNode(
                snapshot,
                snapshot.pendingNodeId,
                out completedNode,
                out error))
        {
            return false;
        }

        snapshot.clearedNodeIds ??= new List<string>();
        snapshot.visitedNodeIds ??= new List<string>();
        if (!snapshot.clearedNodeIds.Contains(completedNode.id))
            snapshot.clearedNodeIds.Add(completedNode.id);
        if (!snapshot.visitedNodeIds.Contains(completedNode.id))
            snapshot.visitedNodeIds.Add(completedNode.id);

        snapshot.clearedFloor = completedNode.floor;
        snapshot.lastClearedNodeId = completedNode.id;
        snapshot.pendingNodeId = string.Empty;
        return true;
    }

    private bool CanResumePendingStage(
        MirrorSessionRoute requestedRoute,
        out string error)
    {
        if (!TryGetRunSnapshot(out StageMapSaveData snapshot) ||
            string.IsNullOrEmpty(snapshot.pendingNodeId))
        {
            error = "재진입할 pending 노드가 없습니다.";
            return false;
        }

        if (!TryFindSelectableStageNode(
                snapshot,
                snapshot.pendingNodeId,
                out StageNodeSaveData pendingNode,
                out error))
        {
            return false;
        }

        MirrorSessionRoute expectedRoute = GetRouteForStageNodeType(pendingNode.type);
        if (requestedRoute != expectedRoute)
        {
            error = $"pending 노드 경로는 {expectedRoute}입니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryFindSelectableStageNode(
        StageMapSaveData snapshot,
        string nodeId,
        out StageNodeSaveData selectedNode,
        out string error)
    {
        selectedNode = null;
        if (!IsRunSnapshotValid(snapshot) || string.IsNullOrWhiteSpace(nodeId))
        {
            error = "Snapshot 또는 nodeId가 유효하지 않습니다.";
            return false;
        }

        int maximumFloor = 0;
        StageNodeSaveData lastClearedNode = null;
        foreach (StageNodeSaveData node in snapshot.nodes)
        {
            if (node == null)
                continue;

            maximumFloor = Math.Max(maximumFloor, node.floor);
            if (node.id == nodeId)
                selectedNode = node;
            if (node.id == snapshot.lastClearedNodeId)
                lastClearedNode = node;
        }

        if (selectedNode == null)
        {
            error = $"Snapshot에 nodeId가 없습니다: {nodeId}";
            return false;
        }

        int selectableFloor = snapshot.clearedFloor + 1;
        if (snapshot.clearedFloor < 0 ||
            snapshot.clearedFloor >= maximumFloor ||
            selectedNode.floor != selectableFloor)
        {
            error = $"현재 선택 가능 층이 아닙니다: nodeFloor={selectedNode.floor}, selectable={selectableFloor}";
            return false;
        }

        if (snapshot.clearedNodeIds != null &&
            snapshot.clearedNodeIds.Contains(selectedNode.id))
        {
            error = $"이미 클리어한 노드입니다: {selectedNode.id}";
            return false;
        }

        bool restrictToConnectedNodes = lastClearedNode != null &&
                                        lastClearedNode.floor == snapshot.clearedFloor;
        if (restrictToConnectedNodes &&
            !IsNodeReachableFrom(snapshot.nodes, lastClearedNode, selectedNode.id))
        {
            error = $"마지막 클리어 노드에서 도달할 수 없습니다: {selectedNode.id}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsNodeReachableFrom(
        List<StageNodeSaveData> nodes,
        StageNodeSaveData startNode,
        string targetNodeId)
    {
        var nodesById = new Dictionary<string, StageNodeSaveData>();
        foreach (StageNodeSaveData node in nodes)
        {
            if (node != null && !string.IsNullOrEmpty(node.id))
                nodesById[node.id] = node;
        }

        var visitedNodeIds = new HashSet<string>();
        var nodesToVisit = new Queue<StageNodeSaveData>();
        nodesToVisit.Enqueue(startNode);
        while (nodesToVisit.Count > 0)
        {
            StageNodeSaveData currentNode = nodesToVisit.Dequeue();
            if (currentNode.nextNodeIds == null)
                continue;

            foreach (string nextNodeId in currentNode.nextNodeIds)
            {
                if (nextNodeId == targetNodeId)
                    return true;

                if (visitedNodeIds.Add(nextNodeId) &&
                    nodesById.TryGetValue(nextNodeId, out StageNodeSaveData nextNode))
                {
                    nodesToVisit.Enqueue(nextNode);
                }
            }
        }

        return false;
    }

    private void ResetRunSnapshot()
    {
        runSnapshotRevision = 0;
        runSnapshotJson = string.Empty;
    }

    private void HandleClientSessionLeadership(MirrorSessionLeadershipMessage message)
    {
        SetClientSessionLeader(message.IsLeader);
        Debug.Log(
            $"[MirrorTestNetworkManager] 로컬 세션 권한: " +
            $"{(message.IsLeader ? "방장" : "참가자")}");
    }

    private void SetClientSessionLeader(bool isLeader)
    {
        if (clientIsSessionLeader == isLeader)
            return;

        clientIsSessionLeader = isLeader;
        ClientSessionLeaderChanged?.Invoke(isLeader);
    }

    private void BroadcastSessionLeadership()
    {
        foreach (int connectionId in compatibleConnectionIds)
        {
            if (!NetworkServer.connections.TryGetValue(
                    connectionId,
                    out NetworkConnectionToClient connection))
            {
                continue;
            }

            connection.Send(new MirrorSessionLeadershipMessage
            {
                IsLeader = connectionId == sessionLeaderConnectionId,
            });
        }
    }

    private static int SelectLowestConnectionId(HashSet<int> connectionIds)
    {
        int selectedConnectionId = -1;
        foreach (int connectionId in connectionIds)
        {
            if (selectedConnectionId < 0 || connectionId < selectedConnectionId)
                selectedConnectionId = connectionId;
        }

        return selectedConnectionId;
    }

    private int SelectLowestReadyConnectionId()
    {
        var readyConnectionIds = new HashSet<int>();
        foreach (int connectionId in compatibleConnectionIds)
        {
            if (NetworkServer.connections.TryGetValue(
                    connectionId,
                    out NetworkConnectionToClient connection) &&
                connection.identity != null)
            {
                readyConnectionIds.Add(connectionId);
            }
        }

        return SelectLowestConnectionId(readyConnectionIds);
    }

    private static bool CanControlSession(
        int requesterConnectionId,
        int leaderConnectionId,
        bool compatible,
        bool hasPlayer)
    {
        return compatible &&
               hasPlayer &&
               leaderConnectionId >= 0 &&
               requesterConnectionId == leaderConnectionId;
    }

    private bool CanConnectionControlSession(
        NetworkConnectionToClient connection,
        string requestName)
    {
        bool compatible = connection != null &&
                          compatibleConnectionIds.Contains(connection.connectionId);
        bool hasPlayer = connection != null && connection.identity != null;
        int requesterConnectionId = connection != null ? connection.connectionId : -1;
        if (CanControlSession(
                requesterConnectionId,
                sessionLeaderConnectionId,
                compatible,
                hasPlayer))
        {
            return true;
        }

        Debug.LogWarning(
            $"[MirrorTestNetworkManager] 세션 방장 권한이 없는 {requestName} 요청을 거절했습니다. " +
            $"requester={requesterConnectionId}, leader={sessionLeaderConnectionId}, " +
            $"compatible={compatible}, player={hasPlayer}");
        return false;
    }

    private void HandleServerCompatibilityRequest(
        NetworkConnectionToClient connection,
        MirrorCompatibilityRequestMessage request)
    {
        bool accepted = IsCompatibleBuild(request.Version);

        string message = accepted
            ? $"호환 확인 완료: {CompatibilityVersion}"
            : $"빌드 버전 불일치: Client {request.Version}, Server {CompatibilityVersion}";

        connection.Send(new MirrorCompatibilityResponseMessage
        {
            Accepted = accepted,
            ServerVersion = CompatibilityVersion,
        });

        if (!accepted)
        {
            compatibleConnectionIds.Remove(connection.connectionId);
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] {message} | connectionId={connection.connectionId}");
            StartCoroutine(DisconnectRejectedConnectionAfterReply(connection));
            return;
        }

        compatibleConnectionIds.Add(connection.connectionId);
        BroadcastSessionLeadership();
        SendRunSnapshot(connection);
        if (CancelEmptySessionReset())
            Debug.Log("[MirrorTestNetworkManager] 호환되는 재접속을 확인해 빈 세션 초기화를 취소했습니다.");

        Debug.Log(
            $"[MirrorTestNetworkManager] Client 호환 확인: connectionId={connection.connectionId} | " +
            $"호환 버전 {request.Version}");
    }

    private void HandleClientCompatibilityResponse(
        MirrorCompatibilityResponseMessage response)
    {
        CancelClientCompatibilityTimeout();
        compatibilityStatusMessage = response.Accepted
            ? $"호환 확인 완료: {response.ServerVersion}"
            : $"빌드 버전 불일치: Client {CompatibilityVersion}, Server {response.ServerVersion}";

        if (!response.Accepted)
        {
            clientCompatibilityConfirmed = false;
            SetClientSessionLeader(false);
            Debug.LogWarning($"[MirrorTestNetworkManager] {compatibilityStatusMessage}");
            StartCoroutine(StopRejectedClientNextFrame());
            return;
        }

        if (clientCompatibilityConfirmed)
            return;

        clientCompatibilityConfirmed = true;
        Debug.Log($"[MirrorTestNetworkManager] {compatibilityStatusMessage}");

        // 서버 Scene을 처리 중이거나 이미 Ready/AddPlayer를 요청한 상태라면
        // OnClientSceneChanged가 생성 경계를 소유한다. 여기서 다시 기본 연결 처리를 호출하면
        // 재접속 때 같은 연결이 AddPlayer를 두 번 요청할 수 있다.
        if (NetworkClient.isLoadingScene ||
            NetworkClient.ready ||
            NetworkClient.localPlayer != null)
            return;

        // 호환 확인이 끝난 뒤에만 Ready/AddPlayer를 실행한다.
        base.OnClientConnect();
    }

    private void HandleServerStageNodeSelectionRequest(
        NetworkConnectionToClient connection,
        MirrorStageNodeSelectionRequestMessage request)
    {
        if (!CanConnectionControlSession(connection, "스테이지 노드 선택"))
            return;

        if (sessionSceneChangeRequested ||
            NetworkServer.isLoadingScene ||
            SceneManager.GetActiveScene().path != SessionCampScene)
        {
            return;
        }

        if (!TryGetRunSnapshot(out StageMapSaveData snapshot))
        {
            Debug.LogWarning("[MirrorTestNetworkManager] 선택할 Run Snapshot이 없습니다.");
            return;
        }

        if (!TryBeginStageNode(
                snapshot,
                request.NodeId,
                out StageNodeSaveData selectedNode,
                out string error))
        {
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] 스테이지 노드 선택 거부: " +
                $"node={request.NodeId}, reason={error}, connectionId={connection.connectionId}");
            return;
        }

        MirrorSessionRoute targetRoute = GetRouteForStageNodeType(selectedNode.type);
        if (!CanChangeSessionRoute(MirrorSessionRoute.StageSelect, targetRoute))
        {
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] 지원하지 않는 노드 이동 경로: " +
                $"node={selectedNode.id}, type={selectedNode.type}, route={targetRoute}");
            return;
        }

        if (!ServerPublishRunSnapshot(snapshot))
            return;

        pendingSessionRoute = targetRoute;
        sessionSceneChangeRequested = true;
        Debug.Log(
            $"[MirrorTestNetworkManager] 서버 노드 선택 확정: " +
            $"node={selectedNode.id}, type={selectedNode.type}, route={targetRoute}, " +
            $"revision={runSnapshotRevision}, connectionId={connection.connectionId}");
        ServerChangeScene(GetSceneForRoute(targetRoute));
    }

    private void HandleServerUnknownStageChoiceRequest(
        NetworkConnectionToClient connection,
        MirrorUnknownStageChoiceRequestMessage request)
    {
        if (!CanConnectionControlSession(connection, "미지 선택지 확정") ||
            sessionSceneChangeRequested ||
            NetworkServer.isLoadingScene ||
            SceneManager.GetActiveScene().path != SessionUnknownScene)
        {
            return;
        }

        string error = "Run Snapshot 또는 pending 노드가 없습니다.";
        if (!TryGetRunSnapshot(out StageMapSaveData snapshot) ||
            string.IsNullOrEmpty(snapshot.pendingNodeId) ||
            !TryFindSelectableStageNode(
                snapshot,
                snapshot.pendingNodeId,
                out StageNodeSaveData pendingNode,
                out error))
        {
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] 미지 선택지 요청 거부: {error}");
            return;
        }

        YJ_UnknownStageDatabaseSO database =
            Resources.Load<YJ_UnknownStageDatabaseSO>(
                UnknownStageDatabaseResourcePath);
        YJ_UnknownStageDefinitionSO stage =
            pendingNode.type == StageNodeType.Event && database != null
                ? database.GetById(pendingNode.unknownStageId)
                : null;
        int choiceCount = stage != null
            ? Mathf.Clamp(stage.ChoiceNumber, 1, 3)
            : 0;
        if (request.ChoiceIndex >= choiceCount)
        {
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] 유효하지 않은 미지 선택지 요청을 거부했습니다. " +
                $"node={pendingNode.id}, event={pendingNode.unknownStageId}, " +
                $"choice={request.ChoiceIndex}, count={choiceCount}");
            return;
        }

        Debug.Log(
            $"[MirrorTestNetworkManager] 미지 선택지 서버 확정: " +
            $"node={pendingNode.id}, event={pendingNode.unknownStageId}, " +
            $"choice={request.ChoiceIndex + 1}, connectionId={connection.connectionId}");
        ServerTryCompletePendingStageAndReturnToSelection();
    }

    private void HandleServerSessionRouteRequest(
        NetworkConnectionToClient connection,
        MirrorSessionRouteRequestMessage request)
    {
        if (!CanConnectionControlSession(connection, "Scene 이동"))
            return;

        if (sessionSceneChangeRequested || NetworkServer.isLoadingScene)
            return;

        string currentScene = SceneManager.GetActiveScene().path;
        MirrorSessionRoute currentRoute = GetRouteForScene(currentScene);
        if (currentRoute == MirrorSessionRoute.StageSelect &&
            !CanResumePendingStage(request.Route, out string resumeError))
        {
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] 노드 선택을 우회한 Scene 이동 요청을 거절했습니다. " +
                $"target={request.Route}, reason={resumeError}, connectionId={connection.connectionId}");
            return;
        }

        if (!CanChangeSessionRoute(currentRoute, request.Route))
        {
            if (currentRoute == MirrorSessionRoute.Combat &&
                request.Route == MirrorSessionRoute.Combat)
            {
                TryStartCombatSession(connection.connectionId);
            }
            else
            {
                Debug.LogWarning(
                    $"[MirrorTestNetworkManager] 허용되지 않은 Scene 이동 요청: " +
                    $"{currentRoute} → {request.Route} | connectionId={connection.connectionId}");
            }
            return;
        }

        string targetScene = GetSceneForRoute(request.Route);
        if (string.IsNullOrEmpty(targetScene))
            return;

        pendingSessionRoute = request.Route;
        sessionSceneChangeRequested = true;
        Debug.Log(
            $"[MirrorTestNetworkManager] Scene 이동 시작: {currentRoute} → {request.Route} | " +
            $"요청 connectionId={connection.connectionId}");
        ServerChangeScene(targetScene);
    }

    /// <summary>
    /// 서버 Scene 로드가 끝난 뒤 기존 PlayerContext를 새 Stage 시작 지점으로 옮기고 웨이브를 시작한다.
    /// PlayerContext가 가진 Inventory·장비·Stat은 새로 만들지 않으므로 Camp에서의 개인 상태가 유지된다.
    /// </summary>
    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        if (!sessionSceneChangeRequested || sceneName != GetSceneForRoute(pendingSessionRoute))
            return;

        if (pendingSessionRoute != MirrorSessionRoute.Event)
            PlaceServerPlayersAtSceneStarts();
        if (pendingSessionRoute == MirrorSessionRoute.Combat)
            TryStartCombatSession(-1);

        sessionSceneChangeRequested = false;
    }

    /// <summary>
    /// Client가 새 Stage 로드를 끝내 Ready가 된 뒤 소유 플레이어의 서버 위치를 한 번 확정한다.
    /// Client 권한 NetworkTransform이 Camp의 마지막 위치를 다시 서버에 쓰는 현상을 막기 위한 경계다.
    /// </summary>
    public override void OnServerReady(NetworkConnectionToClient connection)
    {
        base.OnServerReady(connection);

        if (!IsManagedSessionScene(SceneManager.GetActiveScene().path) ||
            connection.identity == null)
        {
            return;
        }

        connection.identity
            .GetComponent<MirrorSpawnedPlayerBinder>()
            ?.TargetConfirmSceneStart(
                connection,
                connection.identity.transform.position,
                connection.identity.transform.rotation);
    }

    /// <summary>
    /// 로컬 플레이어가 Scene 로드 중 입력으로 위치를 바꾸지 않도록 잠시 막는다.
    /// </summary>
    public override void OnClientChangeScene(
        string newSceneName,
        SceneOperation sceneOperation,
        bool customHandling)
    {
        LocalPlayerContext?.GetComponent<MirrorSpawnedPlayerBinder>()
            ?.SetLocalInputEnabled(false);
        base.OnClientChangeScene(newSceneName, sceneOperation, customHandling);
    }

    /// <summary>
    /// 새 Scene의 UI Binder가 동일한 로컬 PlayerContext를 즉시 받을 수 있도록 다시 알리고,
    /// 서버 스냅샷의 사망 상태에 맞춰 입력을 복구한다.
    /// </summary>
    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        // DontDestroyOnLoad 플레이어가 새 Scene에 재연결되는 동안 저장된 Context가 원격 복제본으로
        // 잘못 바뀌었더라도, Mirror의 실제 localPlayer를 기준으로 UI와 입력 대상을 다시 확정한다.
        PlayerContext mirrorLocalContext = ResolveMirrorLocalPlayerContext();
        if (mirrorLocalContext != null)
            LocalPlayerContext = mirrorLocalContext;

        if (LocalPlayerContext == null)
            return;

        LocalPlayerContext
            .GetComponent<MirrorSpawnedPlayerBinder>()
            ?.RestoreLocalGameplayAfterScene();
        LocalPlayerContextChanged?.Invoke(LocalPlayerContext);
    }

    /// <summary>
    /// Mirror가 현재 Client의 소유 플레이어로 확정한 NetworkIdentity에서 PlayerContext를 가져온다.
    /// 임의 Find 결과나 먼저 생성된 복제본 순서에 의존하지 않는 로컬 플레이어 판정의 기준점이다.
    /// </summary>
    private static PlayerContext ResolveMirrorLocalPlayerContext()
    {
        NetworkIdentity localIdentity = NetworkClient.localPlayer;
        return localIdentity != null
            ? localIdentity.GetComponent<PlayerContext>()
            : null;
    }

    private bool TryStartCombatSession(int requesterConnectionId)
    {
        NetworkEnemyWaveSpawner_MirrorTest waveSpawner =
            FindFirstObjectByType<NetworkEnemyWaveSpawner_MirrorTest>();
        if (waveSpawner == null)
        {
            Debug.LogError("[MirrorTestNetworkManager] 전투 세션을 시작할 웨이브 Spawner가 없습니다.");
            return false;
        }

        if (waveSpawner.ServerTryStartSession())
        {
            string requester = requesterConnectionId >= 0
                ? requesterConnectionId.ToString()
                : "Camp 최초 요청";
            Debug.Log(
                $"[MirrorTestNetworkManager] 전투 세션 시작: 요청={requester}");
            return true;
        }

        return false;
    }

    private void PlaceServerPlayersAtSceneStarts()
    {
        var players = new List<PlayerContext>(serverPlayerContexts);
        players.Sort((left, right) =>
        {
            uint leftNetId = left != null && left.TryGetComponent(out NetworkIdentity leftIdentity)
                ? leftIdentity.netId
                : 0;
            uint rightNetId = right != null && right.TryGetComponent(out NetworkIdentity rightIdentity)
                ? rightIdentity.netId
                : 0;
            return leftNetId.CompareTo(rightNetId);
        });

        foreach (PlayerContext player in players)
        {
            if (player == null)
                continue;

            Transform start = GetStartPosition();
            if (start == null)
            {
                Debug.LogWarning(
                    "[MirrorTestNetworkManager] Stage에 NetworkStartPosition이 없어 기존 위치를 유지합니다.");
                return;
            }

            player.GetComponent<MirrorSpawnedPlayerBinder>()
                ?.ServerPlaceAtSceneStart(start.position, start.rotation);
        }
    }

    private IEnumerator DisconnectUnverifiedConnectionAfterTimeout(
        NetworkConnectionToClient connection)
    {
        yield return new WaitForSecondsRealtime(CompatibilityTimeoutSeconds);

        if (!compatibleConnectionIds.Contains(connection.connectionId) &&
            NetworkServer.connections.TryGetValue(
                connection.connectionId,
                out NetworkConnectionToClient current) &&
            current == connection)
        {
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] {CompatibilityTimeoutSeconds:F0}초 안에 호환 버전을 보내지 않은 " +
                $"연결을 종료합니다. connectionId={connection.connectionId}");
            connection.Disconnect();
        }
    }

    private IEnumerator DisconnectRejectedConnectionAfterReply(
        NetworkConnectionToClient connection)
    {
        yield return new WaitForSecondsRealtime(RejectionDeliveryDelaySeconds);
        if (NetworkServer.connections.TryGetValue(
                connection.connectionId,
                out NetworkConnectionToClient current) &&
            current == connection)
        {
            connection.Disconnect();
        }
    }

    private IEnumerator StopUnverifiedClientAfterTimeout()
    {
        yield return new WaitForSecondsRealtime(CompatibilityTimeoutSeconds);
        clientCompatibilityTimeoutRoutine = null;
        if (clientCompatibilityConfirmed || !NetworkClient.active)
            yield break;

        compatibilityStatusMessage =
            "서버 호환 응답 시간 초과: 같은 버전의 Server와 Client를 사용하세요.";
        Debug.LogWarning($"[MirrorTestNetworkManager] {compatibilityStatusMessage}");
        StopClient();
    }

    private IEnumerator StopRejectedClientNextFrame()
    {
        yield return null;
        if (NetworkClient.active)
            StopClient();
    }

    private void CancelClientCompatibilityTimeout()
    {
        if (clientCompatibilityTimeoutRoutine == null)
            return;

        StopCoroutine(clientCompatibilityTimeoutRoutine);
        clientCompatibilityTimeoutRoutine = null;
    }

    private static bool IsCompatibleBuild(int clientVersion)
    {
        return clientVersion == CompatibilityVersion;
    }

    /// <summary>
    /// Mirror 세션 테스트가 소유하는 Scene만 이동 대상으로 인정한다.
    /// Scene 이름 문자열 판정은 이 경계에 모아 UI와 서버 요청 처리의 기준이 갈라지지 않게 한다.
    /// </summary>
    private static MirrorSessionRoute GetRouteForScene(string scenePath)
    {
        return scenePath switch
        {
            SessionCampScene => MirrorSessionRoute.StageSelect,
            SessionCampGameplayScene => MirrorSessionRoute.Camp,
            SessionUnknownScene => MirrorSessionRoute.Event,
            SessionCombatScene => MirrorSessionRoute.Combat,
            _ => MirrorSessionRoute.Unknown,
        };
    }

    private static MirrorSessionRoute GetRouteForStageNodeType(StageNodeType nodeType)
    {
        return nodeType switch
        {
            StageNodeType.Camp => MirrorSessionRoute.Camp,
            StageNodeType.Event => MirrorSessionRoute.Event,
            _ => MirrorSessionRoute.Combat,
        };
    }

    private static string GetSceneForRoute(MirrorSessionRoute route)
    {
        return route switch
        {
            MirrorSessionRoute.StageSelect => SessionCampScene,
            MirrorSessionRoute.Camp => SessionCampGameplayScene,
            MirrorSessionRoute.Event => SessionUnknownScene,
            MirrorSessionRoute.Combat => SessionCombatScene,
            _ => string.Empty,
        };
    }

    private static bool IsManagedSessionScene(string scenePath)
    {
        return GetRouteForScene(scenePath) != MirrorSessionRoute.Unknown;
    }

    /// <summary>
    /// 테스트 범위는 선택 화면에서 Camp 또는 전투로 들어가고, 플레이 Scene에서 다시 선택 화면으로
    /// 돌아오는 왕복까지만 허용한다. Camp와 전투를 직접 잇는 운영 진행 규칙은 정식 전환 때 결정한다.
    /// </summary>
    private static bool CanChangeSessionRoute(
        MirrorSessionRoute current,
        MirrorSessionRoute target)
    {
        if (current == MirrorSessionRoute.StageSelect)
        {
            return target == MirrorSessionRoute.Camp ||
                   target == MirrorSessionRoute.Event ||
                   target == MirrorSessionRoute.Combat;
        }

        return (current == MirrorSessionRoute.Camp ||
                current == MirrorSessionRoute.Event ||
                current == MirrorSessionRoute.Combat) &&
               target == MirrorSessionRoute.StageSelect;
    }

    /// <summary>
    /// 실제 PlayerContext가 한 번이라도 생성된 세션에서 마지막 플레이어가 사라졌을 때만
    /// 초기화 유예 시간을 시작합니다. 서버를 처음 켜고 아무도 접속하지 않은 대기 상태와,
    /// 이미 종료 중인 상태에서는 중복 Coroutine이나 반복 종료 요청을 만들지 않습니다.
    /// </summary>
    private void TryScheduleEmptySessionReset()
    {
        if (!CanScheduleEmptySessionReset(
                NetworkServer.active,
                sessionHasStarted,
                isEndingEmptySession,
                emptySessionResetRoutine != null,
                serverPlayerContexts.Count))
        {
            return;
        }

        emptySessionResetRoutine = StartCoroutine(RestartDedicatedServerAfterDelay());
        Debug.Log(
            $"[MirrorTestNetworkManager] 모든 플레이어가 나갔습니다. " +
            $"{emptySessionResetDelay:F1}초 안에 재접속이 없으면 새 세션으로 초기화합니다.");
    }

    /// <summary>
    /// 순간적인 연결 끊김을 새 게임 종료로 오인하지 않도록 실시간 기준 유예 시간을 기다립니다.
    /// 시간이 끝난 순간에도 PlayerContext와 연결이 모두 0개인지 다시 검사합니다.
    /// 전용 서버 빌드에서는 같은 Scene을 그 자리에서 다시 불러오지 않고 프로세스를 정상 종료합니다.
    /// 빌드에 함께 생성되는 실행 스크립트가 새 프로세스를 시작하므로, Scene이 소유한 웨이브·적·월드 드롭·
    /// 공유 상점뿐 아니라 DontDestroyOnLoad와 static 상태까지 새 게임 기준으로 확실하게 초기화됩니다.
    /// </summary>
    private IEnumerator RestartDedicatedServerAfterDelay()
    {
        if (emptySessionResetDelay > 0f)
            yield return new WaitForSecondsRealtime(emptySessionResetDelay);

        emptySessionResetRoutine = null;
        if (!NetworkServer.active ||
            !sessionHasStarted ||
            isEndingEmptySession ||
            serverPlayerContexts.Count > 0 ||
            NetworkServer.connections.Count > 0)
        {
            yield break;
        }

        isEndingEmptySession = true;
        sessionHasStarted = false;
        FindFirstObjectByType<NetworkEnemyWaveSpawner_MirrorTest>()?.ServerMarkSessionResetting();
#if UNITY_SERVER
        Debug.Log(
            "[MirrorTestNetworkManager] 빈 세션을 종료합니다. " +
            "전용 서버 실행 스크립트가 새 프로세스를 시작해 새 게임 상태로 초기화합니다.");
        Application.Quit(0);
#else
        isEndingEmptySession = false;
        Debug.LogWarning(
            "[MirrorTestNetworkManager] 빈 세션 자동 재시작은 전용 서버 빌드에서만 실행됩니다.");
#endif
    }

    /// <summary>
    /// 유예 시간 중 새 연결 또는 PlayerContext가 들어오면 예약된 초기화만 취소합니다.
    /// 이미 시작된 전용 서버 종료에는 영향을 주지 않습니다.
    /// </summary>
    private bool CancelEmptySessionReset()
    {
        if (emptySessionResetRoutine == null)
            return false;

        StopCoroutine(emptySessionResetRoutine);
        emptySessionResetRoutine = null;
        return true;
    }

    /// <summary>
    /// 빈 세션 초기화를 예약할 수 있는 조건을 한곳에서 판정합니다.
    /// 전용 서버가 활성 상태이고 실제 게임이 시작된 적이 있으며, 플레이어가 0명이고,
    /// 기존 예약이나 전용 서버 종료가 진행 중이지 않을 때만 참을 반환합니다.
    /// </summary>
    private static bool CanScheduleEmptySessionReset(
        bool serverActive,
        bool hasStarted,
        bool shutdownInProgress,
        bool resetAlreadyScheduled,
        int playerCount)
    {
        return serverActive &&
               hasStarted &&
               !shutdownInProgress &&
               !resetAlreadyScheduled &&
               playerCount == 0;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Inspector의 Context Menu에서 실행하는 최소 회귀 검사입니다.
    /// 마지막 플레이어 이탈만 초기화를 예약하고, 최초 대기·플레이어 잔존·중복 예약 상태에서는
    /// 예약하지 않는다는 핵심 조건이 깨지면 Unity Assertion으로 즉시 알려 줍니다.
    /// </summary>
    [ContextMenu("Mirror 테스트/빈 세션 초기화 규칙 검사")]
    private void ValidateEmptySessionResetRule()
    {
        Debug.Assert(CanScheduleEmptySessionReset(true, true, false, false, 0));
        Debug.Assert(!CanScheduleEmptySessionReset(true, false, false, false, 0));
        Debug.Assert(!CanScheduleEmptySessionReset(true, true, false, false, 1));
        Debug.Assert(!CanScheduleEmptySessionReset(true, true, false, true, 0));
        Debug.Assert(!CanScheduleEmptySessionReset(true, true, true, false, 0));
        Debug.Log("[MirrorTestNetworkManager] 빈 세션 초기화 규칙 검사 통과");
    }

    /// <summary>
    /// 같은 호환 버전만 통과하고 이전·이후 버전은 거절되는지 확인하는 최소 회귀 검사다.
    /// </summary>
    [ContextMenu("Mirror 테스트/빌드 호환 규칙 검사")]
    private void ValidateCompatibilityRule()
    {
        Debug.Assert(IsCompatibleBuild(CompatibilityVersion));
        Debug.Assert(!IsCompatibleBuild(CompatibilityVersion - 1));
        Debug.Assert(!IsCompatibleBuild(CompatibilityVersion + 1));
        Debug.Log("[MirrorTestNetworkManager] 빌드 호환 규칙 검사 통과");
    }

    /// <summary>
    /// 런 Snapshot JSON이 그래프와 진행 필드를 보존하고 불완전한 데이터는 거부하는지 확인한다.
    /// </summary>
    [ContextMenu("Mirror 테스트/런 스냅샷 직렬화 규칙 검사")]
    private void ValidateRunSnapshotSerializationRule()
    {
        StageMapSaveData source = new()
        {
            mapSeed = InitialRunSeed,
            clearedFloor = 2,
            lastClearedNodeId = "A1_F02_N01",
            pendingNodeId = "A1_F03_N01",
        };
        source.clearedNodeIds.Add("A1_F02_N01");
        source.visitedNodeIds.Add("A1_F02_N01");
        source.nodes.Add(new StageNodeSaveData
        {
            id = "A1_F03_N01",
            floor = 3,
            nodeIndex = 0,
            type = StageNodeType.Camp,
            sceneName = "Act1_Camp",
        });

        string json = JsonUtility.ToJson(source);
        Debug.Assert(TryDeserializeRunSnapshot(json, out StageMapSaveData restored));
        Debug.Assert(restored != source);
        Debug.Assert(restored.mapSeed == source.mapSeed);
        Debug.Assert(restored.clearedFloor == source.clearedFloor);
        Debug.Assert(restored.pendingNodeId == source.pendingNodeId);
        Debug.Assert(restored.nodes.Count == 1);
        Debug.Assert(restored.nodes[0].type == StageNodeType.Camp);
        Debug.Assert(!TryDeserializeRunSnapshot("{}", out _));
        Debug.Log("[MirrorTestNetworkManager] 런 스냅샷 직렬화 규칙 검사 통과");
    }

    /// <summary>
    /// 서버가 pending/cleared를 순서대로 갱신하고 마지막 클리어 노드와 연결되지 않은 다음 경로를
    /// 거절하는지 확인하는 7-3 최소 회귀 검사다.
    /// </summary>
    [ContextMenu("Mirror 테스트/스테이지 진행 규칙 검사")]
    private void ValidateStageProgressionRule()
    {
        StageMapSaveData snapshot = new()
        {
            act = StageActType.Act1,
            mapSeed = InitialRunSeed,
            clearedFloor = 0,
        };

        StageNodeSaveData floor1Left = new()
        {
            id = "A1_F01_N01",
            floor = 1,
            nodeIndex = 0,
            type = StageNodeType.Battle,
        };
        floor1Left.nextNodeIds.Add("A1_F02_N01");
        StageNodeSaveData floor1Right = new()
        {
            id = "A1_F01_N02",
            floor = 1,
            nodeIndex = 1,
            type = StageNodeType.Battle,
        };
        floor1Right.nextNodeIds.Add("A1_F02_N02");
        StageNodeSaveData floor2Left = new()
        {
            id = "A1_F02_N01",
            floor = 2,
            nodeIndex = 0,
            type = StageNodeType.Camp,
        };
        StageNodeSaveData floor2Right = new()
        {
            id = "A1_F02_N02",
            floor = 2,
            nodeIndex = 1,
            type = StageNodeType.Elite,
        };
        snapshot.nodes.Add(floor1Left);
        snapshot.nodes.Add(floor1Right);
        snapshot.nodes.Add(floor2Left);
        snapshot.nodes.Add(floor2Right);

        Debug.Assert(TryBeginStageNode(
            snapshot,
            floor1Left.id,
            out StageNodeSaveData selectedNode,
            out _));
        Debug.Assert(selectedNode == floor1Left);
        Debug.Assert(snapshot.pendingNodeId == floor1Left.id);
        Debug.Assert(!TryBeginStageNode(snapshot, floor1Right.id, out _, out _));
        Debug.Assert(TryCompletePendingStageNode(
            snapshot,
            out StageNodeSaveData completedNode,
            out _));
        Debug.Assert(completedNode == floor1Left);
        Debug.Assert(snapshot.clearedFloor == 1);
        Debug.Assert(snapshot.lastClearedNodeId == floor1Left.id);
        Debug.Assert(string.IsNullOrEmpty(snapshot.pendingNodeId));
        Debug.Assert(snapshot.clearedNodeIds.Contains(floor1Left.id));
        Debug.Assert(snapshot.visitedNodeIds.Contains(floor1Left.id));
        Debug.Assert(!TryBeginStageNode(snapshot, floor2Right.id, out _, out _));
        Debug.Assert(TryBeginStageNode(snapshot, floor2Left.id, out _, out _));
        Debug.Log("[MirrorTestNetworkManager] 스테이지 진행 규칙 검사 통과");
    }

    /// <summary>
    /// 가장 먼저 연결된 호환 Client만 세션을 제어하고 이탈 시 다음 연결이 승계되는지 확인한다.
    /// </summary>
    [ContextMenu("Mirror 테스트/세션 방장 권한 규칙 검사")]
    private void ValidateSessionLeaderRule()
    {
        var connectionIds = new HashSet<int> { 7, 2, 4 };
        Debug.Assert(SelectLowestConnectionId(connectionIds) == 2);
        Debug.Assert(CanControlSession(2, 2, true, true));
        Debug.Assert(!CanControlSession(4, 2, true, true));
        Debug.Assert(!CanControlSession(2, 2, false, true));
        Debug.Assert(!CanControlSession(2, 2, true, false));
        connectionIds.Remove(2);
        Debug.Assert(SelectLowestConnectionId(connectionIds) == 4);
        Debug.Log("[MirrorTestNetworkManager] 세션 방장 권한 규칙 검사 통과");
    }

    /// <summary>
    /// 6-C의 최소 왕복만 허용하고, 플레이 Scene 사이의 직접 이동과 같은 Scene 중복 요청은
    /// 거절하는지 확인하는 회귀 검사다.
    /// </summary>
    [ContextMenu("Mirror 테스트/Scene 이동 규칙 검사")]
    private void ValidateSessionRouteRule()
    {
        Debug.Assert(CanChangeSessionRoute(
            MirrorSessionRoute.StageSelect,
            MirrorSessionRoute.Camp));
        Debug.Assert(CanChangeSessionRoute(
            MirrorSessionRoute.StageSelect,
            MirrorSessionRoute.Combat));
        Debug.Assert(CanChangeSessionRoute(
            MirrorSessionRoute.StageSelect,
            MirrorSessionRoute.Event));
        Debug.Assert(CanChangeSessionRoute(
            MirrorSessionRoute.Camp,
            MirrorSessionRoute.StageSelect));
        Debug.Assert(CanChangeSessionRoute(
            MirrorSessionRoute.Combat,
            MirrorSessionRoute.StageSelect));
        Debug.Assert(CanChangeSessionRoute(
            MirrorSessionRoute.Event,
            MirrorSessionRoute.StageSelect));
        Debug.Assert(!CanChangeSessionRoute(
            MirrorSessionRoute.Camp,
            MirrorSessionRoute.Combat));
        Debug.Assert(!CanChangeSessionRoute(
            MirrorSessionRoute.Combat,
            MirrorSessionRoute.Camp));
        Debug.Assert(!CanChangeSessionRoute(
            MirrorSessionRoute.StageSelect,
            MirrorSessionRoute.StageSelect));
        Debug.Log("[MirrorTestNetworkManager] Scene 이동 규칙 검사 통과");
    }
#endif

    private ushort GetServerPort()
    {
        return transport is PortTransport portTransport
            ? portTransport.Port
            : (ushort)0;
    }
}

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
    Combat,
}

public struct MirrorSessionRouteRequestMessage : NetworkMessage
{
    public MirrorSessionRoute Route;
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
/// </summary>
public sealed class MirrorTestNetworkManager : NetworkManager
{
    // ponytail: 현재는 수동 호환 버전 하나면 충분하다. 네트워크 DTO·SyncVar 순서가 바뀔 때만
    // 이 값을 올리며, 빌드가 잦아 수동 갱신 누락이 실제로 반복될 때 Git 해시 자동 생성을 검토한다.
    public const int CompatibilityVersion = 2026081904;

    public const string SessionCampScene =
        "Assets/SW/TEST/MirrorCombat/Scenes/StageSelect_MirrorSessionTest.unity";
    public const string SessionCampGameplayScene =
        "Assets/SW/TEST/MirrorCombat/Scenes/MirrorSessionCampRouteTest.unity";
    public const string SessionCombatScene =
        "Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage1_MirrorSessionTest.unity";

    private const float CompatibilityTimeoutSeconds = 5f;
    private const float RejectionDeliveryDelaySeconds = 0.2f;

    [SerializeField, Min(0f)] private float emptySessionResetDelay = 15f;

    private readonly HashSet<PlayerContext> serverPlayerContexts = new();
    private readonly HashSet<int> compatibleConnectionIds = new();
    private Coroutine emptySessionResetRoutine;
    private Coroutine clientCompatibilityTimeoutRoutine;
    private bool sessionHasStarted;
    private bool isEndingEmptySession;
    private bool clientCompatibilityConfirmed;
    private bool sessionSceneChangeRequested;
    private MirrorSessionRoute pendingSessionRoute = MirrorSessionRoute.StageSelect;
    private string compatibilityStatusMessage = "서버 연결 전";

    public PlayerContext LocalPlayerContext { get; private set; }
    public event Action<PlayerContext> LocalPlayerContextChanged;
    public IReadOnlyCollection<PlayerContext> ServerPlayerContexts => serverPlayerContexts;
    public bool ClientCompatibilityConfirmed => clientCompatibilityConfirmed;
    public string CompatibilityStatusMessage => compatibilityStatusMessage;
    public bool IsSessionSelectionActive =>
        SceneManager.GetActiveScene().path == SessionCampScene;
    public MirrorSessionRoute CurrentSessionRoute =>
        GetRouteForScene(SceneManager.GetActiveScene().path);

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
        if (context == null || LocalPlayerContext == context)
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
        sessionHasStarted = false;
        isEndingEmptySession = false;
        emptySessionResetRoutine = null;
        sessionSceneChangeRequested = false;
        pendingSessionRoute = MirrorSessionRoute.StageSelect;
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
        clientCompatibilityConfirmed = false;
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
        compatibleConnectionIds.Remove(connection.connectionId);
        Debug.Log($"[MirrorTestNetworkManager] Client 접속 종료: connectionId={connection.connectionId}");
        base.OnServerDisconnect(connection);
        TryScheduleEmptySessionReset();
    }

    public override void OnClientDisconnect()
    {
        CancelClientCompatibilityTimeout();
        clientCompatibilityConfirmed = false;
        base.OnClientDisconnect();
    }

    public override void OnStopClient()
    {
        CancelClientCompatibilityTimeout();
        NetworkClient.UnregisterHandler<MirrorCompatibilityResponseMessage>();
        clientCompatibilityConfirmed = false;
        base.OnStopClient();

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

        compatibleConnectionIds.Clear();
        sessionHasStarted = false;
        isEndingEmptySession = false;
        sessionSceneChangeRequested = false;
        pendingSessionRoute = MirrorSessionRoute.StageSelect;
        serverPlayerContexts.Clear();
        Debug.Log("[MirrorTestNetworkManager] 서버 종료");
        base.OnStopServer();
    }

    /// <summary>
    /// 로컬 Client가 대기 중인 전투 세션의 시작을 서버에 요청한다.
    /// 별도 방장·준비 시스템은 만들지 않으며, 현재 6-A 테스트에서는 호환 확인과 Player 생성이 끝난
    /// 접속자라면 누구나 한 번만 시작할 수 있다. 서버가 이미 시작했으면 요청은 무변경으로 끝난다.
    /// </summary>
    public bool RequestStartSession()
    {
        return RequestSessionRoute(MirrorSessionRoute.Combat);
    }

    /// <summary>
    /// 6-C 테스트에서 선택 Scene의 Camp·전투 이동 또는 플레이 Scene의 선택 화면 복귀를 서버에 요청한다.
    /// 실제 Stage 해금·방장·투표 규칙은 아직 넣지 않고, 호환 확인이 끝난 Player의 첫 유효 요청만 받는다.
    /// </summary>
    public bool RequestSessionRoute(MirrorSessionRoute route)
    {
        if (!NetworkClient.active ||
            !NetworkClient.ready ||
            !clientCompatibilityConfirmed ||
            NetworkClient.localPlayer == null)
        {
            return false;
        }

        NetworkClient.Send(new MirrorSessionRouteRequestMessage
        {
            Route = route,
        });
        return true;
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

    private void HandleServerSessionRouteRequest(
        NetworkConnectionToClient connection,
        MirrorSessionRouteRequestMessage request)
    {
        if (!compatibleConnectionIds.Contains(connection.connectionId) ||
            connection.identity == null)
        {
            Debug.LogWarning(
                $"[MirrorTestNetworkManager] 호환 확인 또는 Player 생성 전 Scene 이동 요청을 거절했습니다. " +
                $"connectionId={connection.connectionId}");
            return;
        }

        if (sessionSceneChangeRequested || NetworkServer.isLoadingScene)
            return;

        string currentScene = SceneManager.GetActiveScene().path;
        MirrorSessionRoute currentRoute = GetRouteForScene(currentScene);
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

        if (LocalPlayerContext == null)
            return;

        LocalPlayerContext
            .GetComponent<MirrorSpawnedPlayerBinder>()
            ?.RestoreLocalGameplayAfterScene();
        LocalPlayerContextChanged?.Invoke(LocalPlayerContext);
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
    /// 6-C 테스트가 소유하는 세 Scene만 이동 대상으로 인정한다.
    /// Scene 이름 문자열 판정은 이 경계에 모아 UI와 서버 요청 처리의 기준이 갈라지지 않게 한다.
    /// </summary>
    private static MirrorSessionRoute GetRouteForScene(string scenePath)
    {
        return scenePath switch
        {
            SessionCampScene => MirrorSessionRoute.StageSelect,
            SessionCampGameplayScene => MirrorSessionRoute.Camp,
            SessionCombatScene => MirrorSessionRoute.Combat,
            _ => MirrorSessionRoute.Unknown,
        };
    }

    private static string GetSceneForRoute(MirrorSessionRoute route)
    {
        return route switch
        {
            MirrorSessionRoute.StageSelect => SessionCampScene,
            MirrorSessionRoute.Camp => SessionCampGameplayScene,
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
                   target == MirrorSessionRoute.Combat;
        }

        return (current == MirrorSessionRoute.Camp ||
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
            MirrorSessionRoute.Camp,
            MirrorSessionRoute.StageSelect));
        Debug.Assert(CanChangeSessionRoute(
            MirrorSessionRoute.Combat,
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

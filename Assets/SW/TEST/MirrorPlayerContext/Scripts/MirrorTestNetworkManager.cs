using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public sealed class MirrorTestNetworkManager : NetworkManager
{
    [SerializeField, Min(0f)] private float emptySessionResetDelay = 15f;

    private readonly HashSet<PlayerContext> serverPlayerContexts = new();
    private Coroutine emptySessionResetRoutine;
    private bool sessionHasStarted;
    private bool isEndingEmptySession;

    public PlayerContext LocalPlayerContext { get; private set; }
    public event Action<PlayerContext> LocalPlayerContextChanged;
    public IReadOnlyCollection<PlayerContext> ServerPlayerContexts => serverPlayerContexts;

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
        sessionHasStarted = false;
        isEndingEmptySession = false;
        emptySessionResetRoutine = null;
        Debug.Log($"[MirrorTestNetworkManager] 서버 시작: UDP 포트 {GetServerPort()}");
    }

    public override void OnServerConnect(NetworkConnectionToClient connection)
    {
        base.OnServerConnect(connection);
        if (CancelEmptySessionReset())
            Debug.Log("[MirrorTestNetworkManager] 재접속을 감지해 빈 세션 초기화를 취소했습니다.");
        Debug.Log($"[MirrorTestNetworkManager] Client 접속: connectionId={connection.connectionId}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient connection)
    {
        Debug.Log($"[MirrorTestNetworkManager] Client 접속 종료: connectionId={connection.connectionId}");
        base.OnServerDisconnect(connection);
        TryScheduleEmptySessionReset();
    }

    public override void OnStopClient()
    {
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
        sessionHasStarted = false;
        isEndingEmptySession = false;
        serverPlayerContexts.Clear();
        Debug.Log("[MirrorTestNetworkManager] 서버 종료");
        base.OnStopServer();
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
#endif

    private ushort GetServerPort()
    {
        return transport is PortTransport portTransport
            ? portTransport.Port
            : (ushort)0;
    }
}

using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public sealed class MirrorTestNetworkManager : NetworkManager
{
    private readonly HashSet<PlayerContext> serverPlayerContexts = new();

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
        if (context != null && serverPlayerContexts.Add(context))
            FindFirstObjectByType<NetworkShopState_MirrorTest>()?.ServerRefreshPartyBenefits();
    }

    internal void UnregisterServerPlayer(PlayerContext context)
    {
        if (context != null && serverPlayerContexts.Remove(context))
            FindFirstObjectByType<NetworkShopState_MirrorTest>()?.ServerRefreshPartyBenefits();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log($"[MirrorTestNetworkManager] 서버 시작: UDP 포트 {GetServerPort()}");
    }

    public override void OnServerConnect(NetworkConnectionToClient connection)
    {
        base.OnServerConnect(connection);
        Debug.Log($"[MirrorTestNetworkManager] Client 접속: connectionId={connection.connectionId}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient connection)
    {
        Debug.Log($"[MirrorTestNetworkManager] Client 접속 종료: connectionId={connection.connectionId}");
        base.OnServerDisconnect(connection);
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
        serverPlayerContexts.Clear();
        Debug.Log("[MirrorTestNetworkManager] 서버 종료");
        base.OnStopServer();
    }

    private ushort GetServerPort()
    {
        return transport is PortTransport portTransport
            ? portTransport.Port
            : (ushort)0;
    }
}

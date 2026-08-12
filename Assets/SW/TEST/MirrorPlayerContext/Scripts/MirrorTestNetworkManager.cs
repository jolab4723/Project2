using System;
using System.Collections.Generic;
using Mirror;

public sealed class MirrorTestNetworkManager : NetworkManager
{
    private readonly HashSet<PlayerContext> serverPlayerContexts = new();

    public PlayerContext LocalPlayerContext { get; private set; }
    public event Action<PlayerContext> LocalPlayerContextChanged;
    public IReadOnlyCollection<PlayerContext> ServerPlayerContexts => serverPlayerContexts;

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
        if (context != null)
            serverPlayerContexts.Add(context);
    }

    internal void UnregisterServerPlayer(PlayerContext context)
    {
        if (context != null)
            serverPlayerContexts.Remove(context);
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
        base.OnStopServer();
    }
}

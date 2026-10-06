using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 정식 스테이지 포탈 프리팹을 Mirror 정식 전투·캠프 흐름에 연결한다.
/// 전투에서는 이미 동기화된 웨이브 완료 상태를 읽고, 캠프에서는 입장 즉시 포탈을 연다.
/// 포탈 탑승 판정과 pending 노드 완료·Scene 이동은 서버에서만 수행한다.
/// SW 수정: 전투·캠프 모두 접속 중인 참가자 전원이 포탈에 도착해야 이동한다(파밍 중 강제 이동 방지).
/// 도착한 참가자는 싱글과 같은 포탈 연출로 숨겨진 채 대기한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class MirrorStagePortalAdapter : MonoBehaviour
{
    [SerializeField] private NetworkEnemyWaveSpawner waveSpawner;
    [SerializeField] private YJ_PortalActive portalActive;

    private bool portalOpenRequested;
    private bool transitionRequested;
    // SW 수정: 도착은 최종 상태다. 도착한 플레이어는 숨김·입력 차단으로 대기하므로 이탈을 세지 않는다.
    private readonly HashSet<NetworkIdentity> arrivedPlayers = new();

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (portalOpenRequested)
        {
            // SW 수정: 대기 중 다른 참가자가 접속을 끊어 남은 전원이 도착 상태가 되면 바로 이동한다.
            if (NetworkServer.active && !transitionRequested && arrivedPlayers.Count > 0 &&
                NetworkManager.singleton is MirrorNetworkManager waitingManager &&
                HaveAllServerPlayersArrived(waitingManager) &&
                waitingManager.ServerTryCompletePendingStageAndReturnToSelection())
                transitionRequested = true;
            return;
        }

        ResolveReferences();
        MirrorNetworkManager manager =
            NetworkManager.singleton as MirrorNetworkManager;
        bool combatCompleted = waveSpawner != null &&
                               waveSpawner.SessionPhase == MirrorSessionPhase.Completed;
        if (portalActive == null ||
            manager == null ||
            !CanOpenPortal(
                manager.CurrentSessionRoute,
                combatCompleted,
                waveSpawner != null && waveSpawner.IsBossSession))
        {
            return;
        }

        portalOpenRequested = true;
        portalActive.Active(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkIdentity playerIdentity = ResolveServerPlayer(other);
        if (!CanRequestStageReturn(
                NetworkServer.active,
                portalActive != null && portalActive.IsPortalActive,
                transitionRequested,
                playerIdentity != null))
        {
            return;
        }

        if (NetworkManager.singleton is not MirrorNetworkManager manager)
        {
            Debug.LogError("[MirrorStagePortalAdapter] MirrorNetworkManager를 찾지 못했습니다.");
            return;
        }

        if (arrivedPlayers.Add(playerIdentity))
        {
            int connectedCount = CountConnectedServerPlayers(manager);
            var binder = playerIdentity.GetComponent<MirrorSpawnedPlayerBinder>();
            // 도착을 서버 지속 상태로 먼저 기록해 재접속·최초 관찰 클라이언트도 대기 표시를 복원한다.
            // RPC는 포탈 연출과 도착 인원 알림만 담당한다.
            binder?.ServerSetPortalArrived(true);
            binder?.RpcPlayPortalArrival(
                Mathf.Min(arrivedPlayers.Count, connectedCount), connectedCount);
        }
        if (!HaveAllServerPlayersArrived(manager))
            return;

        if (!manager.ServerTryCompletePendingStageAndReturnToSelection())
            return;

        transitionRequested = true;
    }

    private void ResolveReferences()
    {
        if (portalActive == null)
            portalActive = GetComponentInParent<YJ_PortalActive>();
        if (waveSpawner == null)
            waveSpawner = FindFirstObjectByType<NetworkEnemyWaveSpawner>();
    }

    private static NetworkIdentity ResolveServerPlayer(Collider other)
    {
        if (other == null || other.GetComponentInParent<PlayerContext>() == null)
            return null;

        NetworkIdentity identity = other.GetComponentInParent<NetworkIdentity>();
        return identity != null && identity.connectionToClient != null
            ? identity
            : null;
    }

    private static bool CanRequestStageReturn(
        bool serverActive,
        bool portalOpen,
        bool alreadyRequested,
        bool hasServerPlayer)
    {
        return serverActive && portalOpen && !alreadyRequested && hasServerPlayer;
    }

    private bool HaveAllServerPlayersArrived(MirrorNetworkManager manager)
    {
        int playerCount = 0;
        foreach (PlayerContext context in manager.ServerPlayerContexts)
        {
            NetworkIdentity identity = context != null
                ? context.GetComponent<NetworkIdentity>()
                : null;
            if (identity == null || identity.connectionToClient == null)
                continue;

            playerCount++;
            if (!arrivedPlayers.Contains(identity))
                return false;
        }

        return playerCount > 0;
    }

    private static int CountConnectedServerPlayers(MirrorNetworkManager manager)
    {
        int count = 0;
        foreach (PlayerContext context in manager.ServerPlayerContexts)
            if (context != null && context.TryGetComponent(out NetworkIdentity identity) && identity.connectionToClient != null)
                count++;
        return count;
    }

    private static bool CanOpenPortal(
        MirrorSessionRoute route,
        bool combatCompleted,
        bool bossSession)
    {
        return route == MirrorSessionRoute.Camp ||
               (route == MirrorSessionRoute.Combat && combatCompleted && !bossSession);
    }

}

using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 정식 스테이지 포탈 프리팹을 Mirror 테스트 전투·캠프 흐름에 연결한다.
/// 전투에서는 이미 동기화된 웨이브 완료 상태를 읽고, 캠프에서는 입장 즉시 포탈을 연다.
/// 포탈 탑승 판정과 pending 노드 완료·Scene 이동은 서버에서만 수행한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class MirrorStagePortalAdapter : MonoBehaviour
{
    [SerializeField] private NetworkEnemyWaveSpawner waveSpawner;
    [SerializeField] private YJ_PortalActive portalActive;

    private bool portalOpenRequested;
    private bool transitionRequested;
    private readonly Dictionary<NetworkIdentity, int> campPlayerColliderCounts = new();

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (portalOpenRequested)
            return;

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

        if (manager.CurrentSessionRoute == MirrorSessionRoute.Camp)
        {
            campPlayerColliderCounts.TryGetValue(playerIdentity, out int colliderCount);
            campPlayerColliderCounts[playerIdentity] = colliderCount + 1;
            if (!HaveAllServerPlayersArrived(manager))
                return;
        }

        if (!manager.ServerTryCompletePendingStageAndReturnToSelection())
            return;

        transitionRequested = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!NetworkServer.active)
            return;

        NetworkIdentity playerIdentity = ResolveServerPlayer(other);
        if (playerIdentity == null ||
            !campPlayerColliderCounts.TryGetValue(playerIdentity, out int colliderCount))
        {
            return;
        }

        if (colliderCount <= 1)
            campPlayerColliderCounts.Remove(playerIdentity);
        else
            campPlayerColliderCounts[playerIdentity] = colliderCount - 1;
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
            if (!campPlayerColliderCounts.ContainsKey(identity))
                return false;
        }

        return playerCount > 0;
    }

    private static bool CanOpenPortal(
        MirrorSessionRoute route,
        bool combatCompleted,
        bool bossSession)
    {
        return route == MirrorSessionRoute.Camp ||
               (route == MirrorSessionRoute.Combat && combatCompleted && !bossSession);
    }

#if UNITY_EDITOR
    [ContextMenu("Mirror 테스트/포탈 서버 복귀 규칙 검사")]
    private void ValidatePortalReturnRule()
    {
        Debug.Assert(CanOpenPortal(MirrorSessionRoute.Camp, false, false));
        Debug.Assert(CanOpenPortal(MirrorSessionRoute.Combat, true, false));
        Debug.Assert(!CanOpenPortal(MirrorSessionRoute.Combat, false, false));
        Debug.Assert(!CanOpenPortal(MirrorSessionRoute.Combat, true, true));
        Debug.Assert(!CanOpenPortal(MirrorSessionRoute.StageSelect, true, false));
        Debug.Assert(CanRequestStageReturn(true, true, false, true));
        Debug.Assert(!CanRequestStageReturn(false, true, false, true));
        Debug.Assert(!CanRequestStageReturn(true, false, false, true));
        Debug.Assert(!CanRequestStageReturn(true, true, true, true));
        Debug.Assert(!CanRequestStageReturn(true, true, false, false));
        Debug.Log("[MirrorStagePortalAdapter] 포탈 서버 복귀 규칙 검사 통과");
    }
#endif
}

using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Run Snapshot의 일반 전투 노드 하나에서만 보이고, 현재 접속한 플레이어가 모두 탑승했을 때
/// 서버 권한으로 한 번 왕복하는 Mirror 테스트 엘리베이터다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(NetworkTransformReliable), typeof(Rigidbody))]
public sealed class MirrorFourPlayerElevator_MirrorTest : NetworkBehaviour
{
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Collider boardingTrigger;
    [SerializeField] private Rigidbody platformRigidbody;
    [SerializeField] private Vector3 travelOffset = new(0f, 10.75f, 0f);
    [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float topWait = 1f;
    [SerializeField, Min(0.1f)] private float navMeshSearchDistance = 1.5f;

    [SyncVar(hook = nameof(HandleAvailabilityChanged))]
    private bool available;

    private readonly HashSet<NetworkIdentity> boardedPlayers = new();
    private Coroutine serverRideRoutine;
    private bool completedTrip;

    private Coroutine localRideRoutine;
    private Rigidbody localPlayerRigidbody;
    private NavMeshAgent localPlayerAgent;
    private T_PlayerController localPlayerController;
    private bool localAgentWasEnabled;
    private bool localControlWasEnabled;

    private readonly WaitForFixedUpdate waitForFixedUpdate = new();

    private void Reset()
    {
        platformRigidbody = GetComponent<Rigidbody>();
        boardingTrigger = GetComponentInChildren<Collider>();
    }

    private void Awake()
    {
        if (platformRigidbody == null)
            platformRigidbody = GetComponent<Rigidbody>();

        ApplyAvailability(false);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        available = IsCurrentNodeDesignatedForElevator();
        ApplyAvailability(available);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyAvailability(available);
    }

    public override void OnStopServer()
    {
        if (serverRideRoutine != null)
            StopCoroutine(serverRideRoutine);

        boardedPlayers.Clear();
        base.OnStopServer();
    }

    public override void OnStopClient()
    {
        RestoreLocalPlayer();
        base.OnStopClient();
    }

    [ServerCallback]
    private void OnTriggerEnter(Collider other)
    {
        if (!available || completedTrip || serverRideRoutine != null ||
            !TryGetNetworkPlayer(other, out NetworkIdentity player))
        {
            return;
        }

        boardedPlayers.Add(player);
        TryStartRide();
    }

    [ServerCallback]
    private void OnTriggerExit(Collider other)
    {
        if (serverRideRoutine != null ||
            !TryGetNetworkPlayer(other, out NetworkIdentity player))
        {
            return;
        }

        boardedPlayers.Remove(player);
    }

    [Server]
    private void TryStartRide()
    {
        boardedPlayers.RemoveWhere(player =>
            player == null || player.connectionToClient == null);

        int connectedPlayerCount = 0;
        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
        {
            if (connection?.identity != null &&
                connection.identity.TryGetComponent(out PlayerContext _))
            {
                connectedPlayerCount++;
            }
        }

        if (!CanStartRide(
                available,
                completedTrip,
                serverRideRoutine != null,
                connectedPlayerCount,
                boardedPlayers.Count))
        {
            return;
        }

        serverRideRoutine = StartCoroutine(RideRoundTrip());
    }

    [Server]
    private IEnumerator RideRoundTrip()
    {
        if (boardingTrigger != null)
            boardingTrigger.enabled = false;

        foreach (NetworkIdentity player in boardedPlayers)
        {
            if (player != null && player.connectionToClient != null)
                TargetBeginRide(player.connectionToClient);
        }

        Vector3 startPosition = platformRigidbody.position;
        yield return MovePlatform(startPosition + travelOffset);

        if (topWait > 0f)
            yield return new WaitForSeconds(topWait);

        yield return MovePlatform(startPosition);
        yield return waitForFixedUpdate;

        foreach (NetworkIdentity player in boardedPlayers)
        {
            if (player != null && player.connectionToClient != null)
                TargetEndRide(player.connectionToClient);
        }

        completedTrip = true;
        boardedPlayers.Clear();
        serverRideRoutine = null;
        Debug.Log("[MirrorFourPlayerElevator] 접속 플레이어 전원 탑승 왕복 완료", this);
    }

    [Server]
    private IEnumerator MovePlatform(Vector3 destination)
    {
        while ((platformRigidbody.position - destination).sqrMagnitude > 0.0001f)
        {
            platformRigidbody.MovePosition(Vector3.MoveTowards(
                platformRigidbody.position,
                destination,
                moveSpeed * Time.fixedDeltaTime));
            yield return waitForFixedUpdate;
        }

        platformRigidbody.position = destination;
    }

    [TargetRpc]
    private void TargetBeginRide(NetworkConnectionToClient target)
    {
        RestoreLocalPlayer();

        NetworkIdentity localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null)
            return;

        localPlayerRigidbody = localPlayer.GetComponent<Rigidbody>();
        localPlayerAgent = localPlayer.GetComponent<NavMeshAgent>();
        localPlayerController = localPlayer.GetComponent<T_PlayerController>();

        localControlWasEnabled = localPlayerController?.IsControlEnabled == true;
        if (localControlWasEnabled)
            localPlayerController.SetControlEnable(false);

        localAgentWasEnabled = localPlayerAgent != null && localPlayerAgent.enabled;
        if (localAgentWasEnabled)
        {
            if (localPlayerAgent.isOnNavMesh)
                localPlayerAgent.ResetPath();

            localPlayerAgent.isStopped = true;
            localPlayerAgent.enabled = false;
        }

        localRideRoutine = StartCoroutine(FollowPlatform());
    }

    [TargetRpc]
    private void TargetEndRide(NetworkConnectionToClient target)
    {
        RestoreLocalPlayer();
    }

    private IEnumerator FollowPlatform()
    {
        Vector3 previousPlatformPosition = platformRigidbody.position;
        while (true)
        {
            yield return waitForFixedUpdate;

            Vector3 currentPlatformPosition = platformRigidbody.position;
            Vector3 delta = currentPlatformPosition - previousPlatformPosition;
            previousPlatformPosition = currentPlatformPosition;

            if (localPlayerRigidbody != null)
                localPlayerRigidbody.MovePosition(localPlayerRigidbody.position + delta);
        }
    }

    private void RestoreLocalPlayer()
    {
        if (localRideRoutine != null)
        {
            StopCoroutine(localRideRoutine);
            localRideRoutine = null;
        }

        if (localAgentWasEnabled && localPlayerAgent != null)
        {
            Vector3 playerPosition = localPlayerRigidbody != null
                ? localPlayerRigidbody.position
                : localPlayerAgent.transform.position;

            localPlayerAgent.enabled = true;
            if (NavMesh.SamplePosition(
                    playerPosition,
                    out NavMeshHit hit,
                    navMeshSearchDistance,
                    localPlayerAgent.areaMask))
            {
                localPlayerAgent.Warp(hit.position);
            }
        }

        if (localControlWasEnabled)
            localPlayerController?.SetControlEnable(true);

        localPlayerRigidbody = null;
        localPlayerAgent = null;
        localPlayerController = null;
        localAgentWasEnabled = false;
        localControlWasEnabled = false;
    }

    private void HandleAvailabilityChanged(bool _, bool isAvailable)
    {
        ApplyAvailability(isAvailable);
    }

    private void ApplyAvailability(bool isAvailable)
    {
        if (visualRoot != null)
            visualRoot.SetActive(isAvailable);

        if (boardingTrigger != null)
            boardingTrigger.enabled = isAvailable && isServer && !completedTrip;
    }

    [Server]
    private bool IsCurrentNodeDesignatedForElevator()
    {
        MirrorTestNetworkManager manager =
            NetworkManager.singleton as MirrorTestNetworkManager;
        if (manager == null ||
            !manager.TryGetRunSnapshot(out StageMapSaveData snapshot) ||
            !manager.TryGetPendingStageNode(out StageNodeSaveData pendingNode))
        {
            return false;
        }

        StageNodeSaveData designatedNode = FindDesignatedBattleNode(snapshot.nodes);
        return designatedNode != null && pendingNode.id == designatedNode.id;
    }

    private static StageNodeSaveData FindDesignatedBattleNode(
        List<StageNodeSaveData> nodes)
    {
        StageNodeSaveData designated = null;
        if (nodes == null)
            return null;

        foreach (StageNodeSaveData node in nodes)
        {
            if (node == null || node.type != StageNodeType.Battle)
                continue;

            if (designated == null || CompareNodeOrder(node, designated) < 0)
                designated = node;
        }

        return designated;
    }

    private static int CompareNodeOrder(StageNodeSaveData left, StageNodeSaveData right)
    {
        int floorOrder = left.floor.CompareTo(right.floor);
        if (floorOrder != 0)
            return floorOrder;

        int indexOrder = left.nodeIndex.CompareTo(right.nodeIndex);
        return indexOrder != 0
            ? indexOrder
            : string.CompareOrdinal(left.id, right.id);
    }

    private static bool CanStartRide(
        bool isAvailable,
        bool hasCompletedTrip,
        bool isMoving,
        int connectedPlayerCount,
        int boardedPlayerCount)
    {
        return isAvailable &&
               !hasCompletedTrip &&
               !isMoving &&
               connectedPlayerCount > 0 &&
               boardedPlayerCount == connectedPlayerCount;
    }

    private static bool TryGetNetworkPlayer(
        Collider other,
        out NetworkIdentity player)
    {
        player = null;
        Rigidbody attachedRigidbody = other.attachedRigidbody;
        return attachedRigidbody != null &&
               other.gameObject == attachedRigidbody.gameObject &&
               attachedRigidbody.TryGetComponent(out player) &&
               player.connectionToClient != null &&
               player.TryGetComponent(out PlayerContext _);
    }

#if UNITY_EDITOR
    [ContextMenu("Mirror 테스트/엘리베이터 노드 및 접속자 전원 조건 검사")]
    private void ValidateElevatorRules()
    {
        StageNodeSaveData later = new()
        {
            id = "A1_F02_N00",
            floor = 2,
            nodeIndex = 0,
            type = StageNodeType.Battle,
        };
        StageNodeSaveData first = new()
        {
            id = "A1_F01_N01",
            floor = 1,
            nodeIndex = 1,
            type = StageNodeType.Battle,
        };
        StageNodeSaveData elite = new()
        {
            id = "A1_F01_N00",
            floor = 1,
            nodeIndex = 0,
            type = StageNodeType.Elite,
        };

        Debug.Assert(FindDesignatedBattleNode(new List<StageNodeSaveData>
        {
            later,
            elite,
            first,
        }) == first);
        Debug.Assert(CanStartRide(true, false, false, 1, 1));
        Debug.Assert(CanStartRide(true, false, false, 2, 2));
        Debug.Assert(CanStartRide(true, false, false, 4, 4));
        Debug.Assert(!CanStartRide(true, false, false, 4, 3));
        Debug.Assert(!CanStartRide(true, false, false, 2, 1));
        Debug.Assert(!CanStartRide(true, false, false, 0, 0));
        Debug.Assert(!CanStartRide(true, true, false, 4, 4));
        Debug.Log("[MirrorFourPlayerElevator] 노드 및 4인 탑승 규칙 검사 통과", this);
    }
#endif
}

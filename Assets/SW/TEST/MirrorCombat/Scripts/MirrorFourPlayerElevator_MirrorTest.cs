using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Run Snapshot의 일반 전투 노드 하나에서만 보이는 Mirror 테스트 엘리베이터다.
/// 현재 접속한 플레이어가 모두 탑승하면 서버가 상승시키고, 위에 도착한 즉시 각 플레이어의 조작을 복구한다.
/// 잠시 뒤 엘리베이터만 출발 위치로 돌아오며, 내려온 뒤에는 같은 조건으로 횟수 제한 없이 다시 탈 수 있다.
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
    [SerializeField, Min(0f)] private float topWait = 5f;
    [SerializeField, Min(0.1f)] private float navMeshSearchDistance = 1.5f;

    [SyncVar(hook = nameof(HandleAvailabilityChanged))]
    private bool available;

    private readonly HashSet<NetworkIdentity> boardedPlayers = new();
    private Coroutine serverRideRoutine;

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
        if (!available || serverRideRoutine != null ||
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

    /// <summary>
    /// 현재 연결되어 PlayerContext가 준비된 모든 플레이어가 탑승했을 때만 왕복을 시작한다.
    /// 이전 왕복 완료 여부는 조건에 넣지 않아 테스트 중 같은 엘리베이터를 반복해서 검증할 수 있다.
    /// </summary>
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
                serverRideRoutine != null,
                connectedPlayerCount,
                boardedPlayers.Count))
        {
            return;
        }

        serverRideRoutine = StartCoroutine(RideRoundTrip());
    }

    /// <summary>
    /// 상승 중에는 기존 방식대로 탑승자의 입력을 잠시 막아 플랫폼과 함께 이동시킨다.
    /// 정상에 도착하면 곧바로 입력과 NavMesh 상태를 복구하여 플레이어가 위쪽 공간을 움직일 수 있게 한다.
    /// 엘리베이터가 원위치로 돌아온 뒤에는 탑승 Trigger를 다시 열어 다음 왕복을 허용한다.
    /// </summary>
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

        foreach (NetworkIdentity player in boardedPlayers)
        {
            if (player != null && player.connectionToClient != null)
                TargetEndRide(player.connectionToClient);
        }

        if (topWait > 0f)
            yield return new WaitForSeconds(topWait);

        yield return MovePlatform(startPosition);
        yield return waitForFixedUpdate;

        boardedPlayers.Clear();
        serverRideRoutine = null;
        ApplyAvailability(available);
        Debug.Log("[MirrorFourPlayerElevator] 접속 플레이어 전원 탑승 왕복 완료, 재탑승 가능", this);
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
            boardingTrigger.enabled = isAvailable && isServer && serverRideRoutine == null;
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
        bool isMoving,
        int connectedPlayerCount,
        int boardedPlayerCount)
    {
        return isAvailable &&
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
        Debug.Assert(CanStartRide(true, false, 1, 1));
        Debug.Assert(CanStartRide(true, false, 2, 2));
        Debug.Assert(CanStartRide(true, false, 4, 4));
        Debug.Assert(!CanStartRide(true, false, 4, 3));
        Debug.Assert(!CanStartRide(true, false, 2, 1));
        Debug.Assert(!CanStartRide(true, false, 0, 0));
        Debug.Assert(!CanStartRide(true, true, 4, 4));
        Debug.Log("[MirrorFourPlayerElevator] 노드, 전원 탑승 및 반복 사용 규칙 검사 통과", this);
    }
#endif
}

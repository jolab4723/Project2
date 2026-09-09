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
    [SerializeField] private bool alwaysAvailable;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform destinationPoint;
    [SerializeField] private Transform safeLandingPoint;
    [SerializeField] private Vector3 travelOffset = new(0f, 10.75f, 0f);
    [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float topWait = 5f;
    [SerializeField, Min(0.1f)] private float navMeshSearchDistance = 1.5f;

    public Collider BoardingTrigger => boardingTrigger;

    [SyncVar(hook = nameof(HandleAvailabilityChanged))]
    private bool available;

    private readonly HashSet<NetworkIdentity> boardedPlayers = new();
    private Coroutine serverRideRoutine;
    private float nextBoardingCheck;
    private Vector3 localPassengerOffset;
    private Vector3 localSafeStart;
    private bool hasLocalSafeStart;

    private Coroutine localRideRoutine;
    private Rigidbody localPlayerRigidbody;
    private NavMeshAgent localPlayerAgent;
    private T_PlayerController localPlayerController;
    private bool localAgentWasEnabled;
    private bool localAgentWasStopped;
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

        available = IsAvailable(alwaysAvailable, IsCurrentNodeDesignatedForElevator());
        if (alwaysAvailable && (startPoint == null || destinationPoint == null || safeLandingPoint == null ||
            startPoint.IsChildOf(platformRigidbody.transform) || destinationPoint.IsChildOf(platformRigidbody.transform) ||
            safeLandingPoint.IsChildOf(platformRigidbody.transform)))
        {
            available = false;
            Debug.LogError("[MirrorFourPlayerElevator] Stage5 requires fixed start, destination and safe landing points outside the moving platform.", this);
        }
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
        serverRideRoutine = null;

        boardedPlayers.Clear();
        base.OnStopServer();
    }

    public override void OnStopClient()
    {
        RestoreLocalPlayer(false);
        base.OnStopClient();
    }

    [ServerCallback]
    private void Update()
    {
        // Rebuild actual overlaps: duplicate colliders, disconnects, death and returning
        // passengers must not depend on another OnTriggerEnter being delivered.
        if (!available || serverRideRoutine != null || Time.unscaledTime < nextBoardingCheck)
            return;
        nextBoardingCheck = Time.unscaledTime + 0.2f;
        TryStartRide();
    }

    [Server]
    private void TryStartRide()
    {
        if (boardingTrigger is not BoxCollider box || !box.enabled)
            return;

        boardedPlayers.Clear();
        Vector3 scale = box.transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Collider[] overlaps = Physics.OverlapBox(
            box.transform.TransformPoint(box.center), Vector3.Scale(box.size, scale) * 0.5f,
            box.transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Collide);
        foreach (Collider overlap in overlaps)
        {
            if (TryGetNetworkPlayer(overlap, out NetworkIdentity player) && IsWaitingPlayer(player))
                boardedPlayers.Add(player);
        }

        int waitingPlayerCount = 0;
        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
        {
            if (IsWaitingPlayer(connection?.identity))
                waitingPlayerCount++;
        }

        if (CanStartRide(available, serverRideRoutine != null, waitingPlayerCount, boardedPlayers.Count))
            serverRideRoutine = StartCoroutine(RideRoundTrip());
    }

    private bool IsWaitingPlayer(NetworkIdentity player)
    {
        bool connected = player != null && player.connectionToClient != null;
        PlayerContext context = player != null ? player.GetComponent<PlayerContext>() : null;
        bool alive = context != null && context.RuntimeState != null && !context.RuntimeState.IsDead;
        bool absent = player != null && player.GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent == true;
        // Once a player reached the upper floor, they do not block passengers who
        // return downstairs for another trip. Default test mode keeps the whole-party rule.
        bool alreadyUpstairs = alwaysAvailable && destinationPoint != null && player != null &&
            player.transform.position.y >= destinationPoint.position.y - navMeshSearchDistance;
        return IsWaitingParticipant(connected, alive, absent, alreadyUpstairs);
    }

    public static bool IsAvailable(bool always, bool designatedNode) => always || designatedNode;

    public static bool IsWaitingParticipant(bool connected, bool alive, bool absent, bool alreadyUpstairs)
        => connected && alive && !absent && !alreadyUpstairs;

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
                TargetBeginRide(player.connectionToClient, platformRigidbody.position);
        }

        Vector3 startPosition = startPoint != null ? startPoint.position : platformRigidbody.position;
        Vector3 destination = destinationPoint != null ? destinationPoint.position : startPosition + travelOffset;
        yield return MovePlatform(destination);

        foreach (NetworkIdentity player in boardedPlayers)
        {
            if (player != null && player.connectionToClient != null)
                TargetEndRide(player.connectionToClient, destination,
                    player.GetComponent<PlayerContext>()?.RuntimeState?.IsDead == false);
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
    private void TargetBeginRide(NetworkConnectionToClient target, Vector3 platformStart)
    {
        RestoreLocalPlayer(false);
        if (!CanRestoreLocalControl())
            return;

        NetworkIdentity localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null)
            return;

        localPlayerRigidbody = localPlayer.GetComponent<Rigidbody>();
        localPlayerAgent = localPlayer.GetComponent<NavMeshAgent>();
        localPlayerController = localPlayer.GetComponent<T_PlayerController>();

        localPassengerOffset = localPlayerRigidbody != null
            ? localPlayerRigidbody.position - platformStart : Vector3.zero;
        hasLocalSafeStart = localPlayerAgent != null && localPlayerAgent.isOnNavMesh &&
            NavMesh.SamplePosition(localPlayer.transform.position, out _, navMeshSearchDistance, localPlayerAgent.areaMask);
        localSafeStart = localPlayer.transform.position;
        localControlWasEnabled = localPlayerController?.IsControlEnabled == true;
        if (localControlWasEnabled)
            localPlayerController.SetControlEnable(false);

        localAgentWasEnabled = localPlayerAgent != null && localPlayerAgent.enabled;
        localAgentWasStopped = localAgentWasEnabled && localPlayerAgent.isOnNavMesh && localPlayerAgent.isStopped;
        if (localAgentWasEnabled)
        {
            if (localPlayerAgent.isOnNavMesh)
                localPlayerAgent.ResetPath();

            if (localPlayerAgent.isOnNavMesh)
                localPlayerAgent.isStopped = true;
            localPlayerAgent.enabled = false;
        }

        localRideRoutine = StartCoroutine(FollowPlatform());
    }

    [TargetRpc]
    private void TargetEndRide(NetworkConnectionToClient target, Vector3 finalPlatformPosition, bool alive)
    {
        // End RPC can arrive before the final interpolated NetworkTransform sample.
        // Apply the absolute endpoint and captured offset before searching the NavMesh.
        if (alive && CanRestoreLocalControl() && localPlayerRigidbody != null)
            localPlayerRigidbody.position = finalPlatformPosition + localPassengerOffset;
        RestoreLocalPlayer(alive);
    }

    private IEnumerator FollowPlatform()
    {
        while (true)
        {
            yield return waitForFixedUpdate;
            if (!CanRestoreLocalControl())
            {
                localRideRoutine = null;
                RestoreLocalPlayer(false);
                yield break;
            }
            if (localPlayerRigidbody != null)
                localPlayerRigidbody.MovePosition(platformRigidbody.position + localPassengerOffset);
        }
    }

    private static bool CanRestoreLocalControl()
    {
        NetworkIdentity player = NetworkClient.localPlayer;
        return NetworkClient.active && NetworkClient.ready && player != null &&
            player.GetComponent<PlayerContext>()?.RuntimeState?.IsDead == false &&
            player.GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent != true;
    }

    private void RestoreLocalPlayer(bool restoreControl)
    {
        if (localRideRoutine != null)
        {
            StopCoroutine(localRideRoutine);
            localRideRoutine = null;
        }

        bool canRestore = restoreControl && CanRestoreLocalControl();
        if (canRestore && localAgentWasEnabled && localPlayerAgent != null)
        {
            Vector3 playerPosition = localPlayerRigidbody != null
                ? localPlayerRigidbody.position : localPlayerAgent.transform.position;
            bool found = NavMesh.SamplePosition(playerPosition, out NavMeshHit hit,
                navMeshSearchDistance, localPlayerAgent.areaMask);
            if (!found && safeLandingPoint != null)
                found = NavMesh.SamplePosition(safeLandingPoint.position, out hit,
                    navMeshSearchDistance, localPlayerAgent.areaMask);
            if (!found && hasLocalSafeStart)
                found = NavMesh.SamplePosition(localSafeStart, out hit,
                    navMeshSearchDistance, localPlayerAgent.areaMask);

            if (found)
            {
                if (localPlayerRigidbody != null)
                    localPlayerRigidbody.position = hit.position;
                localPlayerAgent.transform.position = hit.position;
                localPlayerAgent.enabled = true;
                canRestore = localPlayerAgent.Warp(hit.position);
                if (!canRestore)
                    localPlayerAgent.enabled = false;
                else
                    localPlayerAgent.isStopped = localAgentWasStopped;
            }
            else
            {
                canRestore = false;
                Debug.LogError("[MirrorFourPlayerElevator] No valid landing or boarding NavMesh; control remains blocked. Configure safeLandingPoint on the upper floor.", this);
            }
        }

        if (canRestore && localControlWasEnabled)
            localPlayerController?.SetControlEnable(true);

        localPlayerRigidbody = null;
        localPlayerAgent = null;
        localPlayerController = null;
        localAgentWasEnabled = false;
        localControlWasEnabled = false;
        hasLocalSafeStart = false;
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

    public static bool CanStartRide(
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

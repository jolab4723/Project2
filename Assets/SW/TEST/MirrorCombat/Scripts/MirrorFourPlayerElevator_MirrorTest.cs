using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mirror 세션 테스트용 Stage 5 엘리베이터다.
/// 서버가 승강 중 플레이어 위치를 책임지고, 서버와 소유 클라이언트가 모두 2층 착지를
/// 확인한 뒤에만 이동과 스킬 입력을 다시 허용한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(NetworkTransformReliable), typeof(Rigidbody))]
public sealed class MirrorFourPlayerElevator_MirrorTest : NetworkBehaviour
{
    private enum ElevatorState : byte
    {
        Unavailable,
        Available,
        Preparing,
        Ascending,
        Landing,
        WaitingForExit,
        Descending,
    }

    private sealed class PassengerRideState
    {
        public NetworkIdentity Identity;
        public Vector3 Offset;
        public NavMeshAgent Agent;
        public bool OriginalAgentEnabled;
        public bool OriginalAgentStopped;
    }

    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Collider boardingTrigger;
    [SerializeField] private Rigidbody platformRigidbody;
    [SerializeField] private bool alwaysAvailable;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform destinationPoint;
    [SerializeField] private Transform safeLandingPoint;
    [SerializeField] private Vector3 travelOffset = new(0f, 10.75f, 0f);
    [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
    [SerializeField, Min(0.1f)] private float navMeshSearchDistance = 1.5f;

    public Collider BoardingTrigger => boardingTrigger;

    [SyncVar(hook = nameof(HandleAvailabilityChanged))]
    private bool available;

    private readonly HashSet<NetworkIdentity> boardedPlayers = new();
    private readonly Dictionary<NetworkIdentity, PassengerRideState> passengerStates = new();
    private readonly HashSet<NetworkIdentity> expectedReadyPassengers = new();
    private readonly HashSet<NetworkIdentity> pendingLandingPassengers = new();
    private readonly List<NetworkIdentity> invalidPassengers = new();
    private readonly WaitForFixedUpdate waitForFixedUpdate = new();
    private readonly WaitForSeconds topOccupancyPoll = new(0.2f);

    private ElevatorState state = ElevatorState.Unavailable;
    private Coroutine serverRideRoutine;
    private float nextBoardingCheck;
    private uint currentRideId;

    private uint localRideId;
    private Coroutine localPrepareRoutine;
    private Coroutine localRideRoutine;
    private Coroutine localLandingRoutine;
    private NetworkIdentity localRidePlayer;
    private Rigidbody localPlayerRigidbody;
    private NavMeshAgent localPlayerAgent;
    private MirrorSpawnedPlayerBinder localPlayerBinder;
    private FighterSkillAuthority_MirrorTest localPlayerSkills;
    private Vector3 localPassengerOffset;
    private bool localAgentWasEnabled;
    private bool localAgentWasStopped;
    private bool localAcquiredCutsceneBlock;

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
        if (available && !HasValidFixedPoints())
        {
            available = false;
            Debug.LogError(
                "[MirrorFourPlayerElevator] start, destination, safe landing 지점은 이동 발판 밖의 고정 Transform이어야 합니다.",
                this);
        }

        state = available ? ElevatorState.Available : ElevatorState.Unavailable;
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

        invalidPassengers.Clear();
        invalidPassengers.AddRange(passengerStates.Keys);
        foreach (NetworkIdentity passenger in invalidPassengers)
            ReleaseServerPassenger(passenger);

        ClearServerRideCollections();
        state = ElevatorState.Unavailable;
        base.OnStopServer();
    }

    public override void OnStopClient()
    {
        TeardownLocalRide(false);
        base.OnStopClient();
    }

    [ServerCallback]
    private void Update()
    {
        if (!available || state != ElevatorState.Available || serverRideRoutine != null ||
            Time.unscaledTime < nextBoardingCheck)
            return;

        nextBoardingCheck = Time.unscaledTime + 0.2f;
        TryStartRide();
    }

    [Server]
    private void TryStartRide()
    {
        if (boardingTrigger is not BoxCollider box || !box.enabled)
            return;

        CollectPlayersInside(box, boardedPlayers);
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
        bool alreadyUpstairs = alwaysAvailable && destinationPoint != null && player != null &&
            player.transform.position.y >= destinationPoint.position.y - navMeshSearchDistance;
        return IsWaitingParticipant(connected, alive, absent, alreadyUpstairs);
    }

    public static bool IsAvailable(bool always, bool designatedNode) => always || designatedNode;

    public static bool IsWaitingParticipant(bool connected, bool alive, bool absent, bool alreadyUpstairs)
        => connected && alive && !absent && !alreadyUpstairs;

    [Server]
    private IEnumerator RideRoundTrip()
    {
        state = ElevatorState.Preparing;
        if (boardingTrigger != null)
            boardingTrigger.enabled = false;

        AdvanceRideId();
        BeginServerRide();

        while (passengerStates.Count > 0 && expectedReadyPassengers.Count > 0)
        {
            PruneInvalidPassengers(true);
            yield return null;
        }

        while (passengerStates.Count > 0 && !AreServerPassengersSettled())
        {
            PruneInvalidPassengers(true);
            yield return null;
        }

        Vector3 startPosition = startPoint != null ? startPoint.position : platformRigidbody.position;
        Vector3 destination = destinationPoint != null ? destinationPoint.position : startPosition + travelOffset;

        if (passengerStates.Count > 0)
        {
            ArmServerPassengers();
            state = ElevatorState.Ascending;
            yield return MovePlatform(destination, true);
        }

        if (passengerStates.Count > 0)
        {
            state = ElevatorState.Landing;
            BeginServerLanding(destination);
            while (passengerStates.Count > 0 && pendingLandingPassengers.Count > 0)
            {
                PruneInvalidPassengers(true);
                yield return null;
            }
        }

        state = ElevatorState.WaitingForExit;
        yield return waitForFixedUpdate;
        while (!CanDescend(pendingLandingPassengers.Count > 0, CountPlatformOccupants()))
        {
            PruneInvalidPassengers(true);
            yield return topOccupancyPoll;
        }

        state = ElevatorState.Descending;
        yield return MovePlatform(startPosition, false);
        yield return waitForFixedUpdate;

        ClearServerRideCollections();
        serverRideRoutine = null;
        state = available ? ElevatorState.Available : ElevatorState.Unavailable;
        ApplyAvailability(available);
        Debug.Log("[MirrorFourPlayerElevator] 전원이 내린 뒤 빈 발판이 출발점으로 복귀했습니다.", this);
    }

    [Server]
    private void BeginServerRide()
    {
        passengerStates.Clear();
        expectedReadyPassengers.Clear();
        pendingLandingPassengers.Clear();

        Vector3 platformPosition = platformRigidbody.position;
        foreach (NetworkIdentity player in boardedPlayers)
        {
            if (!IsActivePassenger(player))
                continue;

            NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
            PassengerRideState passenger = new()
            {
                Identity = player,
                Offset = GetBodyPosition(player) - platformPosition,
                Agent = agent,
                OriginalAgentEnabled = agent != null && agent.enabled,
                OriginalAgentStopped = agent != null && agent.enabled && agent.isOnNavMesh && agent.isStopped,
            };
            passengerStates.Add(player, passenger);
            expectedReadyPassengers.Add(player);

            player.GetComponent<FighterSkillAuthority_MirrorTest>()?.SetServerRideLocked(true);
            player.GetComponent<PlayerNetworkTransform_MirrorTest>()?.ServerClearSnapshots();
            TargetRequestRideReady(player.connectionToClient, currentRideId, platformPosition);
        }
    }

    [Server]
    private void ArmServerPassengers()
    {
        foreach (PassengerRideState passenger in passengerStates.Values)
        {
            NavMeshAgent agent = passenger.Agent;
            if (agent == null || !agent.enabled)
                continue;
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
            agent.enabled = false;
        }
    }

    [Server]
    private bool AreServerPassengersSettled()
    {
        foreach (NetworkIdentity player in passengerStates.Keys)
        {
            WBH_PlayerStateMachine stateMachine = player.GetComponent<WBH_PlayerStateMachine>();
            if (stateMachine != null && stateMachine.IsAnyState(
                    PlayerState.Hit, PlayerState.Attack, PlayerState.Skill, PlayerState.Dodge))
                return false;

            if (player.GetComponent<PlayerCombatAuthority_MirrorTest>()?.ServerAttackPending == true ||
                player.GetComponent<FighterSkillAuthority_MirrorTest>()?.ServerMotionLocked == true)
                return false;
        }
        return true;
    }

    [Server]
    private IEnumerator MovePlatform(Vector3 destination, bool movePassengers)
    {
        while ((platformRigidbody.position - destination).sqrMagnitude > 0.0001f)
        {
            Vector3 nextPosition = Vector3.MoveTowards(
                platformRigidbody.position, destination, moveSpeed * Time.fixedDeltaTime);
            platformRigidbody.MovePosition(nextPosition);

            if (movePassengers)
            {
                PruneInvalidPassengers(true);
                foreach (PassengerRideState passenger in passengerStates.Values)
                    SetBodyPosition(passenger.Identity, nextPosition + passenger.Offset);
                if (passengerStates.Count == 0)
                    yield break;
            }
            yield return waitForFixedUpdate;
        }

        platformRigidbody.position = destination;
        if (movePassengers)
        {
            foreach (PassengerRideState passenger in passengerStates.Values)
                SetBodyPosition(passenger.Identity, destination + passenger.Offset);
        }
    }

    [Server]
    private void BeginServerLanding(Vector3 destination)
    {
        pendingLandingPassengers.Clear();
        List<PassengerRideState> landingPassengers = new(passengerStates.Values);
        foreach (PassengerRideState passenger in landingPassengers)
        {
            NetworkIdentity player = passenger.Identity;
            pendingLandingPassengers.Add(player);
            if (!TryFindUpperLanding(passenger, destination, out Vector3 landingPosition) ||
                !TryPlaceServerPassenger(passenger, landingPosition))
            {
                Debug.LogError(
                    $"[MirrorFourPlayerElevator] netId={player.netId}의 2층 NavMesh 착지점을 찾지 못해 조작 잠금을 유지합니다.",
                    player);
                continue;
            }
            TargetEndRide(player.connectionToClient, currentRideId, landingPosition);
        }
    }

    [Server]
    private bool TryFindUpperLanding(PassengerRideState passenger, Vector3 destination, out Vector3 position)
    {
        int areaMask = passenger.Agent != null ? passenger.Agent.areaMask : NavMesh.AllAreas;
        Vector3 candidate = destination + passenger.Offset;
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSearchDistance, areaMask))
        {
            position = hit.position;
            return true;
        }

        if (safeLandingPoint != null && NavMesh.SamplePosition(
                safeLandingPoint.position, out hit, Mathf.Max(3f, navMeshSearchDistance), areaMask))
        {
            position = hit.position;
            return true;
        }

        position = default;
        return false;
    }

    [Server]
    private bool TryPlaceServerPassenger(PassengerRideState passenger, Vector3 position)
    {
        SetBodyPosition(passenger.Identity, position);
        Physics.SyncTransforms();

        NavMeshAgent agent = passenger.Agent;
        if (agent == null || !passenger.OriginalAgentEnabled)
            return true;

        agent.transform.position = position;
        agent.enabled = true;
        if (!agent.isOnNavMesh || !agent.Warp(position))
        {
            agent.enabled = false;
            return false;
        }
        agent.ResetPath();
        agent.isStopped = true;
        return true;
    }

    [TargetRpc]
    private void TargetRequestRideReady(
        NetworkConnectionToClient target, uint rideId, Vector3 platformPosition)
    {
        if (localRideId == rideId && localPrepareRoutine != null)
            return;
        TeardownLocalRide(CanUseLocalRide());
        localPrepareRoutine = StartCoroutine(LocalPrepareRide(rideId, platformPosition));
    }

    private IEnumerator LocalPrepareRide(uint rideId, Vector3 platformPosition)
    {
        NetworkIdentity player = NetworkClient.localPlayer;
        if (player == null || !CanUseLocalRide())
        {
            localPrepareRoutine = null;
            yield break;
        }

        localRideId = rideId;
        localRidePlayer = player;
        localPlayerRigidbody = player.GetComponent<Rigidbody>();
        localPlayerAgent = player.GetComponent<NavMeshAgent>();
        localPlayerBinder = player.GetComponent<MirrorSpawnedPlayerBinder>();
        localPlayerSkills = player.GetComponent<FighterSkillAuthority_MirrorTest>();
        localPassengerOffset = GetBodyPosition(player) - platformPosition;
        localAgentWasEnabled = localPlayerAgent != null && localPlayerAgent.enabled;
        localAgentWasStopped = localAgentWasEnabled && localPlayerAgent.isOnNavMesh && localPlayerAgent.isStopped;

        if (localPlayerBinder != null && !localPlayerBinder.IsCutsceneInputBlocked)
        {
            localPlayerBinder.SetCutsceneInputBlocked(true);
            localAcquiredCutsceneBlock = true;
        }
        localPlayerSkills?.SetLocalRideLocked(true);

        WBH_PlayerStateMachine stateMachine = player.GetComponent<WBH_PlayerStateMachine>();
        while (CanUseLocalRide() && stateMachine != null && stateMachine.IsAnyState(
                   PlayerState.Hit, PlayerState.Attack, PlayerState.Skill, PlayerState.Dodge))
            yield return null;

        if (!CanUseLocalRide() || localRideId != rideId)
        {
            localPrepareRoutine = null;
            TeardownLocalRide(false);
            yield break;
        }

        if (localPlayerAgent != null && localPlayerAgent.enabled)
        {
            if (localPlayerAgent.isOnNavMesh)
            {
                localPlayerAgent.ResetPath();
                localPlayerAgent.isStopped = true;
            }
            localPlayerAgent.enabled = false;
        }

        if (!isServer)
            localRideRoutine = StartCoroutine(FollowPlatform(rideId));
        localPrepareRoutine = null;
        CmdReportRideReady(rideId);
    }

    [Command(requiresAuthority = false)]
    private void CmdReportRideReady(uint rideId, NetworkConnectionToClient sender = null)
    {
        if (TryValidatePassengerReport(sender, rideId, ElevatorState.Preparing, expectedReadyPassengers))
            expectedReadyPassengers.Remove(sender.identity);
    }

    private IEnumerator FollowPlatform(uint rideId)
    {
        while (localRideId == rideId && CanUseLocalRide())
        {
            yield return waitForFixedUpdate;
            if (localPlayerRigidbody != null)
                localPlayerRigidbody.MovePosition(platformRigidbody.position + localPassengerOffset);
            else if (localRidePlayer != null)
                localRidePlayer.transform.position = platformRigidbody.position + localPassengerOffset;
        }

        localRideRoutine = null;
        if (localRideId == rideId)
            TeardownLocalRide(false);
    }

    [TargetRpc]
    private void TargetEndRide(
        NetworkConnectionToClient target, uint rideId, Vector3 confirmedLandingPosition)
    {
        if (localRideId != rideId || localRidePlayer == null)
            return;
        if (localLandingRoutine != null)
            StopCoroutine(localLandingRoutine);
        localLandingRoutine = StartCoroutine(LocalCompleteLanding(rideId, confirmedLandingPosition));
    }

    private IEnumerator LocalCompleteLanding(uint rideId, Vector3 confirmedLandingPosition)
    {
        if (localRideRoutine != null)
        {
            StopCoroutine(localRideRoutine);
            localRideRoutine = null;
        }

        while (localRideId == rideId && CanUseLocalRide())
        {
            SetBodyPosition(localRidePlayer, confirmedLandingPosition);
            Physics.SyncTransforms();
            bool placed = true;
            if (localPlayerAgent != null && localAgentWasEnabled)
            {
                localPlayerAgent.transform.position = confirmedLandingPosition;
                localPlayerAgent.enabled = true;
                placed = localPlayerAgent.isOnNavMesh && localPlayerAgent.Warp(confirmedLandingPosition);
                if (placed)
                {
                    localPlayerAgent.ResetPath();
                    localPlayerAgent.isStopped = true;
                }
                else
                    localPlayerAgent.enabled = false;
            }

            if (placed)
            {
                localLandingRoutine = null;
                CmdReportLandingComplete(rideId);
                yield break;
            }
            yield return null;
        }

        localLandingRoutine = null;
        if (localRideId == rideId)
            TeardownLocalRide(false);
    }

    [Command(requiresAuthority = false)]
    private void CmdReportLandingComplete(uint rideId, NetworkConnectionToClient sender = null)
    {
        if (!TryValidatePassengerReport(sender, rideId, ElevatorState.Landing, pendingLandingPassengers))
            return;

        NetworkIdentity player = sender.identity;
        pendingLandingPassengers.Remove(player);
        if (passengerStates.TryGetValue(player, out PassengerRideState passenger))
        {
            // 2층 도착 전에 전송된 오래된 위치를 먼저 버린 뒤 이동 잠금을 푼다.
            player.GetComponent<PlayerNetworkTransform_MirrorTest>()?.ServerClearSnapshots();
            player.GetComponent<FighterSkillAuthority_MirrorTest>()?.SetServerRideLocked(false);
            RestoreServerAgentAfterLanding(passenger);
        }

        TargetReleaseRideControl(sender, rideId);
        passengerStates.Remove(player);
        expectedReadyPassengers.Remove(player);
    }

    [TargetRpc]
    private void TargetReleaseRideControl(NetworkConnectionToClient target, uint rideId)
    {
        if (localRideId == rideId)
            TeardownLocalRide(true);
    }

    [TargetRpc]
    private void TargetCancelRideControl(
        NetworkConnectionToClient target, uint rideId, bool hasSafePosition, Vector3 safePosition)
    {
        if (localRideId != rideId)
            return;
        if (hasSafePosition && localRidePlayer != null)
            SetBodyPosition(localRidePlayer, safePosition);
        TeardownLocalRide(false);
    }

    private void TeardownLocalRide(bool restoreAgentState)
    {
        if (localPrepareRoutine != null)
        {
            StopCoroutine(localPrepareRoutine);
            localPrepareRoutine = null;
        }
        if (localRideRoutine != null)
        {
            StopCoroutine(localRideRoutine);
            localRideRoutine = null;
        }
        if (localLandingRoutine != null)
        {
            StopCoroutine(localLandingRoutine);
            localLandingRoutine = null;
        }

        if (restoreAgentState && localPlayerAgent != null && localAgentWasEnabled &&
            localPlayerAgent.enabled && localPlayerAgent.isOnNavMesh)
            localPlayerAgent.isStopped = localAgentWasStopped;

        localPlayerSkills?.SetLocalRideLocked(false);
        if (localAcquiredCutsceneBlock && localPlayerBinder != null)
            localPlayerBinder.SetCutsceneInputBlocked(false);

        localRideId = 0;
        localRidePlayer = null;
        localPlayerRigidbody = null;
        localPlayerAgent = null;
        localPlayerBinder = null;
        localPlayerSkills = null;
        localPassengerOffset = default;
        localAgentWasEnabled = false;
        localAgentWasStopped = false;
        localAcquiredCutsceneBlock = false;
    }

    [Server]
    private bool TryValidatePassengerReport(
        NetworkConnectionToClient sender,
        uint rideId,
        ElevatorState requiredState,
        HashSet<NetworkIdentity> expectedPassengers)
    {
        NetworkIdentity player = sender?.identity;
        return player != null && rideId == currentRideId && state == requiredState &&
               player.connectionToClient == sender && passengerStates.ContainsKey(player) &&
               expectedPassengers.Contains(player);
    }

    [Server]
    private void PruneInvalidPassengers(bool notifyOwner)
    {
        invalidPassengers.Clear();
        foreach (NetworkIdentity player in passengerStates.Keys)
        {
            if (!IsActivePassenger(player))
                invalidPassengers.Add(player);
        }
        foreach (NetworkIdentity player in invalidPassengers)
            CleanupPassenger(player, notifyOwner);
    }

    [Server]
    private void CleanupPassenger(NetworkIdentity passenger, bool notifyOwner)
    {
        if (!passengerStates.TryGetValue(passenger, out PassengerRideState passengerState))
            return;

        NetworkConnectionToClient connection = passenger != null ? passenger.connectionToClient : null;
        bool hasSafePosition = TryPlaceCanceledPassenger(passengerState, out Vector3 safePosition);
        if (notifyOwner && connection != null)
            TargetCancelRideControl(connection, currentRideId, hasSafePosition, safePosition);

        ReleaseServerPassenger(passenger);
        passengerStates.Remove(passenger);
        expectedReadyPassengers.Remove(passenger);
        pendingLandingPassengers.Remove(passenger);
        boardedPlayers.Remove(passenger);
    }

    [Server]
    private bool TryPlaceCanceledPassenger(PassengerRideState passenger, out Vector3 position)
    {
        position = GetBodyPosition(passenger.Identity);
        if (state is ElevatorState.Ascending or ElevatorState.Landing)
        {
            Vector3 destination = destinationPoint != null
                ? destinationPoint.position
                : platformRigidbody.position + travelOffset;
            if (!TryFindUpperLanding(passenger, destination, out position))
                return false;
        }

        bool placed = TryPlaceServerPassenger(passenger, position);
        if (placed)
            RestoreServerAgentAfterLanding(passenger);
        return placed;
    }

    private static void ReleaseServerPassenger(NetworkIdentity passenger)
    {
        if (passenger == null)
            return;
        // 잠금을 풀기 전에 잠금 중 쌓인 오래된 위치부터 버려야 한다.
        passenger.GetComponent<PlayerNetworkTransform_MirrorTest>()?.ServerClearSnapshots();
        passenger.GetComponent<FighterSkillAuthority_MirrorTest>()?.SetServerRideLocked(false);
    }

    private static void RestoreServerAgentAfterLanding(PassengerRideState passenger)
    {
        NavMeshAgent agent = passenger.Agent;
        if (agent != null && passenger.OriginalAgentEnabled && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = passenger.OriginalAgentStopped;
    }

    [Server]
    private int CountPlatformOccupants()
    {
        if (boardingTrigger is not BoxCollider box)
            return 0;
        CollectPlayersInside(box, boardedPlayers);
        return boardedPlayers.Count;
    }

    private static void CollectPlayersInside(BoxCollider box, HashSet<NetworkIdentity> results)
    {
        results.Clear();
        Vector3 scale = box.transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Collider[] overlaps = Physics.OverlapBox(
            box.transform.TransformPoint(box.center),
            Vector3.Scale(box.size, scale) * 0.5f,
            box.transform.rotation,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        foreach (Collider overlap in overlaps)
        {
            if (!TryGetNetworkPlayer(overlap, out NetworkIdentity player))
                continue;
            PlayerContext context = player.GetComponent<PlayerContext>();
            bool active = context?.RuntimeState != null && !context.RuntimeState.IsDead &&
                          player.GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent != true;
            if (active)
                results.Add(player);
        }
    }

    private static bool IsActivePassenger(NetworkIdentity player)
    {
        if (player == null || player.connectionToClient == null || player.connectionToClient.identity != player)
            return false;
        PlayerContext context = player.GetComponent<PlayerContext>();
        return context?.RuntimeState != null && !context.RuntimeState.IsDead &&
               player.GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent != true;
    }

    private static bool CanUseLocalRide()
    {
        NetworkIdentity player = NetworkClient.localPlayer;
        return NetworkClient.active && NetworkClient.ready && player != null &&
               player.GetComponent<PlayerContext>()?.RuntimeState?.IsDead == false &&
               player.GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent != true;
    }

    private static Vector3 GetBodyPosition(NetworkIdentity player)
    {
        Rigidbody body = player != null ? player.GetComponent<Rigidbody>() : null;
        return body != null ? body.position : player != null ? player.transform.position : default;
    }

    private static void SetBodyPosition(NetworkIdentity player, Vector3 position)
    {
        if (player == null)
            return;
        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
            body.position = position;
        player.transform.position = position;
    }

    private void AdvanceRideId()
    {
        currentRideId++;
        if (currentRideId == 0)
            currentRideId++;
    }

    private void ClearServerRideCollections()
    {
        boardedPlayers.Clear();
        passengerStates.Clear();
        expectedReadyPassengers.Clear();
        pendingLandingPassengers.Clear();
        invalidPassengers.Clear();
    }

    private bool HasValidFixedPoints()
    {
        return platformRigidbody != null && startPoint != null && destinationPoint != null && safeLandingPoint != null &&
               !startPoint.IsChildOf(platformRigidbody.transform) &&
               !destinationPoint.IsChildOf(platformRigidbody.transform) &&
               !safeLandingPoint.IsChildOf(platformRigidbody.transform);
    }

    private void HandleAvailabilityChanged(bool _, bool isAvailable) => ApplyAvailability(isAvailable);

    private void ApplyAvailability(bool isAvailable)
    {
        if (visualRoot != null)
            visualRoot.SetActive(isAvailable);
        if (boardingTrigger != null)
            boardingTrigger.enabled = isAvailable && isServer && state == ElevatorState.Available && serverRideRoutine == null;
    }

    [Server]
    private bool IsCurrentNodeDesignatedForElevator()
    {
        MirrorTestNetworkManager manager = NetworkManager.singleton as MirrorTestNetworkManager;
        if (manager == null ||
            !manager.TryGetRunSnapshot(out StageMapSaveData snapshot) ||
            !manager.TryGetPendingStageNode(out StageNodeSaveData pendingNode))
            return false;

        StageNodeSaveData designatedNode = FindDesignatedBattleNode(snapshot.nodes);
        return designatedNode != null && pendingNode.id == designatedNode.id;
    }

    private static StageNodeSaveData FindDesignatedBattleNode(List<StageNodeSaveData> nodes)
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
        return indexOrder != 0 ? indexOrder : string.CompareOrdinal(left.id, right.id);
    }

    public static bool CanStartRide(
        bool isAvailable, bool isMoving, int connectedPlayerCount, int boardedPlayerCount)
    {
        return isAvailable && !isMoving && connectedPlayerCount > 0 &&
               boardedPlayerCount == connectedPlayerCount;
    }

    public static bool CanDescend(bool hasPendingLanding, int platformOccupants)
        => !hasPendingLanding && platformOccupants == 0;

    private static bool TryGetNetworkPlayer(Collider other, out NetworkIdentity player)
    {
        player = null;
        Rigidbody attachedRigidbody = other.attachedRigidbody;
        return attachedRigidbody != null && attachedRigidbody.TryGetComponent(out player) &&
               player.connectionToClient != null && player.TryGetComponent(out PlayerContext _);
    }

#if UNITY_EDITOR
    [ContextMenu("Mirror Test/Validate Elevator Rules")]
    private void ValidateElevatorRules()
    {
        StageNodeSaveData later = new() { id = "A1_F02_N00", floor = 2, nodeIndex = 0, type = StageNodeType.Battle };
        StageNodeSaveData first = new() { id = "A1_F01_N01", floor = 1, nodeIndex = 1, type = StageNodeType.Battle };
        StageNodeSaveData elite = new() { id = "A1_F01_N00", floor = 1, nodeIndex = 0, type = StageNodeType.Elite };
        Debug.Assert(FindDesignatedBattleNode(new List<StageNodeSaveData> { later, elite, first }) == first);
        Debug.Assert(CanStartRide(true, false, 1, 1));
        Debug.Assert(CanStartRide(true, false, 2, 2));
        Debug.Assert(CanStartRide(true, false, 4, 4));
        Debug.Assert(!CanStartRide(true, false, 4, 3));
        Debug.Assert(!CanStartRide(true, false, 0, 0));
        Debug.Assert(!CanStartRide(true, true, 4, 4));
        Debug.Assert(!CanDescend(true, 0));
        Debug.Assert(!CanDescend(false, 1));
        Debug.Assert(CanDescend(false, 0));
        Debug.Log("[MirrorFourPlayerElevator] 활성화, 탑승, 상층 점유 규칙 검사를 통과했습니다.", this);
    }
#endif
}

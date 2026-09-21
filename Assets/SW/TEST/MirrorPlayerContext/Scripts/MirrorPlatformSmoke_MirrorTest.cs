using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ItemSystem;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 명시적 --mirror-smoke-platform 실행에서만 Stage5 엘리베이터의 탑승, 이동 중 사망,
/// 왕복 후 부활과 소유 클라이언트 NavMesh 복구를 실제 네트워크 흐름으로 검증한다.
/// 전투 난이도나 웨이브 완료는 이 fixture의 검증 범위가 아니다.
/// </summary>
public sealed class MirrorPlatformSmoke_MirrorTest : MonoBehaviour
{
    private const string PlatformArgument = "--mirror-smoke-platform";
    private const string Stage5SceneName = "Act1_Stage5";
    private const double DefaultTimeout = 180d;
    private const double FullModeTimeout = 300d;
    private const double DefaultWait = 20d;
    private const double SceneEntryWait = 90d;
    private const byte StageReadyPhase = 1;
    private const byte MovePhase = 2;
    private const byte HoldThreePhase = 3;
    private const byte RisenPhase = 4;
    private const byte DeathPhase = 5;
    private const byte RevivePhase = 6;
    private const byte CompletePhase = 254;
    private const byte FailurePhase = 255;

    /// <summary>MirrorCombatSmoke_MirrorTest와 이름이 겹치지 않는 플랫폼 전용 단계 메시지다.</summary>
    public struct PlatformStepMessage_MirrorTest : NetworkMessage
    {
        public int Step;
        public byte Phase;
        public uint Actor;
        public Vector3 Destination;
        public float Height;
        public bool TriggerEnabled;
        public bool Outside;
        public string Detail;
    }

    /// <summary>각 원본 connection에서만 받을 수 있는 플랫폼 단계 확인 응답이다.</summary>
    public struct PlatformAckMessage_MirrorTest : NetworkMessage
    {
        public int Step;
        public byte Phase;
        public uint Actor;
        public bool Passed;
        public string Detail;
    }

    public static bool Completed { get; private set; }
    public static bool Passed { get; private set; }

    private MirrorTestNetworkManager manager;
    private string role;
    private double startedAt;
    private double duration;
    private bool serverRegistered;
    private bool clientRegistered;
    private bool serverRoutineStarted;
    private bool stage5SelectionRequested;
    private bool clientRosterValidated;
    private bool failed;
    private int step;
    private byte phase;
    private PlatformStepMessage_MirrorTest currentStep;
    private int lastClientStep;

    private readonly HashSet<int> participants = new();
    private readonly HashSet<int> acknowledgements = new();
    private readonly HashSet<uint> participantNetIds = new();
    private readonly List<PlayerContext> serverActors = new();
    private readonly List<PlayerContext> firstBoarders = new();

    private PlayerContext fourthBoarder;
    private PlayerContext deathActor;
    private MirrorFourPlayerElevator_MirrorTest elevator;
    private BoxCollider boardingBox;
    private Rigidbody platformBody;
    private float platformStartHeight;

    private WBH_PlayerInputHandler_MirrorTest movementInput;
    private PlayerActionInputHandler_MirrorTest actionInput;
    private bool movementInputWasEnabled;
    private bool actionInputWasEnabled;
    private bool inputsCaptured;

    private static string Argument(string key) =>
        MirrorSmokeConfiguration_MirrorTest.Argument(key);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetResult()
    {
        Completed = false;
        Passed = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachWhenRequested()
    {
        if (Argument(PlatformArgument) != "true" ||
            !IsDevelopmentBuild() ||
            Argument("--mirror-smoke-count") != "4" ||
            !IsSupportedRole(Argument("--mirror-smoke-role")))
        {
            return;
        }

        if (NetworkManager.singleton is MirrorTestNetworkManager manager &&
            manager.GetComponent<MirrorPlatformSmoke_MirrorTest>() == null)
        {
            manager.gameObject.AddComponent<MirrorPlatformSmoke_MirrorTest>();
        }
    }

    private static bool IsDevelopmentBuild() => Debug.isDebugBuild || Application.isEditor;

    private static bool IsSupportedRole(string value) =>
        value == "server" || value == "host" || value == "client";

    private void Start()
    {
        manager = GetComponent<MirrorTestNetworkManager>();
        role = Argument("--mirror-smoke-role");
        startedAt = Time.realtimeSinceStartupAsDouble;
        duration = ResolveDuration();

        if (!IsDevelopmentBuild() || manager == null ||
            Argument(PlatformArgument) != "true" ||
            Argument("--mirror-smoke-count") != "4" ||
            !IsSupportedRole(role))
        {
            Fail("requires development build, --mirror-smoke-platform=true, role=server|host|client and count=4");
        }
    }

    private double ResolveDuration()
    {
        bool fullMode = Argument("--mirror-smoke-travel") == "full-run";
        double fallback = fullMode ? FullModeTimeout : DefaultTimeout;
        string configured = Argument("--mirror-smoke-duration");
        if (!double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) ||
            seconds <= 0d)
        {
            return fallback;
        }

        return Math.Min(seconds, fullMode ? FullModeTimeout : DefaultTimeout);
    }

    private void Update()
    {
        if (failed || Completed || manager == null)
            return;

        if (Time.realtimeSinceStartupAsDouble - startedAt >= duration)
        {
            Fail($"platform timeout {duration:F0}s");
            return;
        }

        if (NetworkServer.active)
        {
            if (!manager.ServerDevelopmentCommandsEnabled)
            {
                Fail("server development commands disabled");
                return;
            }

            StopEnemyPatterns();
            if (!serverRegistered)
            {
                NetworkServer.RegisterHandler<PlatformAckMessage_MirrorTest>(OnAck);
                serverRegistered = true;
            }
        }

        if (NetworkClient.active && !clientRegistered)
        {
            NetworkClient.RegisterHandler<PlatformStepMessage_MirrorTest>(OnStep);
            clientRegistered = true;
        }

        if (NetworkServer.active && !serverRoutineStarted &&
            manager.ServerRoster.RunStarted &&
            manager.IsSessionSelectionActive &&
            manager.ServerPlayerContexts.Count == 4 &&
            manager.TryGetRunSnapshot(out _))
        {
            serverRoutineStarted = true;
            StartCoroutine(Guard(RunServer()));
        }

        if (NetworkClient.active && !stage5SelectionRequested &&
            manager.ClientCompatibilityConfirmed &&
            manager.IsSessionSelectionActive &&
            manager.CanLocalClientVote &&
            manager.ClientLobby.Members != null &&
            manager.ClientLobby.Members.Length == 4 &&
            TryValidateClientRoster(out _) &&
            manager.TryGetRunSnapshot(out StageMapSaveData snapshot))
        {
            StageNodeSaveData firstNode = FindFirstNextNode(snapshot);
            if (firstNode != null && firstNode.type == StageNodeType.Battle &&
                firstNode.sceneName == Stage5SceneName &&
                manager.RequestStageNodeSelection(firstNode.id))
            {
                stage5SelectionRequested = true;
                Debug.Log($"[MirrorPlatformSmoke] client selected shared Stage5 node={firstNode.id}");
            }
        }
    }

    private IEnumerator RunServer()
    {
        RequireServerDevelopment("run start");
        yield return WaitFor(() => manager.ServerRoster.RunStarted &&
                                  manager.IsSessionSelectionActive &&
                                  manager.ServerPlayerContexts.Count == 4,
            "server lobby run and four contexts", SceneEntryWait);

        CaptureServerParticipants();
        RequireServerParty("initial four-player roster");
        Require(manager.TryGetRunSnapshot(out StageMapSaveData snapshot), "initial Run Snapshot");

        StageNodeSaveData firstNode = FindFirstNextNode(snapshot);
        Require(firstNode != null, "next-floor first node");
        firstNode.type = StageNodeType.Battle;
        firstNode.sceneName = Stage5SceneName;
        Require(manager.ServerPublishRunSnapshot(snapshot), "publish Stage5 Run Snapshot");
        Debug.Log($"[MirrorPlatformSmoke] server published Stage5 node={firstNode.id} type={firstNode.type} scene={firstNode.sceneName}; route sceneName preserved after publish");

        yield return WaitFor(() => manager.TryGetPendingStageNode(out StageNodeSaveData pending) &&
                                  pending.id == firstNode.id,
            "four owner votes and pending Stage5 node", SceneEntryWait);
        Require(manager.TryGetPendingStageNode(out StageNodeSaveData selected) &&
                selected.type == StageNodeType.Battle &&
                selected.sceneName == Stage5SceneName,
            "server-selected Stage5 node route");

        yield return WaitFor(() => SceneManager.GetActiveScene().path == MirrorTestNetworkManager.SessionStage5Scene,
            "Stage5 map entry", SceneEntryWait);
        yield return WaitFor(ServerPartyReadyForStage, "Stage5 server four-player readiness");

        ResolveElevator();
        yield return WaitFor(() => boardingBox != null && boardingBox.enabled,
            "Stage5 elevator boarding trigger enabled");
        StopEnemyPatterns();
        Debug.Log("[MirrorPlatformSmoke] Stage5 elevator fixture active; enemy patterns are stopped, combat difficulty validation remains separate.");

        SendPhase(StageReadyPhase, detail: "Stage5 scene-start readiness");
        yield return WaitForAcks("all four Stage5 scene-start observations");

        SelectBoardingActors();
        Require(firstBoarders.Count == 3 && fourthBoarder != null && deathActor != null,
            "three-first boarding actors and death actor");
        platformStartHeight = PlatformHeight();

        // Establish the fourth actor outside before any of the first three move.  A
        // fresh scene can spawn all four inside the trigger; moving the fourth first
        // prevents the elevator's own polling loop from starting prematurely.
        Vector3 outsideDestination = FindOutsidePosition(fourthBoarder);
        SendPhase(MovePhase, fourthBoarder.GetComponent<NetworkIdentity>().netId,
            outsideDestination, true, "fourth owner move outside BoardingTrigger");
        yield return WaitForAcks("fourth owner outside move command");
        yield return WaitFor(() => !IsBoarded(fourthBoarder),
            "server observes fourth player outside trigger");

        foreach (PlayerContext actor in firstBoarders)
        {
            Vector3 boardingDestination = FindBoardingPosition(actor);
            SendPhase(MovePhase, actor.GetComponent<NetworkIdentity>().netId,
                boardingDestination, false, "owner move to BoardingTrigger center");
            yield return WaitForAcks($"owner move command actor={ActorLabel(actor)}");
            yield return WaitFor(() => IsBoarded(actor),
                $"server observes first-boarder actor={ActorLabel(actor)}");
        }

        SendPhase(HoldThreePhase, fourthBoarder.GetComponent<NetworkIdentity>().netId,
            detail: "three boarded, fourth outside");
        yield return WaitForAcks("all clients observe three boarded state");
        Require(boardingBox.enabled && CountBoardedParticipants() == 3 &&
                !IsBoarded(fourthBoarder) && HeightDeltaFromStart() < 0.05f,
            "three boarded hold starts with trigger enabled and platform stationary");
        yield return new WaitForSecondsRealtime(1f);
        Require(boardingBox.enabled && CountBoardedParticipants() == 3 &&
                !IsBoarded(fourthBoarder) && HeightDeltaFromStart() < 0.05f,
            "three boarded hold remains stationary for one second");

        Vector3 fourthBoardingDestination = FindBoardingPosition(fourthBoarder);
        SendPhase(MovePhase, fourthBoarder.GetComponent<NetworkIdentity>().netId,
            fourthBoardingDestination, false, "fourth owner move to BoardingTrigger center");
        yield return WaitForAcks("fourth owner boarding move command");
        yield return WaitFor(() => CountBoardedParticipants() == 4,
            "server observes all four boarded");
        yield return WaitFor(() => !boardingBox.enabled,
            "elevator trigger disabled after fourth boarding");
        // Binder의 주변 NavMesh 탐색 반경(4m) 밖인 승강 중간 높이에서 사망시킨다.
        yield return WaitFor(() => HeightDeltaFromStart() >= 4.5f,
            "platform reaches mid-ride height above four metres");

        SendPhase(RisenPhase, deathActor.GetComponent<NetworkIdentity>().netId,
            detail: "platform moving with all four boarded");
        yield return WaitForAcks("all clients observe rising platform");

        RequireServerDevelopment("actual Stage5 moving-death damage");
        Require(deathActor.Health != null && deathActor.RuntimeState != null &&
                deathActor.Health.MaxHealth > 0f,
            "selected death actor health runtime");
        float deathMaxHealth = deathActor.Health.MaxHealth;
        LogServerActor("before moving-death damage", deathActor);
        deathActor.Health.TakeDamage(deathMaxHealth * 2f);
        yield return null;
        Require(deathActor.Health.CurrentHealth <= 0f && deathActor.RuntimeState.IsDead,
            "server actual death after platform rise");
        LogServerActor("after moving-death damage", deathActor);

        SendPhase(DeathPhase, deathActor.GetComponent<NetworkIdentity>().netId,
            detail: "server snapshot death observed");
        yield return WaitForAcks("all clients observe death snapshot");

        yield return WaitFor(() => boardingBox.enabled &&
                                  HeightDeltaFromStart() < 0.25f,
            "elevator roundtrip trigger restore");
        RequireServerDevelopment("server development revive after roundtrip");
        Require(deathActor.RuntimeState.ServerReviveForTest(),
            "ServerReviveForTest after elevator roundtrip");
        yield return WaitFor(() => !deathActor.RuntimeState.IsDead &&
                                  deathActor.Health.CurrentHealth >= deathActor.Health.MaxHealth,
            "server revived death actor");
        LogServerActor("after server revive", deathActor);

        SendPhase(RevivePhase, deathActor.GetComponent<NetworkIdentity>().netId,
            detail: "revive and owner NavMesh move probe");
        yield return WaitForAcks("all owners observe landing recovery and revived move");

        SendPhase(CompletePhase, detail: "bounded Stage5 platform scenario complete");
        yield return WaitForAcks("all four final platform smoke acknowledgements");
        Complete(true, "PASS Stage5 three-first boarding, moving death, roundtrip revive and owner movement");
    }

    private void CaptureServerParticipants()
    {
        participants.Clear();
        participantNetIds.Clear();
        serverActors.Clear();

        foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values
                     .Where(value => value != null && value.isReady && value.identity != null)
                     .OrderBy(value => value.connectionId))
        {
            participants.Add(connection.connectionId);
            participantNetIds.Add(connection.identity.netId);
        }

        serverActors.AddRange(manager.ServerPlayerContexts
            .Where(context => context != null && context.TryGetComponent(out NetworkIdentity _))
            .OrderBy(context => context.GetComponent<NetworkIdentity>().netId));
    }

    private void RequireServerParty(string detail)
    {
        RequireServerDevelopment(detail);
        Require(participants.Count == 4, detail + ": four original ready connections");
        Require(serverActors.Count == 4 && manager.ServerPlayerContexts.Count == 4,
            detail + ": four server PlayerContexts");
        Require(serverActors.All(context =>
                    participantNetIds.Contains(context.GetComponent<NetworkIdentity>().netId)),
            detail + ": server contexts match original participant identities");
        foreach (int connectionId in participants)
        {
            Require(NetworkServer.connections.TryGetValue(connectionId,
                        out NetworkConnectionToClient connection) &&
                    connection != null && connection.isReady && connection.identity != null,
                detail + $": connection={connectionId} ready identity");
            MirrorSessionRoster_MirrorTest.Member member =
                manager.ServerRoster.FindByConnection(connectionId);
            Require(member != null && member.ConnectionId >= 0 && !member.HasForfeited &&
                    member.RuntimeContext != null &&
                    member.RuntimeContext.GetComponent<NetworkIdentity>().netId == connection.identity.netId,
                detail + $": connection={connectionId} initial roster membership");
        }
    }

    private bool ServerPartyReadyForStage()
    {
        if (!NetworkServer.active || !manager.ServerDevelopmentCommandsEnabled ||
            manager.ServerRoster.Members.Count != 4 ||
            manager.ServerPlayerContexts.Count != 4 ||
            participants.Count != 4)
        {
            return false;
        }

        foreach (int connectionId in participants)
        {
            if (!NetworkServer.connections.TryGetValue(connectionId,
                    out NetworkConnectionToClient connection) ||
                connection == null || !connection.isReady || connection.identity == null)
            {
                return false;
            }

            MirrorSessionRoster_MirrorTest.Member member =
                manager.ServerRoster.FindByConnection(connectionId);
            PlayerContext context = member?.RuntimeContext;
            if (member == null || member.ConnectionId < 0 || member.HasForfeited ||
                context == null || context.RuntimeState == null || !context.RuntimeState.HasSnapshot ||
                !participantNetIds.Contains(connection.identity.netId))
            {
                return false;
            }
        }

        return SceneManager.GetActiveScene().path == MirrorTestNetworkManager.SessionStage5Scene;
    }

    private void ResolveElevator()
    {
        elevator = FindFirstObjectByType<MirrorFourPlayerElevator_MirrorTest>();
        Require(elevator != null && elevator.BoardingTrigger is BoxCollider,
            "Stage5 actual MirrorFourPlayerElevator and BoxCollider trigger");
        boardingBox = (BoxCollider)elevator.BoardingTrigger;
        platformBody = elevator.GetComponent<Rigidbody>();
        Require(platformBody != null, "Stage5 elevator platform Rigidbody");
    }

    private void SelectBoardingActors()
    {
        firstBoarders.Clear();
        fourthBoarder = null;
        deathActor = null;

        List<PlayerContext> ordered = serverActors
            .Where(context => context != null && context.RuntimeState != null &&
                              !context.RuntimeState.IsDead)
            .OrderBy(context => context.GetComponent<NetworkIdentity>().netId)
            .ToList();
        Require(ordered.Count == 4, "four alive Stage5 boarding actors");

        deathActor = ordered.FirstOrDefault(IsPreferredRemoteGunner) ??
                     ordered.FirstOrDefault(IsRemoteActor) ??
                     ordered.FirstOrDefault(context =>
                         context.Equipment != null &&
                         context.Equipment.CurrentCharacterClass == CharacterClass.Gunner) ??
                     ordered[ordered.Count - 1];
        fourthBoarder = deathActor;
        firstBoarders.AddRange(ordered.Where(context => context != fourthBoarder).Take(3));
        Require(firstBoarders.Count == 3, "three actors before selected death actor");
        Debug.Log($"[MirrorPlatformSmoke] boarding order first={string.Join(",", firstBoarders.Select(ActorLabel))} fourth/death={ActorLabel(deathActor)}");
    }

    private bool IsPreferredRemoteGunner(PlayerContext context)
    {
        NetworkIdentity identity = context.GetComponent<NetworkIdentity>();
        return context.Equipment != null &&
               context.Equipment.CurrentCharacterClass == CharacterClass.Gunner &&
               IsRemoteActor(context) && identity != null;
    }

    private bool IsRemoteActor(PlayerContext context)
    {
        NetworkIdentity identity = context.GetComponent<NetworkIdentity>();
        return identity != null && identity.connectionToClient != null &&
               identity.connectionToClient != NetworkServer.localConnection;
    }

    private void StopEnemyPatterns()
    {
        if (!NetworkServer.active || manager == null || !manager.ServerDevelopmentCommandsEnabled)
            return;

        foreach (WBH_EnemyPattern_MirrorTest pattern in
                 FindObjectsByType<WBH_EnemyPattern_MirrorTest>(FindObjectsSortMode.None))
        {
            if (pattern != null)
                pattern.StopServer();
        }
    }

    private Vector3 FindBoardingPosition(PlayerContext actor)
    {
        NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
        Require(agent != null && agent.enabled && agent.isOnNavMesh,
            "server boarding actor NavMesh before MoveCommand " + ActorLabel(actor));

        Vector3 center = boardingBox.bounds.center;
        Require(NavMesh.SamplePosition(center, out NavMeshHit hit, 3f, agent.areaMask),
            "BoardingTrigger bounds center NavMesh " + ActorLabel(actor));
        NavMeshPath path = new();
        Require(agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete,
            "BoardingTrigger path " + ActorLabel(actor));
        return hit.position;
    }

    private Vector3 FindOutsidePosition(PlayerContext actor)
    {
        NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
        Require(agent != null && agent.enabled && agent.isOnNavMesh,
            "server fourth actor NavMesh before outside MoveCommand");

        Bounds bounds = boardingBox.bounds;
        Vector3 center = bounds.center;
        Vector3[] directions =
        {
            boardingBox.transform.right,
            -boardingBox.transform.right,
            boardingBox.transform.forward,
            -boardingBox.transform.forward,
        };
        float edgeDistance = Mathf.Max(bounds.extents.x, bounds.extents.z) + 3f;
        foreach (Vector3 direction in directions)
        {
            Vector3 candidate = center + direction.normalized * edgeDistance;
            candidate.y = actor.transform.position.y;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, agent.areaMask) ||
                bounds.Contains(hit.position))
            {
                continue;
            }

            NavMeshPath path = new();
            if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                return hit.position;
        }

        throw new InvalidOperationException("reachable NavMesh point outside BoardingTrigger edge + 3m");
    }

    private bool IsBoarded(PlayerContext actor)
    {
        if (actor == null || boardingBox == null)
            return false;

        return FindBoardedActors().Contains(actor);
    }

    private int CountBoardedParticipants() => FindBoardedActors().Count;

    private HashSet<PlayerContext> FindBoardedActors()
    {
        var result = new HashSet<PlayerContext>();
        if (boardingBox == null)
            return result;

        Vector3 scale = boardingBox.transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Collider[] overlaps = Physics.OverlapBox(
            boardingBox.transform.TransformPoint(boardingBox.center),
            Vector3.Scale(boardingBox.size, scale) * 0.5f,
            boardingBox.transform.rotation,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);
        foreach (Collider overlap in overlaps)
        {
            Rigidbody rigidbody = overlap.attachedRigidbody;
            if (rigidbody == null || !rigidbody.TryGetComponent(out NetworkIdentity identity) ||
                !participantNetIds.Contains(identity.netId))
            {
                continue;
            }

            PlayerContext actor = identity.GetComponent<PlayerContext>();
            if (actor != null && actor.RuntimeState != null && !actor.RuntimeState.IsDead)
                result.Add(actor);
        }

        return result;
    }

    private float PlatformHeight() => platformBody != null
        ? platformBody.position.y
        : elevator != null ? elevator.transform.position.y : 0f;

    private float HeightDeltaFromStart() => Mathf.Abs(PlatformHeight() - platformStartHeight);

    private void SendPhase(byte nextPhase, uint actor = 0, Vector3 destination = default,
        bool outside = false, string detail = null)
    {
        RequireServerDevelopment("send platform phase");
        Require(participants.Count == 4, "send phase original participant list");
        step++;
        phase = nextPhase;
        acknowledgements.Clear();
        currentStep = new PlatformStepMessage_MirrorTest
        {
            Step = step,
            Phase = nextPhase,
            Actor = actor,
            Destination = destination,
            Height = platformStartHeight,
            TriggerEnabled = boardingBox != null && boardingBox.enabled,
            Outside = outside,
            Detail = detail,
        };

        foreach (int connectionId in participants)
        {
            Require(NetworkServer.connections.TryGetValue(connectionId,
                        out NetworkConnectionToClient connection) &&
                    connection != null && connection.isReady && connection.identity != null,
                "send phase connection=" + connectionId + " remains original ready participant");
            connection.Send(currentStep);
        }
    }

    private void OnAck(NetworkConnectionToClient connection, PlatformAckMessage_MirrorTest message)
    {
        if (failed || Completed || !NetworkServer.active || NetworkManager.singleton != manager ||
            !manager.ServerDevelopmentCommandsEnabled || connection == null ||
            !participants.Contains(connection.connectionId) ||
            !NetworkServer.connections.TryGetValue(connection.connectionId,
                out NetworkConnectionToClient original) || original != connection ||
            connection.identity == null || message.Step != step || message.Phase != phase ||
            !participantNetIds.Contains(message.Actor) ||
            message.Actor != connection.identity.netId)
        {
            return;
        }

        if (!message.Passed)
        {
            Fail($"client connection={connection.connectionId} actor={message.Actor}: {message.Detail}");
            return;
        }

        acknowledgements.Add(connection.connectionId);
    }

    private void OnStep(PlatformStepMessage_MirrorTest message)
    {
        if (failed || Completed || NetworkManager.singleton != manager ||
            message.Step <= lastClientStep)
        {
            return;
        }

        lastClientStep = message.Step;
        if (message.Phase == FailurePhase)
        {
            RestoreInputs();
            Completed = true;
            Passed = false;
            Debug.LogError("[MirrorPlatformSmoke] FAIL server: " + message.Detail);
            return;
        }

        if (message.Phase == CompletePhase)
        {
            RestoreInputs();
            // Host의 서버 코루틴은 네 명의 마지막 응답까지 확인해야 한다.
            if (!NetworkServer.active) { Completed = true; Passed = true; }
            NetworkClient.Send(new PlatformAckMessage_MirrorTest
            {
                Step = message.Step,
                Phase = message.Phase,
                Actor = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0,
                Passed = true,
                Detail = "client final platform smoke pass",
            });
            Debug.Log($"[MirrorPlatformSmoke] PASS client role={role} final Stage5 platform/revive observation");
            return;
        }

        StartCoroutine(Guard(RunClient(message), message));
    }

    private IEnumerator RunClient(PlatformStepMessage_MirrorTest message)
    {
        yield return WaitFor(() => NetworkClient.isConnected &&
                                  NetworkClient.ready &&
                                  manager.ClientCompatibilityConfirmed &&
                                  manager.ClientLobby.RunStarted &&
                                  manager.LocalPlayerContext?.RuntimeState?.HasSnapshot == true,
            "client runtime snapshot before platform phase");

        yield return WaitFor(() => TryValidateClientRoster(out _),
            "client initial four-player roster");
        clientRosterValidated = true;
        PlayerContext local = manager.LocalPlayerContext;
        CaptureInputs(local);

        if (message.Phase == StageReadyPhase)
        {
            MirrorSpawnedPlayerBinder binder = local.GetComponent<MirrorSpawnedPlayerBinder>();
            NavMeshAgent agent = local.GetComponent<NavMeshAgent>();
            yield return WaitFor(() => SceneManager.GetActiveScene().path == MirrorTestNetworkManager.SessionStage5Scene &&
                                      binder != null && binder.IsSceneStartConfirmed &&
                                      local.Controller != null && local.Controller.enabled &&
                                      local.Controller.IsControlEnabled && agent != null && agent.enabled &&
                                      agent.isOnNavMesh,
                "client Stage5 scene-start confirmation and NavMesh");
            Require(binder != null && binder.IsSceneStartConfirmed &&
                    local.Controller != null && local.Controller.enabled &&
                    local.Controller.IsControlEnabled && agent != null && agent.enabled &&
                    agent.isOnNavMesh,
                ClientNavigationState(local, "Stage5 scene-start"));
        }
        else if (message.Phase == MovePhase)
        {
            yield return RunClientMove(local, message);
        }
        else if (message.Phase == HoldThreePhase)
        {
            yield return WaitFor(() => FindFirstObjectByType<MirrorFourPlayerElevator_MirrorTest>() != null &&
                                      Mathf.Abs(PlatformHeightOnClient() - message.Height) < 0.25f,
                "client observes stationary three-boarder platform");
        }
        else if (message.Phase == RisenPhase)
        {
            yield return WaitFor(() => PlatformHeightOnClient() >= message.Height + 4.5f,
                "client observes platform rise");
        }
        else if (message.Phase == DeathPhase)
        {
            PlayerContext observedDeathActor = null;
            yield return WaitFor(() => (observedDeathActor = FindClientActor(message.Actor)) != null &&
                                      observedDeathActor.RuntimeState != null &&
                                      observedDeathActor.RuntimeState.HasSnapshot &&
                                      observedDeathActor.RuntimeState.IsDead &&
                                      observedDeathActor.RuntimeState.CurrentHealth <= 0f,
                "client death snapshot actor=" + message.Actor);
            if (local.GetComponent<NetworkIdentity>().netId == message.Actor)
            {
                Require(!local.Controller.enabled && !local.Controller.IsControlEnabled &&
                        local.StateMachine.Is(PlayerState.Dead),
                    ClientNavigationState(local, "moving-death client state"));
            }
        }
        else if (message.Phase == RevivePhase)
        {
            PlayerContext observedActor = FindClientActor(message.Actor);
            yield return WaitFor(() => (observedActor = FindClientActor(message.Actor)) != null &&
                                      observedActor.RuntimeState != null &&
                                      observedActor.RuntimeState.HasSnapshot &&
                                      !observedActor.RuntimeState.IsDead,
                "client revive snapshot actor=" + message.Actor);
            if (local.GetComponent<NetworkIdentity>().netId == message.Actor)
            {
                yield return WaitFor(() => IsUsableOwnerNavigation(local),
                    "revived owner NavMesh/controller recovery");
                Vector3 origin = local.transform.position;
                Vector3 destination = FindRecoveryDestination(local);
                local.Controller.MoveCommand(destination);
                yield return WaitFor(() => Vector3.Distance(origin, local.transform.position) > 0.15f,
                    "revived owner actual MoveCommand displacement");
                Require(IsUsableOwnerNavigation(local) &&
                        Vector3.Distance(origin, local.transform.position) > 0.15f,
                    ClientNavigationState(local, "revived owner after MoveCommand"));
                Debug.Log($"[MirrorPlatformSmoke] PASS revived owner actor={message.Actor} origin={origin} position={local.transform.position} displacement={Vector3.Distance(origin, local.transform.position):F3}");
            }
            else
            {
                yield return WaitFor(() => IsUsableOwnerNavigation(local) &&
                                          local.transform.position.y > message.Height + 0.5f,
                    "other owner upper-floor landing recovery");
                Require(IsUsableOwnerNavigation(local) &&
                        local.transform.position.y > message.Height + 0.5f,
                    ClientNavigationState(local, "other owner upper-floor landing"));
                Debug.Log($"[MirrorPlatformSmoke] PASS upper landing owner actor={local.GetComponent<NetworkIdentity>().netId} position={local.transform.position}");
            }
        }

        NetworkClient.Send(new PlatformAckMessage_MirrorTest
        {
            Step = message.Step,
            Phase = message.Phase,
            Actor = local.GetComponent<NetworkIdentity>().netId,
            Passed = true,
            Detail = clientRosterValidated ? "client platform phase observed" : "client roster not validated",
        });
    }

    private IEnumerator RunClientMove(PlayerContext local, PlatformStepMessage_MirrorTest message)
    {
        if (local.GetComponent<NetworkIdentity>().netId == message.Actor)
        {
            NavMeshAgent agent = local.GetComponent<NavMeshAgent>();
            Require(local.Controller != null && local.Controller.enabled &&
                    local.Controller.IsControlEnabled && agent != null && agent.enabled &&
                    agent.isOnNavMesh,
                ClientNavigationState(local, message.Outside ? "outside move before command" : "boarding move before command"));
            Vector3 origin = local.transform.position;
            local.Controller.MoveCommand(message.Destination);
            yield return WaitFor(() => Vector3.Distance(origin, local.transform.position) > 0.15f ||
                                      Vector3.Distance(local.transform.position, message.Destination) < 1.5f,
                message.Outside ? "owner actual outside MoveCommand" : "owner actual boarding MoveCommand");
            Require(Vector3.Distance(origin, local.transform.position) > 0.15f ||
                    Vector3.Distance(local.transform.position, message.Destination) < 1.5f,
                ClientNavigationState(local, message.Outside ? "outside move after command" : "boarding move after command"));
        }
        else
        {
            PlayerContext observed = null;
            yield return WaitFor(() => (observed = FindClientActor(message.Actor)) != null &&
                                      Vector3.Distance(observed.transform.position, message.Destination) < 1.75f,
                message.Outside ? "remote replica outside move" : "remote replica boarding move");
        }
    }

    private bool TryValidateClientRoster(out string reason)
    {
        reason = null;
        MirrorLobbyMember_MirrorTest[] members = manager.ClientLobby.Members;
        if (members == null || members.Length != 4 ||
            members.Count(member => member.IsConnected && !member.HasForfeited) != 4)
        {
            reason = "client lobby roster is not four connected non-forfeited members";
            return false;
        }

        if (string.IsNullOrEmpty(manager.LocalParticipantId) ||
            members.Count(member => member.ParticipantId == manager.LocalParticipantId) != 1 ||
            NetworkClient.localPlayer == null || manager.LocalPlayerContext == null ||
            !NetworkClient.localPlayer.isLocalPlayer ||
            manager.LocalPlayerContext.GetComponent<NetworkIdentity>() != NetworkClient.localPlayer)
        {
            reason = "client local owner/participant identity is not resolved";
            return false;
        }

        return true;
    }

    private PlayerContext FindClientActor(uint netId)
    {
        return NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity)
            ? identity.GetComponent<PlayerContext>()
            : null;
    }

    private bool IsUsableOwnerNavigation(PlayerContext actor)
    {
        NavMeshAgent agent = actor?.GetComponent<NavMeshAgent>();
        return actor != null && actor.Controller != null && actor.Controller.enabled &&
               actor.Controller.IsControlEnabled && agent != null && agent.enabled &&
               agent.isOnNavMesh && actor.GetComponent<MirrorSpawnedPlayerBinder>()?.IsSceneStartConfirmed == true;
    }

    private Vector3 FindRecoveryDestination(PlayerContext actor)
    {
        NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
        NavMeshPath path = new();
        Vector3[] directions =
        {
            actor.transform.right,
            -actor.transform.right,
            actor.transform.forward,
            -actor.transform.forward,
            Quaternion.Euler(0f, 45f, 0f) * actor.transform.forward,
            Quaternion.Euler(0f, -45f, 0f) * actor.transform.forward,
        };
        foreach (Vector3 direction in directions)
        {
            Vector3 desired = actor.transform.position + direction.normalized * 0.75f;
            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 0.3f, agent.areaMask) ||
                Vector3.Distance(actor.transform.position, hit.position) < 0.5f ||
                Vector3.Distance(actor.transform.position, hit.position) > 1.1f ||
                !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            return hit.position;
        }

        throw new InvalidOperationException("reachable 0.5-1m revived owner NavMesh destination");
    }

    private float PlatformHeightOnClient()
    {
        MirrorFourPlayerElevator_MirrorTest current =
            FindFirstObjectByType<MirrorFourPlayerElevator_MirrorTest>();
        Rigidbody body = current != null ? current.GetComponent<Rigidbody>() : null;
        return body != null ? body.position.y : current != null ? current.transform.position.y : float.NaN;
    }

    private string ClientNavigationState(PlayerContext actor, string moment)
    {
        NavMeshAgent agent = actor?.GetComponent<NavMeshAgent>();
        MirrorSpawnedPlayerBinder binder = actor?.GetComponent<MirrorSpawnedPlayerBinder>();
        return $"actor={actor?.GetComponent<NetworkIdentity>()?.netId} moment={moment} position={actor?.transform.position} " +
               $"agentEnabled={agent != null && agent.enabled} onNavMesh={agent != null && agent.enabled && agent.isOnNavMesh} " +
               $"controllerEnabled={actor?.Controller != null && actor.Controller.enabled} " +
               $"control={actor?.Controller != null && actor.Controller.IsControlEnabled} " +
               $"sceneStartConfirmed={binder?.IsSceneStartConfirmed} state={actor?.StateMachine?.CurrentState}";
    }

    private void SendFailure(string reason)
    {
        if (!NetworkServer.active)
            return;

        PlatformStepMessage_MirrorTest message = new()
        {
            Step = step + 1,
            Phase = FailurePhase,
            Detail = reason,
        };
        foreach (int connectionId in participants)
        {
            if (NetworkServer.connections.TryGetValue(connectionId,
                    out NetworkConnectionToClient connection) &&
                connection != null)
            {
                connection.Send(message);
            }
        }
    }

    private void Complete(bool passed, string detail)
    {
        Completed = true;
        Passed = passed;
        if (passed)
            Debug.Log("[MirrorPlatformSmoke] " + detail);
        else
            Debug.LogError("[MirrorPlatformSmoke] FAIL " + detail);
        RestoreInputs();
    }

    private void Fail(string reason)
    {
        if (failed || Completed)
            return;

        failed = true;
        Completed = true;
        Passed = false;
        RestoreInputs();
        if (NetworkServer.active)
        {
            StopEnemyPatterns();
            SendFailure(reason);
        }

        Debug.LogError("[MirrorPlatformSmoke] FAIL " + reason);
        if (NetworkServer.active)
        {
            foreach (PlayerContext actor in serverActors)
                LogFailureActor("failure state", actor);
        }
        else if (manager?.LocalPlayerContext != null)
        {
            Debug.LogError("[MirrorPlatformSmoke] FAIL local " +
                           ClientNavigationState(manager.LocalPlayerContext, "failure state"));
        }
    }

    private void RequireServerDevelopment(string operation)
    {
        Require(NetworkServer.active && manager != null && manager.ServerDevelopmentCommandsEnabled,
            "server development commands required for " + operation);
    }

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }

    private void LogServerActor(string moment, PlayerContext actor)
    {
        if (actor == null)
            return;

        NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
        NetworkIdentity identity = actor.GetComponent<NetworkIdentity>();
        Debug.Log($"[MirrorPlatformSmoke] actor={identity?.netId} moment={moment} position={actor.transform.position} " +
                       $"health={actor.Health?.CurrentHealth}/{actor.Health?.MaxHealth} dead={actor.RuntimeState?.IsDead} " +
                       $"agentEnabled={agent != null && agent.enabled} onNavMesh={agent != null && agent.enabled && agent.isOnNavMesh} " +
                       $"controllerEnabled={actor.Controller != null && actor.Controller.enabled} " +
                       $"control={actor.Controller != null && actor.Controller.IsControlEnabled} state={actor.StateMachine?.CurrentState}");
    }

    private void LogFailureActor(string moment, PlayerContext actor)
    {
        if (actor == null)
            return;

        NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
        NetworkIdentity identity = actor.GetComponent<NetworkIdentity>();
        Debug.LogError($"[MirrorPlatformSmoke] actor={identity?.netId} moment={moment} position={actor.transform.position} " +
                       $"health={actor.Health?.CurrentHealth}/{actor.Health?.MaxHealth} dead={actor.RuntimeState?.IsDead} " +
                       $"agentEnabled={agent != null && agent.enabled} onNavMesh={agent != null && agent.enabled && agent.isOnNavMesh} " +
                       $"controllerEnabled={actor.Controller != null && actor.Controller.enabled} " +
                       $"control={actor.Controller != null && actor.Controller.IsControlEnabled} state={actor.StateMachine?.CurrentState}");
    }

    private IEnumerator WaitForAcks(string label) =>
        WaitFor(() => acknowledgements.Count == participants.Count, label);

    private IEnumerator WaitFor(Func<bool> predicate, string label, double timeout = DefaultWait)
    {
        double deadline = Math.Min(Time.realtimeSinceStartupAsDouble + timeout, startedAt + duration);
        while (!predicate())
        {
            Require(!failed && Time.realtimeSinceStartupAsDouble < deadline,
                label + " timeout");
            yield return null;
        }
    }

    private IEnumerator Guard(IEnumerator routine,
        PlatformStepMessage_MirrorTest? clientMessage = null)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        while (stack.Count > 0 && !failed && !Completed)
        {
            object next = null;
            Exception error = null;
            try
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                next = current.Current;
            }
            catch (Exception exception)
            {
                error = exception;
            }

            if (error != null)
            {
                if (clientMessage.HasValue && NetworkClient.active)
                {
                    PlatformStepMessage_MirrorTest message = clientMessage.Value;
                    NetworkClient.Send(new PlatformAckMessage_MirrorTest
                    {
                        Step = message.Step,
                        Phase = message.Phase,
                        Actor = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0,
                        Passed = false,
                        Detail = error.Message,
                    });
                }

                Fail(error.Message);
                yield break;
            }

            if (next is IEnumerator nested)
                stack.Push(nested);
            else
                yield return next;
        }
    }

    private void CaptureInputs(PlayerContext local)
    {
        if (inputsCaptured || local == null)
            return;

        movementInput = local.GetComponent<WBH_PlayerInputHandler_MirrorTest>();
        actionInput = local.GetComponent<PlayerActionInputHandler_MirrorTest>();
        movementInputWasEnabled = movementInput != null && movementInput.enabled;
        actionInputWasEnabled = actionInput != null && actionInput.enabled;
        if (movementInput != null)
            movementInput.enabled = false;
        if (actionInput != null)
            actionInput.enabled = false;
        inputsCaptured = true;
    }

    private void RestoreInputs()
    {
        if (!inputsCaptured)
            return;

        if (movementInput != null)
            movementInput.enabled = movementInputWasEnabled;
        if (actionInput != null)
            actionInput.enabled = actionInputWasEnabled;
        inputsCaptured = false;
    }

    private static StageNodeSaveData FindFirstNextNode(StageMapSaveData snapshot)
    {
        if (snapshot?.nodes == null)
            return null;

        return snapshot.nodes
            .Where(node => node != null && node.floor == snapshot.clearedFloor + 1)
            .OrderBy(node => node.nodeIndex)
            .ThenBy(node => node.id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private string ActorLabel(PlayerContext actor)
    {
        NetworkIdentity identity = actor?.GetComponent<NetworkIdentity>();
        return identity != null ? identity.netId.ToString() : "missing";
    }

    private void OnDestroy()
    {
        RestoreInputs();
        if (serverRegistered)
            NetworkServer.UnregisterHandler<PlatformAckMessage_MirrorTest>();
        if (clientRegistered)
            NetworkClient.UnregisterHandler<PlatformStepMessage_MirrorTest>();
    }
}

using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Mirror가 생성한 플레이어의 로컬·서버 등록과 Scene 간 수명주기를 연결한다.
/// <para>세션 이동과 재접속 예약 동안 런타임을 보존하고, 서버 스냅샷과 생존·참가 상태를 확인한 뒤 입력을 복구한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerContext))]
public sealed class MirrorSpawnedPlayerBinder : NetworkBehaviour
{
    private const int SceneRestoreFrameLimit = 120;
    private const float NavMeshSampleDistance = 4f;

    [SerializeField] private PlayerContext context;
    [Tooltip("로컬 플레이어에게만 켤 입력 컴포넌트")]
    [SerializeField] private Behaviour[] localOnlyBehaviours;
    [SerializeField] private PlayerNameplate nameplatePrefab;
    [SyncVar] private string participantDisplayName;
    [SyncVar] private int participantSlot;
    private PlayerNameplate nameplate;

    private Coroutine localSceneRestoreRoutine;
    private bool gameplayInputEnabled;
    private bool textInputBlocked;
    private bool menuInputBlocked;
    private bool cutsceneInputBlocked;
    private Coroutine textInputReleaseRoutine;
    private int textInputReleaseFrame = -1;
    private bool hasServerSceneStart;
    private string serverSceneStartPath;
    private Vector3 serverSceneStartPosition;
    private Quaternion serverSceneStartRotation;
    private int confirmedSceneHandle = -1;
    [SyncVar(hook = nameof(OnTemporarilyAbsentChanged))] private bool temporarilyAbsent;
    private Renderer[] absentRenderers;
    private bool[] rendererStates;
    private Collider[] absentColliders;
    private bool[] colliderStates;
    private bool absenceApplied;
    private bool controllerWasEnabled;
    private bool controllerHadControl;
    private NavMeshAgent absentAgent;

    public PlayerContext Context => context;
    /// <summary>미러 플레이어의 필수 상태와 같은 루트의 네트워크 구성만 허용한다.</summary>
    public bool IsConfigured => context != null && context.gameObject == gameObject && context.IsComplete &&
        context.Potions != null && context.Potions.gameObject == gameObject &&
        context.ItemTriggers != null && context.ItemTriggers.gameObject == gameObject &&
        context.RuntimeState != null && context.RuntimeState.gameObject == gameObject &&
        context.CombatAuthority != null && context.CombatAuthority.gameObject == gameObject &&
        GetComponent<PlayerInventorySync>() != null &&
        GetComponent<NetworkShopPlayerState>() != null &&
        GetComponent<PlayerNetworkTransform>() != null &&
        GetComponent<WBH_PlayerInputHandler>() != null && GetComponent<PlayerActionInputHandler>() != null &&
        GetComponent<WBH_PlayerAnimation>() != null && GetComponent<NetworkPlayerAnimation>() != null;
    public string ParticipantDisplayName => participantDisplayName;
    public int ParticipantSlot => participantSlot;
    /// <summary>재접속 예약으로 시각·충돌·조작이 정지된 참가자인지 반환한다.</summary>
    public bool IsTemporarilyAbsent => temporarilyAbsent;
    /// <summary>같은 이름의 Scene 재방문도 구분하여 소유자의 시작 위치 최종 확정을 확인한다.</summary>
    public bool IsSceneStartConfirmed => confirmedSceneHandle == SceneManager.GetActiveScene().handle;
    private bool CanRestoreGameplay => IsConfigured && !temporarilyAbsent && context?.RuntimeState?.HasSnapshot == true &&
        !context.RuntimeState.IsDead && !context.RuntimeState.IsReviving && (!isLocalPlayer || !RequiresNavMesh || IsSceneStartConfirmed);
    private bool RequiresNavMesh => GetTestNetworkManager()?.CurrentSessionRoute is
        MirrorSessionRoute.Combat or MirrorSessionRoute.Camp;

    private void Awake()
    {
        context ??= GetComponent<PlayerContext>();
        ResolveLocalOnlyBehaviours();
        SetLocalOnlyBehaviours(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        context ??= GetComponent<PlayerContext>();
        ResolveLocalOnlyBehaviours();
    }
#endif

    private void Reset()
    {
        context = GetComponent<PlayerContext>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        if (!IsConfigured)
        {
            Debug.LogError("[MirrorSpawnedPlayerBinder] 필수 네트워크 참조가 없어 로컬 등록을 중단합니다.", this);
            SetLocalOnlyBehaviours(false);
            return;
        }
        RegisterLocalContext();

        textInputBlocked = false;
        menuInputBlocked = false;
        SetLocalOnlyBehaviours(CanRestoreGameplay);
        RestoreLocalGameplayAfterScene();
    }

    /// <summary>
    /// Mirror가 생성한 플레이어 묶음 자체가 Camp와 Stage보다 오래 살아야 한다.
    /// 원격 복제본도 각 Client에서 함께 유지되어야 하므로 모든 Client 복제본을 DontDestroyOnLoad로 옮긴다.
    /// 재접속 예약 중에도 동일한 런타임을 보존하며 예약 만료 후 서버가 명시적으로 파괴한다.
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        PreserveAcrossNetworkSceneChange();
        ApplyTemporaryAbsence();
        if (nameplatePrefab != null && nameplate == null)
        {
            nameplate = Instantiate(nameplatePrefab);
            DontDestroyOnLoad(nameplate.gameObject);
            nameplate.Bind(this);
        }
    }

    public override void OnStopClient()
    {
        if (nameplate != null) Destroy(nameplate.gameObject);
        nameplate = null;
        base.OnStopClient();
    }

    /// <summary>인증된 명부의 표시 정보만 플레이어 복제본에 전달한다.</summary>
    [Server]
    internal void ServerSetDisplayIdentity(MirrorSessionRoster.Member member)
    {
        if (member == null || member.RuntimeContext != context) return;
        participantDisplayName = member.DisplayName;
        participantSlot = member.Slot;
    }

    public override void OnStopLocalPlayer()
    {
        confirmedSceneHandle = -1;
        StopLocalSceneRestore();
        context?.Combat?.CancelChase();
        context?.Controller?.StopMovement();
        SetLocalOnlyBehaviours(false);
        UnregisterLocalContext();
        base.OnStopLocalPlayer();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!IsConfigured)
        {
            Debug.LogError("[MirrorSpawnedPlayerBinder] 필수 네트워크 참조가 없어 서버 등록을 중단합니다.", this);
            return;
        }
        PreserveAcrossNetworkSceneChange();
        GetTestNetworkManager()?.RegisterServerPlayer(context);
    }

    public override void OnStopServer()
    {
        GetTestNetworkManager()?.UnregisterServerPlayer(context);
        base.OnStopServer();
    }

    private void RegisterLocalContext()
    {
        MirrorNetworkManager manager = GetTestNetworkManager();
        if (manager == null)
        {
            Debug.LogError(
                "[MirrorSpawnedPlayerBinder] MirrorNetworkManager를 찾지 못했습니다.",
                this);
            return;
        }

        manager.RegisterLocalPlayer(context);
    }

    private void UnregisterLocalContext()
    {
        GetTestNetworkManager()?.UnregisterLocalPlayer(context);
    }

    private static MirrorNetworkManager GetTestNetworkManager()
    {
        return NetworkManager.singleton as MirrorNetworkManager;
    }

    private void SetLocalOnlyBehaviours(bool enabled)
    {
        gameplayInputEnabled = enabled;
        RefreshLocalInput();
    }

    public void SetTextInputBlocked(bool blocked)
    {
        if (textInputBlocked == blocked) return;
        textInputBlocked = blocked;
        RefreshInputBlock();
    }

    /// <summary>메뉴 입력 차단을 채팅과 별도로 기록해, 한쪽을 닫아도 다른 쪽의 차단을 유지한다.</summary>
    public void SetMenuInputBlocked(bool blocked)
    {
        if (menuInputBlocked == blocked) return;
        menuInputBlocked = blocked;
        RefreshInputBlock();
    }

    /// <summary>인트로 차단을 메뉴·채팅·스킬 차단과 별도로 유지한다.</summary>
    public void SetCutsceneInputBlocked(bool blocked)
    {
        if (cutsceneInputBlocked == blocked) return;
        cutsceneInputBlocked = blocked;
        if (blocked && isLocalPlayer && context?.Controller?.agent != null)
        {
            var agent = context.Controller.agent;
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        }
        RefreshInputBlock();
    }

    public bool IsCutsceneInputBlocked => cutsceneInputBlocked;

    private void RefreshInputBlock()
    {
        bool blocked = textInputBlocked || menuInputBlocked || cutsceneInputBlocked;
        if (textInputReleaseRoutine != null) StopCoroutine(textInputReleaseRoutine);
        textInputReleaseRoutine = null;
        if (blocked && isLocalPlayer)
            GetComponent<NetworkPlayerActionInputHandler>()?.ReleaseHeldSkills();
        if (!blocked)
        {
            textInputReleaseFrame = Time.frameCount;
            if (isActiveAndEnabled) textInputReleaseRoutine = StartCoroutine(ReleaseTextInputNextFrame());
        }
        RefreshLocalInput();
    }

    private IEnumerator ReleaseTextInputNextFrame()
    {
        yield return null;
        RefreshLocalInput();
        textInputReleaseRoutine = null;
    }

    private void RefreshLocalInput()
    {
        bool canControl = isLocalPlayer && gameplayInputEnabled && CanRestoreGameplay && RequiresNavMesh &&
            GetTestNetworkManager()?.IsLocalGameplayReady == true;
        // 생존 스냅샷/OnStartLocalPlayer가 먼저 와도 최종 위치 확정 전에 실제 입력을 열지 않는다.
        // 서버의 원격 캐릭터 Controller 상태는 이 로컬 입력 경계에서 바꾸지 않는다.
        if (isLocalPlayer && !canControl && context?.Controller != null)
        {
            context.Controller.SetControlEnable(false);
            context.Controller.enabled = false;
        }
        if (localOnlyBehaviours == null)
            return;

        bool enableInput = canControl && !textInputBlocked && !menuInputBlocked && !cutsceneInputBlocked &&
            Time.frameCount > textInputReleaseFrame;
        // 활성화는 권한 연결 후 입력, 비활성화는 입력 해제 후 권한 연결 해제 순서다.
        for (int i = 0; i < localOnlyBehaviours.Length; i++)
        {
            Behaviour behaviour = localOnlyBehaviours[enableInput ? i : localOnlyBehaviours.Length - 1 - i];
            if (behaviour != null)
                behaviour.enabled = enableInput;
        }
    }

    /// <summary>
    /// 서버가 확정한 사망·부활 상태에 맞춰 이 컴퓨터의 로컬 입력만 켜고 끈다.
    /// 원격 플레이어 복제본에는 입력 컴포넌트가 항상 꺼진 상태로 남는다.
    /// </summary>
    public void SetLocalInputEnabled(bool enabled)
    {
        // 부재 중 부활이 끝나면 재접속 복구에도 새 생존 상태를 사용한다.
        if (temporarilyAbsent && absenceApplied && enabled && isServer &&
            context?.RuntimeState?.IsDead == false && !context.RuntimeState.IsReviving)
        {
            controllerWasEnabled = true;
            controllerHadControl = true;
        }
        // 생존 스냅샷이 Controller를 켠 직후에도 예약 상태의 정지를 다시 적용한다.
        if (temporarilyAbsent) ApplyTemporaryAbsence();
        if (!RequiresNavMesh)
        {
            enabled = false;
            context?.Controller?.SetControlEnable(false);
            if (context?.Controller != null) context.Controller.enabled = false;
        }
        if (isLocalPlayer)
        {
            bool wasInputEnabled = gameplayInputEnabled;
            SetLocalOnlyBehaviours(enabled);
            if (enabled && !wasInputEnabled && CanRestoreGameplay && RequiresNavMesh && context?.Controller?.agent != null &&
                (!context.Controller.agent.enabled || !context.Controller.agent.isOnNavMesh))
                RestoreLocalGameplayAfterScene();
        }
    }

    /// <summary>서버가 참가자의 재접속 예약 상태를 변경하고 미완료 행동을 취소한다.</summary>
    [Server]
    public void ServerSetTemporarilyAbsent(bool absent)
    {
        if (temporarilyAbsent == absent) return;
        temporarilyAbsent = absent;
        if (absent)
        {
            context?.CombatAuthority?.ServerCancelForDisconnect();
            GetComponent<FighterSkillAuthority>()?.ServerCancelForDisconnect();
        }
        ApplyTemporaryAbsence();
    }

    private void OnTemporarilyAbsentChanged(bool oldValue, bool newValue)
    {
        ApplyTemporaryAbsence();
        if (!newValue && isLocalPlayer)
            RestoreLocalGameplayAfterScene();
    }

    private void ApplyTemporaryAbsence()
    {
        T_PlayerController controller = context?.Controller;
        if (temporarilyAbsent)
        {
            if (!absenceApplied)
            {
                absentRenderers = GetComponentsInChildren<Renderer>(true);
                rendererStates = new bool[absentRenderers.Length];
                for (int i = 0; i < absentRenderers.Length; i++) rendererStates[i] = absentRenderers[i].enabled;
                absentColliders = GetComponentsInChildren<Collider>(true);
                colliderStates = new bool[absentColliders.Length];
                for (int i = 0; i < absentColliders.Length; i++) colliderStates[i] = absentColliders[i].enabled;
                controllerWasEnabled = controller != null && controller.enabled;
                controllerHadControl = controller != null && controller.IsControlEnabled;
                absentAgent = GetComponent<NavMeshAgent>();
                absenceApplied = true;
                context?.Combat?.CancelChase();
                controller?.StopMovement();
            }
            StopLocalSceneRestore();
            SetLocalOnlyBehaviours(false);
            foreach (Renderer item in absentRenderers) if (item != null) item.enabled = false;
            foreach (Collider item in absentColliders) if (item != null) item.enabled = false;
            controller?.SetControlEnable(false);
            if (controller != null) controller.enabled = false;
            if (absentAgent != null) absentAgent.enabled = false;
            return;
        }
        if (!absenceApplied) return;
        absenceApplied = false;
        for (int i = 0; i < absentRenderers.Length; i++) if (absentRenderers[i] != null) absentRenderers[i].enabled = rendererStates[i];
        for (int i = 0; i < absentColliders.Length; i++) if (absentColliders[i] != null) absentColliders[i].enabled = colliderStates[i];
        bool alive = context?.RuntimeState?.IsDead == false && !context.RuntimeState.IsReviving;
        if (absentAgent != null)
        {
            absentAgent.enabled = false;
            if (alive && RequiresNavMesh) TryWarpToNavMesh(absentAgent, transform.position);
        }
        if (controller != null)
        {
            controller.enabled = controllerWasEnabled && alive && RequiresNavMesh;
            controller.SetControlEnable(controllerHadControl && alive && RequiresNavMesh);
        }
        SetLocalOnlyBehaviours(CanRestoreGameplay && RequiresNavMesh);
    }

    /// <summary>
    /// Scene 전환 뒤 로컬 Context 등록, 입력 컴포넌트, Controller와 NavMeshAgent를 한 경로에서 복구한다.
    /// <para>빌드에서는 <c>OnClientSceneChanged</c>와 NavMesh 준비 순서가 Editor보다 늦을 수 있으므로,
    /// 전투·캠프 Scene에서는 최대 120프레임 동안 실제 NavMesh 연결을 기다린다.</para>
    /// </summary>
    public void RestoreLocalGameplayAfterScene()
    {
        if (!isLocalPlayer || temporarilyAbsent)
            return;

        // OnClientSceneChanged와 TargetRpc의 도착 순서와 무관하게 이미 복구한 이동/스킬은 유지한다.
        if (IsSceneStartConfirmed && CanRestoreGameplay && RequiresNavMesh && gameplayInputEnabled &&
            context.Controller != null && context.Controller.enabled && context.Controller.IsControlEnabled &&
            context.Controller.agent != null && context.Controller.agent.enabled && context.Controller.agent.isOnNavMesh)
        {
            RegisterLocalContext();
            RefreshLocalInput();
            return;
        }
        StopLocalSceneRestore();
        localSceneRestoreRoutine = StartCoroutine(RestoreLocalGameplayRoutine());
    }

    /// <summary>
    /// 서버가 정한 새 Stage 시작 위치를 서버 복제본에 먼저 적용한다.
    /// </summary>
    [Server]
    public void ServerPlaceAtSceneStart(Vector3 position, Quaternion rotation)
    {
        confirmedSceneHandle = -1;
        hasServerSceneStart = true;
        serverSceneStartPath = SceneManager.GetActiveScene().path;
        serverSceneStartPosition = position;
        serverSceneStartRotation = rotation;
        ApplySceneStart(position, rotation);
        // Host는 같은 객체에 서버 위치가 이미 적용됐다. 뒤따르는 TargetRpc가 이동 경로를 다시 지우지 않는다.
        if (isLocalPlayer)
        {
            confirmedSceneHandle = SceneManager.GetActiveScene().handle;
            RestoreLocalGameplayAfterScene();
        }
    }

    /// <summary>
    /// Client 권한 위치가 Scene 전환 중 서버 위치를 다시 덮어써도 서버가 처음 배정한 시작점을 보낸다.
    /// </summary>
    [Server]
    public void ServerConfirmSceneStart(NetworkConnectionToClient target)
    {
        if (!hasServerSceneStart || serverSceneStartPath != SceneManager.GetActiveScene().path)
            return;

        TargetConfirmSceneStart(target, serverSceneStartPosition, serverSceneStartRotation);
    }

    /// <summary>
    /// Scene 로드를 끝낸 소유 Client에 같은 위치를 한 번 확정한다.
    /// Camp의 마지막 Client 권한 위치가 Stage 시작 위치를 덮어쓰는 것을 방지한다.
    /// </summary>
    [TargetRpc]
    public void TargetConfirmSceneStart(
        NetworkConnectionToClient target,
        Vector3 position,
        Quaternion rotation)
    {
        if (IsSceneStartConfirmed) return;
        serverSceneStartPosition = position;
        ApplySceneStart(position, rotation);
        confirmedSceneHandle = SceneManager.GetActiveScene().handle;
        Debug.Log($"[MirrorSpawnedPlayerBinder] scene-start confirmed netId={netId} handle={confirmedSceneHandle} position={transform.position}", this);
        RestoreLocalGameplayAfterScene();
    }

    private void PreserveAcrossNetworkSceneChange()
    {
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void ApplySceneStart(Vector3 position, Quaternion rotation)
    {
        if (TryGetComponent(out NavMeshAgent agent))
        {
            if (agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();

            if (RequiresNavMesh && !temporarilyAbsent && context?.RuntimeState?.IsDead == false && TryWarpToNavMesh(agent, position))
            {
                transform.rotation = rotation;
                return;
            }
            agent.enabled = false;
            if (RequiresNavMesh && NavMesh.SamplePosition(position, out NavMeshHit hit, NavMeshSampleDistance, agent.areaMask))
                position = hit.position;
        }

        transform.SetPositionAndRotation(position, rotation);
    }

    private IEnumerator RestoreLocalGameplayRoutine()
    {
        RegisterLocalContext();

        SetLocalOnlyBehaviours(false);
        for (int frame = 0; frame < SceneRestoreFrameLimit && context?.RuntimeState?.HasSnapshot != true; frame++)
        {
            if (temporarilyAbsent) break;
            yield return null;
        }
        if (!CanRestoreGameplay)
        {
            localSceneRestoreRoutine = null;
            yield break;
        }

        T_PlayerController controller = context?.Controller;
        bool requiresNavMesh = RequiresNavMesh;
        if (!requiresNavMesh)
        {
            SetLocalInputEnabled(false);
            localSceneRestoreRoutine = null;
            yield break;
        }
        if (controller != null) controller.enabled = true;

        for (int frame = 0; frame < SceneRestoreFrameLimit; frame++)
        {
            if (!CanRestoreGameplay)
            {
                SetLocalOnlyBehaviours(false);
                localSceneRestoreRoutine = null;
                yield break;
            }
            NavMeshAgent agent = controller != null ? controller.agent : null;
            if (agent != null)
            {
                if (!agent.enabled || !agent.isOnNavMesh)
                {
                    // 승강기 등 NavMesh 밖에서 부활하면 현재 맵의 서버 확정 시작점으로 복구한다.
                    if (!TryWarpToNavMesh(agent, transform.position) && IsSceneStartConfirmed)
                        TryWarpToNavMesh(agent, serverSceneStartPosition);
                }

                if (agent.enabled && agent.isOnNavMesh)
                    break;
            }

            yield return null;
        }

        if (!CanRestoreGameplay)
        {
            SetLocalOnlyBehaviours(false);
            localSceneRestoreRoutine = null;
            yield break;
        }
        bool placed = controller != null && controller.agent != null &&
            controller.agent.enabled && controller.agent.isOnNavMesh;
        controller?.SetControlEnable(placed && GetTestNetworkManager()?.IsLocalGameplayReady == true);
        SetLocalOnlyBehaviours(CanRestoreGameplay && placed);
        localSceneRestoreRoutine = null;
        if (!placed)
        {
            Debug.LogError(
                "[MirrorSpawnedPlayerBinder] Scene 전환 뒤 로컬 플레이어를 NavMesh에 연결하지 못했습니다.",
                this);
        }
    }

    private static bool TryWarpToNavMesh(NavMeshAgent agent, Vector3 position)
    {
        if (agent == null)
            return false;

        if (!NavMesh.SamplePosition(
                position,
                out NavMeshHit hit,
                NavMeshSampleDistance,
                agent.areaMask))
        {
            return false;
        }

        if (!agent.enabled)
        {
            agent.transform.position = hit.position;
            agent.enabled = true;
        }
        return agent.isOnNavMesh && agent.Warp(hit.position);
    }

    private void StopLocalSceneRestore()
    {
        if (localSceneRestoreRoutine == null)
            return;

        StopCoroutine(localSceneRestoreRoutine);
        localSceneRestoreRoutine = null;
    }

    private void ResolveLocalOnlyBehaviours()
    {
        NetworkPlayerInputHandler movementInput = GetComponent<NetworkPlayerInputHandler>();
        NetworkPlayerActionInputHandler actionInput = GetComponent<NetworkPlayerActionInputHandler>();
        localOnlyBehaviours = new Behaviour[] { movementInput, actionInput,
            GetComponent<WBH_PlayerInputHandler>(), GetComponent<PlayerActionInputHandler>() };
    }
}

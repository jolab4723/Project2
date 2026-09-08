using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Mirror가 생성한 테스트 플레이어의 로컬·서버 등록과 Scene 간 수명주기를 연결한다.
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

    private Coroutine localSceneRestoreRoutine;
    private bool gameplayInputEnabled;
    private bool textInputBlocked;
    private Coroutine textInputReleaseRoutine;
    private int textInputReleaseFrame = -1;
    private bool hasServerSceneStart;
    private string serverSceneStartPath;
    private Vector3 serverSceneStartPosition;
    private Quaternion serverSceneStartRotation;
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
    /// <summary>재접속 예약으로 시각·충돌·조작이 정지된 참가자인지 반환한다.</summary>
    public bool IsTemporarilyAbsent => temporarilyAbsent;
    private bool CanRestoreGameplay => !temporarilyAbsent && context?.RuntimeState?.HasSnapshot == true &&
        !context.RuntimeState.IsDead;
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

    [ContextMenu("Validate Mirror Player Configuration")]
    private void ValidateMirrorPlayerConfiguration()
    {
        Debug.Assert(GetComponent<WBH_PlayerInputHandler>() == null, "원본 이동 입력기가 남아 있습니다.", this);
        Debug.Assert(GetComponent<WBH_PlayerAnimation>() == null, "원본 애니메이션 이벤트 수신기가 남아 있습니다.", this);
        Debug.Assert(GetComponent<PlayerActionInputHandler>() == null, "원본 액션 입력기가 남아 있습니다.", this);
        Debug.Assert(GetComponent<FighterSkillController>() == null, "원본 로컬 스킬 판정기가 남아 있습니다.", this);
        Debug.Assert(GetComponent<PotionUseManager>() == null, "원본 로컬 포션 관리자가 남아 있습니다.", this);
        Debug.Assert(GetComponent<PlayerRelicEffectProvider>() == null, "원본 로컬 유물 적용기가 남아 있습니다.", this);
        Debug.Assert(GetComponent<PlayerHudEventBridge>() == null, "원본 전역 HUD 발행기가 남아 있습니다.", this);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        RegisterLocalContext();

        textInputBlocked = false;
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
    }

    public override void OnStopLocalPlayer()
    {
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
        MirrorTestNetworkManager manager = GetTestNetworkManager();
        if (manager == null)
        {
            Debug.LogError(
                "[MirrorSpawnedPlayerBinder] MirrorTestNetworkManager를 찾지 못했습니다.",
                this);
            return;
        }

        manager.RegisterLocalPlayer(context);
    }

    private void UnregisterLocalContext()
    {
        GetTestNetworkManager()?.UnregisterLocalPlayer(context);
    }

    private static MirrorTestNetworkManager GetTestNetworkManager()
    {
        return NetworkManager.singleton as MirrorTestNetworkManager;
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
        if (textInputReleaseRoutine != null) StopCoroutine(textInputReleaseRoutine);
        textInputReleaseRoutine = null;
        if (blocked && isLocalPlayer)
            GetComponent<PlayerActionInputHandler_MirrorTest>()?.ReleaseHeldSkills();
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
        if (localOnlyBehaviours == null)
            return;

        foreach (Behaviour behaviour in localOnlyBehaviours)
        {
            if (behaviour != null)
                behaviour.enabled = isLocalPlayer && gameplayInputEnabled && CanRestoreGameplay && !textInputBlocked &&
                    Time.frameCount > textInputReleaseFrame;
        }
    }

    /// <summary>
    /// 서버가 확정한 사망·부활 상태에 맞춰 이 컴퓨터의 로컬 입력만 켜고 끈다.
    /// 원격 플레이어 복제본에는 입력 컴포넌트가 항상 꺼진 상태로 남는다.
    /// </summary>
    public void SetLocalInputEnabled(bool enabled)
    {
        // 생존 스냅샷이 Controller를 켠 직후에도 예약 상태의 정지를 다시 적용한다.
        if (temporarilyAbsent) ApplyTemporaryAbsence();
        if (!RequiresNavMesh)
        {
            enabled = false;
            context?.Controller?.SetControlEnable(false);
            if (context?.Controller != null) context.Controller.enabled = false;
        }
        if (isLocalPlayer)
            SetLocalOnlyBehaviours(enabled);
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
            GetComponent<FighterSkillAuthority_MirrorTest>()?.ServerCancelForDisconnect();
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
        bool alive = context?.RuntimeState?.IsDead == false;
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

        StopLocalSceneRestore();
        localSceneRestoreRoutine = StartCoroutine(RestoreLocalGameplayRoutine());
    }

    /// <summary>
    /// 서버가 정한 새 Stage 시작 위치를 서버 복제본에 먼저 적용한다.
    /// </summary>
    [Server]
    public void ServerPlaceAtSceneStart(Vector3 position, Quaternion rotation)
    {
        hasServerSceneStart = true;
        serverSceneStartPath = SceneManager.GetActiveScene().path;
        serverSceneStartPosition = position;
        serverSceneStartRotation = rotation;
        ApplySceneStart(position, rotation);
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
        ApplySceneStart(position, rotation);
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

            if (RequiresNavMesh && !temporarilyAbsent && TryWarpToNavMesh(agent, position))
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
                    TryWarpToNavMesh(agent, transform.position);

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
        controller?.SetControlEnable(placed);
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
        WBH_PlayerInputHandler_MirrorTest movementInput = GetComponent<WBH_PlayerInputHandler_MirrorTest>();
        PlayerActionInputHandler_MirrorTest actionInput = GetComponent<PlayerActionInputHandler_MirrorTest>();
        localOnlyBehaviours = new Behaviour[] { movementInput, actionInput };
    }
}

using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Mirror가 생성한 테스트 플레이어의 로컬·서버 등록과 Scene 간 수명주기를 연결한다.
/// <para>6-B 빌드 보완: Scene 전환 직후 한 프레임의 콜백에만 의존하지 않고, 로컬 PlayerContext를 다시
/// 등록한 뒤 전투 Scene의 NavMeshAgent가 실제 NavMesh에 올라올 때까지 기다려 입력과 조작 권한을 복구한다.</para>
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

    public PlayerContext Context => context;

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
        RegisterLocalContext();

        // Host는 첫 서버 스냅샷 발행 전에 OnStartLocalPlayer가 올 수 있다.
        // 아직 스냅샷이 없다면 초기 HP 0을 사망으로 오인하지 않고 입력을 먼저 연다.
        bool hasAuthoritativeSnapshot = context?.RuntimeState?.HasSnapshot == true;
        SetLocalOnlyBehaviours(!hasAuthoritativeSnapshot || context.RuntimeState.IsDead == false);
        RestoreLocalGameplayAfterScene();
    }

    /// <summary>
    /// 6-B 테스트에서는 Mirror가 생성한 플레이어 묶음 자체가 Camp와 Stage보다 오래 살아야 한다.
    /// 원격 복제본도 각 Client에서 함께 유지되어야 하므로 모든 Client 복제본을 DontDestroyOnLoad로 옮긴다.
    /// 연결 종료 시에는 Mirror의 명시적 Destroy가 그대로 실행되므로 다음 접속의 Player와 섞이지 않는다.
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        PreserveAcrossNetworkSceneChange();
    }

    public override void OnStopLocalPlayer()
    {
        StopLocalSceneRestore();
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
        if (localOnlyBehaviours == null)
            return;

        foreach (Behaviour behaviour in localOnlyBehaviours)
        {
            if (behaviour != null)
                behaviour.enabled = enabled;
        }
    }

    /// <summary>
    /// 서버가 확정한 사망·부활 상태에 맞춰 이 컴퓨터의 로컬 입력만 켜고 끈다.
    /// 원격 플레이어 복제본에는 입력 컴포넌트가 항상 꺼진 상태로 남는다.
    /// </summary>
    public void SetLocalInputEnabled(bool enabled)
    {
        if (isLocalPlayer)
            SetLocalOnlyBehaviours(enabled);
    }

    /// <summary>
    /// Scene 전환 뒤 로컬 Context 등록, 입력 컴포넌트, Controller와 NavMeshAgent를 한 경로에서 복구한다.
    /// <para>빌드에서는 <c>OnClientSceneChanged</c>와 NavMesh 준비 순서가 Editor보다 늦을 수 있으므로,
    /// 전투 Scene에 한해서 최대 120프레임 동안 실제 NavMesh 연결을 기다린다.</para>
    /// </summary>
    public void RestoreLocalGameplayAfterScene()
    {
        if (!isLocalPlayer)
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
        ApplySceneStart(position, rotation);
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
        if (TryGetComponent(out NavMeshAgent agent) && agent.enabled)
        {
            if (agent.isOnNavMesh)
                agent.ResetPath();

            if (TryWarpToNavMesh(agent, position))
            {
                transform.rotation = rotation;
                return;
            }
        }

        transform.SetPositionAndRotation(position, rotation);
    }

    private IEnumerator RestoreLocalGameplayRoutine()
    {
        RegisterLocalContext();

        PlayerRuntimeStateSync_MirrorTest runtimeState = context?.RuntimeState;
        bool hasSnapshot = runtimeState?.HasSnapshot == true;
        bool isDead = hasSnapshot && runtimeState.IsDead;
        SetLocalOnlyBehaviours(!isDead);

        if (isDead)
        {
            localSceneRestoreRoutine = null;
            yield break;
        }

        T_PlayerController controller = context?.Controller;
        if (controller != null)
            controller.enabled = true;

        bool requiresNavMesh =
            SceneManager.GetActiveScene().path == MirrorTestNetworkManager.SessionCombatScene;

        for (int frame = 0; frame < SceneRestoreFrameLimit; frame++)
        {
            if (!requiresNavMesh)
                break;

            NavMeshAgent agent = controller != null ? controller.agent : null;
            if (agent != null && agent.enabled)
            {
                if (!agent.isOnNavMesh)
                    TryWarpToNavMesh(agent, transform.position);

                if (agent.isOnNavMesh)
                    break;
            }

            yield return null;
        }

        if (controller != null)
            controller.SetControlEnable(true);

        SetLocalOnlyBehaviours(true);
        localSceneRestoreRoutine = null;

        if (requiresNavMesh &&
            (controller == null || controller.agent == null || !controller.agent.isOnNavMesh))
        {
            Debug.LogError(
                "[MirrorSpawnedPlayerBinder] Scene 전환 뒤 로컬 플레이어를 NavMesh에 연결하지 못했습니다.",
                this);
        }
    }

    private static bool TryWarpToNavMesh(NavMeshAgent agent, Vector3 position)
    {
        if (agent == null || !agent.enabled)
            return false;

        if (!NavMesh.SamplePosition(
                position,
                out NavMeshHit hit,
                NavMeshSampleDistance,
                agent.areaMask))
        {
            return false;
        }

        return agent.Warp(hit.position);
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

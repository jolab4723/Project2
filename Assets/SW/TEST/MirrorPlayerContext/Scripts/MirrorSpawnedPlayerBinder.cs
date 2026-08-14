using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerContext))]
public sealed class MirrorSpawnedPlayerBinder : NetworkBehaviour
{
    [SerializeField] private PlayerContext context;
    [Tooltip("로컬 플레이어에게만 켤 입력 컴포넌트")]
    [SerializeField] private Behaviour[] localOnlyBehaviours;

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
    }

    public override void OnStopLocalPlayer()
    {
        SetLocalOnlyBehaviours(false);
        UnregisterLocalContext();
        base.OnStopLocalPlayer();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
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

    private void ResolveLocalOnlyBehaviours()
    {
        WBH_PlayerInputHandler_MirrorTest movementInput = GetComponent<WBH_PlayerInputHandler_MirrorTest>();
        PlayerActionInputHandler_MirrorTest actionInput = GetComponent<PlayerActionInputHandler_MirrorTest>();
        localOnlyBehaviours = new Behaviour[] { movementInput, actionInput };
    }
}

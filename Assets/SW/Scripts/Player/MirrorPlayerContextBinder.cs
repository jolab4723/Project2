using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerContext))]
public sealed class MirrorPlayerContextBinder : NetworkBehaviour
{
    [SerializeField] private PlayerContext context;
    [Tooltip("로컬 플레이어에게만 켤 입력 컴포넌트")]
    [SerializeField] private Behaviour[] localOnlyBehaviours;

    public PlayerContext Context => context;

    private void Awake()
    {
        context ??= GetComponent<PlayerContext>();
        SetLocalOnlyBehaviours(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        context ??= GetComponent<PlayerContext>();
    }
#endif

    private void Reset()
    {
        context = GetComponent<PlayerContext>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SetLocalOnlyBehaviours(true);
    }

    public override void OnStopLocalPlayer()
    {
        SetLocalOnlyBehaviours(false);
        base.OnStopLocalPlayer();
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
}

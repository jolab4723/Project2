using Mirror;
using UnityEngine;

/// <summary>같은 공통 방어구 실행기의 보호막 수치를 관찰자에게 전달합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerContext))]
public sealed class PlayerArmorEffectProvider : NetworkBehaviour
{
    // 기존 프리팹의 직렬화 참조를 보존한다. 실행기는 같은 PlayerContext에서 참조를 받는다.
    [SerializeField] private EquipmentSystem equipment;
    [SerializeField] private PlayerStatManager stats;
    [SerializeField] private PlayerHealthManager health;
    [SerializeField] private PlayerManaManager mana;
    [SerializeField] private PlayerBuffManager buffs;
    [SyncVar(hook = nameof(OnShieldAmountChanged))] private float shieldAmount;
    private PlayerArmorEffectRuntime runtime;
    public float ShieldAmount => isServer && runtime != null ? runtime.ShieldAmount : shieldAmount;

    private void Awake() => ResolveRuntime();
    private void OnEnable()
    {
        ResolveRuntime();
        runtime.enabled = true;
    }
    private void OnDisable() { if (runtime != null) runtime.enabled = false; }
    public override void OnStartServer()
    {
        base.OnStartServer();
        ResolveRuntime();
        runtime.enabled = true;
        runtime.ReconcileEquipment();
    }
    public override void OnStopServer()
    {
        if (runtime != null) runtime.enabled = false;
        base.OnStopServer();
    }
    public override void OnStopClient()
    {
        if (!isServer && runtime != null) runtime.enabled = false;
        base.OnStopClient();
    }
    private void ResolveRuntime()
    {
        runtime ??= GetComponent<PlayerArmorEffectRuntime>();
        runtime ??= gameObject.AddComponent<PlayerArmorEffectRuntime>();
    }
    public override void OnStartClient()
    {
        base.OnStartClient();
        ResolveRuntime();
        runtime.enabled = true;
        runtime.ApplyVisualAmount(shieldAmount);
    }
    private void LateUpdate()
    {
        if (isServer && runtime != null) shieldAmount = runtime.ShieldAmount;
    }
    private void OnShieldAmountChanged(float oldValue, float newValue)
    {
        ResolveRuntime();
        runtime.ApplyVisualAmount(newValue);
    }
}

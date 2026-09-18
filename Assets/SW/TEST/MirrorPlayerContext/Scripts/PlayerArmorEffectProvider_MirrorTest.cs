using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>
/// 서버에서 장착된 저마나 투구의 효과 실행 객체를 관리합니다.
/// 마나 조건과 버프 적용은 StatThresholdRunner_MirrorTest에 맡깁니다.
/// 장비 해제, 컴포넌트 비활성화, 서버 종료 때 기존 실행 객체를 정리합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerContext))]
public sealed class PlayerArmorEffectProvider_MirrorTest : NetworkBehaviour
{
    [SerializeField] private EquipmentSystem equipment;
    [SerializeField] private PlayerStatManager stats;
    [SerializeField] private PlayerHealthManager health;
    [SerializeField] private PlayerManaManager mana;
    [SerializeField] private PlayerBuffManager buffs;
    private PlayerContext owner;
    private bool started;
    private ItemInstance activeItem;
    private StatThresholdBuffUniqueEffectSO activeEffect;
    private GameObject runtimeObject;

    private void Awake()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        owner ??= GetComponent<PlayerContext>();
        equipment ??= GetComponentInChildren<EquipmentSystem>(true);
        stats ??= GetComponent<PlayerStatManager>();
        health ??= GetComponent<PlayerHealthManager>();
        mana ??= GetComponent<PlayerManaManager>();
        buffs ??= GetComponent<PlayerBuffManager>();
    }

    private void OnEnable()
    {
        EnsureReferences();
        if (equipment != null)
        {
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            equipment.OnEquipmentChanged += HandleEquipmentChanged;
        }
        ReconcileEquipment();
    }

    private void Start()
    {
        started = true;
        ReconcileEquipment();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        started = true;
        EnsureReferences();
        if (equipment != null)
        {
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            equipment.OnEquipmentChanged += HandleEquipmentChanged;
        }
        ReconcileEquipment();
    }

    public override void OnStopServer()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        StopRuntime();
        started = false;
        base.OnStopServer();
    }

    private void HandleEquipmentChanged(EquippedItemInfo[] _)
        => ReconcileEquipment();

    private void OnDisable()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        StopRuntime();
    }

    private void ReconcileEquipment()
    {
        EnsureReferences();
        if (!started || !isActiveAndEnabled || !isServer) return;
        if (owner == null || equipment == null || stats == null ||
            mana == null || buffs == null || health == null ||
            owner.Equipment != equipment || owner.Stats != stats ||
            owner.Mana != mana || owner.Buffs != buffs || owner.Health != health)
        {
            StopRuntime();
            Debug.LogError("[PlayerArmorEffectProvider] 같은 PlayerContext의 참조가 필요합니다.", this);
            return;
        }

        equipment.TryGetEquippedItemInstance(EquipSlotType.Helmet, out ItemInstance next);
        if (!equipment.UsesLowManaHelmetEffect(next)) next = null;
        var nextEffect = next?.definition?.uniqueEffect as StatThresholdBuffUniqueEffectSO;
        if (ReferenceEquals(activeItem, next) && activeEffect == nextEffect) return;

        StopRuntime(); // 이전 구독·효과를 먼저 종료한다.
        if (next == null || nextEffect == null) return;
        stats.EnsureInitialized();
        activeItem = next;
        activeEffect = nextEffect;
        runtimeObject = new GameObject("LowManaHelmetEffect");
        runtimeObject.transform.SetParent(transform, false);
        runtimeObject.AddComponent<StatThresholdRunner_MirrorTest>()
            .Bind(stats, health, mana, buffs, nextEffect);
    }

    /// <summary>
    /// 이전 효과의 구독과 버프를 먼저 해제한 뒤 실행 객체를 제거합니다.
    /// 여러 번 호출해도 이미 정리한 객체를 다시 처리하지 않습니다.
    /// </summary>
    private void StopRuntime()
    {
        GameObject previous = runtimeObject;
        // 버프 제거가 다른 이벤트를 호출해도 이전 객체를 다시 사용하지 않게 합니다.
        runtimeObject = null;
        activeItem = null;
        activeEffect = null;
        if (previous == null)
            return;

        // 객체가 실제로 파괴되기 전에 이벤트 구독과 버프를 먼저 정리합니다.
        previous.GetComponent<StatThresholdRunner_MirrorTest>()?.Unbind();
        previous.SetActive(false);
        if (Application.isPlaying)
            Destroy(previous);
        else
            DestroyImmediate(previous);
    }
}

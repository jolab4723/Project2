using UnityEngine;

/// <summary>
/// 플레이어의 공통 상태를 연결한다. 싱글의 씬 인벤토리는 명시적으로 연결하고,
/// 미러 전용 구성 검사는 MirrorSpawnedPlayerBinder가 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerContext : MonoBehaviour
{
    [Header("Player-owned runtime")]
    [SerializeField] private InventoryController inventory;
    [SerializeField] private EquipmentSystem equipment;
    [SerializeField] private PlayerWallet wallet;

    [Header("Player state")]
    [SerializeField] private PlayerStatManager stats;
    [SerializeField] private PlayerBuffManager buffs;
    [SerializeField] private PlayerHealthManager health;
    [SerializeField] private PlayerManaManager mana;

    // 기존 미러 프리팹과 호출부를 보존한다. 공통 IsComplete의 필수 조건은 아니다.
    [Header("Optional Mirror runtime")]
    [SerializeField] private NetworkPotionUseManager potions;
    [SerializeField] private NetworkItemTriggerManager itemTriggers;
    [SerializeField] private PlayerRuntimeStateSync runtimeState;
    [SerializeField] private PlayerCombatAuthority combatAuthority;

    [Header("Character")]
    [SerializeField] private T_PlayerController controller;
    [SerializeField] private T_PlayerCombat combat;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;
    private bool usesSceneInventory;
    private PlayerItemEffectState effects;

    public PlayerItemEffectState Effects => effects ??= new PlayerItemEffectState(this);

    public InventoryController Inventory => inventory;
    public EquipmentSystem Equipment => equipment;
    public PlayerWallet Wallet => wallet;
    public PlayerStatManager Stats => stats;
    public PlayerBuffManager Buffs => buffs;
    public PlayerHealthManager Health => health;
    public PlayerManaManager Mana => mana;
    public NetworkPotionUseManager Potions => potions;
    public NetworkItemTriggerManager ItemTriggers => itemTriggers;
    public PlayerRuntimeStateSync RuntimeState => runtimeState;
    public PlayerCombatAuthority CombatAuthority => combatAuthority;
    public T_PlayerController Controller => controller;
    public T_PlayerCombat Combat => combat;
    public WBH_PlayerStateMachine StateMachine => stateMachine;

    public bool HasInventoryRuntime =>
        inventory != null &&
        equipment != null && equipment == inventory.EquipmentSystem &&
        wallet != null && wallet == inventory.PlayerWallet &&
        ((Owns(inventory) && Owns(equipment) && Owns(wallet)) ||
         (usesSceneInventory && inventory.BoundPlayer == this &&
          GetComponentInParent<Mirror.NetworkIdentity>() == null &&
          inventory.GetComponentInParent<Mirror.NetworkIdentity>() == null &&
          inventory.gameObject.scene == gameObject.scene));

    public bool IsComplete => HasInventoryRuntime && HasCharacterRuntime;

    /// <summary>상태를 먼저 연결한 뒤 현재 외형과 선택 스킬의 첫 표시 자산을 준비합니다.</summary>
    public bool TryPreparePresentation(out bool ready, out string error)
    {
        ready = false;
        error = null;
        if (!IsComplete) return true;
#if UNITY_SERVER && !UNITY_EDITOR
        ready = true;
        return true;
#else
        foreach (PlayerWeaponVisualPresenter visual in GetComponentsInChildren<PlayerWeaponVisualPresenter>(true))
        {
            if (visual.VisualLoadError != null) { error = visual.VisualLoadError; return false; }
            if (!visual.IsVisualReady) return true;
        }
        ISkillController skills = GetComponent<FighterSkillAuthority>();
        skills ??= GetComponent<FighterSkillController>();
        skills ??= GetComponent<GunnerSkillController>();
        WBH_PlayerEffect effects = GetComponent<WBH_PlayerEffect>();
        if (effects == null) { error = "플레이어 효과 연결이 없습니다."; return false; }
        return effects.TryPrepareCurrentEffects(FindFirstObjectByType<WBH_EffectPoolManager>(), skills,
            GetComponent<FighterSkillController>() != null, out ready, out error);
#endif
    }

    private bool HasCharacterRuntime =>
        Owns(stats) && Owns(buffs) && Owns(health) && Owns(mana) &&
        Owns(controller) && Owns(combat) && Owns(stateMachine);

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        // 싱글은 생성 직후 Spawner가 씬 인벤토리를 전달한다.
        ValidateRequiredReferences();
        ValidateOwnedReferences();
        if (GetComponent<Mirror.NetworkIdentity>() == null && GetComponent<PlayerArmorEffectRuntime>() == null)
            gameObject.AddComponent<PlayerArmorEffectRuntime>();
    }

    private void OnEnable()
    {
        if (usesSceneInventory && inventory != null && !BindSinglePlayerInventory(inventory))
            Debug.LogError("[PlayerContext] 씬 인벤토리를 다시 연결하지 못했습니다.", this);
    }

    private void OnDisable()
    {
        effects?.ResetAttackLifetime();
        if (usesSceneInventory)
            inventory?.UnbindPlayer(this);
    }

    /// <summary>
    /// 싱글 생성 지점에서 기존 씬 인벤토리를 전달한다. 상태를 복사하거나 옮기지 않으며,
    /// 다른 플레이어가 이미 쓰는 인벤토리와 네트워크 플레이어의 외부 참조는 거절한다.
    /// </summary>
    public bool BindSinglePlayerInventory(InventoryController source)
    {
        ResolveReferences();
        if (!HasCharacterRuntime || source == null || source.EquipmentSystem == null || source.PlayerWallet == null ||
            !source.EquipmentSystem.transform.IsChildOf(source.transform) ||
            !source.PlayerWallet.transform.IsChildOf(source.transform) ||
            GetComponentInParent<Mirror.NetworkIdentity>() != null ||
            source.GetComponentInParent<Mirror.NetworkIdentity>() != null ||
            Mirror.NetworkClient.active || Mirror.NetworkServer.active ||
            !gameObject.scene.IsValid() || source.gameObject.scene != gameObject.scene ||
            (source.BoundPlayer != null && source.BoundPlayer != this))
            return false;

        if (!source.TryBindPlayer(this))
            return false;

        if (usesSceneInventory && inventory != source)
            inventory?.UnbindPlayer(this);

        inventory = source;
        equipment = source.EquipmentSystem;
        wallet = source.PlayerWallet;
        usesSceneInventory = true;
        return IsComplete;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
        ValidateOwnedReferences();
    }
#endif

    private void Reset()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        inventory ??= GetComponentInChildren<InventoryController>(true);
        equipment ??= GetComponentInChildren<EquipmentSystem>(true);
        wallet ??= GetComponentInChildren<PlayerWallet>(true);

        stats ??= GetComponent<PlayerStatManager>();
        buffs ??= GetComponent<PlayerBuffManager>();
        health ??= GetComponent<PlayerHealthManager>();
        mana ??= GetComponent<PlayerManaManager>();
        potions ??= GetComponent<NetworkPotionUseManager>();
        itemTriggers ??= GetComponent<NetworkItemTriggerManager>();
        runtimeState ??= GetComponent<PlayerRuntimeStateSync>();
        combatAuthority ??= GetComponent<PlayerCombatAuthority>();

        controller ??= GetComponent<T_PlayerController>();
        combat ??= GetComponent<T_PlayerCombat>();
        stateMachine ??= GetComponent<WBH_PlayerStateMachine>();
    }

    private void ValidateRequiredReferences()
    {
        if (IsComplete)
            return;

        Debug.LogError(
            "[PlayerContext] 플레이어의 Inventory/전투/상태 필수 참조가 비어 있습니다.",
            this);
    }

    private void ValidateOwnedReferences()
    {
        if (!usesSceneInventory || !HasInventoryRuntime)
        {
            ValidateOwnedReference(inventory, nameof(inventory));
            ValidateOwnedReference(equipment, nameof(equipment));
            ValidateOwnedReference(wallet, nameof(wallet));
        }
        ValidateOwnedReference(stats, nameof(stats));
        ValidateOwnedReference(buffs, nameof(buffs));
        ValidateOwnedReference(health, nameof(health));
        ValidateOwnedReference(mana, nameof(mana));
        ValidateOwnedReference(potions, nameof(potions));
        ValidateOwnedReference(itemTriggers, nameof(itemTriggers));
        ValidateOwnedReference(runtimeState, nameof(runtimeState));
        ValidateOwnedReference(combatAuthority, nameof(combatAuthority));
        ValidateOwnedReference(controller, nameof(controller));
        ValidateOwnedReference(combat, nameof(combat));
        ValidateOwnedReference(stateMachine, nameof(stateMachine));
    }

    private void ValidateOwnedReference(Component component, string fieldName)
    {
        if (component == null ||
            component.transform == transform ||
            component.transform.IsChildOf(transform))
        {
            return;
        }

        Debug.LogError(
            $"[PlayerContext] {fieldName} 참조가 다른 플레이어 객체를 가리킵니다.",
            this);
    }

    private bool Owns(Component component) => component != null &&
        (component.transform == transform || component.transform.IsChildOf(transform));
}

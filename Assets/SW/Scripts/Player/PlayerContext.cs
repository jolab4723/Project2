using UnityEngine;

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
    [SerializeField] private PotionUseManager potions;
    [SerializeField] private ItemTriggerManager itemTriggers;

    [Header("Character")]
    [SerializeField] private T_PlayerController controller;
    [SerializeField] private T_PlayerCombat combat;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;

    public InventoryController Inventory => inventory;
    public EquipmentSystem Equipment => equipment;
    public PlayerWallet Wallet => wallet;
    public PlayerStatManager Stats => stats;
    public PlayerBuffManager Buffs => buffs;
    public PlayerHealthManager Health => health;
    public PlayerManaManager Mana => mana;
    public PotionUseManager Potions => potions;
    public ItemTriggerManager ItemTriggers => itemTriggers;
    public T_PlayerController Controller => controller;
    public T_PlayerCombat Combat => combat;
    public WBH_PlayerStateMachine StateMachine => stateMachine;

    public bool HasInventoryRuntime =>
        inventory != null &&
        equipment != null &&
        wallet != null;

    private void Awake()
    {
        ResolveReferences();
        ValidateRequiredReferences();
        ValidateOwnedReferences();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
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
        potions ??= GetComponent<PotionUseManager>();
        itemTriggers ??= GetComponent<ItemTriggerManager>();

        controller ??= GetComponent<T_PlayerController>();
        combat ??= GetComponent<T_PlayerCombat>();
        stateMachine ??= GetComponent<WBH_PlayerStateMachine>();
    }

    private void ValidateRequiredReferences()
    {
        if (stats != null &&
            buffs != null &&
            health != null &&
            mana != null &&
            itemTriggers != null &&
            controller != null &&
            combat != null &&
            stateMachine != null)
        {
            return;
        }

        Debug.LogError(
            "[PlayerContext] Fighter의 필수 전투/상태 컴포넌트 참조가 비어 있습니다.",
            this);
    }

    private void ValidateOwnedReferences()
    {
        ValidateOwnedReference(inventory, nameof(inventory));
        ValidateOwnedReference(equipment, nameof(equipment));
        ValidateOwnedReference(wallet, nameof(wallet));
        ValidateOwnedReference(stats, nameof(stats));
        ValidateOwnedReference(buffs, nameof(buffs));
        ValidateOwnedReference(health, nameof(health));
        ValidateOwnedReference(mana, nameof(mana));
        ValidateOwnedReference(potions, nameof(potions));
        ValidateOwnedReference(itemTriggers, nameof(itemTriggers));
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
}

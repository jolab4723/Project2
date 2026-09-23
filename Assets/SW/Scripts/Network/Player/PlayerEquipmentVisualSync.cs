using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어가 서버에서 확정한 장착 무기 itemId를 모든 Client의 해당 플레이어 외형에 전달한다.
/// <para>원본 PlayerWeaponVisualPresenter는 같은 플레이어의 로컬 EquipmentSystem 이벤트만 읽으므로,
/// 소유권이 없는 원격 복제본에는 장비 모델이 없어 무기 교체를 알 수 없다.</para>
/// <para>이 테스트 복제본은 아이템 전체나 능력치를 복제하지 않고 외형 선택에 필요한 itemId만
/// SyncVar로 보내며, 실제 외형 생성과 해제는 기존 Presenter가 그대로 담당한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerContext), typeof(PlayerWeaponVisualPresenter))]
public sealed class PlayerEquipmentVisualSync : NetworkBehaviour
{
    [SerializeField] private PlayerContext context;
    [SerializeField] private PlayerWeaponVisualPresenter presenter;

    [SyncVar(hook = nameof(HandleWeaponItemIdChanged))]
    private string weaponItemId;

    public string WeaponItemId => weaponItemId;

    private void Awake()
    {
        ResolveReferences();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (context?.Equipment != null)
            context.Equipment.OnEquipmentChanged += HandleServerEquipmentChanged;

        RefreshServerWeaponItemId();
    }

    public override void OnStopServer()
    {
        if (context?.Equipment != null)
            context.Equipment.OnEquipmentChanged -= HandleServerEquipmentChanged;

        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        presenter?.ApplyAuthoritativeWeaponItemId(weaponItemId);
    }

    [Server]
    private void HandleServerEquipmentChanged(EquippedItemInfo[] _)
    {
        RefreshServerWeaponItemId();
    }

    [Server]
    private void RefreshServerWeaponItemId()
    {
        string nextItemId = null;
        if (context?.Equipment != null &&
            context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance weapon) &&
            weapon?.definition != null)
        {
            nextItemId = weapon.definition.itemId;
        }

        weaponItemId = nextItemId;
    }

    private void HandleWeaponItemIdChanged(string _, string nextItemId)
    {
        presenter?.ApplyAuthoritativeWeaponItemId(nextItemId);
    }

    private void ResolveReferences()
    {
        context ??= GetComponent<PlayerContext>();
        presenter ??= GetComponent<PlayerWeaponVisualPresenter>();
    }
}

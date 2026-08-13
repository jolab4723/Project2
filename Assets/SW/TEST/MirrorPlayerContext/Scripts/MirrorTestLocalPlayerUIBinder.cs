using ItemSystem;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mirror 테스트 씬의 클라이언트 전역 UI를 현재 로컬 <see cref="PlayerContext"/>에 연결한다.
/// 필드 아이템 획득 시에도 이 컴포넌트를 <see cref="IItemReceiver"/>로 사용해
/// 씬에 남아 있는 고정 InventoryController가 아니라 같은 로컬 Context의 Inventory로 전달한다.
/// 운영용 KY 입력 Manager는 복제하거나 수정하지 않고, 테스트 씬에서만 I/O/U/ESC로
/// 인벤토리·상점·강화 창을 여는 로컬 단축키 경계도 함께 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MirrorTestLocalPlayerUIBinder : MonoBehaviour, IItemReceiver
{
    [SerializeField] private MirrorTestNetworkManager networkManager;
    [SerializeField] private InventoryView inventoryView;
    [SerializeField] private InventoryPartView inventoryPartView;
    [SerializeField] private MirrorTestPlayerHud playerHud;

    private PlayerContext boundContext;
    private PlayerInventorySync_MirrorTest boundInventorySync;
    private NetworkShopState_MirrorTest boundShopState;

    public PlayerContext BoundContext => boundContext;

    private void OnEnable()
    {
        if (networkManager == null)
            networkManager = FindFirstObjectByType<MirrorTestNetworkManager>();

        if (networkManager == null || inventoryView == null || inventoryPartView == null)
        {
            Debug.LogError(
                "[MirrorTestLocalPlayerUIBinder] NetworkManager 또는 인벤토리 UI 참조가 비어 있습니다.",
                this);
            return;
        }

        networkManager.LocalPlayerContextChanged += HandleLocalPlayerChanged;
        HandleLocalPlayerChanged(networkManager.LocalPlayerContext);
    }

    private void OnDisable()
    {
        if (networkManager != null)
            networkManager.LocalPlayerContextChanged -= HandleLocalPlayerChanged;

        boundShopState?.UnbindLocalView(boundContext);
        boundShopState = null;
        boundInventorySync?.UnbindLocalInventoryView(inventoryView);
        boundInventorySync = null;
        inventoryView?.Unbind();
        playerHud?.Unbind();
        boundContext = null;
    }

    private void HandleLocalPlayerChanged(PlayerContext context)
    {
        boundShopState?.UnbindLocalView(boundContext);
        boundShopState = null;
        boundInventorySync?.UnbindLocalInventoryView(inventoryView);
        boundInventorySync = null;
        inventoryView.Unbind();
        playerHud?.Unbind();
        boundContext = null;

        if (context == null)
            return;

        if (!inventoryView.Bind(context))
        {
            Debug.LogError(
                "[MirrorTestLocalPlayerUIBinder] 로컬 PlayerContext UI Bind에 실패했습니다.",
                this);
            return;
        }

        boundContext = context;
        boundInventorySync = context.GetComponent<PlayerInventorySync_MirrorTest>();
        boundInventorySync?.BindLocalInventoryView(inventoryView);
        boundShopState = FindFirstObjectByType<NetworkShopState_MirrorTest>();
        boundShopState?.BindLocalView(context, inventoryView);
        playerHud?.Bind(context);
    }

    private void Update()
    {
        if (boundContext == null || inventoryPartView == null || Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            inventoryPartView.CloseAll();
        }
        else if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            inventoryPartView.ToggleInventory();
        }
        else if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            inventoryPartView.OpenShop();
        }
        else if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            inventoryPartView.OpenUpgrade();
        }
    }

    public bool AddItem(ItemInstance item)
    {
        if (boundContext?.Inventory != null)
            return boundContext.Inventory.AddItem(item);

        Debug.LogWarning(
            "[MirrorTestLocalPlayerUIBinder] 아이템을 받을 로컬 PlayerContext가 없습니다.",
            this);
        return false;
    }
}

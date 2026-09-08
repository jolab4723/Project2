using System.Reflection;
using ItemSystem;
using UnityEngine;
using UnityEngine.Events;
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
    private static readonly FieldInfo NpcClickedEventField =
        typeof(YJ_ClickNPC).GetField(
            "onClicked",
            BindingFlags.Instance | BindingFlags.NonPublic);

    [SerializeField] private MirrorTestNetworkManager networkManager;
    [SerializeField] private InventoryView inventoryView;
    [SerializeField] private InventoryPartView inventoryPartView;
    [SerializeField] private MirrorTestPlayerHud playerHud;
    [SerializeField] private WorldItemTooltipScanner worldItemScanner;
    [SerializeField] private NetworkUpgradeButton_MirrorTest upgradeButton;
    [SerializeField] private PlayerHudEventBridge_MirrorTest formalHudBridge;
    [SerializeField] private KY_StatusPopup_MirrorTest statusPopup;

    private PlayerContext boundContext;
    private PlayerInventorySync_MirrorTest boundInventorySync;
    private NetworkShopState_MirrorTest boundShopState;

    public PlayerContext BoundContext => boundContext;

    private void OnEnable()
    {
        if (networkManager == null)
            networkManager = FindFirstObjectByType<MirrorTestNetworkManager>();

        if (worldItemScanner == null)
            worldItemScanner = FindFirstObjectByType<WorldItemTooltipScanner>();

        if (upgradeButton == null && inventoryPartView != null)
        {
            upgradeButton =
                inventoryPartView.GetComponentInChildren<NetworkUpgradeButton_MirrorTest>(true);
        }

        formalHudBridge ??= FindFirstObjectByType<PlayerHudEventBridge_MirrorTest>(
            FindObjectsInactive.Include);
        statusPopup ??= FindFirstObjectByType<KY_StatusPopup_MirrorTest>(
            FindObjectsInactive.Include);

#if UNITY_EDITOR
        Canvas inventoryCanvas = inventoryView != null
            ? inventoryView.GetComponentInParent<Canvas>(true)
            : null;
        Debug.Assert(
            inventoryCanvas == null || inventoryCanvas.transform.localScale != Vector3.zero,
            "[MirrorTestLocalPlayerUIBinder] 인벤토리 Canvas 스케일이 0입니다.",
            this);
#endif

        if (networkManager == null || inventoryView == null || inventoryPartView == null)
        {
            Debug.LogError(
                "[MirrorTestLocalPlayerUIBinder] NetworkManager 또는 인벤토리 UI 참조가 비어 있습니다.",
                this);
            return;
        }

        networkManager.LocalPlayerContextChanged += HandleLocalPlayerChanged;
        HandleLocalPlayerChanged(networkManager.LocalPlayerContext);
        BindCampNpcWindows();
    }

    private void OnDisable()
    {
        if (networkManager != null)
            networkManager.LocalPlayerContextChanged -= HandleLocalPlayerChanged;

        // UnityEngine.Object는 파괴된 뒤 C# 참조가 남아 있어도 `obj != null` 비교에서는 null로 취급된다.
        // 반면 null 조건 연산자(`?.`)는 Unity의 이 판정을 거치지 않아 Scene 전환 중 파괴된 HUD를
        // 다시 호출할 수 있으므로, 해제 경계에서는 명시적인 Unity null 검사를 사용한다.
        if (boundShopState != null)
            boundShopState.UnbindLocalView(boundContext);
        boundShopState = null;
        if (boundInventorySync != null)
            boundInventorySync.UnbindLocalInventoryView(inventoryView);
        boundInventorySync = null;
        if (inventoryView != null)
            inventoryView.Unbind();
        if (upgradeButton != null)
            upgradeButton.Unbind();
        if (playerHud != null)
            playerHud.Unbind();
        if (formalHudBridge != null)
            formalHudBridge.Unbind();
        if (statusPopup != null)
        {
            statusPopup.Unbind();
            statusPopup.CloseImmediate();
        }
        if (worldItemScanner != null)
            worldItemScanner.BindPlayer(null);
        boundContext = null;
    }

    private void HandleLocalPlayerChanged(PlayerContext context)
    {
        boundShopState?.UnbindLocalView(boundContext);
        boundShopState = null;
        boundInventorySync?.UnbindLocalInventoryView(inventoryView);
        boundInventorySync = null;
        inventoryView.Unbind();
        upgradeButton?.Unbind();
        playerHud?.Unbind();
        formalHudBridge?.Unbind();
        statusPopup?.Unbind();
        statusPopup?.CloseImmediate();
        worldItemScanner?.BindPlayer(null);
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
        boundShopState = FindInBinderScene<NetworkShopState_MirrorTest>();
        boundShopState?.BindLocalView(context, inventoryView);
        upgradeButton?.Bind(context);
        playerHud?.Bind(context);
        formalHudBridge?.Bind(context);
        statusPopup?.Bind(context.Stats);
        worldItemScanner?.BindPlayer(context.transform);

        Debug.Assert(
            worldItemScanner == null || worldItemScanner.BoundPlayer == context.transform,
            "[MirrorTestLocalPlayerUIBinder] 월드 아이템 툴팁이 로컬 플레이어에 연결되지 않았습니다.",
            this);
    }

    private void Update()
    {
        EnsureSceneShopBinding();

        if (networkManager != null && networkManager.Chat.ConsumesInputThisFrame)
            return;

        if (boundContext == null || inventoryPartView == null || Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (statusPopup != null && statusPopup.IsOpen)
                statusPopup.Close();
            else
                inventoryPartView.CloseAll();
        }
        else if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            CloseStatusPopup();
            inventoryPartView.ToggleInventory();
        }
        else if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            CloseStatusPopup();
            inventoryPartView.OpenShop();
        }
        else if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            CloseStatusPopup();
            inventoryPartView.OpenUpgrade();
        }
        else if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            inventoryPartView.CloseAll();
            statusPopup?.Toggle();
        }
    }

    /// <summary>
    /// Mirror Scene 전환에서는 로컬 UI Binder가 먼저 활성화되고 새 NetworkShopState의 Spawn이
    /// 한두 프레임 뒤에 끝날 수 있다. 최초 Bind 때 상점 상태가 없었으면 같은 Scene의 상태가
    /// 준비될 때까지만 다시 찾아 연결하며, 이전 Scene의 파괴 대기 객체는 선택하지 않는다.
    /// </summary>
    private void EnsureSceneShopBinding()
    {
        if (boundContext == null || inventoryView == null)
            return;

        if (boundShopState != null && boundShopState.gameObject.scene == gameObject.scene)
            return;

        if (boundShopState != null)
            boundShopState.UnbindLocalView(boundContext);

        boundShopState = FindInBinderScene<NetworkShopState_MirrorTest>();
        boundShopState?.BindLocalView(boundContext, inventoryView);
    }

    /// <summary>
    /// production Camp를 복제한 Mirror Scene에서는 Shop/Upgrade NPC의 UnityEvent 대상이
    /// 복제 과정에서 끊어질 수 있다. 원본 NPC 스크립트와 Scene은 건드리지 않고, 현재 Scene의
    /// InventoryPartView를 런타임 Listener로 다시 연결한다.
    /// </summary>
    private void BindCampNpcWindows()
    {
        if (inventoryPartView == null)
            return;

        if (NpcClickedEventField == null)
        {
            Debug.LogError(
                "[MirrorTestLocalPlayerUIBinder] YJ_ClickNPC.onClicked를 찾지 못해 상점/강화 NPC를 연결할 수 없습니다.",
                this);
            return;
        }

        YJ_ClickNPC[] npcs = FindObjectsByType<YJ_ClickNPC>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (YJ_ClickNPC npc in npcs)
        {
            if (npc == null ||
                npc.gameObject.scene != gameObject.scene ||
                NpcClickedEventField.GetValue(npc) is not UnityEvent clickedEvent)
            {
                continue;
            }

            switch (npc.name)
            {
                case "ShopNPC":
                    AddNpcListenerIfMissing(
                        clickedEvent,
                        nameof(InventoryPartView.OpenShop),
                        inventoryPartView.OpenShop);
                    break;

                case "UpgradeNPC":
                    AddNpcListenerIfMissing(
                        clickedEvent,
                        nameof(InventoryPartView.OpenUpgrade),
                        inventoryPartView.OpenUpgrade);
                    break;
            }
        }
    }

    private void AddNpcListenerIfMissing(
        UnityEvent clickedEvent,
        string methodName,
        UnityAction listener)
    {
        for (int index = 0; index < clickedEvent.GetPersistentEventCount(); index++)
        {
            if (clickedEvent.GetPersistentTarget(index) == inventoryPartView &&
                clickedEvent.GetPersistentMethodName(index) == methodName)
            {
                return;
            }
        }

        clickedEvent.RemoveListener(listener);
        clickedEvent.AddListener(listener);
    }

    /// <summary>
    /// DontDestroyOnLoad와 이전 Scene의 종료 순서에 영향을 받지 않도록 이 Binder가 속한
    /// 현재 Scene의 컴포넌트만 반환한다.
    /// </summary>
    private T FindInBinderScene<T>() where T : Component
    {
        T[] candidates = FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (T candidate in candidates)
        {
            if (candidate != null && candidate.gameObject.scene == gameObject.scene)
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Mirror 테스트에서는 인벤토리 계열 창과 MergeTest 스탯창이 동시에 열리지 않도록 한다.
    /// 원본 KY 입력 Manager를 복제하지 않고 기존 로컬 UI 입력 경계에서만 창 우선순위를 정리한다.
    /// </summary>
    private void CloseStatusPopup()
    {
        if (statusPopup != null && statusPopup.IsOpen)
            statusPopup.Close();
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

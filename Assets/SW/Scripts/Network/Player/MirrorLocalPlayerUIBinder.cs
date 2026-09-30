using ItemSystem;
using UnityEngine;

/// <summary>
/// Mirror 테스트 씬의 클라이언트 전역 UI를 현재 로컬 <see cref="PlayerContext"/>에 연결한다.
/// 필드 아이템 획득 시에도 이 컴포넌트를 <see cref="IItemReceiver"/>로 사용해
/// 씬에 남아 있는 고정 InventoryController가 아니라 같은 로컬 Context의 Inventory로 전달한다.
/// 메뉴 단축키는 기존 KY 입력 Manager에 현재 Context와 메뉴 참조를 전달해 처리한다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class MirrorLocalPlayerUIBinder : MonoBehaviour, IItemReceiver
{
    [SerializeField] private MirrorNetworkManager networkManager;
    [SerializeField] private KY_PopupManager popupManager;
    [SerializeField] private KY_UIInputManager uiInputManager;
    private MirrorSpawnedPlayerBinder boundPlayerBinder;
    private WBH_PlayerInputHandler boundPlayerInput;
    private PlayerActionInputHandler boundActionInput;
    private KY_PausePopup pausePopup;
    [SerializeField] private InventoryView inventoryView;
    [SerializeField] private InventoryPartView inventoryPartView;
    [SerializeField] private WorldItemTooltipScanner worldItemScanner;
    [SerializeField] private NetworkUpgradeButton upgradeButton;
    [SerializeField] private NetworkShopRerollButton shopRerollButton;
    [SerializeField] private PlayerHudEventBridge formalHudBridge;
    [SerializeField] private KY_StatusPopup statusPopup;
    [SerializeField] private SkillPopupController skillPopup;
    private FighterSkillAuthority boundSkills;

    private PlayerContext boundContext;
    private PlayerInventorySync boundInventorySync;
    private NetworkShopState boundShopState;
    private YJ_LanguageManager languageManager;

    public PlayerContext BoundContext => boundContext;

    private void OnEnable()
    {
        popupManager ??= FindInBinderScene<KY_PopupManager>();
        uiInputManager ??= FindInBinderScene<KY_UIInputManager>();
        skillPopup ??= FindInBinderScene<SkillPopupController>();
        if (networkManager == null)
            networkManager = FindFirstObjectByType<MirrorNetworkManager>();
        pausePopup = FindInBinderScene<KY_PausePopup>();
        ConfigurePauseMenu(pausePopup, networkManager);
        languageManager = YJ_LanguageManager.Instance;
        if (languageManager != null) languageManager.LanguageChanged += RefreshSessionLanguage;

        if (worldItemScanner == null)
            worldItemScanner = FindFirstObjectByType<WorldItemTooltipScanner>();

        if (upgradeButton == null && inventoryPartView != null)
        {
            upgradeButton =
                inventoryPartView.GetComponentInChildren<NetworkUpgradeButton>(true);
        }
        if (shopRerollButton == null && inventoryPartView != null)
            shopRerollButton = inventoryPartView.GetComponentInChildren<NetworkShopRerollButton>(true);

        formalHudBridge ??= FindInBinderScene<PlayerHudEventBridge>();
        statusPopup ??= FindInBinderScene<KY_StatusPopup>();
        BindMenuInput();

#if UNITY_EDITOR
        Canvas inventoryCanvas = inventoryView != null
            ? inventoryView.GetComponentInParent<Canvas>(true)
            : null;
        Debug.Assert(
            inventoryCanvas == null || inventoryCanvas.transform.localScale != Vector3.zero,
            "[MirrorLocalPlayerUIBinder] 인벤토리 Canvas 스케일이 0입니다.",
            this);
#endif

        if (networkManager == null || inventoryView == null || inventoryPartView == null)
        {
            Debug.LogError(
                "[MirrorLocalPlayerUIBinder] NetworkManager 또는 인벤토리 UI 참조가 비어 있습니다.",
                this);
            return;
        }

        networkManager.LocalPlayerContextChanged += HandleLocalPlayerChanged;
        networkManager.RunSnapshotChanged += RefreshLocation;
        RefreshLocation(0);
        HandleLocalPlayerChanged(networkManager.LocalPlayerContext);
    }

    private void OnDisable()
    {
        if (languageManager != null) languageManager.LanguageChanged -= RefreshSessionLanguage;
        if (uiInputManager != null) uiInputManager.Unbind(IsChatInputConsumed);
        UnbindPlayerInputConsumption();
        if (pausePopup != null) pausePopup.UnbindExitPresentation();
        UnbindSkills();
        if (boundPlayerBinder != null) boundPlayerBinder.SetMenuInputBlocked(false);
        boundPlayerBinder = null;
        if (networkManager != null)
        {
            networkManager.LocalPlayerContextChanged -= HandleLocalPlayerChanged;
            networkManager.RunSnapshotChanged -= RefreshLocation;
        }

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
        if (shopRerollButton != null)
            shopRerollButton.Unbind();
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

    internal static void ConfigurePauseMenu(KY_PausePopup popup, MirrorNetworkManager session)
    {
        if (popup == null || session == null) return;
        bool host = Mirror.NetworkServer.active;
        var labels = Resources.Load<UILabelDatabaseSO>(SessionUIMessageLocalizer.DatabasePath);
        string Text(string source) => SessionUIMessageLocalizer.GetMessage(labels, source);

        // 확인 팝업 문구는 공용 다국어 DB에서 읽는다. DB가 없으면 기존 한국어 문구를 쓴다.
        // 세션 UI를 연결하거나 언어가 바뀔 때 확인 문구와 버튼 표시를 함께 갱신한다.
        // 키가 DB에 없으면(GetLabel이 키를 그대로 반환) 세션 메시지 변환기로 원문을 현지화한다.
        string Label(string key, string fallback)
        {
            string label = labels != null ? labels.GetLabel(key) : null;
            return string.IsNullOrEmpty(label) || label == key ? Text(fallback) : label;
        }

        popup.BindExitActions(new KY_DialogData
        {
            message = host
                ? Label("pause_ui.host_end_confirm", "호스트 세션을 종료하시겠습니까?")
                : Label("pause_ui.leave_confirm", "이 세션에서 떠나시겠습니까?"),
            warningText = host
                ? Label("pause_ui.host_end_warning", "모든 참가자의 연결과 현재 런이 종료됩니다.")
                : Label("pause_ui.leave_warning", "현재 참가 자격을 포기하며 이 런에 재접속할 수 없습니다."),
            onYes = () => { if (session != null) session.RequestLeaveSession(); }
        }, new KY_DialogData
        {
            message = Label("pause_ui.temp_leave_confirm", "잠시 세션에서 나가시겠습니까?"),
            warningText = Label("pause_ui.temp_leave_warning", "서버가 유지되는 동안 5분 안에 재접속할 수 있습니다. 파티의 게임은 계속됩니다."),
            onYes = () => { if (session != null && !Mirror.NetworkServer.active) session.StopClient(); }
        });
        popup.BindExitPresentation(
            Mirror.NetworkClient.active,
            Mirror.NetworkClient.active && !host,
            Text(host ? "호스트 세션 종료" : "세션 떠나기"),
            Text("잠시 나가기"),
            YJ_LanguageManager.Instance?.GetCurrentFont());
    }

    private void RefreshSessionLanguage(GameLanguage _) =>
        ConfigurePauseMenu(pausePopup, networkManager);

    private void HandleLocalPlayerChanged(PlayerContext context)
    {
        UnbindSkills();
        UnbindPlayerInputConsumption();
        if (boundPlayerBinder != null) boundPlayerBinder.SetMenuInputBlocked(false);
        boundPlayerBinder = null;
        boundShopState?.UnbindLocalView(boundContext);
        boundShopState = null;
        boundInventorySync?.UnbindLocalInventoryView(inventoryView);
        boundInventorySync = null;
        inventoryView.Unbind();
        upgradeButton?.Unbind();
        if (shopRerollButton != null) shopRerollButton.Unbind();
        formalHudBridge?.Unbind();
        statusPopup?.Unbind();
        statusPopup?.CloseImmediate();
        worldItemScanner?.BindPlayer(null);
        boundContext = null;
        BindMenuInput();

        if (context == null)
            return;

        if (!inventoryView.Bind(context))
        {
            Debug.LogError(
                "[MirrorLocalPlayerUIBinder] 로컬 PlayerContext UI Bind에 실패했습니다.",
                this);
            return;
        }

        boundContext = context;
        boundPlayerBinder = context.GetComponent<MirrorSpawnedPlayerBinder>();
        boundInventorySync = context.GetComponent<PlayerInventorySync>();
        boundInventorySync?.BindLocalInventoryView(inventoryView);
        boundShopState = inventoryView.HasShop ? FindInBinderScene<NetworkShopState>() : null;
        boundShopState?.BindLocalView(context, inventoryView);
        upgradeButton?.Bind(context);
        if (shopRerollButton != null) shopRerollButton.Bind(context);
        formalHudBridge?.Bind(context);
        statusPopup?.Bind(context.Stats);
        worldItemScanner?.BindPlayer(context.transform);
        boundSkills = context.GetComponent<FighterSkillAuthority>();
        if (boundSkills != null) boundSkills.SkillStateChanged += RefreshSkills;
        RefreshSkills();
        BindMenuInput();
        boundPlayerInput = context.GetComponent<WBH_PlayerInputHandler>();
        boundActionInput = context.GetComponent<PlayerActionInputHandler>();
        if (boundPlayerInput != null) boundPlayerInput.BindInputConsumption(IsChatInputConsumed);
        if (boundActionInput != null) boundActionInput.BindInputConsumption(IsChatInputConsumed);

        Debug.Assert(
            worldItemScanner == null || worldItemScanner.BoundPlayer == context.transform,
            "[MirrorLocalPlayerUIBinder] 월드 아이템 툴팁이 로컬 플레이어에 연결되지 않았습니다.",
            this);
    }

    private void Update()
    {
        EnsureSceneShopBinding();
        RefreshMenuInputBlock();
    }

    private void BindMenuInput()
    {
        if (uiInputManager != null)
            uiInputManager.Bind(boundContext, inventoryPartView, statusPopup, popupManager, IsChatInputConsumed);
    }

    private bool IsChatInputConsumed() =>
        networkManager != null && networkManager.Chat != null && networkManager.Chat.ConsumesInputThisFrame;

    private void UnbindPlayerInputConsumption()
    {
        if (boundPlayerInput != null) boundPlayerInput.BindInputConsumption(null);
        if (boundActionInput != null) boundActionInput.BindInputConsumption(null);
        boundPlayerInput = null;
        boundActionInput = null;
    }

    private void RefreshSkills()
    {
        if (skillPopup != null) skillPopup.Bind(boundSkills);
    }

    private void UnbindSkills()
    {
        if (boundSkills != null) boundSkills.SkillStateChanged -= RefreshSkills;
        boundSkills = null;
        if (skillPopup != null) skillPopup.Bind(null);
    }

    /// <summary>씬의 위치 표시는 개인 저장이 아닌 현재 서버 진행 상태를 사용한다.</summary>
    private void RefreshLocation(uint _)
    {
        if (networkManager == null || formalHudBridge == null ||
            !networkManager.TryGetRunSnapshot(out StageMapSaveData run) ||
            !networkManager.TryGetPendingStageNode(out StageNodeSaveData node)) return;
        foreach (var view in formalHudBridge.GetComponentsInChildren<KY_LocationView>(true))
            view.BindLocation((int)run.act, node.floor, node.type == StageNodeType.Boss, node.type == StageNodeType.Camp);
    }

    /// <summary>메뉴가 열려 있는 동안 이 컴퓨터의 플레이어 입력만 차단한다. 서버 시간과 다른 플레이어는 유지한다.</summary>
    private void RefreshMenuInputBlock()
    {
        if (boundPlayerBinder == null) return;
        boundPlayerBinder.SetMenuInputBlocked(
            (popupManager != null && popupManager.HasOpenModalPopup) ||
            HasOpenSidePopup() ||
            (inventoryPartView != null && inventoryPartView.HasOpenWindow) ||
            (statusPopup != null && statusPopup.IsOpen) ||
            (QuestOfferUI.Instance != null && QuestOfferUI.Instance.IsShowing));
    }

    private bool HasOpenSidePopup()
    {
        if (popupManager == null || popupManager.popupEntries == null)
            return false;

        // 비활성 모드의 팝업은 제외하고, 닫힘 연출 중 화면에 남은 사이드 팝업도 잠금에 포함한다.
        foreach (KY_PopupManager.PopupEntry entry in popupManager.popupEntries)
        {
            if (entry == null || entry.popup == null || !entry.popup.gameObject.activeInHierarchy)
                continue;

            switch (entry.type)
            {
                case PopupType.Inventory:
                case PopupType.Skill:
                case PopupType.Status:
                case PopupType.Quest:
                case PopupType.Buff:
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Mirror Scene 전환에서는 로컬 UI Binder가 먼저 활성화되고 새 NetworkShopState의 Spawn이
    /// 한두 프레임 뒤에 끝날 수 있다. 최초 Bind 때 상점 상태가 없었으면 같은 Scene의 상태가
    /// 준비될 때까지만 다시 찾아 연결하며, 이전 Scene의 파괴 대기 객체는 선택하지 않는다.
    /// </summary>
    private void EnsureSceneShopBinding()
    {
        // 전투 씬에는 상점이 없다. 캠프 UI가 연결된 경우에만 서버 Spawn을 기다린다.
        if (boundContext == null || inventoryView == null || !inventoryView.HasShop)
            return;

        if (boundShopState != null && boundShopState.gameObject.scene == gameObject.scene)
            return;

        if (boundShopState != null)
            boundShopState.UnbindLocalView(boundContext);

        boundShopState = FindInBinderScene<NetworkShopState>();
        boundShopState?.BindLocalView(boundContext, inventoryView);
    }

    /// <summary>
    /// DontDestroyOnLoad와 이전 Scene의 종료 순서에 영향을 받지 않도록 이 Binder가 속한
    /// 현재 Scene의 컴포넌트만 반환한다.
    /// </summary>
    private T FindInBinderScene<T>() where T : Component
    {
        // SW 수정: 공용 씬의 싱글·멀티 UI 중 현재 모드 쪽을 우선 선택한다.
        return MirrorSceneMode.FindInActiveMode<T>(gameObject.scene);
    }

    public bool AddItem(ItemInstance item)
    {
        if (boundContext?.Inventory != null)
            return boundContext.Inventory.AddItem(item);

        Debug.LogWarning(
            "[MirrorLocalPlayerUIBinder] 아이템을 받을 로컬 PlayerContext가 없습니다.",
            this);
        return false;
    }
}

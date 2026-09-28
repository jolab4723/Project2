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
/// 인벤토리·퀘스트·강화 창을 여는 로컬 단축키 경계도 함께 담당한다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class MirrorLocalPlayerUIBinder : MonoBehaviour, IItemReceiver
{
    private static readonly FieldInfo NpcClickedEventField =
        typeof(YJ_ClickNPC).GetField(
            "onClicked",
            BindingFlags.Instance | BindingFlags.NonPublic);

    [SerializeField] private MirrorNetworkManager networkManager;
    [SerializeField] private KY_PopupManager popupManager;
    private MirrorSpawnedPlayerBinder boundPlayerBinder;
    [SerializeField] private InventoryView inventoryView;
    [SerializeField] private InventoryPartView inventoryPartView;
    [SerializeField] private WorldItemTooltipScanner worldItemScanner;
    [SerializeField] private NetworkUpgradeButton upgradeButton;
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
        skillPopup ??= FindInBinderScene<SkillPopupController>();
        if (networkManager == null)
            networkManager = FindFirstObjectByType<MirrorNetworkManager>();
        ConfigurePauseMenu(FindInBinderScene<KY_PausePopup>(), networkManager);
        languageManager = YJ_LanguageManager.Instance;
        if (languageManager != null) languageManager.LanguageChanged += RefreshSessionLanguage;

        if (worldItemScanner == null)
            worldItemScanner = FindFirstObjectByType<WorldItemTooltipScanner>();

        if (upgradeButton == null && inventoryPartView != null)
        {
            upgradeButton =
                inventoryPartView.GetComponentInChildren<NetworkUpgradeButton>(true);
        }

        formalHudBridge ??= FindInBinderScene<PlayerHudEventBridge>();
        statusPopup ??= FindInBinderScene<KY_StatusPopup>();

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
        BindCampNpcWindows();
    }

    /// <summary>
    /// MirrorSceneMode가 멀티 객체를 순서대로 켜므로 OnEnable 시점에는 멀티 NPC가 아직 꺼져 있을 수 있다.
    /// 모든 활성화가 끝난 뒤 한 번 더 연결한다(리스너 추가는 중복 없이 멱등).
    /// </summary>
    private void Start()
    {
        BindCampNpcWindows();
    }

    private void OnDisable()
    {
        if (languageManager != null) languageManager.LanguageChanged -= RefreshSessionLanguage;
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
        popup.PauseGameTime = false;

        // 확인 팝업 문구는 공용 다국어 DB에서 읽는다. DB가 없으면 기존 한국어 문구를 쓴다.
        // 문구는 이 시점(세션 UI 연결)에 정해지므로, 세션 도중 언어를 바꾸면 다음 연결부터 반영된다.
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
        foreach (var button in popup.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            bool leave = button.name == "Giveup";
            if (!leave && button.name != "Save") continue;
            button.gameObject.SetActive(Mirror.NetworkClient.active && (leave || !host));
            var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null)
            {
                // 세션별 동적 문구를 싱글용 고정 라벨의 Awake가 덮어쓰지 않게 한다.
                if (label.TryGetComponent<UILabelText>(out var fixedLabel)) Destroy(fixedLabel);
                label.text = Text(leave ? (host ? "호스트 세션 종료" : "세션 떠나기") : "잠시 나가기");
                var font = YJ_LanguageManager.Instance?.GetCurrentFont();
                if (font != null) label.font = font;
            }
        }
    }

    private void RefreshSessionLanguage(GameLanguage _) =>
        ConfigurePauseMenu(FindInBinderScene<KY_PausePopup>(), networkManager);

    private void HandleLocalPlayerChanged(PlayerContext context)
    {
        UnbindSkills();
        if (boundPlayerBinder != null) boundPlayerBinder.SetMenuInputBlocked(false);
        boundPlayerBinder = null;
        boundShopState?.UnbindLocalView(boundContext);
        boundShopState = null;
        boundInventorySync?.UnbindLocalInventoryView(inventoryView);
        boundInventorySync = null;
        inventoryView.Unbind();
        upgradeButton?.Unbind();
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
                "[MirrorLocalPlayerUIBinder] 로컬 PlayerContext UI Bind에 실패했습니다.",
                this);
            return;
        }

        boundContext = context;
        boundPlayerBinder = context.GetComponent<MirrorSpawnedPlayerBinder>();
        boundInventorySync = context.GetComponent<PlayerInventorySync>();
        boundInventorySync?.BindLocalInventoryView(inventoryView);
        boundShopState = FindInBinderScene<NetworkShopState>();
        boundShopState?.BindLocalView(context, inventoryView);
        upgradeButton?.Bind(context);
        formalHudBridge?.Bind(context);
        statusPopup?.Bind(context.Stats);
        worldItemScanner?.BindPlayer(context.transform);
        boundSkills = context.GetComponent<FighterSkillAuthority>();
        if (boundSkills != null) boundSkills.SkillStateChanged += RefreshSkills;
        RefreshSkills();

        Debug.Assert(
            worldItemScanner == null || worldItemScanner.BoundPlayer == context.transform,
            "[MirrorLocalPlayerUIBinder] 월드 아이템 툴팁이 로컬 플레이어에 연결되지 않았습니다.",
            this);
    }

    private void Update()
    {
        EnsureSceneShopBinding();
        RefreshMenuInputBlock();

        if (networkManager != null && networkManager.Chat.ConsumesInputThisFrame)
            return;

        if (boundContext == null || inventoryPartView == null || Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (popupManager != null && popupManager.HasOpenModalPopup)
                popupManager.Hide();
            else if (statusPopup != null && statusPopup.IsOpen)
                statusPopup.Close();
            else if (inventoryPartView.HasOpenWindow)
                inventoryPartView.CloseAll();
            else if (QuestOfferUI.Instance != null && QuestOfferUI.Instance.IsShowing)
                QuestOfferUI.Instance.Hide();
            else if (popupManager != null)
                popupManager.Show(PopupType.Pause);
        }
        else if (popupManager != null && popupManager.HasOpenModalPopup)
            return;
        else if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            CloseStatusPopup();
            inventoryPartView.ToggleInventory();
        }
        else if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            inventoryPartView.CloseAll();
            CloseStatusPopup();
            if (popupManager != null) popupManager.Show(PopupType.Quest);
        }
        else if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            CloseStatusPopup();
            inventoryPartView.OpenUpgrade();
        }
        else if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            inventoryPartView.CloseAll();
            if (statusPopup != null)
            {
                if (statusPopup.IsOpen) statusPopup.Close(); else statusPopup.Open();
            }
        }
        else if (Keyboard.current.kKey.wasPressedThisFrame && popupManager != null)
        {
            inventoryPartView.CloseAll();
            CloseStatusPopup();
            popupManager.Show(PopupType.Skill);
        }
        RefreshMenuInputBlock();
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
            (inventoryPartView != null && inventoryPartView.HasOpenWindow) ||
            (statusPopup != null && statusPopup.IsOpen));
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

        boundShopState = FindInBinderScene<NetworkShopState>();
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
                "[MirrorLocalPlayerUIBinder] YJ_ClickNPC.onClicked를 찾지 못해 상점/강화 NPC를 연결할 수 없습니다.",
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
        // SW 수정: 공용 씬의 싱글·멀티 UI 중 현재 모드 쪽을 우선 선택한다.
        return MirrorSceneMode.FindInActiveMode<T>(gameObject.scene);
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
            "[MirrorLocalPlayerUIBinder] 아이템을 받을 로컬 PlayerContext가 없습니다.",
            this);
        return false;
    }
}

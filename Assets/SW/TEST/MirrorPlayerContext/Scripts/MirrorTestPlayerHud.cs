using System.Collections.Generic;
using ItemSystem;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Mirror PlayerContext 테스트 전용 HUD다.
/// 기존 HP/MP 표시와 함께 로컬 Context, 플레이어별 Inventory 인스턴스,
/// 실제 ItemInstance 및 소유권 이벤트를 한 화면에서 확인한다.
/// <para>3-1 차이: 로컬 플레이어의 Mirror 인벤토리 요청 결과를 구독하고,
/// 서버 상태 변경 번호와 처리 대기 요청 수를 진단 패널에 표시한다.</para>
/// <para>3-2 차이: 실제 아이템 드래그 결과도 같은 요청 번호 로그로 표시한다.</para>
/// </summary>
/// <para>3-3 차이: 그리드 이동·회전 요청 결과를 같은 요청 번호 로그에 표시하고,
/// 로컬 및 서버 상황실에서 각 아이템의 셀 좌표와 회전 상태를 직접 비교할 수 있다.</para>
/// <para>3-4 차이: 장착·해제·교환 결과와 서버 장비 슬롯 상태를 함께 표시하며,
/// 어려운 네트워크 용어는 초보자도 읽을 수 있는 한국어 설명으로 바꾼다.</para>
/// <para>3-5 차이: 모든 플레이어가 공유하는 상점 재고, 무료·유료 리롤 사용량,
/// 가장 높은 상점 강화 적용자와 플레이어별 골드·할인을 종합상황실에서 확인한다.</para>
/// <para>3-6 차이: 서버가 확정한 최종 Stat·Buff·HP·MP·포션 상태를 각 플레이어 복제본에 적용하고,
/// 로컬 화면과 서버 종합상황실에서 같은 상태 번호와 전투 수치를 비교한다.</para>
/// <para>4단계 차이: 플레이어별 공격 요청·사망 상태와 서버 적의 체력·타깃·마지막 공격자·보상 횟수를 표시하고,
/// 서버 상황판에서 사망한 플레이어를 수동 부활시킨다.</para>
/// <para>5-A 보완: 녹화 중에는 작은 열기 버튼만 남기고 종합상황실 본문을 접을 수 있다.
/// 표시만 숨기며 PlayerContext 바인딩과 진단 이벤트 수집은 계속 유지한다.</para>
/// <para>6-A 보완: Client·Server 빌드 호환 확인 결과와 전투 세션의 대기·진행·완료 상태를 표시한다.
/// 대기 상태에서는 호환 확인과 로컬 PlayerContext 생성이 끝난 Client가 서버에 시작을 요청할 수 있다.</para>
/// <para>6-C 보완: 테스트 전용 선택 화면에서 Camp 또는 전투를 요청하고, 두 플레이 Scene에서 다시
/// 선택 화면으로 돌아오는 왕복 버튼과 로컬 조작 복구 상태를 같은 종합상황실에서 확인한다.</para>
[DisallowMultipleComponent]
public sealed class MirrorTestPlayerHud : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider manaSlider;
    [SerializeField] private TMP_Text playerText;
    [SerializeField] private TMP_Text potionText;
    [SerializeField] private TMP_Text buffText;

    [Header("Player separation test")]
    [SerializeField] private ItemDefinitionSO[] distinctTestItems;
    [SerializeField] private bool showDiagnostics = true;

    private PlayerContext context;
    private PlayerInventorySync_MirrorTest inventorySync;
    private NetworkShopPlayerState_MirrorTest shopPlayerState;
    private PlayerRuntimeStateSync_MirrorTest runtimeState;
    private readonly Queue<string> ownershipEvents = new();
    private int displayedLevel = -1;
    private float displayedExp = float.NaN;
    private float displayedRequiredExp = float.NaN;
    private GUIStyle titleStyle;
    private GUIStyle localStyle;
    private GUIStyle remoteStyle;
    private GUIStyle passStyle;
    private GUIStyle failStyle;
    private Vector2 diagnosticsScroll;
    private bool diagnosticsCollapsed;

    public PlayerContext BoundContext => context;

    public void Bind(PlayerContext newContext)
    {
        Unbind();
        context = newContext;

        if (context == null)
            return;

        inventorySync = context.GetComponent<PlayerInventorySync_MirrorTest>();
        if (inventorySync != null)
            inventorySync.RequestCompleted += HandleInventoryRequestCompleted;

        shopPlayerState = context.GetComponent<NetworkShopPlayerState_MirrorTest>();
        if (shopPlayerState != null)
            shopPlayerState.RequestCompleted += HandleShopRequestCompleted;

        runtimeState = context.RuntimeState;
        if (runtimeState != null)
            runtimeState.StateApplied += RefreshAll;

        context.Health.OnHealthChanged += RefreshHealth;
        context.Mana.OnManaChanged += RefreshMana;
        context.Buffs.OnBuffsChanged += RefreshBuffs;
        context.Potions.ChargesChanged += RefreshPotions;
        context.Equipment.OnEquipmentChanged += HandleEquipmentChanged;
        context.Inventory.OnItemAdded += HandleItemAdded;
        context.Inventory.OnItemRemoved += HandleItemRemoved;
        context.Inventory.OnItemOwnershipGained += HandleOwnershipGained;
        context.Inventory.OnItemOwnershipLost += HandleOwnershipLost;
        if (context.Stats.Stat != null)
            context.Stats.Stat.OnStatChanged += RefreshPlayer;

        RefreshAll();

        // 기본 테스트 아이템은 PlayerInventorySync_MirrorTest.OnStartServer에서 플레이어당 한 번만 지급한다.
        // Scene UI Bind 시점에는 장착 중인 아이템이 인벤토리 Grid 목록에서 빠져 있으므로,
        // Grid가 비었다는 이유로 다시 지급하면 Scene 왕복마다 기본 투구가 중복된다.
    }

    public void Unbind()
    {
        if (context == null)
            return;

        if (inventorySync != null)
            inventorySync.RequestCompleted -= HandleInventoryRequestCompleted;
        if (shopPlayerState != null)
            shopPlayerState.RequestCompleted -= HandleShopRequestCompleted;
        if (runtimeState != null)
            runtimeState.StateApplied -= RefreshAll;

        context.Health.OnHealthChanged -= RefreshHealth;
        context.Mana.OnManaChanged -= RefreshMana;
        context.Buffs.OnBuffsChanged -= RefreshBuffs;
        context.Potions.ChargesChanged -= RefreshPotions;
        context.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        context.Inventory.OnItemAdded -= HandleItemAdded;
        context.Inventory.OnItemRemoved -= HandleItemRemoved;
        context.Inventory.OnItemOwnershipGained -= HandleOwnershipGained;
        context.Inventory.OnItemOwnershipLost -= HandleOwnershipLost;
        if (context.Stats.Stat != null)
            context.Stats.Stat.OnStatChanged -= RefreshPlayer;
        context = null;
        inventorySync = null;
        shopPlayerState = null;
        runtimeState = null;
        ownershipEvents.Clear();
        RefreshAll();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Update()
    {
        if (context == null ||
            (displayedLevel == context.Stats.CurrentLevel &&
             Mathf.Approximately(displayedExp, context.Stats.CurrentExp) &&
             Mathf.Approximately(displayedRequiredExp, context.Stats.ExpToNextLevel)))
        {
            return;
        }

        RefreshPlayer();
    }

    private void RefreshAll()
    {
        RefreshHealth();
        RefreshMana();
        RefreshBuffs();

        if (context != null)
        {
            RefreshPotions(context.Potions.CurrentCharges, context.Potions.MaxCharges);
            RefreshPlayer();
        }
        else
        {
            if (playerText != null)
                playerText.text = "내 플레이어를 기다리는 중";
            if (potionText != null)
                potionText.text = "포션 -/-";
        }
    }

    private void RefreshHealth()
    {
        if (healthSlider == null)
            return;

        healthSlider.maxValue = context != null ? context.Health.MaxHealth : 1f;
        healthSlider.value = context != null ? context.Health.CurrentHealth : 0f;
    }

    private void RefreshMana()
    {
        if (manaSlider == null)
            return;

        manaSlider.maxValue = context != null ? context.Mana.MaxMana : 1f;
        manaSlider.value = context != null ? context.Mana.CurrentMana : 0f;
    }

    private void RefreshBuffs()
    {
        if (buffText != null)
            buffText.text = context != null ? $"버프 {context.Buffs.ActiveBuffs.Count}" : "버프 0";
    }

    private void RefreshPotions(int current, int max)
    {
        if (potionText != null)
            potionText.text = $"포션 {current}/{max}";
    }

    private void RefreshPlayer()
    {
        if (playerText == null || context == null)
            return;

        float requiredExp = context.Stats.ExpToNextLevel;
        displayedLevel = context.Stats.CurrentLevel;
        displayedExp = context.Stats.CurrentExp;
        displayedRequiredExp = requiredExp;

        string expText = requiredExp > 0f
            ? $"경험치 {context.Stats.CurrentExp:0}/{requiredExp:0}"
            : "최대 레벨";

        playerText.text = $"{context.name}  레벨 {context.Stats.CurrentLevel}  {expText}";
    }

    [ContextMenu("Mirror Test/Grant distinct local item")]
    public bool GrantDistinctTestItem()
    {
        if (inventorySync != null && NetworkClient.active)
        {
            bool requested = inventorySync.RequestGrantDistinctTestItem();
            AddEvent(requested ? "서버에 구분용 아이템 지급 요청" : "서버 지급 요청 실패");
            return requested;
        }

        if (context?.Inventory == null || distinctTestItems == null || distinctTestItems.Length == 0)
        {
            AddEvent("지급 실패: Context 또는 테스트 아이템 없음");
            return false;
        }

        NetworkIdentity identity = context.GetComponent<NetworkIdentity>();
        uint netId = identity != null ? identity.netId : 0;
        int itemIndex = (int)(netId > 0 ? (netId - 1) % (uint)distinctTestItems.Length : 0);
        ItemDefinitionSO definition = distinctTestItems[itemIndex];

        if (definition == null)
        {
            AddEvent($"지급 실패: 테스트 아이템 {itemIndex + 1}번이 비었음");
            return false;
        }

        ItemInstance item = ItemDataCreator.CreateItemData(definition);
        bool added = ItemAcquisition.Acquire(item, context.Inventory);
        AddEvent(added
            ? $"플레이어 {itemIndex + 1} 실제 아이템 지급: {definition.itemName}"
            : $"지급 실패: {definition.itemName}");
        return added;
    }

    private void HandleItemAdded(InventoryItem item)
    {
        AddItemEvent("인벤토리에 추가", item);
    }

    private void HandleItemRemoved(InventoryItem item)
    {
        AddItemEvent("인벤토리에서 빠짐", item);
    }

    private void HandleOwnershipGained(InventoryItem item)
    {
        AddItemEvent("소유권 획득", item);
    }

    private void HandleOwnershipLost(InventoryItem item)
    {
        AddItemEvent("소유권 해제", item);
    }

    private void HandleEquipmentChanged(EquippedItemInfo[] items)
    {
        AddEvent($"장비 변경: {items?.Length ?? 0}개 장착 중");
    }

    private void HandleInventoryRequestCompleted(MirrorTestInventoryRequestCompleted completed)
    {
        AddEvent(
            $"서버 요청 #{completed.RequestId} {GetOperationLabel(completed.Operation)}: " +
            $"{GetResultLabel(completed.Result)} | 상태 번호 " +
            $"{completed.RequestedRevision}->{completed.AuthoritativeRevision}");
    }

    private void HandleShopRequestCompleted(MirrorTestShopRequestCompleted completed)
    {
        AddEvent(
            $"상점 요청 #{completed.RequestId} {GetShopOperationLabel(completed.Operation)}: " +
            $"{GetShopResultLabel(completed.Result)} | 상점 상태 번호 " +
            $"{completed.RequestedShopRevision}->{completed.AuthoritativeShopRevision}");
    }

    private void AddItemEvent(string action, InventoryItem item)
    {
        ItemInstance data = item?.itemData;
        string name = data?.definition != null ? data.definition.itemName : "알 수 없는 아이템";
        AddEvent($"{action}: {name} [{ShortId(data?.instanceId)}]");
    }

    private void AddEvent(string message)
    {
        ownershipEvents.Enqueue(message);
        while (ownershipEvents.Count > 6)
            ownershipEvents.Dequeue();
    }

    private void OnGUI()
    {
        if (!showDiagnostics)
            return;

        EnsureGuiStyles();

        Rect toggleArea = new(12f, 30f, 120f, 28f);
        if (GUI.Button(toggleArea, diagnosticsCollapsed ? "상황실 열기" : "상황실 접기"))
            diagnosticsCollapsed = !diagnosticsCollapsed;

        if (diagnosticsCollapsed)
            return;

        float width = Mathf.Min(460f, Screen.width - 24f);
        Rect area = new(12f, 64f, width, Screen.height - 76f);
        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Label("PLAYER CONTEXT MIRROR 테스트", titleStyle);
        GUILayout.Label("I 인벤토리 | O 상점 | U 강화 | ESC 닫기");
        DrawCompatibilityAndSession();

        if (context == null)
        {
            GUILayout.Label("내 PlayerContext를 기다리는 중");
            GUILayout.EndArea();
            return;
        }

        diagnosticsScroll = GUILayout.BeginScrollView(diagnosticsScroll);
        DrawContext(context, true);

        if (inventorySync != null)
        {
            GUILayout.Label(
                $"내 동기화 상태 번호={inventorySync.StateRevision} | " +
                $"서버 처리 대기={inventorySync.PendingRequestCount}건",
                inventorySync.PendingRequestCount == 0 ? passStyle : localStyle);
        }

        if (runtimeState != null)
        {
            GUILayout.Label(
                $"내 전투 상태 번호={runtimeState.StateRevision} | " +
                $"HP={runtimeState.CurrentHealth:0}/{runtimeState.MaxHealth:0} | " +
                $"MP={runtimeState.CurrentMana:0}/{runtimeState.MaxMana:0} | " +
                $"생존={(runtimeState.IsDead ? "사망" : "생존")} | " +
                $"버프={runtimeState.ActiveBuffCount}개 | 포션={runtimeState.PotionCharges}/{runtimeState.MaxPotionCharges}",
                runtimeState.HasSnapshot ? passStyle : failStyle);

            if (GUILayout.Button(runtimeState.TestMutationActive
                    ? "서버 수치 검증 상태 복구"
                    : "서버 수치 검증: HP·MP 감소 + 공격 버프"))
            {
                AddEvent(runtimeState.RequestToggleTestMutation()
                    ? "서버 전투 수치 변경 요청"
                    : "서버 전투 수치 변경 요청 실패");
            }
        }

        NetworkShopState_MirrorTest sharedShop = FindFirstObjectByType<NetworkShopState_MirrorTest>();
        if (shopPlayerState != null && sharedShop != null)
        {
            GUILayout.Space(6f);
            GUILayout.Label("내 상점 상태", titleStyle);
            GUILayout.Label(
                $"골드={shopPlayerState.Gold} | 내 할인={shopPlayerState.DiscountPercent * 100f:0.#}% | " +
                $"내 상점 강화 레벨={shopPlayerState.ShopEnhanceLevel} | 서버 처리 대기={shopPlayerState.PendingRequestCount}건",
                shopPlayerState.PendingRequestCount == 0 ? passStyle : localStyle);
            GUILayout.Label(
                $"공유 재고={sharedShop.StockCount}개 (생성 {sharedShop.GeneratedStockCount} / 플레이어 판매 {sharedShop.PlayerSoldStockCount}) | " +
                $"상점 상태 번호={sharedShop.StateRevision}");
            GUILayout.Label(
                $"무료 리롤 남음={sharedShop.RemainingFreeRerollCount}/{sharedShop.TotalFreeRerollCount} | " +
                $"이후 비용={sharedShop.PaidRerollGoldCost}골드 | 최고 강화 적용자 netId={sharedShop.HighestBenefitPlayerNetId}");

        }

        if (GUILayout.Button("서버에 내 구분용 실제 아이템 지급 요청"))
            GrantDistinctTestItem();

        if (inventorySync != null && GUILayout.Button("내 첫 인벤토리 아이템 서버 드랍"))
        {
            AddEvent(inventorySync.RequestDropFirstInventoryItem()
                ? "서버 드랍 요청"
                : "서버 드랍 요청 실패");
        }

        GUILayout.Space(6f);
        GUILayout.Label("현재 인벤토리", titleStyle);
        DrawInventory(context);

        GUILayout.Space(6f);
        GUILayout.Label("현재 장비", titleStyle);
        DrawEquipment(context);

        GUILayout.Space(6f);
        GUILayout.Label("같은 클라이언트의 플레이어 복제본", titleStyle);
        PlayerContext[] replicas = FindObjectsByType<PlayerContext>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int remoteCount = 0;
        foreach (PlayerContext replica in replicas)
        {
            if (replica == context)
                continue;

            DrawContext(replica, false);
            remoteCount++;
        }

        if (remoteCount == 0)
            GUILayout.Label("- 원격 복제본 없음 (단일 Host 검증 중)");

        GUILayout.Space(6f);
        GUILayout.Label("최근 소유권 이벤트", titleStyle);
        if (ownershipEvents.Count == 0)
            GUILayout.Label("- 아직 이벤트 없음");
        else
            foreach (string entry in ownershipEvents)
                GUILayout.Label("- " + entry);

        if (NetworkServer.active)
            DrawServerControlRoom();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawCompatibilityAndSession()
    {
        MirrorTestNetworkManager manager =
            NetworkManager.singleton as MirrorTestNetworkManager;

        if (manager != null)
        {
            GUILayout.Label(
                $"빌드 호환: {manager.CompatibilityStatusMessage}",
                manager.ClientCompatibilityConfirmed ? passStyle : localStyle);
            GUILayout.Label(
                $"세션 권한: {(manager.ClientIsSessionLeader ? "방장" : "참가자")}",
                manager.ClientIsSessionLeader ? passStyle : localStyle);
        }

        if (manager == null)
        {
            GUILayout.Label("세션 상태: NetworkManager를 기다리는 중", localStyle);
            return;
        }

        bool canRequest = context != null && manager.CanLocalClientControlSession;
        switch (manager.CurrentSessionRoute)
        {
            case MirrorSessionRoute.StageSelect:
                GUILayout.Label("세션 상태: 스테이지 선택 테스트", localStyle);
                if (canRequest && GUILayout.Button("Camp 테스트 이동 요청"))
                {
                    AddEvent(manager.RequestSessionRoute(MirrorSessionRoute.Camp)
                        ? "서버에 Camp 이동 요청"
                        : "Camp 이동 요청 실패");
                }

                if (canRequest && GUILayout.Button("Stage1 전투 이동 요청"))
                {
                    AddEvent(manager.RequestSessionRoute(MirrorSessionRoute.Combat)
                        ? "서버에 Stage1 이동·전투 시작 요청"
                        : "Stage1 전투 이동 요청 실패");
                }
                return;

            case MirrorSessionRoute.Camp:
                GUILayout.Label("세션 상태: Camp 플레이 테스트", localStyle);
                if (canRequest && GUILayout.Button("스테이지 선택으로 복귀 요청"))
                {
                    AddEvent(manager.RequestSessionRoute(MirrorSessionRoute.StageSelect)
                        ? "서버에 스테이지 선택 복귀 요청"
                        : "스테이지 선택 복귀 요청 실패");
                }
                return;

            case MirrorSessionRoute.Combat:
                DrawCombatSession(manager, canRequest);
                if (canRequest && GUILayout.Button("스테이지 선택으로 복귀 요청"))
                {
                    AddEvent(manager.RequestSessionRoute(MirrorSessionRoute.StageSelect)
                        ? "서버에 스테이지 선택 복귀 요청"
                        : "스테이지 선택 복귀 요청 실패");
                }
                return;

            default:
                GUILayout.Label("세션 상태: 6-C 테스트 범위 밖 Scene", localStyle);
                return;
        }
    }

    private void DrawCombatSession(
        MirrorTestNetworkManager manager,
        bool canRequest)
    {
        NetworkEnemyWaveSpawner_MirrorTest waveSpawner =
            FindFirstObjectByType<NetworkEnemyWaveSpawner_MirrorTest>();
        if (waveSpawner == null)
        {
            GUILayout.Label("세션 상태: 웨이브 Spawner를 기다리는 중", localStyle);
            return;
        }

        GUILayout.Label(
            $"세션 상태: {GetSessionPhaseLabel(waveSpawner.SessionPhase)} | " +
            $"상태 번호={waveSpawner.SessionStateRevision}",
            waveSpawner.SessionPhase == MirrorTestSessionPhase.Playing
                ? passStyle
                : localStyle);

        if (waveSpawner.SessionPhase == MirrorTestSessionPhase.Waiting &&
            canRequest &&
            GUILayout.Button("전투 세션 시작 요청"))
        {
            AddEvent(manager.RequestStartSession()
                ? "서버에 전투 세션 시작 요청"
                : "전투 세션 시작 요청 실패");
        }
        else if (waveSpawner.SessionPhase == MirrorTestSessionPhase.Completed)
        {
            GUILayout.Label(
                waveSpawner.IsBossSession
                    ? "보스 클리어: 결과 화면에서 방장이 로비 복귀를 선택합니다."
                    : "전투 완료: 열린 포탈로 이동하면 스테이지 선택으로 돌아갑니다.");
        }
        else if (waveSpawner.SessionPhase == MirrorTestSessionPhase.Resetting)
        {
            GUILayout.Label("세션 초기화 중");
        }
    }

    private void DrawContext(PlayerContext target, bool isLocal)
    {
        if (target == null)
            return;

        NetworkIdentity identity = target.GetComponent<NetworkIdentity>();
        uint netId = identity != null ? identity.netId : 0;
        int itemCount = target.Inventory != null ? target.Inventory.GetAllInventoryItems().Count : -1;
        PlayerStat stat = target.Stats != null ? target.Stats.Stat : null;
        PlayerRuntimeStateSync_MirrorTest targetState = target.RuntimeState;
        float health = targetState?.CurrentHealth ?? target.Health?.CurrentHealth ?? 0f;
        float maxHealth = targetState?.MaxHealth ?? target.Health?.MaxHealth ?? 0f;
        float attack = targetState?.AttackPower ?? stat?.attackPower ?? 0f;
        float defense = targetState?.DefensePower ?? stat?.defensePower ?? 0f;
        PlayerCombatAuthority_MirrorTest combatAuthority = target.CombatAuthority;

        string status = isLocal ? "내 플레이어" : "다른 플레이어 복제본";
        string line = $"{status} | netId={netId} | 로컬 객체 번호: PlayerContext#{target.GetInstanceID()} | 인벤토리#{target.Inventory?.GetInstanceID() ?? 0}";
        GUILayout.Label(line, isLocal ? localStyle : remoteStyle);
        GUILayout.Label($"로컬 객체 번호: 장비 시스템#{target.Equipment?.GetInstanceID() ?? 0} | 스탯 시스템#{target.Stats?.GetInstanceID() ?? 0}");
        GUILayout.Label($"인벤토리 아이템={itemCount}개 | 장착={CountEquipment(target)}개 | 체력={health:0}/{maxHealth:0} | {(targetState?.IsDead == true ? "사망" : "생존")} | 공격={attack:0} | 방어={defense:0} | 전투 상태 번호={targetState?.StateRevision ?? 0}");
        GUILayout.Label(
            $"공격 접수={combatAuthority?.AcceptedRequestCount ?? 0} | 이동 취소={combatAuthority?.CanceledRequestCount ?? 0} | " +
            $"모션 미확인={combatAuthority?.UnconfirmedAttackCount ?? 0} | 거절={combatAuthority?.RejectedRequestCount ?? 0} | " +
            $"최근 결과={GetCombatResultLabel(combatAuthority?.LastResult ?? MirrorCombatRequestResult.None)} | 대상 netId={combatAuthority?.LastTargetNetId ?? 0} | " +
            $"데미지={combatAuthority?.LastDamage ?? 0f:0.#} | 치명타={(combatAuthority?.LastHitCritical == true ? "예" : "아니오")}");
        GUILayout.Label(
            $"요청 전달 시간={combatAuthority?.LastRequestBackdateSeconds * 1000f ?? 0f:0}ms | " +
            $"공격 대기 남음={combatAuthority?.LastCooldownRemainingSeconds ?? 0f:0.000}초 | " +
            $"이전 타격 처리 중={(combatAuthority?.LastRejectedWhileImpactPending == true ? "예" : "아니오")}");

        if (isLocal)
        {
            MirrorSpawnedPlayerBinder binder = target.GetComponent<MirrorSpawnedPlayerBinder>();
            WBH_PlayerInputHandler_MirrorTest movementInput =
                target.GetComponent<WBH_PlayerInputHandler_MirrorTest>();
            NavMeshAgent agent = target.Controller != null ? target.Controller.agent : null;
            GUILayout.Label(
                $"로컬 조작 | 입력={(movementInput != null && movementInput.enabled ? "켜짐" : "꺼짐")} | " +
                $"Controller={(target.Controller != null && target.Controller.IsControlEnabled ? "허용" : "차단")} | " +
                $"NavMesh={(agent != null && agent.enabled && agent.isOnNavMesh ? "연결" : "대기")} | " +
                $"복구 담당={(binder != null ? "있음" : "없음")}");
        }
    }

    private void DrawInventory(PlayerContext target)
    {
        IReadOnlyList<InventoryItem> items = target.Inventory.GetAllInventoryItems();
        if (items.Count == 0)
        {
            GUILayout.Label("- 비어 있음");
            return;
        }

        foreach (InventoryItem item in items)
        {
            ItemInstance data = item?.itemData;
            ItemDefinitionSO definition = data?.definition;
            string name = definition != null ? definition.itemName : "알 수 없는 아이템";
            string itemId = definition != null ? definition.itemId : "종류 ID 없음";
            GUILayout.Label(
                $"- {name} | 종류 ID={itemId} | instance={ShortId(data?.instanceId)} " +
                $"| 인벤토리 칸=({item.x},{item.y}) | 회전={(item.isRotated ? "예" : "아니오")}");
        }
    }

    private void DrawEquipment(PlayerContext target)
    {
        int count = 0;
        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in target.Equipment.GetEquippedItems())
        {
            ItemInstance data = pair.Value?.itemData;
            string itemName = data?.definition != null ? data.definition.itemName : "알 수 없는 아이템";
            GUILayout.Label($"- {GetSlotLabel(pair.Key)}: {itemName} | instance={ShortId(data?.instanceId)} | 강화 +{data?.upgradeLevel ?? 0}");
            count++;
        }

        if (count == 0)
            GUILayout.Label("- 장착 아이템 없음");
    }

    private void DrawServerControlRoom()
    {
        MirrorTestNetworkManager manager = NetworkManager.singleton as MirrorTestNetworkManager;
        if (manager == null)
            return;

        List<PlayerContext> serverPlayers = new(manager.ServerPlayerContexts);
        serverPlayers.Sort((left, right) => GetNetId(left).CompareTo(GetNetId(right)));

        GUILayout.Space(10f);
        GUILayout.Label("서버 종합상황실", titleStyle);
        GUILayout.Label("서버가 확정한 원본 | PlayerContext별 전투 상태와 적 타깃·보상·드랍을 함께 확인");
        GUILayout.Label($"세션 방장 connectionId={manager.ServerSessionLeaderConnectionId}");

        NetworkShopState_MirrorTest sharedShop = FindFirstObjectByType<NetworkShopState_MirrorTest>();
        if (sharedShop != null)
        {
            GUILayout.Label(
                $"공유 상점 | 상태 번호={sharedShop.StateRevision} | 전체={sharedShop.StockCount}개 | " +
                $"생성={sharedShop.GeneratedStockCount}개 | 플레이어 판매={sharedShop.PlayerSoldStockCount}개",
                localStyle);
            GUILayout.Label(
                $"무료 리롤 사용={sharedShop.UsedFreeRerollCount}/{sharedShop.TotalFreeRerollCount} | " +
                $"최고 상점 강화 netId={sharedShop.HighestBenefitPlayerNetId} 레벨={sharedShop.HighestShopEnhanceLevel} | " +
                $"최근 처리={sharedShop.LastServerEvent}");
        }

        bool unique = HasUniquePlayerReferences(serverPlayers);
        bool expectedCount = serverPlayers.Count == 4;
        GUILayout.Label(
            $"접속 플레이어 {serverPlayers.Count}/4명 | 플레이어별 시스템 분리 {(unique ? "정상" : "문제 있음")}",
            unique && expectedCount ? passStyle : failStyle);

        foreach (PlayerContext player in serverPlayers)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            PlayerInventorySync_MirrorTest sync = player.GetComponent<PlayerInventorySync_MirrorTest>();
            NetworkShopPlayerState_MirrorTest playerShop = player.GetComponent<NetworkShopPlayerState_MirrorTest>();
            PlayerRuntimeStateSync_MirrorTest playerState = player.RuntimeState;
            PlayerCombatAuthority_MirrorTest playerCombat = player.CombatAuthority;
            int connectionId = identity != null && identity.connectionToClient != null
                ? identity.connectionToClient.connectionId
                : -1;

            GUILayout.Label(
                $"netId={identity?.netId ?? 0} | 접속 번호={connectionId} | " +
                $"로컬 객체 번호: PlayerContext#{player.GetInstanceID()} 인벤토리#{player.Inventory.GetInstanceID()} " +
                $"장비#{player.Equipment.GetInstanceID()} 스탯#{player.Stats.GetInstanceID()}",
                localStyle);
            GUILayout.Label(
                $"  서버 인벤토리={player.Inventory.GetAllInventoryItems().Count}개 | " +
                $"서버 소유 기록={sync?.SyncedItemCount ?? -1}개 | 상태 변경 번호={sync?.StateRevision ?? 0} | " +
                $"장착={CountEquipment(player)}개");
            GUILayout.Label(
                $"  골드={playerShop?.Gold ?? -1} | 상점 강화 레벨={playerShop?.ShopEnhanceLevel ?? 0} | " +
                $"추가 무료 리롤={playerShop?.ExtraRerollCount ?? 0} | 개인 할인={(playerShop?.DiscountPercent ?? 0f) * 100f:0.#}%");
            GUILayout.Label(
                $"  전투 상태 번호={playerState?.StateRevision ?? 0} | " +
                $"HP={playerState?.CurrentHealth ?? 0f:0}/{playerState?.MaxHealth ?? 0f:0} | " +
                $"MP={playerState?.CurrentMana ?? 0f:0}/{playerState?.MaxMana ?? 0f:0} | " +
                $"생존={(playerState?.IsDead == true ? "사망" : "생존")} | " +
                $"공격={playerState?.AttackPower ?? 0f:0} | 방어={playerState?.DefensePower ?? 0f:0} | " +
                $"버프={playerState?.ActiveBuffCount ?? 0}개 | 포션={playerState?.PotionCharges ?? 0}/{playerState?.MaxPotionCharges ?? 0}");
            GUILayout.Label(
                $"  공격 접수={playerCombat?.AcceptedRequestCount ?? 0} | 이동 취소={playerCombat?.CanceledRequestCount ?? 0} | " +
                $"모션 미확인={playerCombat?.UnconfirmedAttackCount ?? 0} | 거절={playerCombat?.RejectedRequestCount ?? 0} | " +
                $"최근 결과={GetCombatResultLabel(playerCombat?.LastResult ?? MirrorCombatRequestResult.None)} | 대상 netId={playerCombat?.LastTargetNetId ?? 0} | " +
                $"데미지={playerCombat?.LastDamage ?? 0f:0.#}");
            GUILayout.Label(
                $"  요청 전달 시간={playerCombat?.LastRequestBackdateSeconds * 1000f ?? 0f:0}ms | " +
                $"공격 대기 남음={playerCombat?.LastCooldownRemainingSeconds ?? 0f:0.000}초 | " +
                $"이전 타격 처리 중={(playerCombat?.LastRejectedWhileImpactPending == true ? "예" : "아니오")}");

            if (playerState != null && GUILayout.Button($"netId={identity?.netId ?? 0} 체력 회복 및 수동 부활"))
                AddEvent(playerState.ServerReviveForTest()
                    ? $"netId={identity?.netId ?? 0} 서버 수동 부활"
                    : $"netId={identity?.netId ?? 0} 부활 실패");

            foreach (InventoryItem item in player.Inventory.GetAllInventoryItems())
            {
                GUILayout.Label(
                    $"    instance={ShortId(item?.itemData?.instanceId)} | " +
                    $"인벤토리 칸=({item?.x ?? -1},{item?.y ?? -1}) | 회전={(item != null && item.isRotated ? "예" : "아니오")}");
            }

            foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in player.Equipment.GetEquippedItems())
            {
                GUILayout.Label(
                    $"    장착 칸={GetSlotLabel(pair.Key)} | " +
                    $"instance={ShortId(pair.Value?.itemData?.instanceId)} | " +
                    $"이름={pair.Value?.itemData?.definition?.itemName ?? "알 수 없음"}");
            }
        }

        DrawServerEnemies();
    }

    private void DrawServerEnemies()
    {
        List<NetworkEnemyAuthority_MirrorTest> enemies = new();
        foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
        {
            if (identity != null && identity.TryGetComponent(out NetworkEnemyAuthority_MirrorTest enemy))
                enemies.Add(enemy);
        }

        enemies.Sort((left, right) => left.netId.CompareTo(right.netId));
        GUILayout.Space(8f);
        GUILayout.Label($"서버 적 상황 | 현재 {enemies.Count}기", titleStyle);

        NetworkEnemyWaveSpawner_MirrorTest waveSpawner = FindFirstObjectByType<NetworkEnemyWaveSpawner_MirrorTest>();
        GUILayout.Label(
            $"전투 누적 | 서버 사망={NetworkEnemyAuthority_MirrorTest.ServerDeathCount} | " +
            $"처치 보상={NetworkEnemyAuthority_MirrorTest.ServerRewardCount} | " +
            $"드롭={NetworkEnemyAuthority_MirrorTest.ServerDropCount} | " +
            $"이 화면의 파괴 연출={NetworkEnemyAuthority_MirrorTest.LocalDeathPresentationCount} | " +
            $"서버 투사체={NetworkEnemyProjectile_MirrorTest.ServerSpawnCount} | " +
            $"확인한 투사체={NetworkEnemyProjectile_MirrorTest.ClientObservedCount}");
        if (waveSpawner != null)
        {
            GUILayout.Label(
                $"웨이브={waveSpawner.CurrentWave} | 생존 적={waveSpawner.AliveEnemyCount} | " +
                $"누적 생성={waveSpawner.TotalSpawnCount} | 완료 웨이브={waveSpawner.CompletedWaveCount}");
        }

        if (enemies.Count == 0)
        {
            GUILayout.Label("- 생성된 네트워크 적 없음");
            return;
        }

        foreach (NetworkEnemyAuthority_MirrorTest enemy in enemies)
        {
            GUILayout.Label(
                $"적 netId={enemy.netId} | {enemy.EnemyInfo?.enemyName ?? "일반 적"} | " +
                $"HP={enemy.CurrentHealth:0}/{enemy.MaxHealth:0} | {(enemy.IsDead ? "사망" : "생존")} | " +
                $"대상 netId={enemy.TargetNetId} | 마지막 공격자 netId={enemy.LastAttackerNetId}",
                enemy.IsDead ? failStyle : remoteStyle);
            GUILayout.Label(
                $"  상태 변경 번호={enemy.StateChangeNumber} | 공격 시작={enemy.AttackStartCount} | 플레이어 적중={enemy.HitCount} | " +
                $"최근 받은 데미지={enemy.LastDamage:0.#} | 처치 보상={enemy.KillRewardCount} | 드랍={enemy.DropSpawnCount} | 파괴 연출={enemy.DestructionPresentationCount}");
        }
    }

    private static bool HasUniquePlayerReferences(List<PlayerContext> players)
    {
        HashSet<int> contexts = new();
        HashSet<int> inventories = new();
        HashSet<int> equipment = new();
        HashSet<int> stats = new();

        foreach (PlayerContext player in players)
        {
            if (player == null ||
                !contexts.Add(player.GetInstanceID()) ||
                !inventories.Add(player.Inventory.GetInstanceID()) ||
                !equipment.Add(player.Equipment.GetInstanceID()) ||
                !stats.Add(player.Stats.GetInstanceID()))
            {
                return false;
            }
        }

        return true;
    }

    private static int CountEquipment(PlayerContext target)
    {
        int count = 0;
        foreach (KeyValuePair<EquipSlotType, InventoryItem> _ in target.Equipment.GetEquippedItems())
            count++;
        return count;
    }

    private static uint GetNetId(PlayerContext target)
    {
        NetworkIdentity identity = target != null ? target.GetComponent<NetworkIdentity>() : null;
        return identity != null ? identity.netId : 0;
    }

    private static string GetOperationLabel(MirrorTestInventoryOperation operation)
    {
        return operation switch
        {
            MirrorTestInventoryOperation.GrantDistinctItem => "테스트 아이템 지급",
            MirrorTestInventoryOperation.DropFirstItem => "첫 아이템 필드 드랍",
            MirrorTestInventoryOperation.PickupWorldItem => "필드 아이템 획득",
            MirrorTestInventoryOperation.DropInventoryItem => "선택 아이템 필드 드랍",
            MirrorTestInventoryOperation.MoveGridItem => "인벤토리 이동·회전",
            MirrorTestInventoryOperation.ChangeEquipment => "장비 장착·해제·교환",
            _ => "알 수 없는 작업",
        };
    }

    private static string GetResultLabel(MirrorTestInventoryRequestResult result)
    {
        return result switch
        {
            MirrorTestInventoryRequestResult.Success => "성공",
            MirrorTestInventoryRequestResult.InvalidRequest => "요청 내용이 올바르지 않음",
            MirrorTestInventoryRequestResult.DuplicateRequest => "같은 요청이 이미 처리됨",
            MirrorTestInventoryRequestResult.StaleRevision => "서버 상태가 먼저 바뀌어 다시 시도해야 함",
            MirrorTestInventoryRequestResult.InventoryUnavailable => "인벤토리를 찾을 수 없음",
            MirrorTestInventoryRequestResult.ItemUnavailable => "아이템을 찾을 수 없음",
            MirrorTestInventoryRequestResult.InventoryFull => "인벤토리 공간 부족",
            MirrorTestInventoryRequestResult.PickupUnavailable => "필드 아이템을 찾을 수 없음",
            MirrorTestInventoryRequestResult.OutOfRange => "아이템이 너무 멀리 있음",
            MirrorTestInventoryRequestResult.AlreadyClaimed => "다른 플레이어가 먼저 획득함",
            MirrorTestInventoryRequestResult.ServerSetupInvalid => "서버 테스트 설정 누락",
            MirrorTestInventoryRequestResult.SpawnFailed => "필드 아이템 생성 실패",
            MirrorTestInventoryRequestResult.StateApplyFailed => "서버 상태 적용 실패",
            MirrorTestInventoryRequestResult.RecoveryFailed => "이전 상태 복구 실패",
            _ => "알 수 없는 결과",
        };
    }

    private static string GetShopOperationLabel(MirrorTestShopOperation operation)
    {
        return operation switch
        {
            MirrorTestShopOperation.Buy => "구매",
            MirrorTestShopOperation.Sell => "판매",
            MirrorTestShopOperation.Reroll => "리롤",
            _ => "알 수 없는 상점 작업",
        };
    }

    private static string GetCombatResultLabel(MirrorCombatRequestResult result)
    {
        return result switch
        {
            MirrorCombatRequestResult.Accepted => "접수",
            MirrorCombatRequestResult.Hit => "적중",
            MirrorCombatRequestResult.NoTarget => "대상 없음",
            MirrorCombatRequestResult.Dead => "사망 상태",
            MirrorCombatRequestResult.InvalidAim => "잘못된 조준",
            MirrorCombatRequestResult.DuplicateRequest => "중복 요청",
            MirrorCombatRequestResult.AttackOnCooldown => "공격 대기 중",
            MirrorCombatRequestResult.InvalidTiming => "잘못된 공격 시각",
            MirrorCombatRequestResult.CanceledByMove => "이동으로 공격 취소",
            MirrorCombatRequestResult.AnimationNotConfirmed => "공격 모션 미확인",
            _ => "없음",
        };
    }

    private static string GetSessionPhaseLabel(MirrorTestSessionPhase phase)
    {
        return phase switch
        {
            MirrorTestSessionPhase.Waiting => "대기",
            MirrorTestSessionPhase.Playing => "전투 중",
            MirrorTestSessionPhase.Completed => "전투 완료",
            MirrorTestSessionPhase.Resetting => "초기화 중",
            _ => "알 수 없음",
        };
    }

    private static string GetShopResultLabel(MirrorTestShopRequestResult result)
    {
        return result switch
        {
            MirrorTestShopRequestResult.Success => "성공",
            MirrorTestShopRequestResult.InvalidRequest => "요청 내용이 올바르지 않음",
            MirrorTestShopRequestResult.DuplicateRequest => "같은 요청이 이미 처리됨",
            MirrorTestShopRequestResult.ShopStateChanged => "다른 거래가 먼저 처리되어 다시 시도해야 함",
            MirrorTestShopRequestResult.InventoryStateChanged => "내 인벤토리가 먼저 바뀌어 다시 시도해야 함",
            MirrorTestShopRequestResult.ItemUnavailable => "아이템을 찾을 수 없음",
            MirrorTestShopRequestResult.NotEnoughGold => "골드 부족",
            MirrorTestShopRequestResult.InventoryFull => "인벤토리 공간 부족",
            MirrorTestShopRequestResult.ShopFull => "상점 공간 부족",
            MirrorTestShopRequestResult.RerollUnavailable => "리롤 불가",
            MirrorTestShopRequestResult.ServerSetupInvalid => "서버 테스트 설정 누락",
            MirrorTestShopRequestResult.StateApplyFailed => "서버 상태 적용 실패",
            MirrorTestShopRequestResult.RecoveryFailed => "이전 상태 복구 실패",
            _ => "알 수 없는 결과",
        };
    }

    private static string GetSlotLabel(EquipSlotType slotType)
    {
        return slotType switch
        {
            EquipSlotType.Weapon => "무기",
            EquipSlotType.Helmet => "머리 방어구",
            EquipSlotType.Chest => "몸통 방어구",
            EquipSlotType.Boots => "신발",
            EquipSlotType.Potion => "포션",
            _ => "장비 칸 없음",
        };
    }

    private void EnsureGuiStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 14,
        };
        localStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
        };
        localStyle.normal.textColor = new Color(0.25f, 0.85f, 0.45f);
        remoteStyle = new GUIStyle(GUI.skin.label);
        remoteStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
        passStyle = new GUIStyle(localStyle);
        failStyle = new GUIStyle(localStyle);
        failStyle.normal.textColor = new Color(1f, 0.35f, 0.3f);
    }

    private static string ShortId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "no-id";

        return value.Length <= 8 ? value : value.Substring(0, 8);
    }
}

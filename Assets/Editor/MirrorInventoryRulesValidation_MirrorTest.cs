using System;
using System.Linq;
using ItemSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

/// <summary>임시 UI와 Grid로 입력 경계만 검사한다. 실제 서버 거래 검증은 별도로 수행한다.</summary>
public static class MirrorInventoryRulesValidation_MirrorTest
{
    private const string SourcePath = "Assets/SW/Prefabs/ItemPrefab.prefab";
    private const string MirrorPath = "Assets/SW/TEST/MirrorPlayerContext/Prefabs/ItemPrefab_MirrorTest.prefab";
    private const string ItemPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/item.armor.helmet.alienskullcrown_우주 괴물 두개골.asset";

    [MenuItem("SW/Mirror 테스트/싱글 실제 인벤토리 검증")]
    public static void ValidateSinglePlayerInventory()
    {
        Require(Application.isPlaying && !Mirror.NetworkClient.active && !Mirror.NetworkServer.active, "싱글 Play Mode에서 실행하세요.");
        InventoryController owner = InventoryController.Instance;
        PlayerStatManager stats = PlayerStatManager.Instance;
        Require(owner != null && stats != null && stats.Stat != null &&
            Object.FindObjectsByType<T_PlayerController>(FindObjectsSortMode.None).Any(p => p.isActiveAndEnabled), "활성 실제 플레이어가 없습니다.");
        Require(owner.EquipmentSystem != null && new SerializedObject(stats).FindProperty("equipmentSystem").objectReferenceValue == owner.EquipmentSystem,
            "실제 PlayerStatManager와 InventoryController의 EquipmentSystem 참조가 다릅니다.");
        Require(!owner.EquipmentSystem.TryGetEquippedItem(EquipSlotType.Helmet, out _), "검증하려면 Helmet 슬롯이 비어 있어야 합니다.");
        InventoryPartView view = Object.FindFirstObjectByType<InventoryPartView>(FindObjectsInactive.Include);
        if (view != null) view.OpenInventory();
        else
        {
            var popup = owner.PlayerGrid != null ? owner.PlayerGrid.GridRect?.GetComponentInParent<KY_InventoryPopup>(true) : null;
            Require(popup != null, "실제 싱글 인벤토리 화면이 없습니다.");
            popup.Open();
        }
        InventoryGrid grid = owner.PlayerGrid;
        Require(grid != null && grid.HasView && owner.PlayerWallet != null && EventSystem.current != null, "실제 Grid View/지갑/EventSystem이 없습니다.");
        ItemDefinitionSO definition = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(ItemPath);
        Require(definition != null, "검증용 Helmet SO가 없습니다.");
        int beforeCount = grid.GetAllItems().Count, beforeGold = owner.PlayerWallet.Gold;
        float beforeHealth = stats.Stat.maxHealth;
        ItemInstance data = ItemDataCreator.CreateItemData(definition);
        InventoryItem item = null;
        ItemUI ui = null;
        ItemDragHandler drag = null;
        try
        {
            Require(owner.TryAddItemData(data).Result == InventoryAddResult.Success, "실제 인벤토리 fixture 지급 실패.");
            item = grid.GetAllItems().FirstOrDefault(i => i.itemData.instanceId == data.instanceId);
            ui = ItemUIFinder.FindInGrid(grid, item);
            Require(item != null && ui != null && !ui.HasExternalInput && ui.isActiveAndEnabled, "실제 싱글 spawned ItemUI가 없습니다.");
            Vector2Int target = new Vector2Int(-1, -1);
            int width = item.CurrentHeight, height = item.CurrentWidth;
            for (int y = 0; y < grid.GridHeight && target.x < 0; y++)
                for (int x = 0; x < grid.GridWidth; x++)
                    if ((x != item.x || y != item.y) && InventoryGridMath.CanPlaceRectIgnoring(grid, new InventoryCellRect(x, y, width, height), item))
                    { target = new Vector2Int(x, y); break; }
            Require(target.x >= 0, "회전 후 이동할 다른 빈 공간이 없습니다.");
            Canvas canvas = ui.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, ui.Rect.TransformPoint(new Vector3(8, -8))),
                pointerCurrentRaycast = new RaycastResult { gameObject = grid.GridRect.gameObject } };
            drag = ui.GetComponent<ItemDragHandler>();
            Require(drag != null, "실제 ItemDragHandler가 없습니다.");
            bool wasRotated = item.isRotated;
            drag.OnBeginDrag(pointer);
            Require(drag.IsDragging && !grid.ContainsItem(item), "실제 싱글 드래그 모델 분리 실패.");
            ui.RotateDraggingItem();
            ui.Rect.position = grid.ItemsContainer.TransformPoint(new Vector3(target.x * grid.Step, -target.y * grid.Step));
            pointer.position = RectTransformUtility.WorldToScreenPoint(camera, ui.Rect.TransformPoint(new Vector3(8, -8)));
            drag.OnEndDrag(pointer);
            Require(grid.ContainsItem(item) && item.x == target.x && item.y == target.y && item.isRotated != wasRotated &&
                !drag.IsDragging && ui.IsGridPlacementVisualized(grid), "실제 이동/회전/UI 복구 실패.");
            pointer.button = PointerEventData.InputButton.Right;
            ui.OnPointerClick(pointer);
            Require(owner.EquipmentSystem.TryGetEquippedItem(EquipSlotType.Helmet, out InventoryItem equipped) && equipped == item &&
                !grid.ContainsItem(item) && stats.Stat.maxHealth > beforeHealth, "실제 Helmet 장착/최대체력 증가 실패.");
            if (ui == null) ui = owner.allEquipSlots.FirstOrDefault(s => s != null && s.SlotType == EquipSlotType.Helmet)?.EquippedItemUI;
            Require(ui != null && ui.Item == item, "장착 후 실제 fixture UI가 없습니다.");
            ui.OnPointerClick(pointer);
            ui = ItemUIFinder.FindInGrid(grid, item);
            Require(!owner.EquipmentSystem.TryGetEquippedItem(EquipSlotType.Helmet, out _) && grid.ContainsItem(item) &&
                ui != null && ui.IsGridPlacementVisualized(grid) && Mathf.Approximately(stats.Stat.maxHealth, beforeHealth), "실제 해제/최대체력 복원 실패.");
        }
        finally
        {
            item ??= grid.GetAllItems().FirstOrDefault(i => i.itemData.instanceId == data.instanceId);
            if (drag != null && drag.IsDragging) drag.OnEndDrag(null);
            if (item != null && owner.EquipmentSystem.TryGetEquippedItem(EquipSlotType.Helmet, out InventoryItem remaining) && remaining == item)
                Require(new EquipmentTransaction(owner.EquipmentSystem).TryUnequipForTransfer(EquipSlotType.Helmet, item).IsSuccess, "fixture 장비 정리 실패.");
            if (item != null && grid.ContainsItem(item)) Require(owner.TryRemoveInventoryItem(item) == InventoryRemoveResult.Success, "fixture Grid 정리 실패.");
            foreach (EquipSlotUI slot in owner.allEquipSlots)
                if (slot != null && slot.EquippedItemUI != null && slot.EquippedItemUI.Item == item) slot.ClearItemUI();
            foreach (ItemUI fixtureUI in Object.FindObjectsByType<ItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (fixtureUI.Item?.itemData?.instanceId == data.instanceId) Object.DestroyImmediate(fixtureUI.gameObject);
        }
        Require(grid.GetAllItems().Count == beforeCount && owner.PlayerWallet.Gold == beforeGold &&
            Mathf.Approximately(stats.Stat.maxHealth, beforeHealth), "fixture 정리 후 항목 수/골드/최대체력이 복원되지 않았습니다.");
        Debug.Log("[Mirror Single Inventory] PASS: 실제 플레이어 지급→spawned UI 드래그 분리→회전/이동→우클릭 장착/최대체력 증가→해제/최대체력 복원→fixture 정리 및 항목 수/골드 보존.");
    }

    [MenuItem("SW/Mirror 테스트/캠프 인벤토리 참조 복구")]
    public static void RepairCampInventoryPrefab()
    {
        Require(!Application.isPlaying, "Edit Mode에서 실행하세요.");
        const string path = "Assets/SW/TEST/MirrorPlayerContext/Prefabs/InventoryCamp_MirrorTest.prefab";
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            InventoryView view = contents.GetComponentInChildren<InventoryView>(true);
            InventoryController legacy = contents.GetComponentInChildren<InventoryController>(true);
            if (legacy == null) return;
            InventoryGrid grid = legacy.PlayerGrid;
            Require(view != null && grid != null && grid.HasView && legacy.allEquipSlots.All(slot => slot != null),
                "원본 UI의 현재 그리드·슬롯 참조가 필요합니다.");
            InventoryItemUISpawner spawner = view.GetComponent<InventoryItemUISpawner>();
            TooltipManager tooltip = contents.GetComponentsInChildren<TooltipManager>(true).Single(component =>
                new SerializedObject(component).FindProperty("primaryTooltip").objectReferenceValue != null);
            ShopController shop = contents.GetComponentInChildren<ShopController>(true);
            UpgradeController upgrade = contents.GetComponentInChildren<UpgradeController>(true);
            Require(spawner != null && shop != null && upgrade != null, "Mirror 표시·상점·강화 컴포넌트가 필요합니다.");

            var serialized = new SerializedObject(view);
            serialized.FindProperty("gridRect").objectReferenceValue = grid.GridRect;
            serialized.FindProperty("itemsContainer").objectReferenceValue = grid.ItemsContainer;
            serialized.FindProperty("highlight").objectReferenceValue = grid.Highlight;
            serialized.FindProperty("logText").objectReferenceValue = legacy.logText;
            serialized.FindProperty("goldText").objectReferenceValue = legacy.goldText;
            serialized.FindProperty("tooltipManager").objectReferenceValue = tooltip;
            serialized.FindProperty("itemSpawner").objectReferenceValue = spawner;
            serialized.FindProperty("shopController").objectReferenceValue = shop;
            serialized.FindProperty("upgradeController").objectReferenceValue = upgrade;
            SerializedProperty slots = serialized.FindProperty("equipmentSlots");
            slots.arraySize = legacy.allEquipSlots.Length;
            for (int index = 0; index < slots.arraySize; index++)
                slots.GetArrayElementAtIndex(index).objectReferenceValue = legacy.allEquipSlots[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetReference(shop, "itemUISpawner", spawner);
            SetReference(shop, "inventoryController", null);
            SetReference(shop, "playerGrid", null);
            SetReference(shop, "playerWallet", null);
            SetReference(upgrade, "playerWallet", null);
            SetReference(upgrade, "equipmentSystem", null);
            SetReference(tooltip, "playerStatManager", null);
            SetReference(tooltip, "equipmentSystem", null);
            tooltip.gameObject.SetActive(true);

            foreach (InventoryItemUISpawner component in contents.GetComponentsInChildren<InventoryItemUISpawner>(true))
                if (component != spawner) Object.DestroyImmediate(component);
            foreach (TooltipManager component in contents.GetComponentsInChildren<TooltipManager>(true))
                if (component != tooltip) Object.DestroyImmediate(component);
            Object.DestroyImmediate(legacy.PlayerWallet);
            Object.DestroyImmediate(legacy.EquipmentSystem);
            Object.DestroyImmediate(grid);
            Object.DestroyImmediate(legacy);
            Require(PrefabUtility.SaveAsPrefabAsset(contents, path) != null, "Camp 프리팹 저장 실패.");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        ValidateAssetBindings();
    }

    private static void SetReference(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("SW/Mirror 테스트/인벤토리 자산 연결 검증")]
    public static void ValidateAssetBindings()
    {
        const string folder = "Assets/SW/TEST/MirrorPlayerContext/Prefabs/";
        foreach (string name in new[] { "FighterNetworkPlayer", "GunnerNetworkPlayer_MirrorTest" })
        {
            PlayerContext player = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab").GetComponent<PlayerContext>();
            Require(player.Inventory != null && player.Inventory.PlayerGrid != null && player.Inventory.PlayerWallet == player.Wallet,
                name + "의 인벤토리 모델·지갑 연결이 잘못됐습니다.");
        }
        InventoryView view = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "InventoryCamp_MirrorTest.prefab")
            .GetComponentInChildren<InventoryView>(true);
        Require(view != null, "Camp InventoryView가 없습니다.");
        var serialized = new SerializedObject(view);
        foreach (string field in new[] { "gridRect", "itemsContainer", "itemSpawner", "shopController", "upgradeController", "tooltipManager" })
            Require(serialized.FindProperty(field).objectReferenceValue != null, "Camp InventoryView." + field + " 참조가 없습니다.");
        var tooltip = (TooltipManager)serialized.FindProperty("tooltipManager").objectReferenceValue;
        Require(tooltip.enabled && tooltip.GetComponentsInParent<Transform>(true).All(t => t.gameObject.activeSelf),
            "Camp TooltipManager의 Runtime 또는 상위 오브젝트가 비활성 상태입니다.");
        Require(view.EquipmentSlots.Length == 5 && view.EquipmentSlots.All(slot => slot != null), "Camp 장비 슬롯 참조가 없습니다.");
        Require(view.transform.root.GetComponentsInChildren<InventoryController>(true).Length == 0 &&
                view.transform.root.GetComponentsInChildren<TooltipManager>(true).Length == 1 &&
                view.transform.root.GetComponentsInChildren<InventoryItemUISpawner>(true).Length == 1,
                "Camp에 싱글 소유 모델 또는 중복 화면 컴포넌트가 남아 있습니다.");
        Debug.Log("[Mirror Inventory Rules] PASS: Fighter/Gunner 인벤토리·지갑, Camp InventoryView 필수 자산 참조.");
    }

    [MenuItem("SW/Mirror Test/Migrate Inventory Input Prefab")]
    public static void MigrateInputPrefab()
    {
        Require(!Application.isPlaying, "Prefab 마이그레이션은 Edit Mode에서 실행하세요.");
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(MirrorPath) != null, "Mirror Item Prefab이 없습니다.");
        GameObject contents = PrefabUtility.LoadPrefabContents(MirrorPath);
        try
        {
            foreach (MonoBehaviour component in contents.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Require(component != null, "Mirror Item Prefab에 Missing Script가 있습니다.");
                string type = component.GetType().Name;
                if (type == "NetworkInventoryWorldDrop_MirrorTest" ||
                    type == "NetworkInventoryEquipmentPlacement_MirrorTest" ||
                    type == "NetworkShopItemTrade_MirrorTest")
                    Object.DestroyImmediate(component);
            }
            ValidatePrefab(contents, true);
            var input = new SerializedObject(contents.GetComponent<NetworkInventoryInput_MirrorTest>());
            SerializedProperty typeIdentifier = input.FindProperty("m_EditorClassIdentifier");
            if (typeIdentifier != null)
            {
                typeIdentifier.stringValue = "Assembly-CSharp::NetworkInventoryInput_MirrorTest";
                input.ApplyModifiedPropertiesWithoutUndo();
            }
            Require(PrefabUtility.SaveAsPrefabAsset(contents, MirrorPath) != null, "Mirror Item Prefab 저장에 실패했습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
        Debug.Log("[Mirror Inventory Rules] Mirror Item Prefab 입력 컴포넌트 마이그레이션 완료.");
    }

    [MenuItem("SW/Mirror 테스트/인벤토리 입력 규칙 검증")]
    public static void Run()
    {
        Require(Application.isPlaying, "InventoryGrid.Awake 초기화를 위해 Play Mode에서 실행하세요.");
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        GameObject mirrorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MirrorPath);
        ValidatePrefab(sourcePrefab, false);
        ValidatePrefab(mirrorPrefab, true);
        ItemDefinitionSO definition = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(ItemPath);
        Require(definition != null && definition.itemWidth == 3 && definition.itemHeight == 2 && definition.icon != null,
            "검증용 실제 3×2 아이템 SO 또는 아이콘을 찾지 못했습니다.");
        InventoryController owner = Object.FindFirstObjectByType<InventoryController>(FindObjectsInactive.Include);
        Require(owner != null, "Play Scene에 기존 InventoryController가 필요합니다.");
        Require(owner.gameObject.scene.isLoaded, "InventoryController가 로드된 런타임 Scene에 없습니다.");

        // HideAndDontSave는 Editor에서 객체를 Scene에서 제외할 수 있어 복구 생명주기 검사와 맞지 않는다.
        // 일반 Play Scene 객체로 생성하고 아래 finally에서 정리한다.
        GameObject root = new GameObject("MirrorInventoryRulesValidation_Temporary");
        ItemUI ui = null;
        try
        {
            GameObject gridObject = new GameObject("TemporaryGrid", typeof(RectTransform), typeof(InventoryGrid));
            gridObject.transform.SetParent(root.transform, false);
            InventoryGrid grid = gridObject.GetComponent<InventoryGrid>();
            RectTransform rect = gridObject.GetComponent<RectTransform>();
            grid.BindView(rect, rect, null);
            InventoryItem source = new InventoryItem(new ItemInstance { definition = definition });
            InventoryItem other = new InventoryItem(new ItemInstance { definition = definition });
            Require(grid.TryPlaceItem(source, 0, 0) && grid.TryPlaceItem(other, 4, 0), "임시 Grid 배치 실패.");
            InventoryItem[,] cells = CaptureCells(grid);

            ui = CreateUI(sourcePrefab, root.transform, source, grid);
            Require(!ui.HasExternalInput, "싱글 Prefab에 외부 입력이 연결됐습니다.");
            ItemDropHandler drop = ui.GetComponent<ItemDropHandler>();
            Require(drop != null, "싱글 ItemDropHandler가 없습니다.");
            drop.Bind(null, owner, Array.Empty<EquipSlotUI>());
            ui.SaveOriginalState();
            Require(ui.gameObject.scene.isLoaded && grid.gameObject.scene.isLoaded,
                $"fixture Scene 미로드: ui={ui.gameObject.scene.isLoaded}, grid={grid.gameObject.scene.isLoaded}, owner={owner.gameObject.scene.isLoaded}");
            Require(ui.OriginalPlacement.IsValid && ui.IsGridPlacementVisualized(grid),
                $"분리 전 fixture 배치 실패: snapshot={ui.OriginalPlacement.IsValid}, visual={ui.IsGridPlacementVisualized(grid)}");
            drop.PrepareRestore();
            Require(ui.TryDetachFromCurrentSlotOrGrid() && !grid.ContainsItem(source), "로컬 모델 분리가 실패했습니다.");
            bool restored = drop.TryRestoreOriginalPlacement();
            bool contained = grid.ContainsItem(source);
            bool sameItem = ReferenceEquals(ui.Item, source);
            bool visualized = ui.IsGridPlacementVisualized(grid);
            string restoreState = $"restored={restored}, pending={drop.HasPendingRestore}, contains={contained}, sameItem={sameItem}, " +
                $"visual={visualized}, snapshot={ui.OriginalPlacement.IsValid}, originalGrid={ui.OriginalGrid == grid}, " +
                $"playerOwns={owner.PlayerGrid != null && owner.PlayerGrid.ContainsItem(source)}, equipped={source.isEquipped}, " +
                $"equipSlot={ui.CurrentEquipSlot != null}, uiSceneLoaded={ui.gameObject.scene.isLoaded}, " +
                $"ownerSceneLoaded={owner.gameObject.scene.isLoaded}, gridSceneLoaded={grid.gameObject.scene.isLoaded}";
            Require(restored, "로컬 복구 API 실패: " + restoreState);
            Require(contained, "로컬 Grid 소유 복구 실패: " + restoreState);
            Require(sameItem, "로컬 원본 참조 복구 실패: " + restoreState);
            Require(visualized, "로컬 UI 원위치 복구 실패: " + restoreState);
            AssertCells(grid, cells);
            Object.DestroyImmediate(ui.gameObject);

            ui = CreateUI(mirrorPrefab, root.transform, source, grid);
            Require(ui.HasExternalInput, "Mirror 입력 컴포넌트의 Awake 바인딩이 없습니다.");
            int rejectedClicks = 0;
            ui.BindExternalInput((action, data) => { rejectedClicks++; return false; });
            ui.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            Require(rejectedClicks == 1 && grid.ContainsItem(source) && !source.isEquipped,
                "거절한 외부 우클릭이 로컬 거래로 이어졌습니다.");
            ui.SaveOriginalState();
            Require(ui.BeginDragPreview(), "표시용 드래그 시작 실패.");
            Require(ReferenceEquals(ui.DragSourceItem, source) && !ReferenceEquals(ui.Item, source) &&
                ReferenceEquals(ui.Item.itemData, source.itemData) && grid.ContainsItem(source), "표시 모델과 소유 모델 분리 실패.");
            ui.RotateDraggingItem();
            Require(ui.Item.isRotated && source.x == 0 && source.y == 0 && !source.isRotated && !source.isEquipped,
                "미리보기 회전이 원본 모델을 변경했습니다.");
            AssertCells(grid, cells);
            ui.RotateDraggingItem();
            InventoryCellRect originalRect = new InventoryCellRect(0, 0, 3, 2);
            Require(InventoryGridMath.CanPlaceRectIgnoring(grid, originalRect, ui.DragSourceItem),
                "원본 점유 제외 검사가 실패했습니다.");
            Require(!InventoryGridMath.CanPlaceRectIgnoring(grid, originalRect, ui.Item),
                "미리보기 객체를 제외했는데 원본 점유까지 무시했습니다.");
            InventorySwapPlan plan = InventorySwapPlanner.BuildPlan(grid, ui.Item, ui.OriginalPlacement, other, ui.DragSourceItem);
            Require(plan.IsValid, "원본 점유를 유지한 교환 계획 생성 실패.");
            Require(source.x == 0 && source.y == 0 && !source.isRotated && !source.isEquipped &&
                other.x == 4 && other.y == 0 && !other.isRotated && !other.isEquipped,
                "교환 계획 생성이 소유 모델을 변경했습니다.");
            AssertCells(grid, cells);
            ui.EndDragPreview();
            Require(!ui.IsDragPreviewActive && ReferenceEquals(ui.Item, source), "미리보기 종료 후 원본 참조 복구 실패.");
            Require(ui.BeginDragPreview(), "재바인딩 검사 시작 실패.");
            ui.Setup(other, grid);
            ui.EndDragPreview();
            Require(ReferenceEquals(ui.Item, other) && !ui.IsDragPreviewActive, "새 Setup 상태를 이전 미리보기가 덮어썼습니다.");
            AssertCells(grid, cells);
            Debug.Log("[Mirror Inventory Rules] PASS: Prefab 입력 구성, 로컬 분리/복구, 외부 우클릭 거절, " +
                "미리보기 회전/종료/재바인딩, 점유 제외, 교환 계획 비변경. " +
                "임시 fixture 검증이며 전체 드래그 콜백·실제 네트워크 거래는 포함하지 않습니다.");
        }
        finally
        {
            if (ui != null) ui.EndDragPreview();
            Object.DestroyImmediate(root);
        }
    }

    private static ItemUI CreateUI(GameObject prefab, Transform parent, InventoryItem item, InventoryGrid grid)
    {
        ItemUI ui = Object.Instantiate(prefab, parent).GetComponent<ItemUI>();
        Require(ui != null, "ItemUI가 없습니다.");
        ui.Setup(item, grid);
        ui.SetGridPosition(grid, item.x, item.y);
        Require(ReferenceEquals(ui.Item, item), "실제 Prefab UI 초기화 실패.");
        return ui;
    }

    private static void ValidatePrefab(GameObject prefab, bool mirror)
    {
        Require(prefab != null && prefab.GetComponent<ItemUI>() != null, "ItemUI Prefab을 찾지 못했습니다.");
        Require(prefab.GetComponent<ItemDragHandler>() != null, "ItemDragHandler가 없습니다.");
        int inputs = 0;
        foreach (MonoBehaviour component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Require(component != null, "Item Prefab에 Missing Script가 있습니다.");
            string type = component.GetType().Name;
            if (type == nameof(NetworkInventoryInput_MirrorTest)) inputs++;
            Require(type != "NetworkInventoryWorldDrop_MirrorTest" && type != "NetworkInventoryGridPlacement_MirrorTest" &&
                type != "NetworkInventoryEquipmentPlacement_MirrorTest" && type != "NetworkShopItemTrade_MirrorTest",
                "구 입력 어댑터가 남아 있습니다: " + type);
        }
        Require(inputs == (mirror ? 1 : 0), "Prefab의 최종 Mirror 입력 컴포넌트 수가 잘못됐습니다.");
        if (mirror) Require(prefab.GetComponent<NetworkInventoryInput_MirrorTest>() != null, "Mirror 입력은 ItemUI 루트에 있어야 합니다.");
    }

    private static InventoryItem[,] CaptureCells(InventoryGrid grid)
    {
        var cells = new InventoryItem[grid.GridWidth, grid.GridHeight];
        for (int x = 0; x < grid.GridWidth; x++)
            for (int y = 0; y < grid.GridHeight; y++) cells[x, y] = grid.GetItemAt(x, y);
        return cells;
    }

    private static void AssertCells(InventoryGrid grid, InventoryItem[,] cells)
    {
        for (int x = 0; x < grid.GridWidth; x++)
            for (int y = 0; y < grid.GridHeight; y++)
                Require(ReferenceEquals(cells[x, y], grid.GetItemAt(x, y)), "Grid 점유가 변경됐습니다.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Mirror Inventory Rules] " + message);
    }
}

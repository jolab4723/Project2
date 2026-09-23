using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>KY 원본 씬과 두 클래스 원본을 재사용해 격리된 Mirror 로비·거너 자산을 만든다.</summary>
public static class MirrorLobbySceneSetup_MirrorTest
{
    private const string FighterPath = "Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab";
    public const string GunnerPath = "Assets/SW/TEST/MirrorPlayerContext/Prefabs/GunnerNetworkPlayer_MirrorTest.prefab";
    private const string OriginalGunnerPath = "Assets/Resources/Prefabs/Character/Player/Gunner.prefab";
    private const string OriginalLobbyPath = "Assets/Scenes/Test/KY/MultiplayerLobbySeane.unity";
    private const string KoreanFontPath = "Assets/Resources/Font/Pretendard-Medium SDF.asset";

    /// <summary>세 로비의 기존 패시브 화면에 실제 매니저와 DB를 연결한다. 멀티 저장 연결 전에는 조회만 허용한다.</summary>
    [MenuItem("SW/Mirror Test/Connect Passive Lobby UI")]
    public static void ConnectPassiveLobbyUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 끝난 Edit Mode에서 실행하세요.");
        string[] paths = { MirrorTestNetworkManager.SessionLobbyScene, OriginalLobbyPath,
            "Assets/Scenes/Test/KY/SinglePlayerLobbySeane.unity" };
        foreach (string path in paths)
        {
            Scene loaded = SceneManager.GetSceneByPath(path);
            if (loaded.isLoaded && loaded.isDirty)
                throw new InvalidOperationException("저장하지 않은 로비 편집이 있습니다: " + path);
        }
        Scene previous = SceneManager.GetActiveScene();
        var database = AssetDatabase.LoadAssetAtPath<PassiveSkillDatabaseSO>(
            "Assets/WJ_TestPlace/Data/Passive/PassiveSkillDatabase.asset");
        if (database == null) throw new InvalidOperationException("패시브 DB를 찾지 못했습니다.");
        foreach (string path in paths)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var popup = InScene<KY_PassiveSkillPopup>(scene).Single();
                var manager = InScene<PassiveSkillManager>(scene).SingleOrDefault();
                if (manager == null)
                {
                    var root = new GameObject("Passive Profile");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    manager = root.AddComponent<PassiveSkillManager>();
                }
                SetReference(manager, "database", database);
                SetReference(popup, "skillManager", manager);
                var data = new SerializedObject(popup);
                bool singlePlayer = path.EndsWith("/SinglePlayerLobbySeane.unity", StringComparison.Ordinal);
                data.FindProperty("allowChanges").boolValue = singlePlayer;
                data.FindProperty("unavailableReason").stringValue =
                    "프로필 저장 연결이 준비되면 패시브를 변경할 수 있습니다.";
                data.ApplyModifiedPropertiesWithoutUndo();
                var popups = InScene<KY_PopupManager>(scene).Single();
                var pause = InScene<KY_PausePopup>(scene).SingleOrDefault();
                if (pause == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UI/Popup/PausePopup.prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, popup.transform.parent);
                    pause = instance.GetComponent<KY_PausePopup>();
                    instance.SetActive(false);
                }
                var pauseData = new SerializedObject(pause);
                pauseData.FindProperty("pauseGameTime").boolValue = singlePlayer;
                pauseData.ApplyModifiedPropertiesWithoutUndo();
                if (!popups.popupEntries.Any(entry => entry.type == PopupType.Pause))
                    popups.popupEntries.Add(new KY_PopupManager.PopupEntry { type = PopupType.Pause, popup = pause });
                EditorUtility.SetDirty(popups);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("로비 저장 실패: " + path);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }

    /// <summary>WJ의 새 패시브 화면만 읽어 Mirror 로비의 구매·표시·닫기 버튼에 연결한다.</summary>
    [MenuItem("SW/Mirror Test/Import Latest Passive Popup")]
    public static void ImportLatestPassivePopup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Prefab Stage를 닫고 컴파일이 끝난 Edit Mode에서 실행하세요.");
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(MirrorTestNetworkManager.SessionLobbyScene);
        if (scene.isLoaded && scene.isDirty) throw new InvalidOperationException("Mirror 로비에 저장하지 않은 편집이 있습니다.");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(MirrorTestNetworkManager.SessionLobbyScene, OpenSceneMode.Additive);
        Scene source = EditorSceneManager.OpenPreviewScene("Assets/WJ_TestPlace/Scene/WJ_StatSystemTestScene.unity");
        try
        {
            var template = InScene<KY_PassiveSkillPopup>(source).Single(p => p.transform.Find("Contents/MainRow/ControlArea/ControlPanel") != null);
            var old = InScene<KY_PassiveSkillPopup>(scene).Single();
            var popups = InScene<KY_PopupManager>(scene).Single();
            var profile = InScene<PassiveSkillManager>(scene).Single();
            var popup = Object.Instantiate(template, old.transform.parent);
            popup.name = old.name;
            popup.gameObject.SetActive(false);
            popup.allSkills.Clear();
            popup.activeListView = null;
            popup.pointText = popup.transform.Find("Contents/HeaderRow/ControlArea/CreditText").GetComponent<TextMeshProUGUI>();
            popup.transform.Find("Contents/HeaderRow/ControlArea/SkillPointsText").gameObject.SetActive(false);
            Transform controls = popup.transform.Find("Contents/MainRow/ControlArea/ControlPanel");
            popup.descriptionView = controls.gameObject.AddComponent<KY_PassiveSkillDescriptionView>();
            popup.descriptionView.nameText = controls.Find("PassiveNameText").GetComponent<TextMeshProUGUI>();
            popup.descriptionView.descriptionText = controls.Find("PassiveDescriptionText").GetComponent<TextMeshProUGUI>();
            popup.descriptionView.descriptionText.enableAutoSizing = true;
            popup.descriptionView.descriptionText.fontSizeMin = 20;
            popup.descriptionView.descriptionText.fontSizeMax = 28;
            SetReference(popup, "skillManager", profile);
            var data = new SerializedObject(popup);
            data.FindProperty("allowChanges").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var unused in popup.GetComponentsInChildren<PassiveSkillPanelUI>(true)) Object.DestroyImmediate(unused);
            foreach (Button button in popup.GetComponentsInChildren<Button>(true)) button.onClick = new Button.ButtonClickedEvent();
            var innerClose = popup.transform.Find("btnClosePopup/innerShadow").GetComponent<Button>();
            if (innerClose != null) Object.DestroyImmediate(innerClose);
            SetReference(popup, "levelUpButton", controls.Find("ControlButton/btn_LevelUp").GetComponent<Button>());
            SetReference(popup, "levelDownButton", controls.Find("ControlButton/btn_LevelDown").GetComponent<Button>());
            SetReference(popup, "confirmButton", controls.Find("btn_Confilm").GetComponent<Button>());
            SetReference(popup, "resetButton", popup.transform.Find("Contents/HeaderRow/ControlArea/btn_SkillClear").GetComponent<Button>());
            SetReference(popup, "pendingLevelText", controls.Find("ControlButton/Divider").GetComponent<TMP_Text>());
            SetReference(popup, "confirmButtonText", controls.Find("btn_Confilm/Label").GetComponent<TMP_Text>());
            UnityEditor.Events.UnityEventTools.AddPersistentListener(popup.transform.Find("btnClosePopup").GetComponent<Button>().onClick, popups.Hide);
            foreach (Graphic graphic in popup.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (Button button in popup.GetComponentsInChildren<Button>(true)) button.targetGraphic.raycastTarget = true;
            foreach (KY_PassiveSkillSlot slot in popup.slots)
            {
                slot.transform.Find("IconFrame").GetComponent<Image>().raycastTarget = true;
                var label = new GameObject("Skill Name", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(slot.transform.Find("IconFrame"), false);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(6, 6);
                label.rectTransform.offsetMax = new Vector2(-6, -6);
                label.font = popup.pointText.font;
                label.fontSize = 28;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16;
                label.fontSizeMax = 28;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                SetReference(slot, "missingIconLabel", label);
                SetReference(slot, "levelLabel", slot.transform.Find("SkillLevelIcon/SkillLevelText").GetComponent<TMP_Text>());
            }
            VerifyReferences(new[] { popup.gameObject });
            foreach (var entry in popups.popupEntries) if (entry.popup == old) entry.popup = popup;
            SetReference(InScene<MirrorLobbyBridge_MirrorTest>(scene).Single(), "passivePopup", popup);
            Object.DestroyImmediate(old.gameObject);
            EditorUtility.SetDirty(popups);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Mirror 로비 저장 실패");
            Debug.Log("[MirrorLobbySetup] 새 패시브 화면·단계 버튼·프로필 연결 완료");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(source);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    /// <summary>캠프 이동 영역을 별도 프리뷰 씬에서 굽고 Mirror 캠프에만 연결한다.</summary>
    [MenuItem("SW/Mirror 테스트/캠프 이동 영역 생성")]
    public static void BakeCampNavigation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Edit Mode에서 컴파일 완료 후 실행하세요.");
        const string path = MirrorTestNetworkManager.SessionCampGameplayScene;
        if (SceneManager.GetSceneByPath(path).isLoaded)
            throw new InvalidOperationException("검증용 캠프 씬을 닫은 상태에서 실행하세요.");
        const string folder = "Assets/SW/TEST/MirrorCombat/Scenes/Act1_Camp_MirrorSessionTest";
        const string dataPath = folder + "/NavMesh-Camp.asset";
        Scene preview = EditorSceneManager.OpenPreviewScene(path);
        NavMeshData data;
        try
        {
            NavMeshSurface surface = preview.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true)).Single();
            surface.BuildNavMesh();
            data = surface.navMeshData;
            if (data == null || data.sourceBounds.size.sqrMagnitude <= 0)
                throw new InvalidOperationException("Camp NavMesh 생성에 실패했습니다.");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/SW/TEST/MirrorCombat/Scenes", "Act1_Camp_MirrorSessionTest");
            NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(dataPath);
            if (existing == null) AssetDatabase.CreateAsset(data, dataPath);
            else
            {
                EditorUtility.CopySerialized(data, existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                data = existing;
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        Scene previousActive = SceneManager.GetActiveScene();
        Scene camp = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            NavMeshSurface surface = camp.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true)).Single();
            surface.navMeshData = data;
            EditorSceneManager.MarkSceneDirty(camp);
            if (!EditorSceneManager.SaveScene(camp))
                throw new InvalidOperationException("Mirror Camp 씬 저장에 실패했습니다.");
            Debug.Log($"[MirrorCampAssets] NavMesh 연결 완료: {data.sourceBounds}");
        }
        finally
        {
            EditorSceneManager.CloseScene(camp, true);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
    }

    [MenuItem("SW/Mirror Test/Validate Lobby Assets")]
    public static void ValidateLobbyAssets()
    {
        foreach (string path in new[] { FighterPath, GunnerPath })
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (player == null || player.GetComponent<MirrorSpawnedPlayerBinder>()?.IsConfigured != true)
                throw new InvalidOperationException("PlayerContext/미러 필수 참조: " + path);
            VerifyReferences(new[] { player });
            foreach (Component component in player.GetComponentsInChildren<Component>(true))
            {
                var data = new SerializedObject(component);
                SerializedProperty property = data.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object reference = property.objectReferenceValue;
                    Transform linked = reference is Component value ? value.transform : (reference as GameObject)?.transform;
                    if (linked == null || linked == player.transform || linked.IsChildOf(player.transform)) continue;
                    // 다른 프리팹 자산(투사체·VFX 등)은 허용하지만 다른 플레이어의 런타임 참조는 금지한다.
                    if (linked.GetComponentInParent<PlayerContext>() != null)
                        throw new InvalidOperationException("다른 플레이어 참조: " + component.GetType().Name + "." + property.propertyPath);
                }
            }
            Debug.Log($"[MirrorLobbyAssets] PASS {player.name} assetId={player.GetComponent<NetworkIdentity>().assetId}");
        }
    }

    [MenuItem("SW/Mirror Test/Create KY Lobby")]
    public static void CreateLobby()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Edit Mode에서 컴파일 완료 후 실행하세요.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("프리팹 편집을 마친 뒤 실행하세요. 자동으로 저장하지 않습니다.");

        Scene previousActive = SceneManager.GetActiveScene();
        Scene lobbyScene = default;
        Scene sourceScene = default;
        try
        {
            GameObject gunner = AssetDatabase.LoadAssetAtPath<GameObject>(GunnerPath);
            if (gunner == null) gunner = CreateGunnerPrefab();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MirrorTestNetworkManager.SessionLobbyScene) != null)
                throw new InvalidOperationException("로비 테스트 씬이 이미 있습니다. 기존 편집을 덮어쓰지 않습니다.");
            if (!AssetDatabase.CopyAsset(OriginalLobbyPath, MirrorTestNetworkManager.SessionLobbyScene))
                throw new InvalidOperationException("KY 로비 씬 복사에 실패했습니다.");
            lobbyScene = EditorSceneManager.OpenScene(MirrorTestNetworkManager.SessionLobbyScene, OpenSceneMode.Additive);
            sourceScene = EditorSceneManager.OpenScene(MirrorTestNetworkManager.SessionCampScene, OpenSceneMode.Additive);
            MirrorTestNetworkManager template = sourceScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MirrorTestNetworkManager>(true)).Single();
            GameObject managerObject = Object.Instantiate(template.gameObject);
            managerObject.name = "Mirror Session";
            SceneManager.MoveGameObjectToScene(managerObject, lobbyScene);
            EditorSceneManager.CloseScene(sourceScene, true);
            SceneManager.SetActiveScene(lobbyScene);
            // 싱글과 같은 효과음 재생기를 플레이어 생성 전에 준비한다. 영구 사운드 객체는 씬 이동에도 유지된다.
            if (!InScene<YJ_SfxPlayer>(lobbyScene).Any())
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Manager/SoundManager.prefab"), lobbyScene);
            Object.DestroyImmediate(managerObject.GetComponent<NetworkManagerHUD>());
            Object.DestroyImmediate(managerObject.GetComponent<DataManager_MirrorTest>());
            MirrorTestNetworkManager manager = managerObject.GetComponent<MirrorTestNetworkManager>();
            manager.authenticator = managerObject.GetComponent<MirrorSessionAuthenticator_MirrorTest>() ??
                managerObject.AddComponent<MirrorSessionAuthenticator_MirrorTest>();
            SetReference(manager.authenticator, "passiveDatabase", AssetDatabase.LoadAssetAtPath<PassiveSkillDatabaseSO>(
                "Assets/WJ_TestPlace/Data/Passive/PassiveSkillDatabase.asset"));
            manager.autoCreatePlayer = false;
            manager.maxConnections = 4;
            manager.offlineScene = MirrorTestNetworkManager.SessionLobbyScene;
            manager.onlineScene = string.Empty;
            if (!manager.spawnPrefabs.Contains(gunner)) manager.spawnPrefabs.Add(gunner);
            var skillVisual = AssetDatabase.LoadAssetAtPath<GameObject>(MirrorSkillSceneSetup_MirrorTest.NetworkVisualPath);
            if (skillVisual != null && !manager.spawnPrefabs.Contains(skillVisual)) manager.spawnPrefabs.Add(skillVisual);
            SetReference(manager, "gunnerPlayerPrefab", gunner);

            KY_LobbyFlowController flow = InScene<KY_LobbyFlowController>(lobbyScene).Single();
            KY_MultiplayerLobbyController lobby = InScene<KY_MultiplayerLobbyController>(lobbyScene).Single();
            var lobbyData = new SerializedObject(lobby);
            Button ready = (Button)lobbyData.FindProperty("readyButton").objectReferenceValue;
            lobbyData.FindProperty("readyButtonText").objectReferenceValue = ready.transform.parent.GetComponentInChildren<TMP_Text>(true);
            lobbyData.ApplyModifiedPropertiesWithoutUndo();
            CreateConnectionPanel(flow, lobby);
            EventSystem[] systems = InScene<EventSystem>(lobbyScene)
                .OrderByDescending(item => item.transform.IsChildOf(manager.transform))
                .ThenByDescending(item => item.gameObject.activeInHierarchy).ToArray();
            if (systems.Length > 0)
            {
                systems[0].transform.SetParent(manager.transform, false);
                systems[0].gameObject.SetActive(true);
            }
            foreach (EventSystem duplicate in systems.Skip(1))
            {
                foreach (BaseInputModule module in duplicate.GetComponents<BaseInputModule>()) Object.DestroyImmediate(module);
                Object.DestroyImmediate(duplicate);
            }
            VerifyReferences(lobbyScene.GetRootGameObjects());
            EditorSceneManager.SaveScene(lobbyScene);
            Debug.Log("[MirrorLobbySetup] 로비·거너 테스트 자산 생성 완료");
        }
        finally
        {
            if (sourceScene.IsValid() && sourceScene.isLoaded) EditorSceneManager.CloseScene(sourceScene, true);
            if (lobbyScene.IsValid() && lobbyScene.isLoaded) EditorSceneManager.CloseScene(lobbyScene, true);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
    }

    private static GameObject CreateGunnerPrefab()
    {
        GameObject fighter = AssetDatabase.LoadAssetAtPath<GameObject>(FighterPath);
        GameObject sourceGunner = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalGunnerPath);
        GameObject gunner = PrefabUtility.LoadPrefabContents(OriginalGunnerPath);
        gunner.name = "GunnerNetworkPlayer_MirrorTest";
        try
        {
            Transform sourceInventory = fighter.GetComponent<PlayerContext>().Inventory.transform;
            while (sourceInventory.parent != fighter.transform) sourceInventory = sourceInventory.parent;
            GameObject inventory = Object.Instantiate(sourceInventory.gameObject, gunner.transform);
            inventory.name = sourceInventory.name;

            // 입력과 로컬 판정만 교체한다. 원본의 메시·애니메이터·클래스 데이터와 손 기준점은 유지한다.
            Type[] localOnly = { typeof(WBH_PlayerInputHandler), typeof(WBH_PlayerAnimation),
                typeof(PlayerActionInputHandler), typeof(GunnerSkillController), typeof(PotionUseManager),
                typeof(PlayerRelicEffectProvider), typeof(PlayerHudEventBridge), typeof(ItemTriggerManager) };
            foreach (Type type in localOnly)
                foreach (Component component in gunner.GetComponents(type)) Object.DestroyImmediate(component);

            var copied = new List<Component>();
            foreach (Component component in fighter.GetComponents<Component>())
            {
                Type type = component.GetType();
                if (component is NetworkIdentity || component is NetworkBehaviour || component is PlayerContext ||
                    type.Name.EndsWith("_MirrorTest", StringComparison.Ordinal))
                {
                    Component target = gunner.GetComponent(type) ?? gunner.AddComponent(type);
                    EditorUtility.CopySerialized(component, target);
                    copied.Add(target);
                }
            }
            foreach (Component target in copied) RemapPlayerReferences(target, fighter.transform, gunner.transform);
            InventoryController ownedInventory = inventory.GetComponentInChildren<InventoryController>(true);
            EquipmentSystem equipment = inventory.GetComponentInChildren<EquipmentSystem>(true);
            foreach (Component component in gunner.GetComponents<Component>())
            {
                var data = new SerializedObject(component);
                SetOptionalReference(data, "equipmentSystem", equipment);
                SetOptionalReference(data, "inventoryController", ownedInventory);
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            // 각 클래스의 기본 스킬 정의를 유지하되 테스트용 강제 진화는 넣지 않는다.
            var sourceSkills = new SerializedObject(sourceGunner.GetComponent<GunnerSkillController>());
            var skills = new SerializedObject(gunner.GetComponent<FighterSkillAuthority_MirrorTest>());
            SerializedProperty sourceArray = sourceSkills.FindProperty("skills");
            SerializedProperty targetArray = skills.FindProperty("skills");
            targetArray.arraySize = sourceArray.arraySize;
            for (int index = 0; index < sourceArray.arraySize; index++)
                targetArray.GetArrayElementAtIndex(index).objectReferenceValue = sourceArray.GetArrayElementAtIndex(index).objectReferenceValue;
            SerializedProperty evolutions = skills.FindProperty("activeEvolutions");
            for (int index = 0; index < evolutions.arraySize; index++) evolutions.GetArrayElementAtIndex(index).intValue = 0;
            skills.ApplyModifiedPropertiesWithoutUndo();
            var identity = new SerializedObject(gunner.GetComponent<NetworkIdentity>());
            identity.FindProperty("_assetId").uintValue = 0;
            identity.FindProperty("sceneId").ulongValue = 0;
            identity.ApplyModifiedPropertiesWithoutUndo();
            gunner.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            MirrorSkillSceneSetup_MirrorTest.ConfigurePlayer(gunner, sourceGunner);
            VerifyReferences(new[] { gunner });
            return PrefabUtility.SaveAsPrefabAsset(gunner, GunnerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(gunner); }
    }

    private static void RemapPlayerReferences(Component target, Transform sourceRoot, Transform targetRoot)
    {
        var data = new SerializedObject(target);
        SerializedProperty property = data.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            Object value = property.objectReferenceValue;
            Transform transform = value is Component component ? component.transform : (value as GameObject)?.transform;
            if (transform == null || (transform != sourceRoot && !transform.IsChildOf(sourceRoot))) continue;
            string relativePath = AnimationUtility.CalculateTransformPath(transform, sourceRoot);
            Transform replacement = string.IsNullOrEmpty(relativePath) ? targetRoot : targetRoot.Find(relativePath);
            if (replacement == null)
            {
                property.objectReferenceValue = null;
                continue;
            }
            property.objectReferenceValue = value is GameObject ? replacement.gameObject : replacement.GetComponent(value.GetType());
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateConnectionPanel(KY_LobbyFlowController flow, KY_MultiplayerLobbyController lobby)
    {
        Canvas canvas = flow.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = InScene<Canvas>(flow.gameObject.scene).First(value => value.isRootCanvas);
        RectTransform panel = Rect("Session Connection", canvas.transform, new Vector2(640, 530), Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0.055f, 0.065f, 0.09f, 0.98f);
        Text(panel, "MULTIPLAYER", 30, new Vector2(560, 55), new Vector2(0, 205));
        Text(panel, "주소", 18, new Vector2(130, 45), new Vector2(-220, 130));
        TMP_InputField address = Input(panel, "localhost", new Vector2(65, 130));
        Text(panel, "이름", 18, new Vector2(130, 45), new Vector2(-220, 65));
        TMP_InputField displayName = Input(panel, "Player", new Vector2(65, 65));
        Button host = Button(panel, "방 만들기 (Host)", new Vector2(-145, -20));
        Button join = Button(panel, "주소로 참가", new Vector2(145, -20));
        Button server = Button(panel, "전용 서버 열기", new Vector2(-145, -85));
        Button reconnect = Button(panel, "최근 세션으로 복귀", new Vector2(145, -85));
        TMP_Text status = Text(panel, "최대 4인 · 연결이 끊겨도 5분 안에 복귀할 수 있습니다.", 17,
            new Vector2(560, 90), new Vector2(0, -185));
        MirrorLobbyBridge_MirrorTest bridge = flow.gameObject.AddComponent<MirrorLobbyBridge_MirrorTest>();
        var data = new SerializedObject(bridge);
        SetOptionalReference(data, "flow", flow);
        SetOptionalReference(data, "lobby", lobby);
        SetOptionalReference(data, "connectionPanel", panel.gameObject);
        SetOptionalReference(data, "addressInput", address);
        SetOptionalReference(data, "displayNameInput", displayName);
        SetOptionalReference(data, "statusText", status);
        SetOptionalReference(data, "hostButton", host);
        SetOptionalReference(data, "joinButton", join);
        SetOptionalReference(data, "serverButton", server);
        SetOptionalReference(data, "reconnectButton", reconnect);
        data.ApplyModifiedPropertiesWithoutUndo();
        StyleConnectionPanel(bridge);
    }

    [MenuItem("SW/Mirror Test/Polish Lobby Connection UI")]
    public static void PolishLobbyConnectionUI()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty ||
            scene.path != MirrorTestNetworkManager.SessionLobbyScene || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("저장된 Mirror 로비를 Edit Mode에서 열어 주세요.");
        StyleConnectionPanel(InScene<MirrorLobbyBridge_MirrorTest>(scene).Single());
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("로비 저장 실패");
    }

    private static void StyleConnectionPanel(MirrorLobbyBridge_MirrorTest bridge)
    {
        var data = new SerializedObject(bridge);
        var panel = ((GameObject)data.FindProperty("connectionPanel").objectReferenceValue).GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(740, 660);
        panel.GetComponent<Image>().color = new Color(0.025f, 0.075f, 0.105f, 0.98f);
        var outline = panel.GetComponent<UnityEngine.UI.Outline>() ?? panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.12f, 0.65f, 0.72f, 0.8f);
        outline.effectDistance = new Vector2(2, -2);
        var address = (TMP_InputField)data.FindProperty("addressInput").objectReferenceValue;
        var nickname = (TMP_InputField)data.FindProperty("displayNameInput").objectReferenceValue;
        Place(nickname.GetComponent<RectTransform>(), new Vector2(440, 56), new Vector2(85, 155));
        Place(address.GetComponent<RectTransform>(), new Vector2(440, 56), new Vector2(85, 80));
        nickname.characterLimit = 24;
        foreach (var input in new[] { nickname, address })
        {
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.textComponent.fontSize = 24;
            input.textComponent.color = new Color(0.91f, 0.97f, 1f);
            input.targetGraphic.color = new Color(0.07f, 0.17f, 0.22f);
            input.textComponent.rectTransform.sizeDelta = new Vector2(410, 50);
        }
        foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true).Where(t => t.transform.parent == panel))
        {
            if (text.text == "MULTIPLAYER" || text.text == "멀티플레이 접속")
            { text.text = "멀티플레이 접속"; text.fontSize = 34; text.color = new Color(0.55f, 0.93f, 1f); Place(text.rectTransform, new Vector2(640, 60), new Vector2(0, 255)); }
            else if (text.text == "이름" || text.text == "닉네임")
            { text.text = "닉네임"; text.fontSize = 22; Place(text.rectTransform, new Vector2(150, 56), new Vector2(-240, 155)); }
            else if (text.text == "주소" || text.text == "서버 주소")
            { text.text = "서버 주소"; text.fontSize = 22; Place(text.rectTransform, new Vector2(150, 56), new Vector2(-240, 80)); }
        }
        StyleButton("joinButton", "서버 접속", new Vector2(610, 62), new Vector2(0, -10), new Color(0.06f, 0.45f, 0.55f));
        StyleButton("reconnectButton", "최근 세션으로 복귀", new Vector2(610, 52), new Vector2(0, -80), new Color(0.08f, 0.24f, 0.31f));
        StyleButton("hostButton", "로컬 테스트 · Host", new Vector2(295, 46), new Vector2(-157.5f, -155), new Color(0.11f, 0.17f, 0.21f));
        StyleButton("serverButton", "로컬 테스트 · 서버", new Vector2(295, 46), new Vector2(157.5f, -155), new Color(0.11f, 0.17f, 0.21f));
        var status = (TMP_Text)data.FindProperty("statusText").objectReferenceValue;
        status.text = "최대 4인 · 먼저 접속한 플레이어가 방장입니다.\n서버 IP 또는 호스트명 입력 · 연결이 끊겨도 5분 안에 복귀 가능";
        status.fontSize = 20;
        status.color = new Color(0.65f, 0.79f, 0.85f);
        Place(status.rectTransform, new Vector2(640, 105), new Vector2(0, -255));
        void StyleButton(string field, string label, Vector2 size, Vector2 position, Color color)
        {
            var button = (Button)data.FindProperty(field).objectReferenceValue;
            Place(button.GetComponent<RectTransform>(), size, position);
            button.targetGraphic.color = color;
            var text = button.GetComponentInChildren<TMP_Text>(true);
            text.text = label;
            text.fontSize = 23;
            text.rectTransform.sizeDelta = size - new Vector2(20, 4);
            var colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.65f);
            button.colors = colors;
        }
        void Place(RectTransform rect, Vector2 size, Vector2 position)
        { rect.sizeDelta = size; rect.anchoredPosition = position; }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        result.SetParent(parent, false);
        result.anchorMin = result.anchorMax = result.pivot = new Vector2(0.5f, 0.5f);
        result.sizeDelta = size;
        result.anchoredPosition = position;
        return result;
    }

    private static TMP_Text Text(Transform parent, string value, float size, Vector2 dimensions, Vector2 position)
    {
        TMP_Text text = Rect("Text", parent, dimensions, position).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static TMP_InputField Input(Transform parent, string value, Vector2 position)
    {
        RectTransform root = Rect("Input", parent, new Vector2(415, 48), position);
        Image image = root.gameObject.AddComponent<Image>();
        image.color = new Color(0.15f, 0.18f, 0.24f);
        TMP_Text text = Text(root, value, 22, new Vector2(385, 44), Vector2.zero);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = root;
        input.textComponent = text;
        input.targetGraphic = image;
        input.characterLimit = 64;
        input.text = value;
        return input;
    }

    private static Button Button(Transform parent, string label, Vector2 position)
    {
        RectTransform root = Rect(label, parent, new Vector2(270, 50), position);
        Image image = root.gameObject.AddComponent<Image>();
        image.color = new Color(0.2f, 0.3f, 0.48f);
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Text(root, label, 20, root.sizeDelta, Vector2.zero);
        return button;
    }

    private static IEnumerable<T> InScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    private static void SetReference(Object target, string name, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(name).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetOptionalReference(SerializedObject data, string name, Object value)
    {
        SerializedProperty property = data.FindProperty(name);
        if (property != null) property.objectReferenceValue = value;
    }

    private static void VerifyReferences(GameObject[] roots)
    {
        foreach (GameObject root in roots)
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    throw new InvalidOperationException("Missing Script: " + child.name);
    }
}

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
    private const string OriginalLobbyPath = "Assets/Scenes/Test/KY/LobbySeane.unity";
    private const string KoreanFontPath = "Assets/Resources/Font/Pretendard-Medium SDF.asset";

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
            if (player == null || !player.GetComponent<PlayerContext>().IsComplete)
                throw new InvalidOperationException("PlayerContext 필수 참조: " + path);
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
            Object.DestroyImmediate(managerObject.GetComponent<NetworkManagerHUD>());
            Object.DestroyImmediate(managerObject.GetComponent<DataManager_MirrorTest>());
            MirrorTestNetworkManager manager = managerObject.GetComponent<MirrorTestNetworkManager>();
            manager.authenticator = managerObject.AddComponent<MirrorSessionAuthenticator_MirrorTest>();
            manager.autoCreatePlayer = false;
            manager.maxConnections = 4;
            manager.offlineScene = MirrorTestNetworkManager.SessionLobbyScene;
            manager.onlineScene = string.Empty;
            if (!manager.spawnPrefabs.Contains(gunner)) manager.spawnPrefabs.Add(gunner);
            SetReference(manager, "gunnerPlayerPrefab", gunner);

            KY_LobbyFlowController flow = InScene<KY_LobbyFlowController>(lobbyScene).Single();
            KY_MultiplayerLobbyController lobby = InScene<KY_MultiplayerLobbyController>(lobbyScene).Single();
            var lobbyData = new SerializedObject(lobby);
            Button ready = (Button)lobbyData.FindProperty("readyButton").objectReferenceValue;
            lobbyData.FindProperty("readyButtonText").objectReferenceValue = ready.transform.parent.GetComponentInChildren<TMP_Text>(true);
            lobbyData.ApplyModifiedPropertiesWithoutUndo();
            CreateConnectionPanel(flow, lobby);
            EventSystem[] systems = InScene<EventSystem>(lobbyScene)
                .OrderByDescending(item => item.gameObject.activeInHierarchy).ToArray();
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

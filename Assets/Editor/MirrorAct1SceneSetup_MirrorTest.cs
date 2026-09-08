using System;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>검증된 Mirror 템플릿에 Act1 원본 환경을 옮긴다. 원본/사용자 씬은 저장하지 않는다.</summary>
public static class MirrorAct1SceneSetup_MirrorTest
{
    private const string Folder = "Assets/SW/TEST/MirrorCombat/Scenes/";
    private const string Template = Folder + "Act1_Stage1_MirrorSessionTest.unity";
    private const string EnvironmentName = "-------------------------------Environment";
    private static readonly string[] CombatNames = Enumerable.Range(1, 6)
        .Select(i => "Act1_Stage" + i).Concat(new[] { "Act1_BossStage" }).ToArray();

    [MenuItem("SW/Mirror 테스트/Act1 실제 맵 테스트 씬 생성")]
    public static void Build()
    {
        RequireEditMode();
        RequireCleanIfLoaded(Template);
        // Existing destinations are deliberately not overwritten: their inspector corrections belong to the user.
        foreach (string name in CombatNames.Skip(1))
        {
            string sourcePath = $"Assets/Scenes/Maps/Act1_Maps/{name}/{name}.unity";
            string targetPath = Folder + name + "_MirrorSessionTest.unity";
            RequireCleanIfLoaded(sourcePath);
            RequireCleanIfLoaded(targetPath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) != null)
            {
                ValidateAsset(targetPath, true);
                Debug.Log("[MirrorAct1] Existing scene checked; skipped: " + targetPath);
                continue;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sourcePath) == null)
                throw new InvalidOperationException("Missing source scene: " + sourcePath);
            BuildScene(name, sourcePath, targetPath);
        }
        Debug.Log("[MirrorAct1] Scene creation finished. Stage5 landing still requires NavMesh and multiplayer ride verification.");
    }

    private static void BuildScene(string name, string sourcePath, string targetPath)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene source = SceneManager.GetSceneByPath(sourcePath);
        bool openedSource = !source.isLoaded;
        Scene target = default;
        bool copied = false;
        bool saved = false;
        try
        {
            if (openedSource) source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
            if (!AssetDatabase.CopyAsset(Template, targetPath))
                throw new InvalidOperationException("Cannot copy Mirror template: " + targetPath);
            copied = true;
            target = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(target);

            GameObject authoredEnvironment = source.GetRootGameObjects().Single(g => g.name.StartsWith(EnvironmentName, StringComparison.Ordinal));
            GameObject oldEnvironment = target.GetRootGameObjects().Single(g => g.name.StartsWith(EnvironmentName, StringComparison.Ordinal));
            Object.DestroyImmediate(oldEnvironment);
            GameObject environment = Object.Instantiate(authoredEnvironment);
            environment.name = authoredEnvironment.name;
            SceneManager.MoveGameObjectToScene(environment, target);

            CopyConfiguration(source, target);
            Vector3 playerPosition = Find(source, "PlayerSpawnPoint").position;
            NetworkStartPosition[] starts = Components<NetworkStartPosition>(target).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            if (starts.Length != 4) throw new InvalidOperationException("Template must contain four NetworkStartPositions.");
            for (int i = 0; i < starts.Length; ++i)
                starts[i].transform.position = playerPosition + Vector3.right * (i * 2 - 3);
            Vector3 portalPosition = Find(source, "PortalSpawnPoint").position;
            Find(target, "Portal_Particle2_MirrorSession").position = portalPosition;
            Find(target, "Portal_Body_MirrorSession").position = portalPosition;

            WBH_EnemySpawnArea area = Components<WBH_EnemySpawnArea>(source).Single();
            SerializedObject areaData = new(area);
            SerializedProperty points = Required(areaData, "spawnPoints");
            NetworkEnemyWaveSpawner_MirrorTest spawner = Components<NetworkEnemyWaveSpawner_MirrorTest>(target).Single();
            SerializedObject spawnData = new(spawner);
            SerializedProperty positions = Required(spawnData, "authoredSpawnPositions");
            if (points.arraySize == 0) throw new InvalidOperationException("Source has no authored enemy spawn points: " + sourcePath);
            positions.arraySize = points.arraySize;
            for (int i = 0; i < points.arraySize; ++i)
            {
                Transform point = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (point == null) throw new InvalidOperationException("Missing authored enemy spawn point.");
                positions.GetArrayElementAtIndex(i).vector3Value = point.position;
            }
            Required(spawnData, "bossSpawnPosition").vector3Value = area.transform.position;
            spawnData.ApplyModifiedPropertiesWithoutUndo();
            MirrorStagePortalAdapter_MirrorTest portalAdapter = Components<MirrorStagePortalAdapter_MirrorTest>(target).Single();
            SerializedObject portalData = new(portalAdapter);
            Required(portalData, "waveSpawner").objectReferenceValue = spawner;
            Required(portalData, "portalActive").objectReferenceValue = portalAdapter.GetComponentInParent<YJ_PortalActive>();
            portalData.ApplyModifiedPropertiesWithoutUndo();

            if (name == "Act1_Stage5") ConfigureElevator(environment);
            if (name == "Act1_Stage6") BakeStage6Navigation(environment);
            ValidateScene(target, true);
            EditorSceneManager.MarkSceneDirty(target);
            if (!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("Cannot save: " + targetPath);
            saved = true;
            Debug.Log("[MirrorAct1] Created: " + targetPath);
        }
        finally
        {
            if (target.IsValid() && target.isLoaded) EditorSceneManager.CloseScene(target, true);
            if (openedSource && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            // A failed new copy must not be mistaken for a finished scene on the next run.
            if (copied && !saved) AssetDatabase.DeleteAsset(targetPath);
        }
    }

    private static void CopyConfiguration(Scene source, Scene target)
    {
        BoxCollider sourceBounds = Find(source, "CameraBounds").GetComponent<BoxCollider>();
        BoxCollider targetBounds = Find(target, "CameraBounds").GetComponent<BoxCollider>();
        if (sourceBounds == null || targetBounds == null) throw new InvalidOperationException("CameraBounds requires BoxCollider.");
        CopyTransform(sourceBounds.transform, targetBounds.transform);
        targetBounds.center = sourceBounds.center;
        targetBounds.size = sourceBounds.size;
        foreach (string name in new[] { "DirectionalLight", "Global Volume" })
        {
            Transform from = Find(source, name);
            Transform to = Find(target, name);
            CopyTransform(from, to);
            foreach (Component component in from.GetComponents<Component>())
            {
                if (component is Transform) continue;
                if (component == null) throw new InvalidOperationException("Missing component on source " + name);
                Component destination = to.GetComponent(component.GetType());
                if (destination == null) throw new InvalidOperationException("Template lacks " + component.GetType().Name);
                // Copy authored component settings, but never scene ownership or prefab bookkeeping.
                SerializedObject input = new(component);
                SerializedObject output = new(destination);
                SerializedProperty property = input.GetIterator();
                if (property.NextVisible(true))
                    do
                    {
                        if (property.name == "m_Script" || property.name == "m_GameObject") continue;
                        output.CopyFromSerializedProperty(property);
                    } while (property.NextVisible(false));
                output.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void BakeStage6Navigation(GameObject environment)
    {
        // 원본 Stage6은 Act2_Stage4의 NavMesh를 가리킨다. SW 환경만 수집해 별도 데이터를 만든다.
        NavMeshSurface surface = environment.GetComponentsInChildren<NavMeshSurface>(true).Single();
        Transform floor = surface.transform;
        Transform[] siblings = environment.transform.Cast<Transform>().Where(t => t != floor).ToArray();
        CollectObjects previousCollection = surface.collectObjects;
        try
        {
            // Additive로 열려 있는 사용자/원본 씬의 지형을 함께 굽지 않는다.
            foreach (Transform sibling in siblings) sibling.SetParent(floor, true);
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Stage6 NavMesh bake failed.");
            const string navPath = Folder + "Act1_Stage6_MirrorSessionTest_NavMesh.asset";
            NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            else
            {
                NavMeshData generated = surface.navMeshData;
                EditorUtility.CopySerialized(generated, existing);
                surface.RemoveData();
                surface.navMeshData = existing;
                surface.AddData();
                Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
            }
        }
        finally
        {
            foreach (Transform sibling in siblings) sibling.SetParent(environment.transform, true);
            surface.collectObjects = previousCollection;
        }
    }

    private static void ConfigureElevator(GameObject environment)
    {
        YJ_PointMove[] movers = environment.GetComponentsInChildren<YJ_PointMove>(true);
        if (movers.Length != 2) throw new InvalidOperationException("Stage5 requires the two authored PointMove controllers.");
        SerializedObject mover = new(movers.Single(m => m.name == "Collider1"));
        Rigidbody platform = Required(mover, "platformRigidbody").objectReferenceValue as Rigidbody;
        SerializedProperty points = Required(mover, "movePoints");
        if (platform == null || points.arraySize != 2) throw new InvalidOperationException("Stage5 platform/path is incomplete.");
        if (movers.Any(m => new SerializedObject(m).FindProperty("platformRigidbody").objectReferenceValue != platform))
            throw new InvalidOperationException("Stage5 controllers reference different platforms.");
        Transform start = points.GetArrayElementAtIndex(0).objectReferenceValue as Transform;
        Transform end = points.GetArrayElementAtIndex(1).objectReferenceValue as Transform;
        if (start == null || end == null || start.IsChildOf(platform.transform) || end.IsChildOf(platform.transform))
            throw new InvalidOperationException("Stage5 move points must be outside the moving platform.");
        // Collider1 is only a thin entry line. Occupancy must cover the authored platform area.
        BoxCollider boarding = movers.Single(m => m.name == "Collider2").GetComponent<BoxCollider>();
        if (boarding == null || !boarding.isTrigger) throw new InvalidOperationException("Stage5 boarding trigger is missing.");
        foreach (YJ_PointMove original in movers) Object.DestroyImmediate(original);
        boarding.transform.SetParent(platform.transform, true);
        platform.isKinematic = true;
        platform.useGravity = false;
        platform.position = start.position;
        MirrorFourPlayerElevator_MirrorTest elevator = platform.gameObject.AddComponent<MirrorFourPlayerElevator_MirrorTest>();
        NetworkTransformReliable networkTransform = platform.GetComponent<NetworkTransformReliable>();
        networkTransform.syncDirection = SyncDirection.ServerToClient;
        networkTransform.target = platform.transform;
        // Empty marker only. The final walkable landing is verified by the main agent in Unity.
        GameObject landing = new("Safe Landing Candidate_MirrorTest");
        landing.transform.SetParent(environment.transform, false);
        landing.transform.position = new Vector3(0f, 12.7f, 15f);
        SerializedObject data = new(elevator);
        Required(data, "platformRigidbody").objectReferenceValue = platform;
        Required(data, "boardingTrigger").objectReferenceValue = boarding;
        // The actual mesh lives on the network root; toggling it would disable the NetworkIdentity itself.
        Required(data, "visualRoot").objectReferenceValue = null;
        Required(data, "alwaysAvailable").boolValue = true;
        Required(data, "startPoint").objectReferenceValue = start;
        Required(data, "destinationPoint").objectReferenceValue = end;
        Required(data, "safeLandingPoint").objectReferenceValue = landing.transform;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("SW/Mirror 테스트/Act1 전체 11개 씬 참조 검증")]
    public static void ValidateAll()
    {
        RequireEditMode();
        foreach (string name in CombatNames) ValidateAsset(Folder + name + "_MirrorSessionTest.unity", true);
        foreach (string name in new[] { "Lobby_MirrorTest", "StageSelect_MirrorSessionTest", "Act1_Camp_MirrorSessionTest", "Unknown_Stage_MirrorSessionTest" })
            ValidateAsset(Folder + name + ".unity", false);
        Debug.Log("[MirrorAct1] All 11 scenes passed missing-script and cross-scene-reference checks; seven combat scenes passed strict reference/wiring checks. Existing non-combat visual reference warnings are reported separately.");
    }

    private static void ValidateAsset(string path, bool combat)
    {
        RequireCleanIfLoaded(path);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) throw new InvalidOperationException("Missing scene: " + path);
        Scene preview = EditorSceneManager.OpenPreviewScene(path);
        try { ValidateScene(preview, combat); }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void ValidateScene(Scene scene, bool combat)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                throw new InvalidOperationException(scene.path + ": Missing script on " + transform.name);
            foreach (Component component in transform.GetComponents<Component>())
            {
                if (combat && component is MonoBehaviour &&
                    new[] { "YJ_PointMove", "YJ_StageManager", "WBH_EnemySpawnArea", "WBH_EnemySpawnManager", "T_PlayerController" }.Contains(component.GetType().Name))
                    throw new InvalidOperationException(scene.path + ": Original runtime controller " + component.GetType().Name);
                SerializedObject data = new(component);
                SerializedProperty property = data.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object reference = property.objectReferenceValue;
                    if (reference == null && property.objectReferenceInstanceIDValue != 0)
                    {
                        string message = scene.path + ": Broken reference " + component.name + "." + property.propertyPath;
                        // 기존 로비/캠프의 원본 연출 누락은 보고하되 새 전투 씬 검증과 구분한다.
                        bool existingVisual = !combat && ((component is UnityEngine.UI.Image && property.propertyPath == "m_Sprite") ||
                            (component is ParticleSystemRenderer && property.propertyPath.StartsWith("m_Materials.")) ||
                            (component is ParticleSystem && property.propertyPath.StartsWith("UVModule.sprites.")));
                        if (!existingVisual) throw new InvalidOperationException(message);
                        Debug.LogWarning("[MirrorAct1 existing visual] " + message);
                    }
                    GameObject referencedObject = reference is GameObject go ? go : (reference as Component)?.gameObject;
                    if (referencedObject != null && referencedObject.scene.IsValid() && referencedObject.scene != scene)
                        throw new InvalidOperationException(scene.path + ": Cross-scene reference " + component.name + "." + property.propertyPath);
                }
            }
        }
        if (!combat) return;
        if (Components<NetworkStartPosition>(scene).Count() != 4 ||
            Components<NetworkEnemyWaveSpawner_MirrorTest>(scene).Count() != 1 ||
            Components<MirrorStagePortalAdapter_MirrorTest>(scene).Count() != 1 ||
            Components<MirrorTestLocalPlayerCameraBinder>(scene).Count() != 1)
            throw new InvalidOperationException(scene.path + ": Four starts / spawner / portal adapter / camera binder required.");
        Find(scene, "CombatCinemachine_MirrorTest");
        SerializedObject spawner = new(Components<NetworkEnemyWaveSpawner_MirrorTest>(scene).Single());
        foreach (string field in new[] { "meleePrefab", "rangedPrefab", "bossPrefab", "enemyDataProvider" })
            if (Required(spawner, field).objectReferenceValue == null)
                throw new InvalidOperationException(scene.path + ": Spawner missing " + field);
        MirrorStagePortalAdapter_MirrorTest adapter = Components<MirrorStagePortalAdapter_MirrorTest>(scene).Single();
        SerializedObject portal = new(adapter);
        // The existing Stage1 template resolves these in Awake; new copies receive explicit bindings.
        if (Required(portal, "portalActive").objectReferenceValue == null && adapter.GetComponentInParent<YJ_PortalActive>() == null)
            throw new InvalidOperationException(scene.path + ": Portal has neither a binding nor its expected parent component.");
        if (scene.path.Contains("Act1_Stage5_"))
        {
            MirrorFourPlayerElevator_MirrorTest elevator = Components<MirrorFourPlayerElevator_MirrorTest>(scene).Single();
            SerializedObject data = new(elevator);
            if (!Required(data, "alwaysAvailable").boolValue || elevator.GetComponent<NetworkTransformReliable>().syncDirection != SyncDirection.ServerToClient)
                throw new InvalidOperationException("Stage5 requires an always available server-authoritative elevator.");
            foreach (string field in new[] { "platformRigidbody", "boardingTrigger", "startPoint", "destinationPoint", "safeLandingPoint" })
                if (Required(data, field).objectReferenceValue == null) throw new InvalidOperationException("Stage5 missing " + field);
            foreach (string field in new[] { "startPoint", "destinationPoint", "safeLandingPoint" })
                if (((Transform)Required(data, field).objectReferenceValue).IsChildOf(elevator.transform))
                    throw new InvalidOperationException("Stage5 path marker moves with platform: " + field);
        }
    }

    private static System.Collections.Generic.IEnumerable<T> Components<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));

    private static Transform Find(Scene scene, string name) => Components<Transform>(scene).Single(t => t.name == name);

    private static SerializedProperty Required(SerializedObject data, string name) => data.FindProperty(name) ??
        throw new InvalidOperationException(data.targetObject.GetType().Name + " requires serialized field " + name);

    private static void CopyTransform(Transform from, Transform to)
    {
        to.SetPositionAndRotation(from.position, from.rotation);
        to.localScale = from.localScale;
    }

    private static void RequireCleanIfLoaded(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        if (scene.isLoaded && scene.isDirty) throw new InvalidOperationException("Close or explicitly save this dirty scene first: " + path);
    }

    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run in Edit Mode after compilation finishes.");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>원본 스킬 실행기를 Mirror 플레이어에 연결하고 스킬 이동의 서버 권한 경계를 구성한다.</summary>
public static class MirrorSkillSceneSetup_MirrorTest
{
    private const string Folder = "Assets/SW/TEST/MirrorPlayerContext/Prefabs/";
    private const string GunnerControllerPath = "Assets/SW/TEST/MirrorCombat/Animations/GunnerSkills_MirrorTest.controller";
    private const string VisualFolder = "Assets/SW/TEST/MirrorCombat/Prefabs/SkillVisuals";
    public const string NetworkVisualPath = VisualFolder + "/NetworkSkillVisual_MirrorTest.prefab";

    [MenuItem("SW/Mirror 테스트/원본 스킬 연결")]
    public static void Connect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 끝난 Edit Mode에서 실행하세요.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Prefab Stage를 닫은 뒤 실행하세요.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("열린 씬의 미저장 변경을 먼저 처리하세요.");
        PrepareSkillVisuals();
        foreach (string character in new[] { "Fighter", "Gunner" })
        {
            string path = Folder + (character == "Fighter" ? "FighterNetworkPlayer.prefab" : "GunnerNetworkPlayer_MirrorTest.prefab");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
                throw new InvalidOperationException("편집 중인 프리팹을 닫아 주세요: " + path);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Character/Player/" + character + ".prefab");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigurePlayer(root, source);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        RegisterSkillVisualInScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[MirrorSkillSetup] Fighter/Gunner 원본 스킬·서버 이동·애니메이션 연결 완료");
    }

    public static void ConfigurePlayer(GameObject root, GameObject source)
    {
        var sourceSkill = (MonoBehaviour)source.GetComponent<FighterSkillController>() ?? source.GetComponent<GunnerSkillController>();
        if (sourceSkill == null) throw new InvalidOperationException("원본 스킬 실행기가 없습니다: " + source.name);
        var original = root.GetComponent(sourceSkill.GetType()) as MonoBehaviour ?? (MonoBehaviour)root.AddComponent(sourceSkill.GetType());
        EditorUtility.CopySerialized(sourceSkill, original);
        RemapOwnedReferences(original, source.transform, root.transform);
        var data = new SerializedObject(original);
        data.FindProperty("inputHandler").objectReferenceValue = null;
        var spawner = data.FindProperty("effectSpawner");
        if (spawner != null) spawner.objectReferenceValue = null;
        data.FindProperty("visibleSkillArea").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        original.enabled = false;

        var presentation = root.GetComponent<NetworkSkillPresentation_MirrorTest>() ?? root.AddComponent<NetworkSkillPresentation_MirrorTest>();
        var presentationData = new SerializedObject(presentation);
        presentationData.FindProperty("gunnerSkills").objectReferenceValue = original as GunnerSkillController;
        presentationData.FindProperty("visualPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkSkillVisual_MirrorTest>(NetworkVisualPath);
        presentationData.ApplyModifiedPropertiesWithoutUndo();

        var authority = root.GetComponent<FighterSkillAuthority_MirrorTest>();
        var authorityData = new SerializedObject(authority);
        authorityData.FindProperty("fighterSkills").objectReferenceValue = original as FighterSkillController;
        authorityData.FindProperty("gunnerSkills").objectReferenceValue = original as GunnerSkillController;
        authorityData.FindProperty("animationView").objectReferenceValue = root.GetComponent<WBH_PlayerAnimation_MirrorTest>();
        CopyArray(data.FindProperty("skills"), authorityData.FindProperty("skills"));
        var evolutions = authorityData.FindProperty("activeEvolutions");
        var originals = data.FindProperty("activeEvolutions");
        evolutions.arraySize = originals.arraySize;
        for (int i = 0; i < evolutions.arraySize; i++)
            evolutions.GetArrayElementAtIndex(i).intValue = originals.GetArrayElementAtIndex(i).intValue;
        authorityData.ApplyModifiedPropertiesWithoutUndo();

        var animator = root.GetComponent<Animator>();
        if (original is GunnerSkillController) animator.runtimeAnimatorController = PrepareGunnerController(source.GetComponent<Animator>().runtimeAnimatorController);
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var clips = animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
        var animationData = new SerializedObject(root.GetComponent<WBH_PlayerAnimation_MirrorTest>());
        animationData.FindProperty("fighterDash").objectReferenceValue = clips.FirstOrDefault(c => c.name.Contains("Fighter_Skill_Dash"));
        animationData.FindProperty("gunnerBackstepMove").objectReferenceValue = clips.FirstOrDefault(c => c.name.Contains("Gunner_Skill_Backstep_Move"));
        animationData.ApplyModifiedPropertiesWithoutUndo();

        var oldTransform = root.GetComponent<NetworkTransformReliable>();
        if (oldTransform != null && oldTransform is not PlayerNetworkTransform_MirrorTest)
        {
            string settings = JsonUtility.ToJson(oldTransform);
            var references = new List<(Component owner, string path)>();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component == oldTransform) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == oldTransform)
                        references.Add((component, property.propertyPath));
            }
            Object.DestroyImmediate(oldTransform);
            var replacement = root.AddComponent<PlayerNetworkTransform_MirrorTest>();
            JsonUtility.FromJsonOverwrite(settings, replacement);
            foreach (var reference in references)
            {
                var serialized = new SerializedObject(reference.owner);
                serialized.FindProperty(reference.path).objectReferenceValue = replacement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (component == null) throw new InvalidOperationException("스킬 연결 결과에 Missing Script가 있습니다.");
    }

    private static RuntimeAnimatorController PrepareGunnerController(RuntimeAnimatorController source)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(GunnerControllerPath) == null &&
            !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), GunnerControllerPath))
            throw new InvalidOperationException("거너 Mirror 컨트롤러를 복제할 수 없습니다.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GunnerControllerPath);
        var queue = new Queue<AnimatorStateMachine>(controller.layers.Select(l => l.stateMachine));
        while (queue.Count > 0)
        {
            var machine = queue.Dequeue();
            foreach (var child in machine.stateMachines) queue.Enqueue(child.stateMachine);
            foreach (var child in machine.states)
            {
                if (child.state.name is not ("BackStep_Shot_Idle" or "BackStep_Shot_Evo1")) continue;
                foreach (var transition in child.state.transitions)
                {
                    if (transition.destinationState == null || transition.destinationState.name != "BackStep_Step") continue;
                    // 짧은 클립의 마지막 프레임에 있는 사격 이벤트를 실행한 뒤 이동한다.
                    transition.exitTime = 1f;
                    EditorUtility.SetDirty(transition);
                }
            }
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void PrepareSkillVisuals()
    {
        if (!AssetDatabase.IsValidFolder(VisualFolder))
            AssetDatabase.CreateFolder("Assets/SW/TEST/MirrorCombat/Prefabs", "SkillVisuals");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Character/Player/Gunner.prefab");
        var skills = new SerializedObject(source.GetComponent<GunnerSkillController>()).FindProperty("skills");
        var sources = new List<GameObject>();
        for (int i = 0; i < skills.arraySize; i++)
        {
            var definition = (SkillDefinitionSO)skills.GetArrayElementAtIndex(i).objectReferenceValue;
            if (definition == null) continue;
            foreach (var prefab in new[] { definition.arcProjectilePrefab, definition.evoCannonProjectilePrefab, definition.bombPrefab, definition.evoDecoyPrefab })
                if (prefab != null && !sources.Contains(prefab)) sources.Add(prefab);
        }
        var visuals = new List<GameObject>();
        foreach (var prefab in sources)
        {
            var clone = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) throw new InvalidOperationException("원본 스킬에 Missing Script: " + prefab.name);
                    // 원본의 빛·회전 연출과 URP Light 설정은 보존한다. 피해 및 수명은 서버 원본만 실행한다.
                    string type = behaviour.GetType().FullName;
                    if (type is "SciFiArsenal.SciFiLightFade" or "SciFiArsenal.SciFiLightFlicker" or "SciFiArsenal.SciFiRotation" or "UnityEngine.Rendering.Universal.UniversalAdditionalLightData") continue;
                    // 사운드 작업 중인 원본 피치 조절 구성도 그대로 전달한다.
                    if (type == "SciFiArsenal.SciFiPitchRandomizer") continue;
                    Object.DestroyImmediate(behaviour);
                }
                foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
                clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                clone.transform.localScale = Vector3.one;
                visuals.Add(PrefabUtility.SaveAsPrefabAsset(clone, VisualFolder + "/" + prefab.name + "_Visual.prefab"));
            }
            finally { Object.DestroyImmediate(clone); }
        }
        var root = new GameObject("NetworkSkillVisual_MirrorTest");
        try
        {
            root.AddComponent<NetworkIdentity>();
            var networkTransform = root.AddComponent<NetworkTransformReliable>();
            networkTransform.target = root.transform;
            networkTransform.syncDirection = SyncDirection.ServerToClient;
            networkTransform.syncScale = true;
            var visual = root.AddComponent<NetworkSkillVisual_MirrorTest>();
            var data = new SerializedObject(visual);
            SetObjects(data.FindProperty("sourcePrefabs"), sources);
            SetObjects(data.FindProperty("visualPrefabs"), visuals);
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, NetworkVisualPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void SetObjects(SerializedProperty array, List<GameObject> values)
    {
        array.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void RegisterSkillVisualInScenes()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkVisualPath);
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/SW/TEST/MirrorCombat/Scenes", "Assets/SW/TEST/MirrorPlayerContext/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                bool changed = false;
                foreach (var manager in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MirrorTestNetworkManager>(true)))
                {
                    if (manager.spawnPrefabs.Contains(prefab)) continue;
                    manager.spawnPrefabs.Add(prefab);
                    EditorUtility.SetDirty(manager);
                    changed = true;
                }
                if (changed) EditorSceneManager.SaveScene(scene);
            }
            finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
    }

    private static void CopyArray(SerializedProperty source, SerializedProperty target)
    {
        target.arraySize = source.arraySize;
        for (int i = 0; i < source.arraySize; i++)
            target.GetArrayElementAtIndex(i).objectReferenceValue = source.GetArrayElementAtIndex(i).objectReferenceValue;
    }

    private static void RemapOwnedReferences(Component component, Transform source, Transform target)
    {
        var data = new SerializedObject(component);
        var property = data.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            Object value = property.objectReferenceValue;
            Transform original = value is Component c ? c.transform : (value as GameObject)?.transform;
            if (original == null || (original != source && !original.IsChildOf(source))) continue;
            string path = AnimationUtility.CalculateTransformPath(original, source);
            Transform mapped = path.Length == 0 ? target : target.Find(path);
            property.objectReferenceValue = mapped == null ? null : value is GameObject ? mapped.gameObject : mapped.GetComponent(value.GetType());
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}

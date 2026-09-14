using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>검증된 캠프 UI를 Mirror 전투 씬에 배치하고 로컬 플레이어 바인딩을 연결한다.</summary>
public static class MirrorGameplayUISetup_MirrorTest
{
    private const string Folder = "Assets/SW/TEST/MirrorCombat/Scenes/";
    private const string UiRoot = "-------------------------------UI";

    [MenuItem("SW/Mirror 테스트/전투 HUD 및 팝업 연결")]
    public static void Connect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 끝난 Edit Mode에서 실행하세요.");
        string sourcePath = MirrorTestNetworkManager.SessionCampGameplayScene;
        string[] paths = Enumerable.Range(1, 6).Select(i => Folder + "Act1_Stage" + i + "_MirrorSessionTest.unity")
            .Concat(new[] { Folder + "Act1_BossStage_MirrorSessionTest.unity", Folder + "Act1_Stage1_MirrorCombatTest.unity" }).ToArray();
        foreach (string path in paths.Append(sourcePath))
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            if (scene.isLoaded && scene.isDirty) throw new InvalidOperationException("저장하지 않은 씬: " + path);
        }
        Scene previous = SceneManager.GetActiveScene();
        Scene source = SceneManager.GetSceneByPath(sourcePath);
        bool openedSource = !source.isLoaded;
        if (openedSource) source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
        try
        {
            GameObject template = source.GetRootGameObjects().Single(x => x.name == UiRoot);
            Configure(source, template);
            Save(source);
            foreach (string path in paths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    GameObject root = scene.GetRootGameObjects().SingleOrDefault(x => x.name == UiRoot);
                    if (root == null)
                    {
                        root = Object.Instantiate(template);
                        root.name = UiRoot;
                        SceneManager.MoveGameObjectToScene(root, scene);
                    }
                    Configure(scene, root);
                    Save(scene);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
        }
        finally
        {
            if (openedSource) EditorSceneManager.CloseScene(source, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
        MirrorQuestSceneSetup_MirrorTest.Connect();
    }

    private static void Configure(Scene scene, GameObject root)
    {
        var binder = InScene<MirrorTestLocalPlayerUIBinder>(scene).Single();
        // Mirror Binder와 동시에 I/ESC를 처리하면 같은 프레임에 열고 다시 닫힌다.
        foreach (var input in InScene<KY_UIInputManager>(scene)) input.enabled = false;
        var popup = root.GetComponentInChildren<KY_PopupManager>(true);
        var hud = root.GetComponentInChildren<PlayerHudEventBridge_MirrorTest>(true);
        SetReference(binder, "skillPopup", root.GetComponentInChildren<SkillPopupController>(true));
        foreach (var location in hud.GetComponentsInChildren<KY_LocationView>(true))
        {
            var locationData = new SerializedObject(location);
            locationData.FindProperty("usesExternalLocation").boolValue = true;
            locationData.ApplyModifiedPropertiesWithoutUndo();
        }
        var placeholder = root.transform.Find("Canvas/NameTag1");
        if (placeholder != null) placeholder.gameObject.SetActive(false);
        // NPC Start에서 숨기기 전에 이름표 Awake가 텍스트 참조를 초기화하게 한다.
        foreach (var hover in InScene<YJ_OutlineOnMouseHover>(scene))
        {
            var tag = new SerializedObject(hover).FindProperty("nameTag").objectReferenceValue as YJ_NameTag;
            if (tag != null) tag.gameObject.SetActive(true);
        }
        var cooldown = hud.GetComponent<MirrorCooldownHud_MirrorTest>() ?? hud.gameObject.AddComponent<MirrorCooldownHud_MirrorTest>();
        foreach (var original in root.GetComponentsInChildren<CooldownIconUIContainer>(true))
        {
            var data = new SerializedObject(original);
            var prefab = data.FindProperty("iconSlotPrefab").objectReferenceValue as Component;
            SetReference(cooldown, "uniqueEffectSlotPrefab", prefab != null ? prefab.gameObject : null);
            SetReference(cooldown, "uniqueEffectSlotParent", data.FindProperty("slotParent").objectReferenceValue ?? original.transform);
            original.enabled = false;
        }
        foreach (var original in root.GetComponentsInChildren<BuffIconUIContainer>(true))
        {
            var replacement = original.GetComponent<BuffIconUIContainer_MirrorTest>() ?? original.gameObject.AddComponent<BuffIconUIContainer_MirrorTest>();
            var data = new SerializedObject(original);
            SetReference(replacement, "iconSlotPrefab", data.FindProperty("iconSlotPrefab").objectReferenceValue);
            SetReference(replacement, "slotParent", data.FindProperty("slotParent").objectReferenceValue ?? original.transform);
            original.enabled = false;
        }
        // 쿨타임은 같은 로컬 Context를 쓰는 Mirror HUD가 표시한다.
        foreach (var original in root.GetComponentsInChildren<KY_SkillView>(true)) original.enabled = false;

        var status = root.GetComponentInChildren<KY_StatusPopup_MirrorTest>(true);
        foreach (var original in root.GetComponentsInChildren<KY_StatusPopup>(true))
        {
            status = original.GetComponent<KY_StatusPopup_MirrorTest>() ?? original.gameObject.AddComponent<KY_StatusPopup_MirrorTest>();
            foreach (FieldInfo field in typeof(KY_StatusPopup).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                FieldInfo destination = typeof(KY_StatusPopup_MirrorTest).GetField(field.Name);
                if (destination != null && destination.FieldType == field.FieldType) destination.SetValue(status, field.GetValue(original));
            }
            ReplaceReferences(root, original, status);
            Object.DestroyImmediate(original);
        }
        foreach (var pause in root.GetComponentsInChildren<KY_PausePopup>(true)) pause.PauseGameTime = false;
        SetReference(binder, "popupManager", popup);
        SetReference(binder, "formalHudBridge", hud);
        SetReference(binder, "statusPopup", status);
        var oldHud = binder.GetComponent<MirrorTestPlayerHud>();
        if (oldHud != null) oldHud.enabled = false;
        SetReference(binder, "playerHud", null);

        var popupData = new SerializedObject(popup);
        var targets = popupData.FindProperty("modalInputTargets");
        for (int i = targets.arraySize - 1; i >= 0; i--)
        {
            var target = targets.GetArrayElementAtIndex(i).objectReferenceValue as CanvasGroup;
            if (target != null && target.gameObject.scene == scene) continue;
            targets.GetArrayElementAtIndex(i).objectReferenceValue = null;
            targets.DeleteArrayElementAtIndex(i);
        }
        var inventory = InScene<InventoryView>(scene).Single();
        var canvas = inventory.GetComponentInParent<Canvas>(true);
        var group = canvas.GetComponent<CanvasGroup>() ?? canvas.gameObject.AddComponent<CanvasGroup>();
        if (!Enumerable.Range(0, targets.arraySize).Any(i => targets.GetArrayElementAtIndex(i).objectReferenceValue == group))
        {
            targets.InsertArrayElementAtIndex(targets.arraySize);
            targets.GetArrayElementAtIndex(targets.arraySize - 1).objectReferenceValue = group;
        }
        popupData.ApplyModifiedPropertiesWithoutUndo();
        RemoveReplacedUi(scene);
    }

    /// <summary>새 UI로 연결이 끝난 이전 Mirror HUD만 외부 참조가 없을 때 제거한다.</summary>
    private static void RemoveReplacedUi(Scene scene)
    {
        var oldRoots = scene.GetRootGameObjects().Where(x => x.name is "MergeTestHud_MirrorTest" or "MergeTestStatusCanvas_MirrorTest").ToArray();
        if (oldRoots.Length == 0) return;
        foreach (var component in scene.GetRootGameObjects().Except(oldRoots).SelectMany(x => x.GetComponentsInChildren<Component>(true)))
        {
            if (component == null) continue;
            var data = new SerializedObject(component);
            var property = data.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var reference = property.objectReferenceValue;
                Transform target = reference is Component child ? child.transform : (reference as GameObject)?.transform;
                if (target != null && oldRoots.Any(x => target.IsChildOf(x.transform)))
                    throw new InvalidOperationException("이전 HUD 참조가 남았습니다: " + component.name + "." + property.propertyPath);
            }
        }
        foreach (var old in oldRoots) Object.DestroyImmediate(old);
    }

    private static void ReplaceReferences(GameObject root, Object from, Object to)
    {
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component == from) continue;
            var data = new SerializedObject(component);
            var property = data.GetIterator();
            bool changed = false;
            while (property.NextVisible(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == from)
                {
                    property.objectReferenceValue = to;
                    changed = true;
                }
            if (changed) data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
    private static void SetReference(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        var property = data.FindProperty(field) ?? throw new InvalidOperationException("필드 누락: " + field);
        property.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("씬 저장 실패: " + scene.path);
    }
}

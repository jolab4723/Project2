using System;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>정식 Act1·Act2의 적 정의와 원본 외형을 네트워크 생성 가능한 프리팹으로 연결합니다.</summary>
public static class MirrorProductionEnemySetup
{
    public const string Folder = "Assets/SW/Prefabs/Network/Enemy";

    [MenuItem("SW/Mirror/정식 Act1·Act2 적 프리팹 준비")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        EnsureFolder(Folder);
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Maps/Act1_Maps/Act1_Stage1/Act1_Stage1.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var provider = roots.SelectMany(g => g.GetComponentsInChildren<WBH_EnemyDataProvider>(true)).First();
            var pool = roots.SelectMany(g => g.GetComponentsInChildren<WBH_EnemyPoolManager>(true)).First();
            var entries = new SerializedObject(pool).FindProperty("enemyPools");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var definition = entry.FindPropertyRelative("enemyDef").objectReferenceValue as EnemySystem.EnemyDefinitionSO;
                if (definition == null || definition.enemyGrade == EnemyGrade.Hidden ||
                    (definition.enemyGrade == EnemyGrade.Boss && definition.enemyId != "enemy.boss.boss.SpiderX" &&
                     definition.enemyId != "enemy.boss.boss.GoliathT")) continue;
                string path = Folder + "/" + definition.enemyId + ".prefab";
                string source = definition.enemyId == "enemy.boss.boss.SpiderX"
                    ? "Assets/SW/Prefabs/Network/Combat/Boss_Act_01.prefab"
                    : AssetDatabase.GetAssetPath(entry.FindPropertyRelative("prefab").objectReferenceValue);
                if (!provider.TryCreateEnemyInfo(definition.enemyId, new WBH_EnemyStatContext(1, "normal", 1), out var info))
                    throw new InvalidOperationException("EnemyInfo 생성 실패: " + definition.enemyId);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null && !AssetDatabase.CopyAsset(source, path))
                    throw new InvalidOperationException("프리팹 복사 실패: " + source);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.name = definition.enemyId;
                    Ensure<NetworkIdentity>(root);
                    var transformSync = Ensure<NetworkTransformReliable>(root);
                    transformSync.target = root.transform;
                    transformSync.syncDirection = SyncDirection.ServerToClient;
                    var animator = root.GetComponent<Animator>();
                    if (animator != null && animator.runtimeAnimatorController != null)
                        Set(Ensure<NetworkAnimator>(root), "animator", animator);
                    else if (root.TryGetComponent<NetworkAnimator>(out var animationSync))
                        Object.DestroyImmediate(animationSync);
                    Ensure<NetworkEnemyPattern>(root);
                    var authority = Ensure<NetworkEnemyAuthority>(root);
                    var data = new SerializedObject(authority);
                    data.FindProperty("enemyInfo").boxedValue = info;
                    if (definition.enemyId != "enemy.boss.boss.SpiderX")
                        data.FindProperty("projectilePrefab").objectReferenceValue = new SerializedObject(
                            AssetDatabase.LoadAssetAtPath<NetworkEnemyAuthority>(
                                "Assets/SW/Prefabs/Network/Combat/Normal_Range.prefab"))
                            .FindProperty("projectilePrefab").objectReferenceValue;
                    if (definition.enemyId == "enemy.boss.boss.GoliathT")
                        data.FindProperty("useAnimatorOnlyDeathPresentation").boolValue = true;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    var view = Ensure<NetworkEnemyCombatView>(root);
                    var originalView = root.GetComponent<WBH_EnemyView>();
                    if (originalView != null)
                    {
                        Set(view, "productionView", originalView);
                        var sourceView = new SerializedObject(originalView);
                        sourceView.FindProperty("externalPresentation").boolValue = true;
                        sourceView.ApplyModifiedPropertiesWithoutUndo();
                        originalView.enabled = true;
                    }
                    foreach (var driver in root.GetComponents<Behaviour>())
                        if (driver is WBH_EnemyController or WBH_EnemyPattern or WBH_EnemyCombat or
                            WBH_EnemyAnimation or EnemyKillReward or WBHEnemyItemDropAdapter or WBHEnemyDestructionAdapter)
                            driver.enabled = false;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static T Ensure<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();
    private static void Set(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
}

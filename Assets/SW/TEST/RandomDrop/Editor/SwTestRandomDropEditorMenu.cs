using System;
using System.IO;
using ItemSystem;
using UnityEditor;
using UnityEngine;

namespace SW.Test.RandomDrop.Editor
{
    public static class SwTestRandomDropEditorMenu
    {
        private const string AssetFolder = "Assets/SW/TEST/RandomDrop/Assets";
        private const string DefaultTablePath = AssetFolder + "/SwTestDefaultEquipmentDropTable.asset";

        [MenuItem("SW/TEST/Random Drop/Create Default Drop Table")]
        public static SwTestEquipmentDropTableSO CreateDefaultDropTable()
        {
            EnsureAssetFolder(AssetFolder);

            SwTestEquipmentDropTableSO table = AssetDatabase.LoadAssetAtPath<SwTestEquipmentDropTableSO>(DefaultTablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<SwTestEquipmentDropTableSO>();
                AssetDatabase.CreateAsset(table, DefaultTablePath);
            }

            table.ResetToDefaultTable();
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = table;

            Debug.Log($"[SW TEST 드랍] 기본 드랍 테이블 준비 완료: {DefaultTablePath}");
            return table;
        }

        [MenuItem("SW/TEST/Random Drop/Create Tester GameObject")]
        public static void CreateTesterGameObject()
        {
            SwTestEquipmentDropTableSO table = AssetDatabase.LoadAssetAtPath<SwTestEquipmentDropTableSO>(DefaultTablePath);
            if (table == null)
                table = CreateDefaultDropTable();

            GameObject testerObject = GameObject.Find("SW_TEST_RandomDropTester");
            if (testerObject == null)
                testerObject = new GameObject("SW_TEST_RandomDropTester");

            SwTestEquipmentDropTester tester = testerObject.GetComponent<SwTestEquipmentDropTester>();
            if (tester == null)
                tester = testerObject.AddComponent<SwTestEquipmentDropTester>();

            SerializedObject serializedTester = new SerializedObject(tester);
            serializedTester.FindProperty("dropTable").objectReferenceValue = table;
            serializedTester.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = testerObject;
            Debug.Log("[SW TEST 드랍] 테스트 오브젝트 준비 완료. 플레이 중 1/2/3/4 키로 등급별 1회 드랍 테스트, F5 키로 선택 등급 시뮬레이션을 실행할 수 있습니다.");
        }

        [MenuItem("SW/TEST/Random Drop/Validate Drop Rarity Modifier")]
        public static void ValidateDropRarityModifier()
        {
            if (Application.isPlaying || Mirror.NetworkServer.active || Mirror.NetworkClient.active)
                throw new InvalidOperationException("세션을 종료한 Edit Mode에서 실행하세요.");
            var effect = ScriptableObject.CreateInstance<DropRarityModifierUniqueEffectSO>();
            effect.coefficients = new[] { 1.2f };
            var definition = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            definition.category = ItemCategory.Relic;
            definition.itemWidth = definition.itemHeight = 1;
            definition.uniqueEffect = effect;
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Drop rarity check");
            root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
            var grid = root.AddComponent<InventoryGrid>();
            var inventory = root.AddComponent<InventoryController>();
            var owner = root.AddComponent<PlayerContext>();
            var serializedInventory = new SerializedObject(inventory);
            serializedInventory.FindProperty("playerGrid").objectReferenceValue = grid;
            serializedInventory.ApplyModifiedPropertiesWithoutUndo();
            var serializedOwner = new SerializedObject(owner);
            serializedOwner.FindProperty("inventory").objectReferenceValue = inventory;
            serializedOwner.ApplyModifiedPropertiesWithoutUndo();
            typeof(InventoryGrid).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(grid, null);
            var first = new InventoryItem(new ItemInstance { definition = definition, instanceId = "drop-rarity-check-1" });
            var second = new InventoryItem(new ItemInstance { definition = definition, instanceId = "drop-rarity-check-2" });

            try
            {
                grid.TryPlaceItem(first, 0, 0);
                RequireMultiplier(ItemRarity.Common, 1f, owner);
                RequireMultiplier(ItemRarity.Rare, 1.2f, owner);
                RequireMultiplier(ItemRarity.Rare, 1f, null);

                grid.TryPlaceItem(second, 1, 0);
                RequireMultiplier(ItemRarity.Unique, 1.2f, owner);
                grid.TryRemoveItem(first);
                RequireMultiplier(ItemRarity.Legendary, 1.2f, owner);

                grid.TryRemoveItem(second);
                RequireMultiplier(ItemRarity.Unique, 1f, owner);
                Debug.Log("[SW TEST 드랍] 희귀도 가중치 효과 검증을 통과했습니다.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(effect);
            }
        }

        private static void RequireMultiplier(ItemRarity rarity, float expected, PlayerContext owner)
        {
            float actual = DropRarityModifierUniqueEffectSO.GetRarityWeightMultiplier(rarity, owner);
            if (!Mathf.Approximately(actual, expected))
            {
                throw new InvalidOperationException(
                    $"{rarity} 가중치가 잘못되었습니다. 예상: {expected}, 실제: {actual}");
            }
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;

            string normalized = assetFolder.Replace("\\", "/");
            string parent = Path.GetDirectoryName(normalized).Replace("\\", "/");
            string folderName = Path.GetFileName(normalized);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}

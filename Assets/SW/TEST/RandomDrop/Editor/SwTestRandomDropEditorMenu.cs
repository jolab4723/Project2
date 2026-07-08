using System.IO;
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

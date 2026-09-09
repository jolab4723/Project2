using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using ItemSystem;

namespace DataSystem
{
    /// <summary>
    /// ItemDisplayNameExcelToJson 결과물(JSON)을 읽어서 ItemDisplayNameDatabaseSO를 갱신한다.
    /// ItemDisplayNameDatabaseSO의 korLabels/engLabels/jpnLabels/chnLabels는 private [SerializeField]라
    /// 그 파일은 그대로 두고 SerializedObject/SerializedProperty로 직접 써넣는다(QuestLabelSOImporter와 동일).
    /// </summary>
    public static class ItemDisplayNameSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/ItemData/2. JSONFile";
        private const string DefaultOutputAssetPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/LabelData/ItemDisplayNameDatabase.asset";

        [MenuItem("DataLoader/Item Display Name/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select item display name JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Import(jsonPath, DefaultOutputAssetPath);
        }

        [MenuItem("DataLoader/Item Display Name/0. Run All Steps")]
        public static void RunAllSteps()
        {
            string jsonPath = ItemDisplayNameExcelToJson.ConvertWithDefaultPaths();
            if (string.IsNullOrEmpty(jsonPath))
            {
                Debug.LogError("[ItemDisplayName] 엑셀을 찾지 못해 중단했습니다.");
                return;
            }

            ImportWithDefaultPaths(jsonPath);
        }

        /// <summary>대화상자 없이 기본 경로만으로 가져온다. 통합 실행(0. Run All Steps)에서 쓴다.</summary>
        public static void ImportWithDefaultPaths(string jsonPath)
        {
            Import(jsonPath, DefaultOutputAssetPath);
        }

        public static void Import(string jsonPath, string outputAssetPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[ItemDisplayName] JSON file not found: {absoluteJsonPath}");
                return;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            ItemDisplayNameJsonData data = JsonConvert.DeserializeObject<ItemDisplayNameJsonData>(json);
            if (data == null)
            {
                Debug.LogError("[ItemDisplayName] JSON parse failed.");
                return;
            }

            ItemDisplayNameDatabaseSO so = GetOrCreateAsset(outputAssetPath);
            if (so == null)
                return;

            SerializedObject serialized = new SerializedObject(so);
            int korCount = WriteLabels(serialized.FindProperty("korLabels"), data.korLabels);
            int engCount = WriteLabels(serialized.FindProperty("engLabels"), data.engLabels);
            int jpnCount = WriteLabels(serialized.FindProperty("jpnLabels"), data.jpnLabels);
            int chnCount = WriteLabels(serialized.FindProperty("chnLabels"), data.chnLabels);
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ItemDisplayName] SO 갱신 완료: {outputAssetPath}\n" +
                      $"KOR: {korCount}, ENG: {engCount}, JPN: {jpnCount}, CHN: {chnCount}");
        }

        private static int WriteLabels(SerializedProperty labelsProp, List<ItemDisplayNameRow> rows)
        {
            labelsProp.ClearArray();

            int index = 0;
            foreach (ItemDisplayNameRow row in rows)
            {
                if (string.IsNullOrEmpty(row.key))
                {
                    Debug.LogWarning("[ItemDisplayName] key가 비어있는 행을 건너뜁니다.");
                    continue;
                }

                labelsProp.InsertArrayElementAtIndex(index);
                SerializedProperty element = labelsProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("key").stringValue = row.key;
                element.FindPropertyRelative("label").stringValue = row.label;
                index++;
            }

            return index;
        }

        private static ItemDisplayNameDatabaseSO GetOrCreateAsset(string path)
        {
            ItemDisplayNameDatabaseSO asset = AssetDatabase.LoadAssetAtPath<ItemDisplayNameDatabaseSO>(path);
            if (asset != null)
                return asset;

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                Debug.LogError($"[ItemDisplayName] Asset already exists but type is not ItemDisplayNameDatabaseSO: {path}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<ItemDisplayNameDatabaseSO>();
            string directory = Path.GetDirectoryName(path).Replace("\\", "/");
            EnsureAssetFolder(directory);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
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

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }
}

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// RestDataExcelToJson 결과물(JSON)을 읽어서 RestDatabaseSO를 갱신한다.
    /// restSettings가 private [SerializeField]라 SerializedObject로 직접 써넣는다(UILabelSOImporter와 동일).
    /// </summary>
    public static class RestDataSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/RestData/2. JSONFile";
        private const string DefaultOutputAssetPath = "Assets/Resources/DataFiles/RestData/3. GeneratedAssets/RestDatabase.asset";

        [MenuItem("DataLoader/Rest Data/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select rest data JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Import(jsonPath, DefaultOutputAssetPath);
        }

        [MenuItem("DataLoader/Rest Data/0. Run All Steps")]
        public static void RunAllSteps()
        {
            string jsonPath = RestDataExcelToJson.ConvertWithDefaultPaths();
            if (string.IsNullOrEmpty(jsonPath))
            {
                Debug.LogError("[RestData] 엑셀을 찾지 못해 중단했습니다.");
                return;
            }

            Import(jsonPath, DefaultOutputAssetPath);
        }

        public static void Import(string jsonPath, string outputAssetPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[RestData] JSON file not found: {absoluteJsonPath}");
                return;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            RestDataJsonData data = JsonConvert.DeserializeObject<RestDataJsonData>(json);
            if (data == null)
            {
                Debug.LogError("[RestData] JSON parse failed.");
                return;
            }

            RestDatabaseSO so = GetOrCreateAsset(outputAssetPath);
            if (so == null)
                return;

            SerializedObject serialized = new SerializedObject(so);
            int count = WriteSettings(serialized.FindProperty("restSettings"), data.restSettings);
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RestData] SO 갱신 완료: {outputAssetPath}\n설정 행: {count}개");
        }

        private static int WriteSettings(SerializedProperty settingsProp, List<RestDataRow> rows)
        {
            settingsProp.ClearArray();

            int index = 0;
            foreach (RestDataRow row in rows)
            {
                if (string.IsNullOrEmpty(row.restId))
                {
                    Debug.LogWarning("[RestData] restId가 비어있는 행을 건너뜁니다.");
                    continue;
                }

                settingsProp.InsertArrayElementAtIndex(index);
                SerializedProperty element = settingsProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("restId").stringValue = row.restId;
                element.FindPropertyRelative("baseHealPercent").floatValue = row.baseHealPercent;
                element.FindPropertyRelative("creditPerAct").intValue = row.creditPerAct;
                element.FindPropertyRelative("potionRechargeAmount").intValue = row.potionRechargeAmount;
                index++;
            }

            return index;
        }

        private static RestDatabaseSO GetOrCreateAsset(string path)
        {
            RestDatabaseSO asset = AssetDatabase.LoadAssetAtPath<RestDatabaseSO>(path);
            if (asset != null)
                return asset;

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                Debug.LogError($"[RestData] Asset already exists but type is not RestDatabaseSO: {path}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<RestDatabaseSO>();
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

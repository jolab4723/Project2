using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// StatLabelExcelToJson 결과물(JSON)을 읽어서 StatLabelDatabaseSO를 갱신한다.
    /// StatLabelDatabaseSO의 korLabels/engLabels는 private [SerializeField]라
    /// 그 파일은 그대로 두고 SerializedObject/SerializedProperty로 직접 써넣는다.
    /// </summary>
    public static class StatLabelSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/CharData/JSONFile";
        private const string DefaultOutputAssetPath = "Assets/WJ_TestPlace/Data/Player/StatLabelDatabase.asset";

        [MenuItem("DataLoader/Stat Label/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select stat label JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Import(jsonPath, DefaultOutputAssetPath);
        }

        public static void Import(string jsonPath, string outputAssetPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[StatLabel] JSON file not found: {absoluteJsonPath}");
                return;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            StatLabelJsonData data = JsonConvert.DeserializeObject<StatLabelJsonData>(json);
            if (data == null)
            {
                Debug.LogError("[StatLabel] JSON parse failed.");
                return;
            }

            StatLabelDatabaseSO so = GetOrCreateAsset(outputAssetPath);
            if (so == null)
                return;

            SerializedObject serialized = new SerializedObject(so);
            int korCount = WriteLabels(serialized.FindProperty("korLabels"), data.korLabels);
            int engCount = WriteLabels(serialized.FindProperty("engLabels"), data.engLabels);
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StatLabel] SO 갱신 완료: {outputAssetPath}\n" +
                      $"KOR: {korCount}, ENG: {engCount}");
        }

        private static int WriteLabels(SerializedProperty labelsProp, List<StatLabelRow> rows)
        {
            labelsProp.ClearArray();

            int index = 0;
            foreach (StatLabelRow row in rows)
            {
                if (string.IsNullOrEmpty(row.statKey))
                {
                    Debug.LogWarning("[StatLabel] statKey가 비어있는 행을 건너뜁니다.");
                    continue;
                }

                labelsProp.InsertArrayElementAtIndex(index);
                SerializedProperty element = labelsProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("statKey").stringValue = row.statKey;
                element.FindPropertyRelative("label").stringValue = row.label;
                index++;
            }

            return index;
        }

        private static StatLabelDatabaseSO GetOrCreateAsset(string path)
        {
            StatLabelDatabaseSO asset = AssetDatabase.LoadAssetAtPath<StatLabelDatabaseSO>(path);
            if (asset != null)
                return asset;

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                Debug.LogError($"[StatLabel] Asset already exists but type is not StatLabelDatabaseSO: {path}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<StatLabelDatabaseSO>();
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

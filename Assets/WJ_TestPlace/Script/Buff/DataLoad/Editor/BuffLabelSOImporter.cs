using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// BuffLabelExcelToJson 결과물(JSON)을 읽어서 BuffLabelDatabaseSO를 갱신한다.
    /// BuffLabelDatabaseSO의 korLabels/engLabels/jpnLabels/chnLabels는 private [SerializeField]라
    /// 그 파일은 그대로 두고 SerializedObject/SerializedProperty로 직접 써넣는다(StatLabel과 동일 방식).
    /// </summary>
    public static class BuffLabelSOImporter
    {
        private const string DefaultJsonPath = "Assets/Resources/DataFiles/BuffData/2. JSONFile/BuffLabel.json";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/BuffData/1. ExcelFile/BuffLabel.xlsx";
        private const string DefaultOutputAssetPath = "Assets/Resources/DataFiles/BuffData/3. GeneratedAssets/BuffLabelDatabase.asset";

        [MenuItem("DataLoader/Buff Label/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            Import(DefaultJsonPath, DefaultOutputAssetPath);
        }

        [MenuItem("DataLoader/Buff Label/0. Run All Steps")]
        public static void RunAllSteps()
        {
            string excelPath = AssetPathToAbsolutePath(DefaultExcelPath);
            string jsonPath = AssetPathToAbsolutePath(DefaultJsonPath);

            BuffLabelExcelToJson.Convert(excelPath, jsonPath);
            Import(jsonPath, DefaultOutputAssetPath);
        }

        public static void Import(string jsonPath, string outputAssetPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[BuffLabel] JSON file not found: {absoluteJsonPath}");
                return;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            BuffLabelJsonData data = JsonConvert.DeserializeObject<BuffLabelJsonData>(json);
            if (data == null)
            {
                Debug.LogError("[BuffLabel] JSON parse failed.");
                return;
            }

            BuffLabelDatabaseSO so = GetOrCreateAsset(outputAssetPath);
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

            Debug.Log($"[BuffLabel] SO 갱신 완료: {outputAssetPath}\n" +
                      $"KOR: {korCount}, ENG: {engCount}, JPN: {jpnCount}, CHN: {chnCount}");
        }

        private static int WriteLabels(SerializedProperty labelsProp, List<BuffLabelRow> rows)
        {
            labelsProp.ClearArray();

            int index = 0;
            foreach (BuffLabelRow row in rows)
            {
                if (string.IsNullOrEmpty(row.buffId))
                {
                    Debug.LogWarning("[BuffLabel] buffId가 비어있는 행을 건너뜁니다.");
                    continue;
                }

                labelsProp.InsertArrayElementAtIndex(index);
                SerializedProperty element = labelsProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("buffId").stringValue = row.buffId;
                element.FindPropertyRelative("buffName").stringValue = row.buffName;
                index++;
            }

            return index;
        }

        private static BuffLabelDatabaseSO GetOrCreateAsset(string path)
        {
            BuffLabelDatabaseSO asset = AssetDatabase.LoadAssetAtPath<BuffLabelDatabaseSO>(path);
            if (asset != null)
                return asset;

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                Debug.LogError($"[BuffLabel] Asset already exists but type is not BuffLabelDatabaseSO: {path}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<BuffLabelDatabaseSO>();
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

using System.Collections.Generic;
using System.IO;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// UniqueEffectLabelExcelToJson 결과물(JSON)을 읽어서 UniqueEffectLabelDatabaseSO를 갱신한다.
    /// korLabels/engLabels/jpnLabels/chnLabels는 private [SerializeField]라
    /// 그 파일은 그대로 두고 SerializedObject/SerializedProperty로 직접 써넣는다.
    /// </summary>
    public static class UniqueEffectLabelSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/ItemData/2. JSONFile";
        private const string DefaultOutputAssetPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/LabelData/UniqueEffectLabelDatabase.asset";

        [MenuItem("DataLoader/Unique Effect Label/0. Run All Steps")]
        public static void RunAllSteps()
        {
            Debug.Log("[UniqueEffectLabel] ===== 통합 실행 시작 =====");

            string jsonPath = UniqueEffectLabelExcelToJson.ConvertWithDefaultPaths();
            if (string.IsNullOrEmpty(jsonPath))
            {
                Debug.LogError("[UniqueEffectLabel] 엑셀을 찾지 못해 중단했습니다.");
                return;
            }

            Import(jsonPath, DefaultOutputAssetPath);

            Debug.Log("[UniqueEffectLabel] ===== 통합 실행 완료 =====");
        }

        [MenuItem("DataLoader/Unique Effect Label/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select unique effect label JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Import(jsonPath, DefaultOutputAssetPath);
        }

        public static void Import(string jsonPath, string outputAssetPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[UniqueEffectLabel] JSON file not found: {absoluteJsonPath}");
                return;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            UniqueEffectLabelJsonData data = JsonConvert.DeserializeObject<UniqueEffectLabelJsonData>(json);
            if (data == null)
            {
                Debug.LogError("[UniqueEffectLabel] JSON parse failed.");
                return;
            }

            UniqueEffectLabelDatabaseSO so = GetOrCreateAsset(outputAssetPath);
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

            Debug.Log($"[UniqueEffectLabel] SO 갱신 완료: {outputAssetPath}\n" +
                      $"KOR: {korCount}, ENG: {engCount}, JPN: {jpnCount}, CHN: {chnCount}");
        }

        private static int WriteLabels(SerializedProperty labelsProp, List<UniqueEffectLabelRow> rows)
        {
            labelsProp.ClearArray();

            int index = 0;
            foreach (UniqueEffectLabelRow row in rows)
            {
                if (string.IsNullOrEmpty(row.uniqueEffectId))
                {
                    Debug.LogWarning("[UniqueEffectLabel] uniqueEffectId가 비어있는 행을 건너뜁니다.");
                    continue;
                }

                labelsProp.InsertArrayElementAtIndex(index);
                SerializedProperty element = labelsProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("uniqueEffectId").stringValue = row.uniqueEffectId;
                element.FindPropertyRelative("name").stringValue = row.effectName;
                element.FindPropertyRelative("description").stringValue = row.effectDescription;
                index++;
            }

            return index;
        }

        private static UniqueEffectLabelDatabaseSO GetOrCreateAsset(string path)
        {
            UniqueEffectLabelDatabaseSO asset = AssetDatabase.LoadAssetAtPath<UniqueEffectLabelDatabaseSO>(path);
            if (asset != null)
                return asset;

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                Debug.LogError($"[UniqueEffectLabel] Asset already exists but type is not UniqueEffectLabelDatabaseSO: {path}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<UniqueEffectLabelDatabaseSO>();
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

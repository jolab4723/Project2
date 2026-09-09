using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// EnemyLabelExcelToJson 결과물(JSON)을 읽어서 EnemyLabelDatabaseSO를 갱신한다.
    /// EnemyLabelDatabaseSO의 korLabels/engLabels/jpnLabels/chnLabels는 private [SerializeField]라
    /// 그 파일은 그대로 두고 SerializedObject/SerializedProperty로 직접 써넣는다.
    /// </summary>
    public static class EnemyLabelSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/EnemyData/2. JSONFile";
        private const string DefaultOutputAssetPath = "Assets/Resources/DataFiles/EnemyData/3. GeneratedAssets/LabelData/EnemyLabelDatabase.asset";

        [MenuItem("DataLoader/Enemy Label/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select enemy label JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Import(jsonPath, DefaultOutputAssetPath);
        }

        [MenuItem("DataLoader/Enemy Label/0. Run All Steps")]
        public static void RunAllSteps()
        {
            Debug.Log("[EnemyLabel] ===== 통합 실행 시작 =====");

            string jsonPath = EnemyLabelExcelToJson.ConvertWithDefaultPaths();
            if (string.IsNullOrEmpty(jsonPath))
            {
                Debug.LogError("[EnemyLabel] 엑셀을 찾지 못해 중단했습니다.");
                return;
            }

            Import(jsonPath, DefaultOutputAssetPath);
            Debug.Log("[EnemyLabel] ===== 통합 실행 완료 =====");
        }

        /// <summary>대화상자 없이 기본 JSON 경로 + 기본 출력 경로로 갱신한다. Enemy Data의 통합 실행에서도 쓴다.</summary>
        public static void ImportWithDefaultPaths(string jsonPath)
        {
            Import(jsonPath, DefaultOutputAssetPath);
        }

        public static void Import(string jsonPath, string outputAssetPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[EnemyLabel] JSON file not found: {absoluteJsonPath}");
                return;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            EnemyLabelJsonData data = JsonConvert.DeserializeObject<EnemyLabelJsonData>(json);
            if (data == null)
            {
                Debug.LogError("[EnemyLabel] JSON parse failed.");
                return;
            }

            EnemyLabelDatabaseSO so = GetOrCreateAsset(outputAssetPath);
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

            Debug.Log($"[EnemyLabel] SO 갱신 완료: {outputAssetPath}\n" +
                      $"KOR: {korCount}, ENG: {engCount}, JPN: {jpnCount}, CHN: {chnCount}");
        }

        private static int WriteLabels(SerializedProperty labelsProp, List<EnemyLabelRow> rows)
        {
            labelsProp.ClearArray();

            int index = 0;
            foreach (EnemyLabelRow row in rows)
            {
                if (string.IsNullOrEmpty(row.enemyId))
                {
                    Debug.LogWarning("[EnemyLabel] enemyId가 비어있는 행을 건너뜁니다.");
                    continue;
                }

                labelsProp.InsertArrayElementAtIndex(index);
                SerializedProperty element = labelsProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("enemyId").stringValue = row.enemyId;
                element.FindPropertyRelative("name").stringValue = row.enemyName;
                index++;
            }

            return index;
        }

        private static EnemyLabelDatabaseSO GetOrCreateAsset(string path)
        {
            EnemyLabelDatabaseSO asset = AssetDatabase.LoadAssetAtPath<EnemyLabelDatabaseSO>(path);
            if (asset != null)
                return asset;

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                Debug.LogError($"[EnemyLabel] Asset already exists but type is not EnemyLabelDatabaseSO: {path}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<EnemyLabelDatabaseSO>();
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

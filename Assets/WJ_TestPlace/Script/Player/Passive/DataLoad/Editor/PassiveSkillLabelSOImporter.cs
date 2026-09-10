using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// PassiveSkillLabelExcelToJson 결과물(JSON)을 읽어서 PassiveSkillLabelDatabaseSO를 갱신한다.
    /// PassiveSkillLabelDatabaseSO의 korLabels/engLabels/jpnLabels/chnLabels는 private [SerializeField]라
    /// 그 파일은 그대로 두고 SerializedObject/SerializedProperty로 직접 써넣는다.
    /// </summary>
    public static class PassiveSkillLabelSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/PassiveSkillData/2. JSONFile";
        private const string DefaultOutputAssetPath = "Assets/Resources/DataFiles/PassiveSkillData/3. GeneratedAssets/PassiveSkillLabelDatabase.asset";

        [MenuItem("DataLoader/Passive Skill Label/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select passive skill label JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Import(jsonPath, DefaultOutputAssetPath);
        }

        [MenuItem("DataLoader/Passive Skill Label/0. Run All Steps")]
        public static void RunAllSteps()
        {
            Debug.Log("[PassiveSkillLabel] ===== 통합 실행 시작 =====");

            string jsonPath = PassiveSkillLabelExcelToJson.ConvertWithDefaultPaths();
            if (string.IsNullOrEmpty(jsonPath))
            {
                Debug.LogError("[PassiveSkillLabel] 엑셀을 찾지 못해 중단했습니다.");
                return;
            }

            Import(jsonPath, DefaultOutputAssetPath);
            Debug.Log("[PassiveSkillLabel] ===== 통합 실행 완료 =====");
        }

        /// <summary>대화상자 없이 기본 JSON 경로 + 기본 출력 경로로 갱신한다.</summary>
        public static void ImportWithDefaultPaths(string jsonPath)
        {
            Import(jsonPath, DefaultOutputAssetPath);
        }

        public static void Import(string jsonPath, string outputAssetPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[PassiveSkillLabel] JSON file not found: {absoluteJsonPath}");
                return;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            PassiveSkillLabelJsonData data = JsonConvert.DeserializeObject<PassiveSkillLabelJsonData>(json);
            if (data == null)
            {
                Debug.LogError("[PassiveSkillLabel] JSON parse failed.");
                return;
            }

            PassiveSkillLabelDatabaseSO so = GetOrCreateAsset(outputAssetPath);
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

            Debug.Log($"[PassiveSkillLabel] SO 갱신 완료: {outputAssetPath}\n" +
                      $"KOR: {korCount}, ENG: {engCount}, JPN: {jpnCount}, CHN: {chnCount}");
        }

        private static int WriteLabels(SerializedProperty labelsProp, List<PassiveSkillLabelRow> rows)
        {
            labelsProp.ClearArray();

            int index = 0;
            foreach (PassiveSkillLabelRow row in rows)
            {
                if (string.IsNullOrEmpty(row.passiveId))
                {
                    Debug.LogWarning("[PassiveSkillLabel] passiveId가 비어있는 행을 건너뜁니다.");
                    continue;
                }

                labelsProp.InsertArrayElementAtIndex(index);
                SerializedProperty element = labelsProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("passiveId").stringValue = row.passiveId;
                element.FindPropertyRelative("name").stringValue = row.passiveName;
                element.FindPropertyRelative("description").stringValue = row.passiveDescription;
                index++;
            }

            return index;
        }

        private static PassiveSkillLabelDatabaseSO GetOrCreateAsset(string path)
        {
            PassiveSkillLabelDatabaseSO asset = AssetDatabase.LoadAssetAtPath<PassiveSkillLabelDatabaseSO>(path);
            if (asset != null)
                return asset;

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                Debug.LogError($"[PassiveSkillLabel] Asset already exists but type is not PassiveSkillLabelDatabaseSO: {path}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<PassiveSkillLabelDatabaseSO>();
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

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// SkillLabelExcelToJson 결과물(JSON)을 읽어서 SkillLabelDatabaseSO를 갱신한다.
    /// SkillLabelDatabaseSO의 korLabels/engLabels/jpnLabels/chnLabels는 private [SerializeField]라
    /// 그 파일은 그대로 두고 SerializedObject/SerializedProperty로 직접 써넣는다.
    /// </summary>
    public static class SkillLabelSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/SkillData/JSONFile";
        private const string DefaultOutputAssetPath = "Assets/WJ_TestPlace/Data/Skill/SkillLabelDatabase.asset";

        [MenuItem("DataLoader/Skill Label/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select skill label JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Import(jsonPath, DefaultOutputAssetPath);
        }

        [MenuItem("DataLoader/Skill Label/0. Run All Steps")]
        public static void RunAllSteps()
        {
            string excelAbsolutePath = AssetPathToAbsolutePath("Assets/Resources/DataFiles/SkillData/ExcelFile/SkillDataLabel.xlsx");
            string jsonAbsolutePath = AssetPathToAbsolutePath("Assets/Resources/DataFiles/SkillData/JSONFile/SkillDataLabel.json");

            SkillLabelExcelToJson.Convert(excelAbsolutePath, jsonAbsolutePath);
            Import(jsonAbsolutePath, DefaultOutputAssetPath);
        }

        public static void Import(string jsonPath, string outputAssetPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[SkillLabel] JSON file not found: {absoluteJsonPath}");
                return;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            SkillLabelJsonData data = JsonConvert.DeserializeObject<SkillLabelJsonData>(json);
            if (data == null)
            {
                Debug.LogError("[SkillLabel] JSON parse failed.");
                return;
            }

            SkillLabelDatabaseSO so = GetOrCreateAsset(outputAssetPath);
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

            Debug.Log($"[SkillLabel] SO 갱신 완료: {outputAssetPath}\n" +
                      $"KOR: {korCount}, ENG: {engCount}, JPN: {jpnCount}, CHN: {chnCount}");
        }

        private static int WriteLabels(SerializedProperty labelsProp, List<SkillLabelRow> rows)
        {
            labelsProp.ClearArray();

            int index = 0;
            foreach (SkillLabelRow row in rows)
            {
                if (string.IsNullOrEmpty(row.skillId))
                {
                    Debug.LogWarning("[SkillLabel] skillId가 비어있는 행을 건너뜁니다.");
                    continue;
                }

                labelsProp.InsertArrayElementAtIndex(index);
                SerializedProperty element = labelsProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("skillId").stringValue = row.skillId;
                element.FindPropertyRelative("skillDescription").stringValue = row.skillDescription;
                element.FindPropertyRelative("evolution1Description").stringValue = row.evolution1Description;
                element.FindPropertyRelative("evolution2Description").stringValue = row.evolution2Description;
                element.FindPropertyRelative("evolution3Description").stringValue = row.evolution3Description;
                element.FindPropertyRelative("enhancement1Description").stringValue = row.enhancement1Description;
                element.FindPropertyRelative("enhancement2Description").stringValue = row.enhancement2Description;
                element.FindPropertyRelative("enhancement3Description").stringValue = row.enhancement3Description;
                index++;
            }

            return index;
        }

        private static SkillLabelDatabaseSO GetOrCreateAsset(string path)
        {
            SkillLabelDatabaseSO asset = AssetDatabase.LoadAssetAtPath<SkillLabelDatabaseSO>(path);
            if (asset != null)
                return asset;

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                Debug.LogError($"[SkillLabel] Asset already exists but type is not SkillLabelDatabaseSO: {path}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<SkillLabelDatabaseSO>();
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// BuffTableExcelToJson 결과물(JSON)을 읽어서 BuffDefinitionSO 에셋을 생성/갱신한다.
    /// 파일명은 buffId 그대로다.
    ///
    /// !! 기존 에셋이 있으면 새로 만들지 않고 값만 덮어쓴다. GUID가 유지되어야
    ///    이미 이 버프를 참조하고 있는 컴포넌트(BuffTestKeyTrigger 등)의 연결이 안 끊긴다.
    /// !! 시트에서 행을 지워도 기존 에셋은 삭제하지 않는다(다른 파이프라인과 동일한 방침).
    ///    참조가 남아 있을 수 있어 사람이 직접 판단하는 편이 안전하다.
    /// </summary>
    public static class BuffTableSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/BuffData/2. JSONFile";
        private const string BuffFolder = "Assets/Resources/DataFiles/BuffData/3. GeneratedAssets";

        [MenuItem("DataLoader/Buff Table/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select buff table JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            GenerateAllFromJson(jsonPath, BuffFolder);
        }

        [MenuItem("DataLoader/Buff Table/0. Run All Steps")]
        public static void RunAllSteps()
        {
            Debug.Log("[BuffTable] ===== 통합 실행 시작 =====");

            string jsonPath = BuffTableExcelToJson.ConvertWithDefaultPaths();
            if (string.IsNullOrEmpty(jsonPath))
            {
                Debug.LogError("[BuffTable] 엑셀을 찾지 못해 중단했습니다.");
                return;
            }

            GenerateAllFromJson(jsonPath, BuffFolder);
            Debug.Log("[BuffTable] ===== 통합 실행 완료 =====");
        }

        public static void GenerateAllFromJson(string jsonPath, string outputFolder)
        {
            List<BuffTableRow> rows = LoadJson(jsonPath);
            if (rows == null)
                return;

            EnsureAssetFolder(outputFolder);

            int created = 0, updated = 0, skipped = 0;

            foreach (BuffTableRow row in rows)
            {
                string id = (row.buffId ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id))
                {
                    skipped++;
                    continue;
                }

                string assetPath = CombineAssetPath(outputFolder, SanitizeFileName(id) + ".asset");
                BuffDefinitionSO asset = AssetDatabase.LoadAssetAtPath<BuffDefinitionSO>(assetPath);

                if (asset == null)
                {
                    UnityEngine.Object other = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (other != null)
                    {
                        Debug.LogError($"[BuffTable] '{id}' 경로에 BuffDefinitionSO가 아닌 에셋이 이미 있습니다: {assetPath}");
                        skipped++;
                        continue;
                    }

                    asset = ScriptableObject.CreateInstance<BuffDefinitionSO>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                    created++;
                }
                else
                {
                    updated++;
                }

                ApplyRow(asset, row);
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BuffTable] SO 생성/갱신 완료. 신규 {created}개, 갱신 {updated}개, 건너뜀 {skipped}개");
        }

        private static void ApplyRow(BuffDefinitionSO asset, BuffTableRow row)
        {
            asset.buffId = row.buffId;
            asset.buffName = row.buffName;
            asset.description = row.description;
            asset.duration = row.duration;
            asset.stackBehavior = ParseEnumOrDefault(row.stackBehavior, BuffStackBehavior.RefreshDuration, row.buffId);
            asset.maxStack = row.maxStack;
            asset.statEffects = ParseStatEffects(row.statEffects, row.buffId);
            // icon은 여기서 건드리지 않는다 - 엑셀에 아이콘 컬럼이 없고, 손으로 지정한 값을 덮어쓰면 안 되기 때문.
        }

        /// <summary>"attackPowerPercent:20;moveSpeedPercent:-30" 형태를 FixedStatValue 배열로 바꾼다.</summary>
        private static FixedStatValue[] ParseStatEffects(string raw, string id)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<FixedStatValue>();

            var result = new List<FixedStatValue>();

            foreach (string entry in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = entry.Split(':');
                if (pair.Length != 2)
                {
                    Debug.LogWarning($"[BuffTable] '{id}'의 statEffects 항목 형식이 잘못됐습니다: '{entry.Trim()}' " +
                                     "(\"statType:값\" 형태여야 함). 해당 항목은 건너뜁니다.");
                    continue;
                }

                if (!Enum.TryParse(pair[0].Trim(), true, out StatType statType))
                {
                    Debug.LogWarning($"[BuffTable] '{id}'의 statEffects에 알 수 없는 statType이 있습니다: '{pair[0].Trim()}'. 해당 항목은 건너뜁니다.");
                    continue;
                }

                if (!float.TryParse(pair[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out float value))
                {
                    Debug.LogWarning($"[BuffTable] '{id}'의 statEffects 값이 숫자가 아닙니다: '{pair[1].Trim()}'. 해당 항목은 건너뜁니다.");
                    continue;
                }

                result.Add(new FixedStatValue { statType = statType, value = value });
            }

            return result.ToArray();
        }

        private static TEnum ParseEnumOrDefault<TEnum>(string value, TEnum fallback, string id) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            if (Enum.TryParse(value.Trim(), true, out TEnum parsed))
                return parsed;

            Debug.LogWarning($"[BuffTable] '{id}'의 {typeof(TEnum).Name} 값 '{value}'을 해석하지 못해 {fallback}으로 둡니다.");
            return fallback;
        }

        private static List<BuffTableRow> LoadJson(string jsonPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[BuffTable] JSON file not found: {absoluteJsonPath}");
                return null;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            List<BuffTableRow> rows = JsonConvert.DeserializeObject<List<BuffTableRow>>(json);
            if (rows == null)
                Debug.LogError("[BuffTable] JSON parse failed.");

            return rows;
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

        private static string CombineAssetPath(string left, string right)
        {
            return (left.TrimEnd('/') + "/" + right.TrimStart('/')).Replace("\\", "/");
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Unnamed" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return result;
        }
    }
}

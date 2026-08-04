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
    /// UniqueEffectTableExcelToJson 결과물(JSON)을 읽어서 UniqueEffectSO 에셋을 생성/갱신한다.
    ///
    /// 파일명은 uniqueEffectId 그대로다. ItemDataTableSOImporter가 UniqueEffectPool 폴더에서
    /// "{ItemDefinitionSO.uniqueEffectId}.asset" 경로로 찾아 연결하기 때문에 접미사를 붙이면 안 된다.
    ///
    /// !! 고유 효과는 effectType에 따라 실제 SO 클래스가 달라진다. ScriptableObject는 생성 후 타입을
    ///    바꿀 수 없으므로, 기존 에셋과 시트의 effectType이 다르면 덮어쓰지 않고 경고 후 건너뛴다
    ///    (지우고 다시 만들면 GUID가 바뀌어 아이템 에셋의 참조가 끊긴다). 타입을 바꾸려면
    ///    기존 에셋을 직접 지우고 다시 실행하면 된다.
    /// </summary>
    public static class UniqueEffectTableSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/ItemData/2. JSONFile";
        private const string UniqueEffectFolder = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/UniqueEffectPool";
        private const string BuffFolder = "Assets/WJ_TestPlace/Data/Buff";

        [MenuItem("DataLoader/Unique Effect/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select unique effect JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            GenerateAllFromJson(jsonPath, UniqueEffectFolder);
        }

        public static void GenerateAllFromJson(string jsonPath, string outputFolder)
        {
            List<UniqueEffectTableRow> rows = LoadJson(jsonPath);
            if (rows == null)
                return;

            EnsureAssetFolder(outputFolder);
            Dictionary<string, BuffDefinitionSO> buffLookup = BuildBuffLookup();

            int created = 0, updated = 0, skipped = 0;

            foreach (UniqueEffectTableRow row in rows)
            {
                string id = (row.uniqueEffectId ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id))
                {
                    skipped++;
                    continue;
                }

                Type soType = ResolveEffectType(row.effectType, id);
                if (soType == null)
                {
                    skipped++;
                    continue;
                }

                string assetPath = CombineAssetPath(outputFolder, SanitizeFileName(id) + ".asset");
                UniqueEffectSO asset = AssetDatabase.LoadAssetAtPath<UniqueEffectSO>(assetPath);

                if (asset == null)
                {
                    UnityEngine.Object other = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (other != null)
                    {
                        Debug.LogError($"[UniqueEffect] '{id}' 경로에 UniqueEffectSO가 아닌 에셋이 이미 있습니다: {assetPath}");
                        skipped++;
                        continue;
                    }

                    asset = (UniqueEffectSO)ScriptableObject.CreateInstance(soType);
                    AssetDatabase.CreateAsset(asset, assetPath);
                    created++;
                }
                else if (asset.GetType() != soType)
                {
                    // 타입 변경은 GUID를 잃지 않고는 불가능하다. 사람이 판단하도록 남긴다.
                    Debug.LogWarning($"[UniqueEffect] '{id}'의 effectType이 기존 에셋과 다릅니다 " +
                                     $"(기존 {asset.GetType().Name} -> 시트 {soType.Name}). " +
                                     "GUID 유지를 위해 건너뜁니다. 타입을 바꾸려면 기존 에셋을 직접 삭제한 뒤 다시 실행해주세요.");
                    skipped++;
                    continue;
                }
                else
                {
                    updated++;
                }

                ApplyRow(asset, row, buffLookup);
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[UniqueEffect] SO 생성/갱신 완료. 신규 {created}개, 갱신 {updated}개, 건너뜀 {skipped}개");
        }

        /// <summary>공통 필드를 채운 뒤 effectType별 고유 필드를 채운다.</summary>
        private static void ApplyRow(UniqueEffectSO asset, UniqueEffectTableRow row, Dictionary<string, BuffDefinitionSO> buffLookup)
        {
            asset.effectName = row.effectName;
            asset.effectDescription = row.effectDescription;
            asset.coefficients = ParseCoefficients(row.coefficients, row.uniqueEffectId);

            switch (asset)
            {
                case PassiveBuffUniqueEffectSO passive:
                    passive.buffToApply = ResolveBuff(row, buffLookup);
                    break;

                case TriggeredBuffUniqueEffectSO triggered:
                    triggered.buffToApply = ResolveBuff(row, buffLookup);
                    triggered.triggerCondition = ParseEnumOrDefault(row.triggerCondition, TriggerCondition.None, row.uniqueEffectId);
                    break;

                case HealthThresholdBuffUniqueEffectSO threshold:
                    threshold.buffToApply = ResolveBuff(row, buffLookup);
                    threshold.healthThresholdPercent = row.healthThresholdPercent;
                    break;

                case PeriodicLogUniqueEffectSO periodic:
                    periodic.intervalSeconds = row.intervalSeconds;
                    periodic.message = row.message;
                    break;
            }
        }

        /// <summary>"30;5" 형태의 한 칸 문자열을 float 배열로 바꾼다.</summary>
        private static float[] ParseCoefficients(string raw, string id)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<float>();

            string[] parts = raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var values = new List<float>(parts.Length);

            foreach (string part in parts)
            {
                if (float.TryParse(part.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out float v))
                    values.Add(v);
                else
                    Debug.LogWarning($"[UniqueEffect] '{id}'의 coefficients에 숫자가 아닌 값이 있습니다: '{part.Trim()}' (해당 값은 제외됨)");
            }

            return values.ToArray();
        }

        private static BuffDefinitionSO ResolveBuff(UniqueEffectTableRow row, Dictionary<string, BuffDefinitionSO> buffLookup)
        {
            string buffId = (row.buffId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(buffId))
                return null;

            if (buffLookup.TryGetValue(buffId, out BuffDefinitionSO buff))
                return buff;

            Debug.LogWarning($"[UniqueEffect] '{row.uniqueEffectId}'의 buffId '{buffId}'에 해당하는 버프 에셋을 {BuffFolder}에서 찾지 못했습니다.");
            return null;
        }

        /// <summary>버프 폴더를 한 번만 훑어서 "에셋 파일명 -> BuffDefinitionSO" 사전을 만든다.</summary>
        private static Dictionary<string, BuffDefinitionSO> BuildBuffLookup()
        {
            var lookup = new Dictionary<string, BuffDefinitionSO>(StringComparer.OrdinalIgnoreCase);

            if (!AssetDatabase.IsValidFolder(BuffFolder))
            {
                Debug.LogWarning($"[UniqueEffect] 버프 폴더를 찾지 못했습니다: {BuffFolder}");
                return lookup;
            }

            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(BuffDefinitionSO)}", new[] { BuffFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var buff = AssetDatabase.LoadAssetAtPath<BuffDefinitionSO>(path);
                if (buff != null)
                    lookup[Path.GetFileNameWithoutExtension(path)] = buff;
            }

            return lookup;
        }

        /// <summary>effectType 문자열을 실제 UniqueEffectSO 파생 타입으로 바꾼다.</summary>
        private static Type ResolveEffectType(string effectType, string id)
        {
            string name = (effectType ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"[UniqueEffect] '{id}'의 effectType이 비어있어 건너뜁니다.");
                return null;
            }

            switch (name)
            {
                case nameof(PassiveBuffUniqueEffectSO): return typeof(PassiveBuffUniqueEffectSO);
                case nameof(TriggeredBuffUniqueEffectSO): return typeof(TriggeredBuffUniqueEffectSO);
                case nameof(HealthThresholdBuffUniqueEffectSO): return typeof(HealthThresholdBuffUniqueEffectSO);
                case nameof(PeriodicLogUniqueEffectSO): return typeof(PeriodicLogUniqueEffectSO);
            }

            Debug.LogWarning($"[UniqueEffect] '{id}'의 effectType '{name}'을 알 수 없어 건너뜁니다. " +
                             "엑셀 ComboBox 시트의 값 중 하나여야 합니다. (새 효과 타입을 추가했다면 이 변환기에도 등록해야 함)");
            return null;
        }

        private static TEnum ParseEnumOrDefault<TEnum>(string value, TEnum fallback, string id) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            if (Enum.TryParse(value.Trim(), true, out TEnum parsed))
                return parsed;

            Debug.LogWarning($"[UniqueEffect] '{id}'의 {typeof(TEnum).Name} 값 '{value}'을 해석하지 못해 {fallback}으로 둡니다.");
            return fallback;
        }

        private static List<UniqueEffectTableRow> LoadJson(string jsonPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[UniqueEffect] JSON file not found: {absoluteJsonPath}");
                return null;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            List<UniqueEffectTableRow> rows = JsonConvert.DeserializeObject<List<UniqueEffectTableRow>>(json);
            if (rows == null)
                Debug.LogError("[UniqueEffect] JSON parse failed.");

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

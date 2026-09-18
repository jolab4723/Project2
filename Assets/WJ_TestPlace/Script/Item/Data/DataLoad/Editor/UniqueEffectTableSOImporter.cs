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

        [MenuItem("DataLoader/Unique Effect/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select unique effect JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            GenerateAllFromJson(jsonPath, UniqueEffectFolder);
        }

        /// <summary>
        /// 대화상자 없이 엑셀 -> JSON -> SO를 한 번에 수행한다.
        /// 아이템 데이터 테이블의 고유효과 연결 단계가 시작할 때 이걸 먼저 불러서,
        /// 연결 대상 SO가 항상 엑셀 최신 내용으로 만들어져 있도록 보장한다.
        /// </summary>
        /// <returns>SO 생성/갱신까지 실제로 수행했으면 true.</returns>
        public static bool RunExcelToSoWithDefaultPaths()
        {
            string jsonPath = UniqueEffectTableExcelToJson.ConvertWithDefaultPaths();
            if (string.IsNullOrEmpty(jsonPath))
                return false;

            GenerateAllFromJson(jsonPath, UniqueEffectFolder);
            return true;
        }

        [MenuItem("DataLoader/Unique Effect/0. Run All Steps")]
        public static void RunAllSteps()
        {
            Debug.Log("[UniqueEffect] ===== 통합 실행 시작 =====");

            if (!RunExcelToSoWithDefaultPaths())
            {
                Debug.LogError("[UniqueEffect] 엑셀을 찾지 못해 중단했습니다.");
                return;
            }

            Debug.Log("[UniqueEffect] ===== 통합 실행 완료 =====");
        }

        public static void GenerateAllFromJson(string jsonPath, string outputFolder)
        {
            List<UniqueEffectTableRow> rows = LoadJson(jsonPath);
            if (rows == null)
                return;

            EnsureAssetFolder(outputFolder);

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

                ApplyRow(asset, row);
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[UniqueEffect] SO 생성/갱신 완료. 신규 {created}개, 갱신 {updated}개, 건너뜀 {skipped}개");
        }

        /// <summary>
        /// 공통 필드를 채운 뒤 effectType별 고유 필드를 채운다.
        /// !! asset.icon은 여기서 건드리지 않는다 - 고유 효과 아이콘은 그 효과가 붙은 아이템의 아이콘을
        ///    쓰기로 했고, ItemDataTableSOImporter의 아이콘 연결 단계가 대신 채워준다.
        /// </summary>
        private static void ApplyRow(UniqueEffectSO asset, UniqueEffectTableRow row)
        {
            asset.effectName = row.effectName;
            asset.effectDescription = row.effectDescription;
            asset.coefficients = ParseCoefficients(row.coefficients, row.uniqueEffectId);

            switch (asset)
            {
                case PassiveBuffUniqueEffectSO passive:
                    passive.buffSpec = BuildBuffSpec(row, BuffStackBehavior.Ignore);
                    break;

                case TriggeredBuffUniqueEffectSO triggered:
                    triggered.buffSpec = BuildBuffSpec(row, BuffStackBehavior.RefreshDuration);
                    triggered.triggerCondition = ParseEnumOrDefault(row.triggerCondition, TriggerCondition.None, row.uniqueEffectId);
                    triggered.cooldownSeconds = row.cooldownSeconds;
                    triggered.duplicatePolicy = ParseEnumOrDefault(row.duplicatePolicy, DuplicateTriggerPolicy.ShareCooldown, row.uniqueEffectId);
                    triggered.persistStackOnItem = row.persistStackOnItem;
                    break;

                case StatThresholdBuffUniqueEffectSO threshold:
                    threshold.buffSpec = BuildBuffSpec(row, BuffStackBehavior.Ignore);
                    threshold.referenceStat = ParseEnumOrDefault(row.referenceStat, StatReference.CurrentHealthPercent, row.uniqueEffectId);
                    threshold.comparisonOperator = ParseEnumOrDefault(row.comparisonOperator, ComparisonOperator.LessOrEqual, row.uniqueEffectId);
                    threshold.thresholdValue = row.thresholdValue;
                    break;

                case FieldAuraUniqueEffectSO aura:
                    aura.buffSpec = BuildBuffSpec(row, BuffStackBehavior.Ignore);
                    aura.radius = row.radius;
                    aura.targetEnemies = row.targetEnemies;
                    break;

                case PeriodicLogUniqueEffectSO periodic:
                    periodic.intervalSeconds = row.intervalSeconds;
                    periodic.message = row.message;
                    break;
            }
        }

        /// <summary>시트의 버프 컬럼들(statEffects/duration/stackBehavior/maxStack/displayKind)로 BuffSpec을 만든다.</summary>
        private static BuffSpec BuildBuffSpec(UniqueEffectTableRow row, BuffStackBehavior fallbackStack)
        {
            return new BuffSpec
            {
                statEffects = ParseStatEffects(row.statEffects, row.uniqueEffectId),
                duration = row.duration,
                stackBehavior = ParseEnumOrDefault(row.stackBehavior, fallbackStack, row.uniqueEffectId),
                maxStack = row.maxStack,
                displayKind = ParseEnumOrDefault(row.displayKind, BuffDisplayKind.Auto, row.uniqueEffectId),
            };
        }

        /// <summary>"moveSpeedPercent:50;attackPowerFlat:10" 형태를 FixedStatValue 배열로 바꾼다.</summary>
        private static FixedStatValue[] ParseStatEffects(string raw, string id)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                Debug.LogWarning($"[UniqueEffect] '{id}'의 statEffects가 비어있습니다. 스탯 효과 없는 버프가 됩니다.");
                return Array.Empty<FixedStatValue>();
            }

            var result = new List<FixedStatValue>();

            foreach (string entry in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = entry.Split(':');
                if (pair.Length != 2)
                {
                    Debug.LogWarning($"[UniqueEffect] '{id}'의 statEffects 항목 형식이 잘못됐습니다: '{entry.Trim()}' " +
                                     "(\"statType:값\" 형태여야 함). 해당 항목은 건너뜁니다.");
                    continue;
                }

                if (!Enum.TryParse(pair[0].Trim(), true, out StatType statType))
                {
                    Debug.LogWarning($"[UniqueEffect] '{id}'의 statEffects에 알 수 없는 statType이 있습니다: '{pair[0].Trim()}'. 해당 항목은 건너뜁니다.");
                    continue;
                }

                if (!float.TryParse(pair[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out float value))
                {
                    Debug.LogWarning($"[UniqueEffect] '{id}'의 statEffects 값이 숫자가 아닙니다: '{pair[1].Trim()}'. 해당 항목은 건너뜁니다.");
                    continue;
                }

                result.Add(new FixedStatValue { statType = statType, value = value });
            }

            return result.ToArray();
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
                case nameof(StatThresholdBuffUniqueEffectSO): return typeof(StatThresholdBuffUniqueEffectSO);
                case nameof(FieldAuraUniqueEffectSO): return typeof(FieldAuraUniqueEffectSO);
                case nameof(PeriodicLogUniqueEffectSO): return typeof(PeriodicLogUniqueEffectSO);
                case nameof(DropRarityModifierUniqueEffectSO): return typeof(DropRarityModifierUniqueEffectSO);
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

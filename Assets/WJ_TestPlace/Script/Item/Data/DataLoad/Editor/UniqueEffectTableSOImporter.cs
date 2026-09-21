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
    ///    바꿀 수 없으므로, 기존 에셋과 시트의 effectType이 다르면 쓰기 전에 전체 생성을 중단한다
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

            // SW 수정: JSON이 있어도 타입/데이터 오류로 SO를 못 만들었다면 실패를 그대로 돌려준다.
            return GenerateAllFromJson(jsonPath, UniqueEffectFolder);
        }

        [MenuItem("DataLoader/Unique Effect/0. Run All Steps")]
        public static void RunAllSteps()
        {
            Debug.Log("[UniqueEffect] ===== 통합 실행 시작 =====");

            if (!RunExcelToSoWithDefaultPaths())
            {
                Debug.LogError("[UniqueEffect] 데이터 변환 또는 SO 생성에 실패해 중단했습니다. 앞의 오류를 확인해주세요.");
                return;
            }

            Debug.Log("[UniqueEffect] ===== 통합 실행 완료 =====");
        }

        /// <summary>SW 수정: 모든 행과 기존 타입을 먼저 검사하여 잘못된 표가 SO 일부만 덮어쓰지 않게 한다.</summary>
        public static bool GenerateAllFromJson(string jsonPath, string outputFolder)
        {
            List<UniqueEffectTableRow> rows = LoadJson(jsonPath);
            if (!ValidateRows(rows))
                return false;

            foreach (UniqueEffectTableRow row in rows)
            {
                string path = CombineAssetPath(outputFolder, row.uniqueEffectId.Trim() + ".asset");
                UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null && existing.GetType() != ResolveEffectType(row.effectType, row.uniqueEffectId))
                {
                    Debug.LogError($"[UniqueEffect] '{row.uniqueEffectId}'의 기존 타입이 표와 다릅니다. GUID와 참조를 보존하기 위해 전체 생성을 중단합니다: {path}");
                    return false;
                }
            }

            EnsureAssetFolder(outputFolder);

            int created = 0, updated = 0;
            foreach (UniqueEffectTableRow row in rows)
            {
                string id = row.uniqueEffectId.Trim();
                string assetPath = CombineAssetPath(outputFolder, id + ".asset");
                UniqueEffectSO asset = AssetDatabase.LoadAssetAtPath<UniqueEffectSO>(assetPath);
                if (asset == null)
                {
                    asset = (UniqueEffectSO)ScriptableObject.CreateInstance(ResolveEffectType(row.effectType, id));
                    AssetDatabase.CreateAsset(asset, assetPath);
                    created++;
                }
                else updated++;

                ApplyRow(asset, row);
                EditorUtility.SetDirty(asset);
                // SW 수정: 이 가져오기에서 바꾼 SO만 저장하고 다른 작업의 Dirty 에셋은 저장하지 않는다.
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            Debug.Log($"[UniqueEffect] SO 생성/갱신 완료. 신규 {created}개, 갱신 {updated}개");
            return true;
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
                // SW 수정: 기존 설명 계수와 cooldown 열을 재사용하여 툴팁과 실제 피해 수치를 함께 갱신한다.
                case ChainLightningUniqueEffectSO chain:
                    chain.maxAdditionalTargets = (int)asset.coefficients[0];
                    chain.jumpRadius = asset.coefficients[1];
                    chain.firstDamageMultiplier = asset.coefficients[2] / 100f;
                    chain.subsequentDamageMultiplier = asset.coefficients[3] / 100f;
                    chain.cooldownSeconds = row.cooldownSeconds;
                    break;
                case InfernoExtraHitUniqueEffectSO inferno:
                    inferno.damageMultiplier = asset.coefficients[0] / 100f;
                    break;
                case GlassRailExtraHitUniqueEffectSO glass:
                    glass.damageMultiplier = asset.coefficients[0] / 100f;
                    break;
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
                Debug.LogError($"[UniqueEffect] '{id}'의 effectType이 비어있습니다.");
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
                // SW 수정: 수동 SO로만 있던 세 무기도 같은 표에서 재생성한다.
                case nameof(ChainLightningUniqueEffectSO): return typeof(ChainLightningUniqueEffectSO);
                case nameof(InfernoExtraHitUniqueEffectSO): return typeof(InfernoExtraHitUniqueEffectSO);
                case nameof(GlassRailExtraHitUniqueEffectSO): return typeof(GlassRailExtraHitUniqueEffectSO);
            }

            Debug.LogError($"[UniqueEffect] '{id}'의 effectType '{name}'을 알 수 없어 변환을 중단합니다. " +
                             "엑셀 ComboBox 시트의 값 중 하나여야 합니다. (새 효과 타입을 추가했다면 이 변환기에도 등록해야 함)");
            return null;
        }

        /// <summary>SW 수정: Excel과 직접 JSON 입력을 같은 기준으로 검사한다. 위치가 중요한 무기 계수는 누락·오타를 허용하지 않는다.</summary>
        internal static bool ValidateRows(List<UniqueEffectTableRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                Debug.LogError("[UniqueEffect] 변환할 행이 없습니다.");
                return false;
            }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool valid = true;
            foreach (UniqueEffectTableRow row in rows)
            {
                string id = row?.uniqueEffectId?.Trim();
                if (string.IsNullOrEmpty(id) || id != SanitizeFileName(id) || id == "." || id == ".." || !ids.Add(id))
                {
                    Debug.LogError($"[UniqueEffect] 비어 있거나 중복된 ID 또는 파일명으로 쓸 수 없는 ID입니다: '{id}'");
                    valid = false;
                    continue;
                }
                Type type = ResolveEffectType(row.effectType, id);
                if (type == null) { valid = false; continue; }
                bool chain = type == typeof(ChainLightningUniqueEffectSO);
                if (!chain && type != typeof(InfernoExtraHitUniqueEffectSO) && type != typeof(GlassRailExtraHitUniqueEffectSO))
                    continue;
                string[] parts = (row.coefficients ?? string.Empty).Split(new[] { ';', ',' }, StringSplitOptions.None);
                var values = new float[parts.Length];
                bool numbers = parts.Length == (chain ? 5 : 1);
                for (int i = 0; i < parts.Length; i++)
                    numbers &= float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) &&
                        !float.IsNaN(values[i]) && !float.IsInfinity(values[i]);
                bool ranges = numbers && (chain
                    ? values[0] >= 1 && values[0] <= 16 && values[0] == Mathf.Floor(values[0]) && values[1] > 0 &&
                      values[2] > 0 && values[3] >= 0 && values[3] <= 100 && values[4] >= 0 &&
                      !float.IsNaN(row.cooldownSeconds) && !float.IsInfinity(row.cooldownSeconds) &&
                      Mathf.Approximately(values[4], row.cooldownSeconds)
                    : values[0] > 0);
                if (ranges) continue;
                Debug.LogError($"[UniqueEffect] '{id}'의 무기 계수를 확인해주세요. " +
                    (chain ? "coefficients는 대상 수(1~16);반경;첫 피해%;이후 피해%(0~100);쿨다운 순서이며 마지막 값은 cooldownSeconds와 같아야 합니다."
                        : "coefficients에는 양수인 추가 피해% 하나가 필요합니다."));
                valid = false;
            }
            return valid;
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

            // SW 수정: 파싱 실패를 성공으로 넘기거나 기존 SO에 부분 반영하지 않는다.
            try
            {
                return JsonConvert.DeserializeObject<List<UniqueEffectTableRow>>(File.ReadAllText(absoluteJsonPath));
            }
            catch (Exception exception) when (exception is JsonException || exception is IOException)
            {
                Debug.LogError($"[UniqueEffect] JSON을 읽지 못했습니다: {exception.Message}");
                return null;
            }
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

using System.Collections.Generic;
using System.IO;
using ExcelDataReader;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using DataSystem.Excel;

namespace DataSystem
{
    /// <summary>
    /// UniqueEffectTable.xlsx(UniqueEffectDefinitions 시트)를 JSON으로 변환한다.
    /// 1행 헤더 / 2행 타입 힌트 / 3행부터 데이터인 아이템 시트와 같은 구조라
    /// ExcelSheetReader.ReadSheetRows를 그대로 사용한다.
    /// 두 번째 ComboBox 시트는 드롭다운 검증용이라 건너뛴다.
    /// </summary>
    public static class UniqueEffectTableExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/ItemData/2. JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/ItemData/1. ExcelFile/UniqueEffectTable.xlsx";

        [MenuItem("DataLoader/Unique Effect/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = ResolveExcelPath();
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string suggestedJsonFileName = Path.GetFileNameWithoutExtension(excelPath) + ".json";
            string jsonPath = EditorUtility.SaveFilePanel("Save unique effect JSON", defaultAbsoluteFolder, suggestedJsonFileName, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Convert(excelPath, jsonPath);
        }

        /// <summary>
        /// 대화상자 없이 사전 설정된 경로만으로 Excel -> JSON 변환을 수행한다.
        /// 아이템 데이터 테이블 통합 실행처럼 다른 단계가 자동으로 호출할 때 쓴다.
        /// </summary>
        /// <returns>생성된 JSON의 절대 경로. 엑셀이 없거나 실패하면 null.</returns>
        public static string ConvertWithDefaultPaths()
        {
            string excelAbsolutePath = AssetPathToAbsolutePath(DefaultExcelPath);
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogWarning($"[UniqueEffect] 고유 효과 엑셀이 없어 변환을 건너뜁니다: {DefaultExcelPath}");
                return null;
            }

            EnsureAssetFolder(DefaultJsonFolder);
            string jsonAbsolutePath = Path.Combine(
                AssetPathToAbsolutePath(DefaultJsonFolder),
                Path.GetFileNameWithoutExtension(DefaultExcelPath) + ".json");

            // SW 수정: 예전 JSON이 남아 있어도 이번 Excel 변환이 실패했다면 이어서 가져오지 않는다.
            return Convert(excelAbsolutePath, jsonAbsolutePath) ? jsonAbsolutePath : null;
        }

        /// <summary>사전 설정된 경로에 파일이 있으면 그것을, 없으면 파일 선택 대화상자를 띄우고 결과를 반환한다.</summary>
        private static string ResolveExcelPath()
        {
            string defaultAbsolutePath = AssetPathToAbsolutePath(DefaultExcelPath);
            if (File.Exists(defaultAbsolutePath))
            {
                Debug.Log("[UniqueEffect] 사전 설정된 엑셀 파일을 사용합니다: " + DefaultExcelPath);
                return defaultAbsolutePath;
            }

            return EditorUtility.OpenFilePanel("Select unique effect table", Application.dataPath, "xlsx");
        }

        /// <summary>SW 수정: 기존·신규 효과의 공통 검증을 통과한 표만 JSON으로 저장하고 읽기·저장 실패는 false로 반환한다.</summary>
        public static bool Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[UniqueEffect] Excel file not found: {excelAbsolutePath}");
                return false;
            }

            try
            {
                List<UniqueEffectTableRow> rows;

                using (FileStream stream = File.Open(excelAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
                {
                    // 첫 번째 시트(UniqueEffectDefinitions)만 사용한다. ComboBox 시트는 읽지 않는다.
                    List<Dictionary<string, string>> sheetRows = ExcelSheetReader.ReadSheetRows(reader);
                    rows = ExcelSheetReader.MapRows<UniqueEffectTableRow>(sheetRows);
                }

                if (rows.Count == 0)
                {
                    Debug.LogError("[UniqueEffect] 변환할 데이터 행이 없습니다. 시트 구조(1행 헤더 / 2행 타입 힌트 / 3행부터 데이터)를 확인해주세요.");
                    return false;
                }

                if (!Validate(rows) || !UniqueEffectTableSOImporter.ValidateRows(rows))
                    return false;

                string json = JsonConvert.SerializeObject(rows, Formatting.Indented);
                string directory = Path.GetDirectoryName(jsonAbsolutePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(jsonAbsolutePath, json);
                AssetDatabase.Refresh();

                Debug.Log($"[UniqueEffect] JSON generated: {jsonAbsolutePath}\n고유 효과 {rows.Count}개");
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[UniqueEffect] Excel 읽기 또는 JSON 저장에 실패해 중단했습니다: {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// 변환 전에 시트 내용을 검사한다. ID 누락/중복은 에셋을 덮어쓰거나 잘못 만들 수 있어 변환 자체를 막고,
        /// 나머지는 경고만 남기고 진행한다.
        /// </summary>
        private static bool Validate(List<UniqueEffectTableRow> rows)
        {
            var seenIds = new HashSet<string>();
            bool blocking = false;

            foreach (UniqueEffectTableRow row in rows)
            {
                string id = (row.uniqueEffectId ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogError($"[UniqueEffect] uniqueEffectId가 비어있는 행이 있습니다. (effectName: '{row.effectName}')");
                    blocking = true;
                    continue;
                }

                if (!seenIds.Add(id))
                {
                    Debug.LogError($"[UniqueEffect] uniqueEffectId가 중복됩니다: '{id}'");
                    blocking = true;
                }

                if (string.IsNullOrWhiteSpace(row.effectType))
                    Debug.LogWarning($"[UniqueEffect] '{id}'의 effectType이 비어있습니다. 공통 행 검증에서 전체 변환을 중단합니다.");

                // 버프 기반 종류는 statEffects가 없으면 실제로 아무 효과가 없다.
                bool needsBuff = row.effectType == nameof(ItemSystem.PassiveBuffUniqueEffectSO)
                    || row.effectType == nameof(ItemSystem.TriggeredBuffUniqueEffectSO)
                    || row.effectType == nameof(ItemSystem.StatThresholdBuffUniqueEffectSO)
                    || row.effectType == nameof(ItemSystem.FieldAuraUniqueEffectSO);

                if (needsBuff && string.IsNullOrWhiteSpace(row.statEffects))
                    Debug.LogWarning($"[UniqueEffect] '{id}'({row.effectType})에 statEffects가 비어있습니다. 적용할 스탯 효과가 없어 효과가 동작하지 않습니다.");

                // 상시/조건부/영역 효과는 수동(또는 영역 진입·이탈)으로 켜고 끄므로 지속시간이 있으면 의도치 않게 꺼진다.
                bool shouldBePermanent = row.effectType == nameof(ItemSystem.PassiveBuffUniqueEffectSO)
                    || row.effectType == nameof(ItemSystem.StatThresholdBuffUniqueEffectSO)
                    || row.effectType == nameof(ItemSystem.FieldAuraUniqueEffectSO);

                if (shouldBePermanent && row.duration > 0f)
                    Debug.LogWarning($"[UniqueEffect] '{id}'({row.effectType})의 duration이 {row.duration}입니다. " +
                                     "상시/조건부/영역 효과는 해제 시점을 직접 관리하므로 duration을 0(영구)으로 두는 것이 맞습니다.");

                if (row.effectType == nameof(ItemSystem.FieldAuraUniqueEffectSO) && row.radius <= 0f)
                    Debug.LogWarning($"[UniqueEffect] '{id}'({row.effectType})의 radius가 {row.radius}입니다. 0 이하면 영역이 사실상 존재하지 않습니다.");
            }

            if (blocking)
                Debug.LogError("[UniqueEffect] 위 오류 때문에 변환을 중단했습니다. 엑셀을 수정한 뒤 다시 실행해주세요.");

            return !blocking;
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

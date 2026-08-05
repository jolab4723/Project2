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
    /// BuffTable.xlsx(BuffDefinitions 시트)를 JSON으로 변환한다.
    /// 1행 헤더 / 2행 타입 힌트 / 3행부터 데이터인 아이템 시트와 같은 구조라
    /// ExcelSheetReader.ReadSheetRows를 그대로 사용한다. 첫 번째 시트만 읽고 ComboBox 시트는 건너뛴다.
    /// </summary>
    public static class BuffTableExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/BuffData/2. JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/BuffData/1. ExcelFile/BuffTable.xlsx";

        [MenuItem("DataLoader/Buff Table/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = ResolveExcelPath();
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string suggestedJsonFileName = Path.GetFileNameWithoutExtension(excelPath) + ".json";
            string jsonPath = EditorUtility.SaveFilePanel("Save buff table JSON", defaultAbsoluteFolder, suggestedJsonFileName, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Convert(excelPath, jsonPath);
        }

        /// <summary>
        /// 대화상자 없이 기본 경로만으로 변환한다. 통합 실행처럼 중간에 멈추면 안 되는 곳에서 쓴다.
        /// </summary>
        /// <returns>생성된 JSON의 절대 경로. 엑셀이 없거나 실패하면 null.</returns>
        public static string ConvertWithDefaultPaths()
        {
            string excelAbsolutePath = AssetPathToAbsolutePath(DefaultExcelPath);
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogWarning($"[BuffTable] 버프 엑셀이 없어 변환을 건너뜁니다: {DefaultExcelPath}");
                return null;
            }

            EnsureAssetFolder(DefaultJsonFolder);
            string jsonAbsolutePath = Path.Combine(
                AssetPathToAbsolutePath(DefaultJsonFolder),
                Path.GetFileNameWithoutExtension(DefaultExcelPath) + ".json");

            Convert(excelAbsolutePath, jsonAbsolutePath);

            return File.Exists(jsonAbsolutePath) ? jsonAbsolutePath : null;
        }

        /// <summary>사전 설정된 경로에 파일이 있으면 그것을, 없으면 파일 선택 대화상자를 띄우고 결과를 반환한다.</summary>
        private static string ResolveExcelPath()
        {
            string defaultAbsolutePath = AssetPathToAbsolutePath(DefaultExcelPath);
            if (File.Exists(defaultAbsolutePath))
            {
                Debug.Log("[BuffTable] 사전 설정된 엑셀 파일을 사용합니다: " + DefaultExcelPath);
                return defaultAbsolutePath;
            }

            return EditorUtility.OpenFilePanel("Select buff table", Application.dataPath, "xlsx");
        }

        public static void Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[BuffTable] Excel file not found: {excelAbsolutePath}");
                return;
            }

            List<BuffTableRow> rows;

            using (FileStream stream = File.Open(excelAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
            {
                List<Dictionary<string, string>> sheetRows = ExcelSheetReader.ReadSheetRows(reader);
                rows = ExcelSheetReader.MapRows<BuffTableRow>(sheetRows);
            }

            if (rows.Count == 0)
            {
                Debug.LogError("[BuffTable] 변환할 데이터 행이 없습니다. 시트 구조(1행 헤더 / 2행 타입 힌트 / 3행부터 데이터)를 확인해주세요.");
                return;
            }

            if (!Validate(rows))
                return;

            string json = JsonConvert.SerializeObject(rows, Formatting.Indented);
            string directory = Path.GetDirectoryName(jsonAbsolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(jsonAbsolutePath, json);
            AssetDatabase.Refresh();

            Debug.Log($"[BuffTable] JSON generated: {jsonAbsolutePath}\n버프/디버프 {rows.Count}개");
        }

        /// <summary>
        /// ID 누락·중복은 엉뚱한 에셋을 덮어쓸 수 있어 변환 자체를 막고,
        /// 나머지는 경고만 남기고 진행한다.
        /// </summary>
        private static bool Validate(List<BuffTableRow> rows)
        {
            var seenIds = new HashSet<string>();
            bool blocking = false;

            foreach (BuffTableRow row in rows)
            {
                string id = (row.buffId ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogError($"[BuffTable] buffId가 비어있는 행이 있습니다. (buffName: '{row.buffName}')");
                    blocking = true;
                    continue;
                }

                if (!seenIds.Add(id))
                {
                    Debug.LogError($"[BuffTable] buffId가 중복됩니다: '{id}'");
                    blocking = true;
                }

                if (string.IsNullOrWhiteSpace(row.statEffects))
                    Debug.LogWarning($"[BuffTable] '{id}'의 statEffects가 비어있습니다. 스탯 변화가 없는 버프가 됩니다.");

                if (row.duration <= 0f)
                    Debug.LogWarning($"[BuffTable] '{id}'의 duration이 {row.duration}입니다. " +
                                     "영구 지속이 되어 직접 RemoveBuff를 부르기 전까지 풀리지 않습니다.");
            }

            if (blocking)
                Debug.LogError("[BuffTable] 위 오류 때문에 변환을 중단했습니다. 엑셀을 수정한 뒤 다시 실행해주세요.");

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

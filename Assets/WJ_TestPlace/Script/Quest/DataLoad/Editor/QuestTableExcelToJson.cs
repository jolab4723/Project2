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
    /// QuestTable.xlsx(첫 번째 시트)를 JSON으로 변환한다.
    /// 1행 헤더 / 2행 타입 힌트 / 3행부터 데이터인 다른 테이블과 같은 구조라
    /// ExcelSheetReader.ReadSheetRows를 그대로 사용한다.
    /// </summary>
    public static class QuestTableExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/QuestData/2. JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/QuestData/1. ExcelFile/QuestTable.xlsx";

        [MenuItem("DataLoader/Quest Table/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = ResolveExcelPath();
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string suggestedJsonFileName = Path.GetFileNameWithoutExtension(excelPath) + ".json";
            string jsonPath = EditorUtility.SaveFilePanel("Save quest table JSON", defaultAbsoluteFolder, suggestedJsonFileName, "json");
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
                Debug.LogWarning($"[QuestTable] 퀘스트 엑셀이 없어 변환을 건너뜁니다: {DefaultExcelPath}");
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
                Debug.Log("[QuestTable] 사전 설정된 엑셀 파일을 사용합니다: " + DefaultExcelPath);
                return defaultAbsolutePath;
            }

            return EditorUtility.OpenFilePanel("Select quest table", Application.dataPath, "xlsx");
        }

        public static void Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[QuestTable] Excel file not found: {excelAbsolutePath}");
                return;
            }

            List<QuestTableRow> rows;

            using (FileStream stream = File.Open(excelAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
            {
                List<Dictionary<string, string>> sheetRows = ExcelSheetReader.ReadSheetRows(reader);
                rows = ExcelSheetReader.MapRows<QuestTableRow>(sheetRows);
            }

            if (rows.Count == 0)
            {
                Debug.LogError("[QuestTable] 변환할 데이터 행이 없습니다. 시트 구조(1행 헤더 / 2행 타입 힌트 / 3행부터 데이터)를 확인해주세요.");
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

            Debug.Log($"[QuestTable] JSON generated: {jsonAbsolutePath}\n퀘스트 {rows.Count}개");
        }

        /// <summary>
        /// ID 누락·중복은 엉뚱한 에셋을 덮어쓸 수 있어 변환 자체를 막고,
        /// 나머지는 경고만 남기고 진행한다. 조건 문자열 형식 검증은 SO 생성 단계(QuestTableSOImporter)에서 한다
        /// (그때 실제 QuestConditionType/ItemDefinitionSO 조회가 가능해서 더 정확히 검증할 수 있음).
        /// </summary>
        private static bool Validate(List<QuestTableRow> rows)
        {
            var seenIds = new HashSet<string>();
            bool blocking = false;

            foreach (QuestTableRow row in rows)
            {
                string id = (row.questId ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogError($"[QuestTable] questId가 비어있는 행이 있습니다. (questName: '{row.questName}')");
                    blocking = true;
                    continue;
                }

                if (!seenIds.Add(id))
                {
                    Debug.LogError($"[QuestTable] questId가 중복됩니다: '{id}'");
                    blocking = true;
                }

                if (string.IsNullOrWhiteSpace(row.conditions))
                    Debug.LogWarning($"[QuestTable] '{id}'의 conditions가 비어있습니다. 조건 없는 퀘스트가 됩니다.");
            }

            if (blocking)
                Debug.LogError("[QuestTable] 위 오류 때문에 변환을 중단했습니다. 엑셀을 수정한 뒤 다시 실행해주세요.");

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

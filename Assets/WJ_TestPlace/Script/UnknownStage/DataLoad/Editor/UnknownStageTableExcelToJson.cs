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
    /// UnknownStageTable.xlsx(UnknownLangTable 시트)를 JSON으로 변환한다.
    /// 1행 헤더, 2행 타입 힌트, 3행부터 데이터인 아이템 시트와 같은 구조라
    /// ExcelSheetReader.ReadSheetRows를 그대로 사용한다. 시트가 하나뿐이라 첫 번째 시트만 읽는다.
    /// </summary>
    public static class UnknownStageTableExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/UnknownStageData/2. JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/UnknownStageData/1. ExcelFile/UnknownStageTable.xlsx";

        [MenuItem("DataLoader/Unknown Stage Table/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = ResolveExcelPath();
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string suggestedJsonFileName = Path.GetFileNameWithoutExtension(excelPath) + ".json";
            string jsonPath = EditorUtility.SaveFilePanel("Save unknown stage table JSON", defaultAbsoluteFolder, suggestedJsonFileName, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Convert(excelPath, jsonPath);
        }

        /// <summary>사전 설정된 경로에 파일이 있으면 그것을, 없으면 파일 선택 대화상자를 띄우고 결과를 반환한다.</summary>
        private static string ResolveExcelPath()
        {
            string defaultAbsolutePath = AssetPathToAbsolutePath(DefaultExcelPath);
            if (File.Exists(defaultAbsolutePath))
            {
                Debug.Log("[UnknownStageTable] 사전 설정된 엑셀 파일을 사용합니다: " + DefaultExcelPath);
                return defaultAbsolutePath;
            }

            return EditorUtility.OpenFilePanel("Select unknown stage table", Application.dataPath, "xlsx");
        }

        public static void Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[UnknownStageTable] Excel file not found: {excelAbsolutePath}");
                return;
            }

            List<UnknownStageTableRow> rows;

            using (FileStream stream = File.Open(excelAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
            {
                // 시트가 하나뿐이라 첫 번째 시트만 읽는다.
                List<Dictionary<string, string>> sheetRows = ExcelSheetReader.ReadSheetRows(reader);
                rows = ExcelSheetReader.MapRows<UnknownStageTableRow>(sheetRows);
            }

            if (rows.Count == 0)
            {
                Debug.LogError("[UnknownStageTable] 변환할 데이터 행이 없습니다. 시트 구조(1행 헤더 / 2행 타입 힌트 / 3행부터 데이터)를 확인해주세요.");
                return;
            }

            string json = JsonConvert.SerializeObject(rows, Formatting.Indented);
            string directory = Path.GetDirectoryName(jsonAbsolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(jsonAbsolutePath, json);
            AssetDatabase.Refresh();

            Debug.Log($"[UnknownStageTable] JSON generated: {jsonAbsolutePath}\n행 {rows.Count}개");
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

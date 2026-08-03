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
    /// StatDataLabel.xlsx(KOR/ENG 시트)를 JSON으로 변환한다.
    /// statKey 컬럼은 PlayerStat.cs의 실제 필드명과 1:1로 맞춰져 있다 (별도 ID 스킴 없음).
    /// 아이템 시트와 같은 구조(1행 헤더, 타입 힌트 행 없음)라 ExcelSheetReader.ReadSheetRows를 그대로 사용한다.
    /// </summary>
    public static class StatLabelExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/CharData/JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/CharData/ExcelFile/StatDataLabel.xlsx";

        [MenuItem("DataLoader/Stat Label/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = ResolveExcelPath();
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string suggestedJsonFileName = Path.GetFileNameWithoutExtension(excelPath) + ".json";
            string jsonPath = EditorUtility.SaveFilePanel("Save stat label JSON", defaultAbsoluteFolder, suggestedJsonFileName, "json");
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
                Debug.Log("[StatLabel] 사전 설정된 엑셀 파일을 사용합니다: " + DefaultExcelPath);
                return defaultAbsolutePath;
            }

            return EditorUtility.OpenFilePanel("Select stat label table", Application.dataPath, "xlsx");
        }

        public static void Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[StatLabel] Excel file not found: {excelAbsolutePath}");
                return;
            }

            StatLabelJsonData data = new StatLabelJsonData();

            using (FileStream stream = File.Open(excelAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
            {
                do
                {
                    string sheetName = reader.Name;
                    List<Dictionary<string, string>> rows = ExcelSheetReader.ReadSheetRows(reader);
                    ApplyRowsToData(sheetName, rows, data);
                }
                while (reader.NextResult());
            }

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            string directory = Path.GetDirectoryName(jsonAbsolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(jsonAbsolutePath, json);
            AssetDatabase.Refresh();

            Debug.Log($"[StatLabel] JSON generated: {jsonAbsolutePath}\n" +
                      $"KOR: {data.korLabels.Count}, ENG: {data.engLabels.Count}");
        }

        private static void ApplyRowsToData(string sheetName, List<Dictionary<string, string>> rows, StatLabelJsonData data)
        {
            switch (sheetName)
            {
                case "KOR":
                    data.korLabels = ExcelSheetReader.MapRows<StatLabelRow>(rows);
                    break;

                case "ENG":
                    data.engLabels = ExcelSheetReader.MapRows<StatLabelRow>(rows);
                    break;

                default:
                    Debug.LogWarning($"[StatLabel] 알 수 없는 시트라 건너뜁니다: {sheetName}");
                    break;
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

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }
}

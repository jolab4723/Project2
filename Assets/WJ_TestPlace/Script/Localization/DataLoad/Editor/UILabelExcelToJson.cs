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
    /// UILabel.xlsx(KOR/ENG/JPN/CHN 시트)를 JSON으로 변환한다. QuestLabelExcelToJson과 완전히 같은 구조
    /// (1행 헤더, key/label 두 컬럼)라 ExcelSheetReader.ReadSheetRows를 그대로 쓴다.
    /// </summary>
    public static class UILabelExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/UIData/2. JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/UIData/1. ExcelFile/UILabel.xlsx";

        [MenuItem("DataLoader/UI Label/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = ResolveExcelPath();
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string suggestedJsonFileName = Path.GetFileNameWithoutExtension(excelPath) + ".json";
            string jsonPath = EditorUtility.SaveFilePanel("Save UI label JSON", defaultAbsoluteFolder, suggestedJsonFileName, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Convert(excelPath, jsonPath);
        }

        /// <summary>
        /// 대화상자 없이 기본 경로만으로 변환한다. 통합 실행(0. Run All Steps)에서 쓴다.
        /// </summary>
        /// <returns>생성된 JSON의 절대 경로. 엑셀이 없거나 실패하면 null.</returns>
        public static string ConvertWithDefaultPaths()
        {
            string excelAbsolutePath = AssetPathToAbsolutePath(DefaultExcelPath);
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogWarning($"[UILabel] UI 라벨 엑셀이 없어 변환을 건너뜁니다: {DefaultExcelPath}");
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
                Debug.Log("[UILabel] 사전 설정된 엑셀 파일을 사용합니다: " + DefaultExcelPath);
                return defaultAbsolutePath;
            }

            return EditorUtility.OpenFilePanel("Select UI label table", Application.dataPath, "xlsx");
        }

        public static void Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[UILabel] Excel file not found: {excelAbsolutePath}");
                return;
            }

            UILabelJsonData data = new UILabelJsonData();

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

            Debug.Log($"[UILabel] JSON generated: {jsonAbsolutePath}\n" +
                      $"KOR: {data.korLabels.Count}, ENG: {data.engLabels.Count}, JPN: {data.jpnLabels.Count}, CHN: {data.chnLabels.Count}");
        }

        private static void ApplyRowsToData(string sheetName, List<Dictionary<string, string>> rows, UILabelJsonData data)
        {
            switch (sheetName)
            {
                case "KOR":
                    data.korLabels = ExcelSheetReader.MapRows<UILabelRow>(rows);
                    break;

                case "ENG":
                    data.engLabels = ExcelSheetReader.MapRows<UILabelRow>(rows);
                    break;

                case "JPN":
                    data.jpnLabels = ExcelSheetReader.MapRows<UILabelRow>(rows);
                    break;

                case "CHN":
                    data.chnLabels = ExcelSheetReader.MapRows<UILabelRow>(rows);
                    break;

                default:
                    Debug.LogWarning($"[UILabel] 알 수 없는 시트라 건너뜁니다: {sheetName}");
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

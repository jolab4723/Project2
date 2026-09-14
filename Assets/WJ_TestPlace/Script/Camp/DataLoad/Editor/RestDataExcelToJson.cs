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
    /// RestData.xlsx(Rest 시트)를 JSON으로 변환한다. UILabelExcelToJson과 같은 구조
    /// (1행 헤더 + 데이터 행)라 ExcelSheetReader.ReadSheetRows를 그대로 쓴다.
    /// </summary>
    public static class RestDataExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/RestData/2. JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/RestData/1. ExcelFile/RestData.xlsx";
        private const string RestSheetName = "Rest";

        [MenuItem("DataLoader/Rest Data/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = ResolveExcelPath();
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string suggestedJsonFileName = Path.GetFileNameWithoutExtension(excelPath) + ".json";
            string jsonPath = EditorUtility.SaveFilePanel("Save rest data JSON", defaultAbsoluteFolder, suggestedJsonFileName, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Convert(excelPath, jsonPath);
        }

        /// <summary>대화상자 없이 기본 경로만으로 변환한다. 통합 실행(0. Run All Steps)에서 쓴다.</summary>
        /// <returns>생성된 JSON의 절대 경로. 엑셀이 없거나 실패하면 null.</returns>
        public static string ConvertWithDefaultPaths()
        {
            string excelAbsolutePath = AssetPathToAbsolutePath(DefaultExcelPath);
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogWarning($"[RestData] 휴식 데이터 엑셀이 없어 변환을 건너뜁니다: {DefaultExcelPath}");
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
                Debug.Log("[RestData] 사전 설정된 엑셀 파일을 사용합니다: " + DefaultExcelPath);
                return defaultAbsolutePath;
            }

            return EditorUtility.OpenFilePanel("Select rest data table", Application.dataPath, "xlsx");
        }

        public static void Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[RestData] Excel file not found: {excelAbsolutePath}");
                return;
            }

            RestDataJsonData data = new RestDataJsonData();

            using (FileStream stream = File.Open(excelAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
            {
                do
                {
                    string sheetName = reader.Name;
                    List<Dictionary<string, string>> rows = ExcelSheetReader.ReadSheetRows(reader);

                    if (sheetName == RestSheetName)
                        data.restSettings = ExcelSheetReader.MapRows<RestDataRow>(rows);
                    else
                        Debug.LogWarning($"[RestData] 알 수 없는 시트라 건너뜁니다: {sheetName}");
                }
                while (reader.NextResult());
            }

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            string directory = Path.GetDirectoryName(jsonAbsolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(jsonAbsolutePath, json);
            AssetDatabase.Refresh();

            Debug.Log($"[RestData] JSON generated: {jsonAbsolutePath}\n설정 행: {data.restSettings.Count}개");
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

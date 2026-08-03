using System.Collections.Generic;
using System.IO;
using ExcelDataReader;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using DataSystem.Excel;

namespace DataSystem
{
    public static class ItemTableExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/ItemData/2. JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/ItemData/1. ExcelFile/ItemSubStatData.xlsx";

        [MenuItem("DataLoader/Item Table/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = ResolveExcelPath();
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string suggestedJsonFileName = Path.GetFileNameWithoutExtension(excelPath) + ".json";
            string jsonPath = EditorUtility.SaveFilePanel("Save item table JSON", defaultAbsoluteFolder, suggestedJsonFileName, "json");
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
                Debug.Log("[ItemTable] 사전 설정된 엑셀 파일을 사용합니다: " + DefaultExcelPath);
                return defaultAbsolutePath;
            }

            return EditorUtility.OpenFilePanel("Select structured item table", Application.dataPath, "xlsx");
        }

        public static void Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[ItemTable] Excel file not found: {excelAbsolutePath}");
                return;
            }

            ItemTableJsonData data = new ItemTableJsonData();

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

            ApplyMaxValueFallback(data);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            string directory = Path.GetDirectoryName(jsonAbsolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(jsonAbsolutePath, json);
            AssetDatabase.Refresh();

            Debug.Log($"[ItemTable] JSON generated: {jsonAbsolutePath}\n" +
                      $"SubStatPools: {data.subStatPools.Count}, ElementalBonusConfig: {(data.elementalBonusConfig != null ? "있음" : "없음")}");
        }

        private static void ApplyRowsToData(string sheetName, List<Dictionary<string, string>> rows, ItemTableJsonData data)
        {
            switch (sheetName)
            {
                case "SubStatPools":
                    data.subStatPools = ExcelSheetReader.MapRows<SubStatPoolRow>(rows);
                    break;

                case "ElementalBonusConfigs":
                    List<ElementalBonusConfigRow> configs = ExcelSheetReader.MapRows<ElementalBonusConfigRow>(rows);
                    if (configs.Count > 1)
                        Debug.LogWarning($"[ItemTable] ElementalBonusConfigs는 전역 설정 하나만 써야 하는데 {configs.Count}개 행이 있습니다. 첫 번째 행만 사용합니다.");
                    data.elementalBonusConfig = configs.Count > 0 ? configs[0] : null;
                    break;

                default:
                    Debug.LogWarning($"[ItemTable] 알 수 없는 시트라 건너뜁니다: {sheetName}");
                    break;
            }
        }

        /// <summary>maxValue가 minValue보다 작으면(비어서 0으로 들어온 경우 포함) minValue로 채운다.</summary>
        private static void ApplyMaxValueFallback(ItemTableJsonData data)
        {
            foreach (SubStatPoolRow row in data.subStatPools)
            {
                if (row.maxValue < row.minValue)
                    row.maxValue = row.minValue;
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

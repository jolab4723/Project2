using System.Collections.Generic;
using System.IO;
using ExcelDataReader;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using DataSystem.Excel;

namespace DataSystem
{
    public static class ItemDataTableExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/ItemData/JSONFile";

        [MenuItem("DataLoader/Item Data Table/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = EditorUtility.OpenFilePanel("Select item data table", Application.dataPath, "xlsx");
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string suggestedJsonFileName = Path.GetFileNameWithoutExtension(excelPath) + ".json";
            string jsonPath = EditorUtility.SaveFilePanel("Save item data table JSON", defaultAbsoluteFolder, suggestedJsonFileName, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Convert(excelPath, jsonPath);
        }

        public static void Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[ItemDataTable] Excel file not found: {excelAbsolutePath}");
                return;
            }

            ItemDataTableJsonData data = new ItemDataTableJsonData();

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

            Debug.Log($"[ItemDataTable] JSON generated: {jsonAbsolutePath}\n" +
                      $"Armor: {data.armorDefinitions.Count}, Weapon: {data.weaponDefinitions.Count}, Potion: {data.potionDefinitions.Count}");
        }

        private static void ApplyRowsToData(string sheetName, List<Dictionary<string, string>> rows, ItemDataTableJsonData data)
        {
            switch (sheetName)
            {
                case "ArmorDefinitions":
                    data.armorDefinitions = ExcelSheetReader.MapRows<ArmorDefinitionRow>(rows);
                    break;

                case "WeaponDefinitions":
                    data.weaponDefinitions = ExcelSheetReader.MapRows<WeaponDefinitionRow>(rows);
                    break;

                case "PotionDefinitions":
                    data.potionDefinitions = ExcelSheetReader.MapRows<PotionDefinitionRow>(rows);
                    break;

                case "RelicDefinitions":
                case "ComboBox":
                    // 아직 빈 시트 - 나중에 내용 채워지면 케이스 추가
                    break;

                default:
                    Debug.LogWarning($"[ItemDataTable] 알 수 없는 시트라 건너뜁니다: {sheetName}");
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

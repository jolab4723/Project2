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
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/ItemData/2. JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/ItemData/1. ExcelFile/ItemDataTable.xlsx";

        [MenuItem("DataLoader/Item Data Table/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = ResolveExcelPath();
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

        /// <summary>
        /// 엑셀은 사전 설정된 경로를 우선 쓰고(없으면 대화상자), JSON은 대화상자 없이
        /// 기본 폴더에 엑셀과 같은 이름으로 저장한다. 통합 실행처럼 중간에 멈추면 안 되는 곳에서 쓴다.
        /// </summary>
        /// <returns>생성된 JSON의 절대 경로. 엑셀을 못 정했으면 null.</returns>
        public static string ConvertPreferringDefaultPaths()
        {
            string excelPath = ResolveExcelPath();
            if (string.IsNullOrEmpty(excelPath))
                return null;

            EnsureAssetFolder(DefaultJsonFolder);
            string jsonPath = Path.Combine(
                AssetPathToAbsolutePath(DefaultJsonFolder),
                Path.GetFileNameWithoutExtension(excelPath) + ".json");

            Convert(excelPath, jsonPath);

            return File.Exists(jsonPath) ? jsonPath : null;
        }

        /// <summary>사전 설정된 경로에 파일이 있으면 그것을, 없으면 파일 선택 대화상자를 띄우고 결과를 반환한다.</summary>
        private static string ResolveExcelPath()
        {
            string defaultAbsolutePath = AssetPathToAbsolutePath(DefaultExcelPath);
            if (File.Exists(defaultAbsolutePath))
            {
                Debug.Log("[ItemDataTable] 사전 설정된 엑셀 파일을 사용합니다: " + DefaultExcelPath);
                return defaultAbsolutePath;
            }

            return EditorUtility.OpenFilePanel("Select item data table", Application.dataPath, "xlsx");
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
                      $"Armor: {data.armorDefinitions.Count}, Weapon: {data.weaponDefinitions.Count}, " +
                      $"Potion: {data.potionDefinitions.Count}, Relic: {data.relicDefinitions.Count}");
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
                    data.relicDefinitions = ExcelSheetReader.MapRows<RelicDefinitionRow>(rows);
                    break;

                case "ComboBox":
                    // 드롭다운 검증용 시트라 데이터가 아니다.
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

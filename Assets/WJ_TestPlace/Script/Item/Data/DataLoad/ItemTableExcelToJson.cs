using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using ExcelDataReader;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    public static class ItemTableExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/ItemData/JSONFile";
        private const string DefaultJsonFileName = "item_table_structured.json";

        [MenuItem("DataLoader/Item Table/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = EditorUtility.OpenFilePanel("Select structured item table", Application.dataPath, "xlsx");
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.SaveFilePanel("Save item table JSON", defaultAbsoluteFolder, DefaultJsonFileName, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Convert(excelPath, jsonPath);
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
                    List<Dictionary<string, string>> rows = ReadSheetRows(reader);
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

            Debug.Log($"[ItemTable] JSON generated: {jsonAbsolutePath}\n" +
                      $"Items: {data.itemDefinitions.Count}, Weapons: {data.weaponDefinitions.Count}, Armors: {data.armorDefinitions.Count}, Options: {data.optionDefinitions.Count}");
        }

        private static List<Dictionary<string, string>> ReadSheetRows(IExcelDataReader reader)
        {
            List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
            List<string> headers = new List<string>();
            bool headerRead = false;

            while (reader.Read())
            {
                if (!headerRead)
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string header = CellToString(reader.GetValue(i));
                        headers.Add(header);
                    }

                    headerRead = true;
                    continue;
                }

                Dictionary<string, string> row = new Dictionary<string, string>();
                bool hasData = false;

                for (int i = 0; i < reader.FieldCount && i < headers.Count; i++)
                {
                    string header = headers[i];
                    if (string.IsNullOrWhiteSpace(header))
                        continue;

                    string value = CellToString(reader.GetValue(i));
                    if (!string.IsNullOrWhiteSpace(value))
                        hasData = true;

                    row[header] = value;
                }

                if (hasData)
                    result.Add(row);
            }

            return result;
        }

        private static void ApplyRowsToData(string sheetName, List<Dictionary<string, string>> rows, ItemTableJsonData data)
        {
            switch (sheetName)
            {
                case "ItemDefinitions":
                    data.itemDefinitions = MapRows<ItemDefinitionRow>(rows);
                    break;
                case "EquipmentDefinitions":
                    data.equipmentDefinitions = MapRows<EquipmentDefinitionRow>(rows);
                    break;
                case "WeaponDefinitions":
                    data.weaponDefinitions = MapRows<WeaponDefinitionRow>(rows);
                    break;
                case "ArmorDefinitions":
                    data.armorDefinitions = MapRows<ArmorDefinitionRow>(rows);
                    break;
                case "PotionDefinitions":
                    data.potionDefinitions = MapRows<PotionDefinitionRow>(rows);
                    break;
                case "RelicDefinitions":
                    data.relicDefinitions = MapRows<RelicDefinitionRow>(rows);
                    break;
                case "OptionDefinitions":
                    data.optionDefinitions = MapRows<OptionDefinitionRow>(rows);
                    break;
                case "SubStatPools":
                    data.subStatPools = MapRows<SubStatPoolRow>(rows);
                    break;
                case "RarityOptionRules":
                    data.rarityOptionRules = MapRows<RarityOptionRuleRow>(rows);
                    break;
                case "ElementalBonusConfigs":
                    data.elementalBonusConfigs = MapRows<ElementalBonusConfigRow>(rows);
                    break;
                case "BuffDefinitions":
                    data.buffDefinitions = MapRows<BuffDefinitionRow>(rows);
                    break;
                case "UniqueEffectDefinitions":
                    data.uniqueEffectDefinitions = MapRows<UniqueEffectDefinitionRow>(rows);
                    break;
                case "DisplayNames":
                    data.displayNames = MapRows<DisplayNameRow>(rows);
                    break;
                case "DraftSourceRows":
                    data.draftSourceRows = MapRows<DraftSourceRow>(rows);
                    break;
            }
        }

        private static List<T> MapRows<T>(List<Dictionary<string, string>> rows) where T : new()
        {
            List<T> result = new List<T>();
            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (Dictionary<string, string> row in rows)
            {
                T item = new T();

                foreach (FieldInfo field in fields)
                {
                    if (!row.TryGetValue(field.Name, out string value))
                        continue;

                    field.SetValue(item, ConvertValue(value, field.FieldType));
                }

                result.Add(item);
            }

            return result;
        }

        private static object ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(string))
                return value ?? string.Empty;

            if (targetType == typeof(int))
            {
                if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int intValue))
                    return intValue;
                if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatValue))
                    return Mathf.RoundToInt(floatValue);
                return 0;
            }

            if (targetType == typeof(float))
            {
                if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatValue))
                    return floatValue;
                return 0f;
            }

            if (targetType == typeof(bool))
            {
                string lower = (value ?? string.Empty).Trim().ToLowerInvariant();
                return lower == "true" || lower == "1" || lower == "yes" || lower == "y" || lower == "o" || lower == "체크";
            }

            return null;
        }

        private static string CellToString(object value)
        {
            if (value == null)
                return string.Empty;

            if (value is double doubleValue)
                return doubleValue.ToString(CultureInfo.InvariantCulture);

            if (value is float floatValue)
                return floatValue.ToString(CultureInfo.InvariantCulture);

            if (value is int intValue)
                return intValue.ToString(CultureInfo.InvariantCulture);

            if (value is bool boolValue)
                return boolValue ? "true" : "false";

            return value.ToString().Trim();
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

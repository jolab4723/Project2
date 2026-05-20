using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExcelDataReader;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class ExcelToJsonConverter
{
    [MenuItem("Tools/Convert Excel to JSON")]
    public static void ConvertExcel()
    {
        string excelBaseFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources/DataFiles/ExcelFile/"));
        string jsonBaseFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources/DataFiles/JSONFile/"));

        try
        {
            if (!Directory.Exists(excelBaseFolder))
            {
                Log.Error($"엑셀 기준 폴더가 존재하지 않습니다: {excelBaseFolder}");
                return;
            }

            string[] excelFiles = Directory.GetFiles(excelBaseFolder, "*.xlsx", SearchOption.AllDirectories);

            if (excelFiles.Length == 0)
            {
                Log.Warning("변환할 엑셀 파일(.xlsx)이 폴더 또는 하위 폴더에 없습니다.");
                return;
            }

            int successCount = 0;

            foreach (string excelFilePath in excelFiles)
            {
                if (Path.GetFileName(excelFilePath).StartsWith("~$"))
                    continue;

                string relativePath = excelFilePath.Replace(excelBaseFolder, "");
                string relativeJsonPath = Path.ChangeExtension(relativePath, ".json");
                string jsonOutputPath = Path.Combine(jsonBaseFolder, relativeJsonPath);
                string targetJsonDirectory = Path.GetDirectoryName(jsonOutputPath);

                if (!Directory.Exists(targetJsonDirectory))
                {
                    Directory.CreateDirectory(targetJsonDirectory);
                }

                var jsonResultList = new List<Dictionary<string, object>>();

                using (var stream = File.Open(excelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        List<string> dataTypes = new List<string>();
                        List<string> headers = new List<string>();
                        int rowCount = 0;

                        while (reader.Read())
                        {
                            if (rowCount == 0)
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    dataTypes.Add(reader.GetValue(i)?.ToString().Trim().ToLower() ?? "string");
                                }
                                rowCount++;
                                continue;
                            }

                            if (rowCount == 1)
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    headers.Add(reader.GetValue(i)?.ToString().Trim() ?? $"Column{i}");
                                }
                                rowCount++;
                                continue;
                            }

                            var rowData = new Dictionary<string, object>();
                            bool hasData = false;

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                if (i >= headers.Count) break;

                                string headerName = headers[i];
                                string typeName = i < dataTypes.Count ? dataTypes[i] : "string";
                                object rawValue = reader.GetValue(i);

                                if (rawValue != null && !string.IsNullOrEmpty(rawValue.ToString()))
                                {
                                    hasData = true;
                                    rowData[headerName] = ParseValue(rawValue.ToString(), typeName);
                                }
                                else
                                {
                                    rowData[headerName] = GetDefaultValue(typeName);
                                }
                            }

                            if (hasData)
                            {
                                jsonResultList.Add(rowData);
                            }

                            rowCount++;
                        }
                    }
                }

                string jsonString = JsonConvert.SerializeObject(jsonResultList, Formatting.Indented);
                File.WriteAllText(jsonOutputPath, jsonString, Encoding.UTF8);
                successCount++;
            }

            AssetDatabase.Refresh();
            Log.Print($"총 {successCount}개의 파일이 JSON으로 변환되었습니다.");
        }
        catch (Exception ex)
        {
            Log.Error($"엑셀 변환 중 오류 발생: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static object ParseValue(string value, string type)
    {
        switch (type)
        {
            case "int":
            case "int32":
                if (int.TryParse(value, out int intVal)) return intVal;
                if (float.TryParse(value, out float fVal)) return (int)fVal;
                return 0;

            case "float":
                return float.TryParse(value, out float floatVal) ? floatVal : 0f;

            case "double":
                return double.TryParse(value, out double doubleVal) ? doubleVal : 0.0;

            case "bool":
            case "boolean":
                string lowerVal = value.ToLower();
                if (lowerVal == "1" || lowerVal == "true") return true;
                return false;

            case "string":
            default:
                return value;
        }
    }

    private static object GetDefaultValue(string type)
    {
        switch (type)
        {
            case "int": return 0;
            case "float": return 0f;
            case "double": return 0.0;
            case "bool": return false;
            default: return "";
        }
    }
}
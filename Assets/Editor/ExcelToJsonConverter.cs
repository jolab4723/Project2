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
        // 1. 기준 폴더 경로 설정 (상대 경로 계산을 위해 마지막에 슬래시 처리)
        string excelBaseFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources/DataFiles/ExcelFile/"));
        string jsonBaseFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources/DataFiles/JSONFile/"));

        try
        {
            if (!Directory.Exists(excelBaseFolder))
            {
                Debug.LogError($"엑셀 기준 폴더가 존재하지 않습니다: {excelBaseFolder}");
                return;
            }

            // 2. SearchOption.AllDirectories 옵션으로 하위 폴더 내의 모든 .xlsx 파일까지 싹 긁어옵니다.
            string[] excelFiles = Directory.GetFiles(excelBaseFolder, "*.xlsx", SearchOption.AllDirectories);

            if (excelFiles.Length == 0)
            {
                Debug.LogWarning("변환할 엑셀 파일(.xlsx)이 폴더 또는 하위 폴더에 없습니다.");
                return;
            }

            // 인코딩 문제 방지용 (필요 없을 시 주석 처리 가능)
            // System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            int successCount = 0;

            foreach (string excelFilePath in excelFiles)
            {
                if (Path.GetFileName(excelFilePath).StartsWith("~$"))
                    continue;

                // 3. 하위 폴더 구조 원본 경로 파악하기 (상대 경로 추출)
                // 예: 원본이 ".../ExcelFile/Monster/Boss.xlsx" 이면 
                // relativePath는 "Monster/Boss.xlsx" 가 됩니다.
                string relativePath = excelFilePath.Replace(excelBaseFolder, "");

                // 4. 저장할 JSON 폴더 구조 및 파일 전체 경로 계산
                // 예: ".../JSONFile/Monster/Boss.json"
                string relativeJsonPath = Path.ChangeExtension(relativePath, ".json");
                string jsonOutputPath = Path.Combine(jsonBaseFolder, relativeJsonPath);

                // 파일이 저장될 부모 폴더 경로 추출 (예: ".../JSONFile/Monster")
                string targetJsonDirectory = Path.GetDirectoryName(jsonOutputPath);

                // 5. 만약 복제될 하위 폴더 구조가 JSONFile 내에 없다면 자동으로 생성해 줍니다.
                if (!Directory.Exists(targetJsonDirectory))
                {
                    Directory.CreateDirectory(targetJsonDirectory);
                }

                // 한 파일의 데이터들을 담을 리스트
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
                            // [1번째 행] 데이터 타입 정의 읽기
                            if (rowCount == 0)
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    dataTypes.Add(reader.GetValue(i)?.ToString().Trim().ToLower() ?? "string");
                                }
                                rowCount++;
                                continue;
                            }

                            // [2번째 행] 변수명(컬럼명) 읽기
                            if (rowCount == 1)
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    headers.Add(reader.GetValue(i)?.ToString().Trim() ?? $"Column{i}");
                                }
                                rowCount++;
                                continue;
                            }

                            // [3번째 행부터] 실제 데이터 파싱
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

                // 6. JSON 변환 및 저장 (폴더 트리 내의 해당 경로에 덮어쓰기)
                string jsonString = JsonConvert.SerializeObject(jsonResultList, Formatting.Indented);
                File.WriteAllText(jsonOutputPath, jsonString, Encoding.UTF8);
                successCount++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"<color=green><b>변환 완료!</b></color> 총 {successCount}개의 파일이 JSON으로 저장되었습니다.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"엑셀 변환 중 오류 발생: {ex.Message}\n{ex.StackTrace}");
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
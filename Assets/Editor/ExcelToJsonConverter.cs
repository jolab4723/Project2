using System;
using System.Data;
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
        // 1. 읽어올 엑셀 폴더 경로와 저장할 JSON 폴더 경로 설정
        string excelFolderPath = Path.Combine(Application.dataPath, "Resources/DataFiles/ExcelFiles");
        string jsonFolderPath = Path.Combine(Application.dataPath, "Resources/DataFiles/JSONFiles");

        try
        {
            // 2. 만약 폴더가 존재하지 않는다면 에러 방지를 위해 자동으로 생성
            if (!Directory.Exists(excelFolderPath))
            {
                Debug.LogError($"엑셀 폴더가 존재하지 않습니다: {excelFolderPath}");
                return;
            }
            if (!Directory.Exists(jsonFolderPath))
            {
                Directory.CreateDirectory(jsonFolderPath);
            }

            // 3. 엑셀 폴더 내의 모든 .xlsx 파일 목록 가져오기
            string[] excelFiles = Directory.GetFiles(excelFolderPath, "*.xlsx");

            if (excelFiles.Length == 0)
            {
                Debug.LogWarning("변환할 엑셀 파일(.xlsx)이 폴더에 없습니다.");
                return;
            }

            int successCount = 0;

            // 4. 모든 엑셀 파일을 하나씩 순회하며 변환
            foreach (string excelFilePath in excelFiles)
            {
                // 임시로 열려있는 임시 파일(~$파일명.xlsx)은 건너뜁니다.
                if (Path.GetFileName(excelFilePath).StartsWith("~$"))
                    continue;

                // 확장자를 제외한 순수 파일명 추출 (예: "ItemTable")
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(excelFilePath);

                // 저장할 JSON 파일의 전체 경로 완성 (예: ".../JSONFile/ItemTable.json")
                string jsonOutputPath = Path.Combine(jsonFolderPath, fileNameWithoutExtension + ".json");

                using (var stream = File.Open(excelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
                        });

                        if (result.Tables.Count > 0)
                        {
                            DataTable table = result.Tables[0];
                            string jsonString = JsonConvert.SerializeObject(table, Formatting.Indented);

                            // 파일이 이미 있으면 덮어쓰기(Overwrite)가 기본적으로 수행됩니다.
                            File.WriteAllText(jsonOutputPath, jsonString, Encoding.UTF8);
                            successCount++;
                        }
                    }
                }
            }

            // 유니티 에디터 파일 시스템 새로고침
            AssetDatabase.Refresh();

            Debug.Log($"<color=green><b>변환 완료!</b></color> 총 {successCount}개의 엑셀 파일이 JSON으로 변환되어 저장되었습니다.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"엑셀 변환 중 오류 발생: {ex.Message}");
        }
    }
}
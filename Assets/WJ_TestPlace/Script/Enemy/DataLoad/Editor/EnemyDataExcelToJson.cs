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
    /// 적 기본 스탯 엑셀(EnemyData.xlsx)을 JSON으로 변환한다.
    ///
    /// !! 아이템 쪽 변환기(ItemDataTableExcelToJson 등)와 시트 구조가 다르다.
    ///    아이템 시트: 1행 헤더, (선택) 2행 타입 힌트  -> ExcelSheetReader.ReadSheetRows 사용
    ///    이 시트: 1행 타입, 2행 헤더                  -> 아래 ReadTypesFirstSheet 사용
    ///    (CharStatTableExcelToJson.cs와 동일한 패턴)
    ///
    /// 첫 번째 시트(EnemyData)만 읽는다. 두 번째 ComboBox 시트는 드롭다운 검증용이라 건너뛴다.
    /// 이름 없는 예약 자리(enemyId만 있고 나머지가 비어있는 행, 예: "enemy.normal.melee")는
    /// 실제 데이터가 아니므로 변환 대상에서 제외하고 개수만 경고로 남긴다.
    /// </summary>
    public static class EnemyDataExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/EnemyData/JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/EnemyData/ExcelFile/EnemyData.xlsx";

        [MenuItem("DataLoader/Enemy Data/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = ResolveExcelPath();
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string suggestedJsonFileName = Path.GetFileNameWithoutExtension(excelPath) + ".json";
            string jsonPath = EditorUtility.SaveFilePanel("Save enemy data JSON", defaultAbsoluteFolder, suggestedJsonFileName, "json");
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
                Debug.Log("[EnemyData] 사전 설정된 엑셀 파일을 사용합니다: " + DefaultExcelPath);
                return defaultAbsolutePath;
            }

            return EditorUtility.OpenFilePanel("Select enemy data table", Application.dataPath, "xlsx");
        }

        public static void Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[EnemyData] Excel file not found: {excelAbsolutePath}");
                return;
            }

            List<EnemyDataRow> allRows;

            using (FileStream stream = File.Open(excelAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
            {
                // 첫 번째 시트(EnemyData)만 사용한다. 두 번째 ComboBox 시트는 읽지 않는다.
                List<Dictionary<string, string>> rows = ReadTypesFirstSheet(reader);
                allRows = ExcelSheetReader.MapRows<EnemyDataRow>(rows);
            }

            List<EnemyDataRow> realRows = new List<EnemyDataRow>();
            int placeholderCount = 0;

            foreach (EnemyDataRow row in allRows)
            {
                // enemyId만 채워두고 나머지가 빈 예약 자리(enemyGrade 미배정 등급 조합용)는 실제 데이터가 아니다.
                if (string.IsNullOrWhiteSpace(row.enemyName))
                {
                    placeholderCount++;
                    continue;
                }

                realRows.Add(row);
            }

            if (placeholderCount > 0)
                Debug.Log($"[EnemyData] enemyName이 비어있는 예약 자리 {placeholderCount}개는 변환에서 제외했습니다.");

            if (realRows.Count == 0)
            {
                Debug.LogError("[EnemyData] 변환할 데이터 행이 없습니다. 시트 구조(1행 타입 / 2행 헤더 / 3행부터 데이터)를 확인해주세요.");
                return;
            }

            string json = JsonConvert.SerializeObject(realRows, Formatting.Indented);
            string directory = Path.GetDirectoryName(jsonAbsolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(jsonAbsolutePath, json);
            AssetDatabase.Refresh();

            Debug.Log($"[EnemyData] JSON generated: {jsonAbsolutePath}\n적 {realRows.Count}개");
        }

        /// <summary>1행이 타입, 2행이 헤더인 시트를 읽어서 "헤더 이름 -> 셀 값" 딕셔너리 목록으로 반환한다.</summary>
        private static List<Dictionary<string, string>> ReadTypesFirstSheet(IExcelDataReader reader)
        {
            var result = new List<Dictionary<string, string>>();
            var headers = new List<string>();
            int rowIndex = 0;

            while (reader.Read())
            {
                if (rowIndex == 0) // 타입 행 - 건너뜀
                {
                    rowIndex++;
                    continue;
                }

                if (rowIndex == 1) // 헤더 행
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                        headers.Add(CellToString(reader.GetValue(i)));

                    rowIndex++;
                    continue;
                }

                var row = new Dictionary<string, string>();
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

                rowIndex++;

                if (!hasData) // 표 아래 빈 행
                    continue;

                result.Add(row);
            }

            return result;
        }

        private static string CellToString(object value)
        {
            if (value == null)
                return string.Empty;

            if (value is double doubleValue)
                return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (value is float floatValue)
                return floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (value is int intValue)
                return intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

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

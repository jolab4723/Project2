using System.Collections.Generic;
using System.IO;
using ExcelDataReader;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// EnemyData.xlsx의 FloorStatScale 시트(두 번째 시트)를 JSON으로 변환한다. EnemyData 시트와
    /// 같은 엑셀 파일을 쓰지만 SO를 만들지 않고 JSON만 만든다 - 층별 배율은 적 하나하나가 아니라
    /// 조회 테이블이라 SO화할 필요가 없다(FloorStatScaleTable이 런타임에 이 JSON을 직접 읽는다).
    ///
    /// 시트 구조는 EnemyData와 동일하게 1행 타입 / 2행 헤더 / 3행부터 데이터다.
    /// </summary>
    public static class FloorStatScaleExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/EnemyData/2. JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/EnemyData/1. ExcelFile/EnemyData.xlsx";
        private const string OutputFileName = "FloorStatScale.json";
        private const string SheetName = "FloorStatScale";

        [MenuItem("DataLoader/Enemy Data/3. Convert FloorStatScale To JSON")]
        public static void ConvertFromMenu()
        {
            ConvertWithDefaultPaths();
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
                Debug.LogWarning($"[FloorStatScale] 적 데이터 엑셀이 없어 변환을 건너뜁니다: {DefaultExcelPath}");
                return null;
            }

            EnsureAssetFolder(DefaultJsonFolder);
            string jsonAbsolutePath = Path.Combine(AssetPathToAbsolutePath(DefaultJsonFolder), OutputFileName);

            Convert(excelAbsolutePath, jsonAbsolutePath);

            return File.Exists(jsonAbsolutePath) ? jsonAbsolutePath : null;
        }

        public static void Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[FloorStatScale] Excel file not found: {excelAbsolutePath}");
                return;
            }

            List<Dictionary<string, string>> rawRows;

            using (FileStream stream = File.Open(excelAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
            {
                if (!SeekToSheet(reader, SheetName))
                {
                    Debug.LogError($"[FloorStatScale] '{SheetName}' 시트를 찾지 못했습니다: {excelAbsolutePath}");
                    return;
                }

                rawRows = ReadTypesFirstSheet(reader);
            }

            if (rawRows.Count == 0)
            {
                Debug.LogError("[FloorStatScale] 변환할 데이터 행이 없습니다. 시트 구조(1행 타입 / 2행 헤더 / 3행부터 데이터)를 확인해주세요.");
                return;
            }

            ApplyDifficultyCarryForward(rawRows);

            List<FloorStatScaleRow> rows = MapRows(rawRows);

            string json = JsonConvert.SerializeObject(rows, Formatting.Indented);
            string directory = Path.GetDirectoryName(jsonAbsolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(jsonAbsolutePath, json);
            AssetDatabase.Refresh();

            Debug.Log($"[FloorStatScale] JSON generated: {jsonAbsolutePath}\n층 {rows.Count}개");
        }

        /// <summary>
        /// 엑셀에서 difficulty는 값이 바뀌는 행에만 적혀있고 그 아래는 빈 셀로 남겨두는 표기 방식을 쓴다
        /// (병합 셀 아님). 빈 칸은 바로 위에서 마지막으로 채워진 값을 그대로 이어받는다.
        /// 맨 첫 행부터 비어있으면(엑셀 작성 실수) 1로 둔다.
        /// </summary>
        private static void ApplyDifficultyCarryForward(List<Dictionary<string, string>> rows)
        {
            string lastDifficulty = "1";

            foreach (Dictionary<string, string> row in rows)
            {
                if (row.TryGetValue("difficulty", out string value) && !string.IsNullOrWhiteSpace(value))
                {
                    lastDifficulty = value;
                }
                else
                {
                    row["difficulty"] = lastDifficulty;
                }
            }
        }

        /// <summary>
        /// ExcelSheetReader.MapRows(리플렉션으로 고정된 필드 이름만 매핑)를 안 쓰고 직접 매핑한다.
        /// difficulty/floor만 이름 있는 필드로 뽑고, 나머지 컬럼(hpMultiplier 등)은 전부 컬럼 이름
        /// 그대로 FloorStatScaleRow.multipliers에 담는다 - 새 배율 컬럼을 엑셀에 추가해도
        /// FloorStatScaleRow에 필드를 새로 안 만들어도 자동으로 JSON에 실린다.
        /// </summary>
        private static List<FloorStatScaleRow> MapRows(List<Dictionary<string, string>> rawRows)
        {
            var result = new List<FloorStatScaleRow>();

            foreach (Dictionary<string, string> rawRow in rawRows)
            {
                if (!rawRow.TryGetValue("floor", out string floorText) || string.IsNullOrWhiteSpace(floorText))
                {
                    Debug.LogWarning("[FloorStatScale] floor 값이 없는 행을 건너뜁니다.");
                    continue;
                }

                var row = new FloorStatScaleRow
                {
                    floor = ParseInt(floorText),
                    difficulty = rawRow.TryGetValue("difficulty", out string difficultyText) ? ParseFloat(difficultyText) : 1f,
                };

                foreach (KeyValuePair<string, string> cell in rawRow)
                {
                    if (cell.Key == "floor" || cell.Key == "difficulty")
                        continue;

                    row.multipliers[cell.Key] = ParseFloat(cell.Value);
                }

                result.Add(row);
            }

            return result;
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out int result) ? result : 0;
        }

        private static float ParseFloat(string value)
        {
            return float.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float result) ? result : 0f;
        }

        /// <summary>워크북의 시트를 이름으로 찾아 그 시트가 현재 결과셋이 되도록 커서를 이동한다.</summary>
        private static bool SeekToSheet(IExcelDataReader reader, string sheetName)
        {
            do
            {
                if (reader.Name == sheetName)
                    return true;
            }
            while (reader.NextResult());

            return false;
        }

        /// <summary>1행이 타입, 2행이 헤더인 시트를 읽어서 "헤더 이름 -> 셀 값" 딕셔너리 목록으로 반환한다.
        /// EnemyDataExcelToJson.ReadTypesFirstSheet와 같은 패턴(시트마다 독립된 변환기로 두는 기존 방침).</summary>
        private static List<Dictionary<string, string>> ReadTypesFirstSheet(IExcelDataReader reader)
        {
            var result = new List<Dictionary<string, string>>();
            var headers = new List<string>();
            int rowIndex = 0;

            while (reader.Read())
            {
                if (rowIndex == 0)
                {
                    rowIndex++;
                    continue;
                }

                if (rowIndex == 1)
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

                if (!hasData)
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

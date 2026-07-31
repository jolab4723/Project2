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
    /// UnknownStageLabel.xlsx(KOR/ENG/JPN/CHN 시트)를 JSON으로 변환한다.
    /// stageId 컬럼은 UnknownStageTable.xlsx의 unknownLanguageId와 키 체계가 다르므로 서로 매핑하지 않고,
    /// 이 파일 자체의 구조(언어별 시트 + stageId 문자열 키) 그대로만 JSON화한다.
    /// 1행 헤더만 있고 타입 힌트 행이 없어 ExcelSheetReader.ReadSheetRows를 그대로 사용한다.
    /// </summary>
    public static class UnknownStageLabelExcelToJson
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/UnknownStageData/2. JSONFile";
        private const string DefaultExcelPath = "Assets/Resources/DataFiles/UnknownStageData/1. ExcelFile/UnknownStageLabel.xlsx";

        [MenuItem("DataLoader/Unknown Stage Label/1. Convert Excel To JSON")]
        public static void ConvertExcelToJsonFromMenu()
        {
            string excelPath = ResolveExcelPath();
            if (string.IsNullOrEmpty(excelPath))
                return;

            EnsureAssetFolder(DefaultJsonFolder);
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string suggestedJsonFileName = Path.GetFileNameWithoutExtension(excelPath) + ".json";
            string jsonPath = EditorUtility.SaveFilePanel("Save unknown stage label JSON", defaultAbsoluteFolder, suggestedJsonFileName, "json");
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
                Debug.Log("[UnknownStageLabel] 사전 설정된 엑셀 파일을 사용합니다: " + DefaultExcelPath);
                return defaultAbsolutePath;
            }

            return EditorUtility.OpenFilePanel("Select unknown stage label table", Application.dataPath, "xlsx");
        }

        public static void Convert(string excelAbsolutePath, string jsonAbsolutePath)
        {
            if (!File.Exists(excelAbsolutePath))
            {
                Debug.LogError($"[UnknownStageLabel] Excel file not found: {excelAbsolutePath}");
                return;
            }

            UnknownStageLabelJsonData data = new UnknownStageLabelJsonData();

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

            Debug.Log($"[UnknownStageLabel] JSON generated: {jsonAbsolutePath}\n" +
                      $"KOR: {data.korLabels.Count}, ENG: {data.engLabels.Count}, JPN: {data.jpnLabels.Count}, CHN: {data.chnLabels.Count}");
        }

        private static void ApplyRowsToData(string sheetName, List<Dictionary<string, string>> rows, UnknownStageLabelJsonData data)
        {
            switch (sheetName)
            {
                case "KOR":
                    data.korLabels = ExcelSheetReader.MapRows<UnknownStageLabelRow>(rows);
                    WarnIfColumnsLookShifted(sheetName, data.korLabels);
                    break;

                case "ENG":
                    data.engLabels = ExcelSheetReader.MapRows<UnknownStageLabelRow>(rows);
                    WarnIfColumnsLookShifted(sheetName, data.engLabels);
                    break;

                case "JPN":
                    data.jpnLabels = ExcelSheetReader.MapRows<UnknownStageLabelRow>(rows);
                    WarnIfColumnsLookShifted(sheetName, data.jpnLabels);
                    break;

                case "CHN":
                    data.chnLabels = ExcelSheetReader.MapRows<UnknownStageLabelRow>(rows);
                    WarnIfColumnsLookShifted(sheetName, data.chnLabels);
                    break;

                default:
                    Debug.LogWarning($"[UnknownStageLabel] 알 수 없는 시트라 건너뜁니다: {sheetName}");
                    break;
            }
        }

        /// <summary>
        /// choice1~3Name 칸에 숫자만 들어있는 행이 있으면 원본 엑셀에서 헤더 없는 컬럼(예: choiceNumber)이
        /// 끼어들어 그 뒤 선택지 칸들이 한 칸씩 밀렸을 가능성이 크다는 뜻이라 경고로 남긴다.
        /// (실제로 ENG/JPN 시트에서 이 패턴이 발견됨 - 자동으로 보정하지 않고 원본 확인을 유도한다)
        /// </summary>
        private static void WarnIfColumnsLookShifted(string sheetName, List<UnknownStageLabelRow> labels)
        {
            int shiftedCount = 0;

            foreach (UnknownStageLabelRow label in labels)
            {
                if (LooksLikeBareNumber(label.choice1Name) || LooksLikeBareNumber(label.choice2Name) || LooksLikeBareNumber(label.choice3Name))
                    shiftedCount++;
            }

            if (shiftedCount > 0)
            {
                Debug.LogWarning($"[UnknownStageLabel] '{sheetName}' 시트에서 {shiftedCount}개 행의 choice1~3Name 칸이 숫자로만 채워져 있습니다. " +
                                  "헤더 없는 컬럼(예: choiceNumber)이 stageDescription 뒤에 끼어들어 선택지 칸들이 한 칸씩 밀렸을 가능성이 큽니다. " +
                                  "원본 엑셀의 해당 시트 컬럼 구성을 확인해주세요 (자동 보정하지 않았습니다).");
            }
        }

        private static bool LooksLikeBareNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (char c in value)
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return true;
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

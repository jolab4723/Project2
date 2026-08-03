using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class YJ_UnknownStageSOImporter
{
    private const string LabelExcelPath =
        "Assets/Resources/DataFiles/UnknownStageData/1. ExcelFile/UnknownStageLabel.xlsx";
    private const string JsonPath =
        "Assets/Resources/DataFiles/UnknownStageData/2. JSONFile/UnknownStageTable.json";
    private const string LabelJsonPath =
        "Assets/Resources/DataFiles/UnknownStageData/2. JSONFile/UnknownStageLabel.json";
    private const string OutputRoot =
        "Assets/Resources/DataFiles/UnknownStageData/3. GeneratedAssets";
    private const string StageOutputFolder = OutputRoot + "/Stages";
    private const string DatabasePath = OutputRoot + "/AllUnknownStages.asset";
    private const string LabelDatabasePath =
        OutputRoot + "/AllUnknownStageLabels.asset";
    private const string BackgroundFolder =
        "Assets/Resources/Images/Background/UnknownStage";

    [MenuItem("DataLoader/Unknown Stage/Generate All From Excel")]
    public static void GenerateFromDefaultExcel()
    {
        string excelAbsolutePath = AssetPathToAbsolutePath(LabelExcelPath);
        if (!File.Exists(excelAbsolutePath))
        {
            Log.Error(
                $"Unknown stage label Excel file was not found: {LabelExcelPath}");
            return;
        }

        string labelJsonAbsolutePath =
            AssetPathToAbsolutePath(LabelJsonPath);

        DataSystem.UnknownStageLabelExcelToJson.Convert(
            excelAbsolutePath,
            labelJsonAbsolutePath);

        GenerateFromDefaultJson();
    }

    [MenuItem("DataLoader/Unknown Stage/Generate SO From JSON")]
    public static void GenerateFromDefaultJson()
    {
        if (!File.Exists(JsonPath))
        {
            Log.Error($"Unknown stage JSON file was not found: {JsonPath}");
            return;
        }

        List<UnknownStageJsonRow> rows;

        try
        {
            string json = File.ReadAllText(JsonPath);
            rows = JsonConvert.DeserializeObject<List<UnknownStageJsonRow>>(json);
        }
        catch (Exception exception)
        {
            Log.Error($"Failed to parse Unknown stage JSON: {exception.Message}");
            return;
        }

        if (rows == null || rows.Count == 0)
        {
            Log.Error("Unknown stage JSON does not contain any entries.");
            return;
        }

        EnsureAssetFolder(StageOutputFolder);

        List<YJ_UnknownStageDefinitionSO> generatedStages = new(rows.Count);
        HashSet<string> stageIds = new(StringComparer.Ordinal);

        foreach (UnknownStageJsonRow row in rows)
        {
            YJ_UnknownStageDefinitionSO stage =
                CreateOrUpdateStage(row, stageIds);

            if (stage != null)
                generatedStages.Add(stage);
        }

        YJ_UnknownStageDatabaseSO database = GetOrCreateDatabase();
        if (database == null)
            return;

        database.SetEditorStages(generatedStages);
        EditorUtility.SetDirty(database);

        if (!GenerateLabelDatabase())
            return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Log.Print(
            $"Generated {generatedStages.Count} Unknown stage assets at {OutputRoot}.");
    }

    private static bool GenerateLabelDatabase()
    {
        if (!File.Exists(LabelJsonPath))
        {
            Log.Error(
                $"Unknown stage label JSON file was not found: {LabelJsonPath}");
            return false;
        }

        UnknownStageLabelJsonData labels;

        try
        {
            string json = File.ReadAllText(LabelJsonPath);
            labels =
                JsonConvert.DeserializeObject<UnknownStageLabelJsonData>(json);
        }
        catch (Exception exception)
        {
            Log.Error(
                $"Failed to parse Unknown stage label JSON: {exception.Message}");
            return false;
        }

        if (labels == null)
        {
            Log.Error("Unknown stage label JSON could not be deserialized.");
            return false;
        }

        YJ_UnknownStageLabelDatabaseSO database =
            GetOrCreateLabelDatabase();
        if (database == null)
            return false;

        database.SetEditorLabels(
            labels.korLabels,
            labels.engLabels,
            labels.jpnLabels,
            labels.chnLabels);
        EditorUtility.SetDirty(database);

        return true;
    }

    private static YJ_UnknownStageDefinitionSO CreateOrUpdateStage(
        UnknownStageJsonRow row,
        HashSet<string> stageIds)
    {
        string stageId = row.stageId?.Trim();
        if (string.IsNullOrWhiteSpace(stageId))
        {
            Log.Warning("Skipped an Unknown stage entry with an empty stageId.");
            return null;
        }

        if (!stageIds.Add(stageId))
        {
            Log.Warning($"Skipped duplicate Unknown stage ID: {stageId}");
            return null;
        }

        string assetName = SanitizeFileName($"{stageId}_{row.stageName}");
        string assetPath = $"{StageOutputFolder}/{assetName}.asset";
        YJ_UnknownStageDefinitionSO stage =
            AssetDatabase.LoadAssetAtPath<YJ_UnknownStageDefinitionSO>(assetPath);

        if (stage == null)
        {
            stage = FindStageById(stageId);

            if (stage != null)
            {
                string currentPath = AssetDatabase.GetAssetPath(stage);
                string renameError = AssetDatabase.RenameAsset(
                    currentPath,
                    assetName);

                if (!string.IsNullOrEmpty(renameError))
                {
                    Log.Warning(
                        $"Failed to rename Unknown stage asset: {renameError}");
                }
            }
            else
            {
                stage = ScriptableObject.CreateInstance<
                    YJ_UnknownStageDefinitionSO>();
                AssetDatabase.CreateAsset(stage, assetPath);
            }
        }

        Sprite background = LoadBackgroundSprite(row.backgroundImage);
        stage.SetEditorData(
            stageId,
            row.stageName,
            row.choiceNumber,
            background);
        EditorUtility.SetDirty(stage);

        return stage;
    }

    private static YJ_UnknownStageDefinitionSO FindStageById(string stageId)
    {
        string[] guids = AssetDatabase.FindAssets(
            $"t:{nameof(YJ_UnknownStageDefinitionSO)}",
            new[] { StageOutputFolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            YJ_UnknownStageDefinitionSO stage =
                AssetDatabase.LoadAssetAtPath<YJ_UnknownStageDefinitionSO>(path);

            if (stage != null &&
                string.Equals(stage.StageId, stageId, StringComparison.Ordinal))
            {
                return stage;
            }
        }

        return null;
    }

    private static Sprite LoadBackgroundSprite(string backgroundName)
    {
        if (string.IsNullOrWhiteSpace(backgroundName))
            return null;

        string path = $"{BackgroundFolder}/{backgroundName.Trim()}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite == null)
            Log.Warning($"Unknown stage background Sprite was not found: {path}");

        return sprite;
    }

    private static YJ_UnknownStageDatabaseSO GetOrCreateDatabase()
    {
        YJ_UnknownStageDatabaseSO database =
            AssetDatabase.LoadAssetAtPath<YJ_UnknownStageDatabaseSO>(
                DatabasePath);

        if (database != null)
            return database;

        UnityEngine.Object existing =
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(DatabasePath);
        if (existing != null)
        {
            Log.Error(
                $"An asset of another type already exists at {DatabasePath}.");
            return null;
        }

        EnsureAssetFolder(OutputRoot);
        database = ScriptableObject.CreateInstance<YJ_UnknownStageDatabaseSO>();
        AssetDatabase.CreateAsset(database, DatabasePath);
        return database;
    }

    private static YJ_UnknownStageLabelDatabaseSO GetOrCreateLabelDatabase()
    {
        YJ_UnknownStageLabelDatabaseSO database =
            AssetDatabase.LoadAssetAtPath<YJ_UnknownStageLabelDatabaseSO>(
                LabelDatabasePath);

        if (database != null)
            return database;

        UnityEngine.Object existing =
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                LabelDatabasePath);
        if (existing != null)
        {
            Log.Error(
                $"An asset of another type already exists at {LabelDatabasePath}.");
            return null;
        }

        EnsureAssetFolder(OutputRoot);
        database =
            ScriptableObject.CreateInstance<YJ_UnknownStageLabelDatabaseSO>();
        AssetDatabase.CreateAsset(database, LabelDatabasePath);
        return database;
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
            return;

        string normalizedPath = assetFolder.Replace("\\", "/");
        string parent = Path.GetDirectoryName(normalizedPath)?.Replace("\\", "/");
        string folderName = Path.GetFileName(normalizedPath);

        if (string.IsNullOrWhiteSpace(parent))
            return;

        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName;

        return string.IsNullOrWhiteSpace(projectRoot)
            ? assetPath
            : Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string SanitizeFileName(string value)
    {
        string sanitized = value ?? string.Empty;

        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            sanitized = sanitized.Replace(invalidCharacter, '_');

        return sanitized.Trim();
    }

    [Serializable]
    private sealed class UnknownStageJsonRow
    {
        public string stageId = string.Empty;
        public string stageName = string.Empty;
        public int choiceNumber = 0;
        public string backgroundImage = string.Empty;
    }

    [Serializable]
    private sealed class UnknownStageLabelJsonData
    {
        public List<YJ_UnknownStageLabel> korLabels = new();
        public List<YJ_UnknownStageLabel> engLabels = new();
        public List<YJ_UnknownStageLabel> jpnLabels = new();
        public List<YJ_UnknownStageLabel> chnLabels = new();
    }
}

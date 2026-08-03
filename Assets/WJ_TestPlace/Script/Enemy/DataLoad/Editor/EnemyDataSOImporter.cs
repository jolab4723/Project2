using System;
using System.Collections.Generic;
using System.IO;
using EnemySystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// EnemyDataExcelToJson 결과물(JSON)을 읽어서 EnemyDefinitionSO를 생성/갱신하고 EnemyDatabaseSO에 등록한다.
    ///
    /// 파일명은 "{enemyId}_{enemyName}" 형태. enemyName이 바뀌면 새 에셋을 만드는 대신
    /// 같은 폴더에서 enemyId가 일치하는 기존 에셋을 찾아 AssetDatabase.RenameAsset으로 이름만 바꾼다
    /// (GUID가 유지되므로 기존 참조가 안 끊김). ItemDataTableSOImporter와 같은 패턴.
    /// </summary>
    public static class EnemyDataSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/EnemyData/JSONFile";
        private const string DefaultOutputRoot = "Assets/Resources/DataFiles/EnemyData/GeneratedAssets/Enemies";
        private const string EnemyDatabasePath = "Assets/Resources/DataFiles/EnemyData/GeneratedAssets/AllEnemies.asset";

        [MenuItem("DataLoader/Enemy Data/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select enemy data JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            GenerateAllFromJson(jsonPath, DefaultOutputRoot);
        }

        public static void GenerateAllFromJson(string jsonPath, string outputRoot)
        {
            List<EnemyDataRow> rows = LoadJson(jsonPath);
            if (rows == null)
                return;

            EnsureAssetFolder(outputRoot);

            EnemyDatabaseSO database = GetOrCreateDatabase();

            int count = 0;
            int registeredCount = 0;

            foreach (EnemyDataRow row in rows)
            {
                EnemyDefinitionSO asset = CreateOrUpdate(outputRoot, row);
                if (asset == null)
                    continue;

                FinalizeAsset(asset, database, ref registeredCount);
                count++;
            }

            if (registeredCount > 0)
                EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnemyData] SO 생성/갱신 완료. Enemy {count}개 (EnemyDatabaseSO에 {registeredCount}개 새로 등록됨)");
        }

        private static EnemyDefinitionSO CreateOrUpdate(string outputFolder, EnemyDataRow row)
        {
            string idText = (row.enemyId ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(idText) || idText.Contains(" "))
                Debug.LogWarning($"[EnemyData] enemyId가 비어있거나 공백을 포함합니다: '{idText}' ({row.enemyName}). 'enemy.grade.attackType.name' 형식을 확인해주세요.");

            EnemyDefinitionSO asset = GetOrCreateAsset<EnemyDefinitionSO>(outputFolder, idText, row.enemyName);
            if (asset == null)
                return null;

            asset.enemyId = idText;
            asset.enemyName = row.enemyName;
            asset.enemyGrade = ParseEnumOrDefault(row.enemyGrade, EnemyGrade.Normal);
            asset.attackType = ParseEnumOrDefault(row.attackType, EnemyAttackType.Melee);
            asset.baseHealth = row.baseHp;
            asset.baseAttackPower = row.baseATK;
            asset.baseDefensePower = row.baseDEF;
            asset.baseMoveSpeed = row.baseMS;
            asset.baseAttackSpeed = row.baseAS;
            asset.attackRange = row.attackRange;
            asset.attackCooldown = row.attackCDR;
            asset.patternId = row.patternId;

            return asset;
        }

        private static void FinalizeAsset(EnemyDefinitionSO asset, EnemyDatabaseSO database, ref int registeredCount)
        {
            EditorUtility.SetDirty(asset);

            if (database != null && !database.allEnemies.Contains(asset))
            {
                database.allEnemies.Add(asset);
                registeredCount++;
            }
        }

        private static EnemyDatabaseSO GetOrCreateDatabase()
        {
            EnemyDatabaseSO database = AssetDatabase.LoadAssetAtPath<EnemyDatabaseSO>(EnemyDatabasePath);
            if (database != null)
                return database;

            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(EnemyDatabasePath);
            if (existing != null)
            {
                Debug.LogError($"[EnemyData] Asset already exists but type is not EnemyDatabaseSO: {EnemyDatabasePath}");
                return null;
            }

            database = ScriptableObject.CreateInstance<EnemyDatabaseSO>();
            string directory = Path.GetDirectoryName(EnemyDatabasePath).Replace("\\", "/");
            EnsureAssetFolder(directory);
            AssetDatabase.CreateAsset(database, EnemyDatabasePath);
            return database;
        }

        private static List<EnemyDataRow> LoadJson(string jsonPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[EnemyData] JSON file not found: {absoluteJsonPath}");
                return null;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            List<EnemyDataRow> rows = JsonConvert.DeserializeObject<List<EnemyDataRow>>(json);
            if (rows == null)
                Debug.LogError("[EnemyData] JSON parse failed.");

            return rows;
        }

        /// <summary>
        /// 파일명은 "{id}_{displayName}" 형태로 만든다. 같은 폴더에서 이미 id가 일치하는 에셋이 있으면
        /// (이름이 뭐였든) 그 에셋을 찾아 새 이름으로 리네임해서 재사용한다 - GUID가 유지되므로 참조가 안 끊김.
        /// </summary>
        private static T GetOrCreateAsset<T>(string folder, string id, string displayName) where T : ScriptableObject
        {
            string idPrefix = SanitizeFileName(id);
            string desiredFileName = SanitizeFileName(id + "_" + displayName);
            string desiredPath = CombineAssetPath(folder, desiredFileName + ".asset");

            T asset = AssetDatabase.LoadAssetAtPath<T>(desiredPath);
            if (asset != null)
                return asset;

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);

                bool idMatches = fileName.Equals(idPrefix, StringComparison.OrdinalIgnoreCase)
                    || fileName.StartsWith(idPrefix + "_", StringComparison.OrdinalIgnoreCase);
                if (!idMatches)
                    continue;

                T existing = AssetDatabase.LoadAssetAtPath<T>(path);
                if (existing == null)
                    continue;

                if (!string.Equals(fileName, desiredFileName, StringComparison.Ordinal))
                {
                    string error = AssetDatabase.RenameAsset(path, desiredFileName);
                    if (!string.IsNullOrEmpty(error))
                        Debug.LogWarning($"[EnemyData] 에셋 리네임 실패 ({path} -> {desiredFileName}): {error}");
                }

                return existing;
            }

            UnityEngine.Object existingAtPath = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(desiredPath);
            if (existingAtPath != null)
            {
                Debug.LogError($"[EnemyData] Asset already exists but type is not {typeof(T).Name}: {desiredPath}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, desiredPath);
            return asset;
        }

        private static TEnum ParseEnumOrDefault<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return TryParseEnum(value, out TEnum parsed) ? parsed : fallback;
        }

        private static bool TryParseEnum<TEnum>(string value, out TEnum parsed) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                parsed = default;
                return false;
            }

            return Enum.TryParse(value.Trim(), true, out parsed);
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

        private static string CombineAssetPath(string left, string right)
        {
            return (left.TrimEnd('/') + "/" + right.TrimStart('/')).Replace("\\", "/");
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Unnamed" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return result;
        }
    }
}

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
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/EnemyData/2. JSONFile";
        private const string DefaultOutputRoot = "Assets/Resources/DataFiles/EnemyData/3. GeneratedAssets/Enemies";
        private const string EnemyDatabasePath = "Assets/Resources/DataFiles/EnemyData/3. GeneratedAssets/AllEnemies.asset";
        private const string EnemyLabelDatabasePath = "Assets/WJ_TestPlace/Data/Enemy/EnemyLabelDatabase.asset";

        [MenuItem("DataLoader/Enemy Data/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select enemy data JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            GenerateAllFromJson(jsonPath, DefaultOutputRoot);
        }

        [MenuItem("DataLoader/Enemy Data/0. Run All Steps")]
        public static void RunAllSteps()
        {
            Debug.Log("[EnemyData] ===== 통합 실행 시작 =====");

            // 이름(enemyName)을 EnemyDataLabel에서 조회해오므로, 적 스탯보다 라벨을 먼저 최신화한다.
            string labelJsonPath = EnemyLabelExcelToJson.ConvertWithDefaultPaths();
            if (!string.IsNullOrEmpty(labelJsonPath))
                EnemyLabelSOImporter.ImportWithDefaultPaths(labelJsonPath);
            else
                Debug.LogWarning("[EnemyData] 적 이름(Label) 갱신을 건너뛰었습니다 - 기존 EnemyLabelDatabase를 그대로 씁니다.");

            string jsonPath = EnemyDataExcelToJson.ConvertWithDefaultPaths();
            if (string.IsNullOrEmpty(jsonPath))
            {
                Debug.LogError("[EnemyData] 엑셀을 찾지 못해 중단했습니다.");
                return;
            }

            GenerateAllFromJson(jsonPath, DefaultOutputRoot);

            FloorStatScaleExcelToJson.ConvertWithDefaultPaths();
            DifficultyStatScaleExcelToJson.ConvertWithDefaultPaths();
            PlayerCountStatScaleExcelToJson.ConvertWithDefaultPaths();

            Debug.Log("[EnemyData] ===== 통합 실행 완료 =====");
        }

        public static void GenerateAllFromJson(string jsonPath, string outputRoot)
        {
            List<EnemyDataRow> rows = LoadJson(jsonPath);
            if (rows == null)
                return;

            EnsureAssetFolder(outputRoot);

            EnemyDatabaseSO database = GetOrCreateDatabase();
            EnemyLabelDatabaseSO labelDatabase = LoadLabelDatabase();

            int count = 0;
            int registeredCount = 0;

            foreach (EnemyDataRow row in rows)
            {
                EnemyDefinitionSO asset = CreateOrUpdate(outputRoot, row, labelDatabase);
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

        private static EnemyDefinitionSO CreateOrUpdate(string outputFolder, EnemyDataRow row, EnemyLabelDatabaseSO labelDatabase)
        {
            string idText = (row.enemyId ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(idText) || idText.Contains(" "))
                Debug.LogWarning($"[EnemyData] enemyId가 비어있거나 공백을 포함합니다: '{idText}'. 'enemy.grade.attackType.name' 형식을 확인해주세요.");

            // 표시 이름은 EnemyData 시트가 아니라 EnemyDataLabel.xlsx(EnemyLabelDatabaseSO)에서 가져온다.
            // Import 시점의 KOR 이름을 스냅샷으로 SO에 박아두는 것 - 언어별 실시간 표시가 필요한 곳은
            // 이 필드가 아니라 EnemyLabelDatabaseSO.GetName(enemyId)를 직접 불러야 한다.
            string displayName = labelDatabase != null ? labelDatabase.GetName(idText, GameLanguage.KOR) : idText;

            EnemyDefinitionSO asset = GetOrCreateAsset<EnemyDefinitionSO>(outputFolder, idText, displayName);
            if (asset == null)
                return null;

            asset.enemyId = idText;
            asset.enemyName = displayName;
            asset.enemyGrade = ParseEnumOrDefault(row.enemyGrade, EnemyGrade.Normal);
            asset.attackType = ParseEnumOrDefault(row.attackType, EnemyAttackType.Melee);
            asset.baseHealth = row.baseHp;
            asset.baseAttackPower = row.baseATK;
            asset.baseDefensePower = row.baseDEF;
            asset.baseMoveSpeed = row.baseMS;
            asset.baseAttackSpeed = row.baseAS;
            asset.attackRange = row.attackRange;
            asset.attackCooldown = row.attackCDR;
            asset.penetration = row.pen;
            asset.projectileSpeed = row.projectileSpeed;
            asset.patternId = row.patternId;
            asset.expReward = row.expReward;
            asset.creditReward = row.creditReward;

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

        /// <summary>없으면 null만 반환하고 경고 로그를 남긴다 - 라벨 없이도 enemyId를 이름 대신 써서 파이프라인 자체는 계속 진행되게 한다.</summary>
        private static EnemyLabelDatabaseSO LoadLabelDatabase()
        {
            EnemyLabelDatabaseSO labelDatabase = AssetDatabase.LoadAssetAtPath<EnemyLabelDatabaseSO>(EnemyLabelDatabasePath);
            if (labelDatabase == null)
                Debug.LogWarning($"[EnemyData] EnemyLabelDatabaseSO를 찾을 수 없습니다: {EnemyLabelDatabasePath}. 표시 이름 대신 enemyId를 그대로 씁니다.");

            return labelDatabase;
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

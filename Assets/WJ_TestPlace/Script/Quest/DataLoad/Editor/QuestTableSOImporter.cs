using System;
using System.Collections.Generic;
using System.IO;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// QuestTableExcelToJson 결과물(JSON)을 읽어서 QuestDefinitionSO 에셋을 생성/갱신하고,
    /// QuestDatabaseSO.allQuests에 등록한다.
    ///
    /// !! 다른 테이블(BuffTable 등)은 "파일명 = id"로 기존 에셋을 찾지만, 퀘스트는 초기 샘플 5개
    ///    (Quest_Sample_*.asset)가 questId와 다른 파일명으로 이미 만들어져 있었다. 그래서 파일 경로 예측
    ///    대신 프로젝트 전체에서 QuestDefinitionSO를 찾아 questId로 매칭한다 - 기존 에셋은 파일명/GUID를
    ///    그대로 유지한 채 값만 갱신되고, 새 questId만 새 파일로 만들어진다.
    /// !! 시트에서 행을 지워도 기존 에셋은 삭제하지 않는다(다른 파이프라인과 동일한 방침).
    ///    QuestDatabaseSO에서도 자동으로 빼지 않는다 - 참조가 남아 있을 수 있어 사람이 직접 판단하는 편이 안전하다.
    /// </summary>
    public static class QuestTableSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/QuestData/2. JSONFile";
        private const string NewQuestFolder = "Assets/Resources/DataFiles/QuestData/3. GeneratedAssets/Quests";
        private const string QuestDatabasePath = "Assets/WJ_TestPlace/Data/Quest/QuestDatabase.asset";

        [MenuItem("DataLoader/Quest Table/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select quest table JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            GenerateAllFromJson(jsonPath);
        }

        [MenuItem("DataLoader/Quest Table/0. Run All Steps")]
        public static void RunAllSteps()
        {
            Debug.Log("[QuestTable] ===== 통합 실행 시작 =====");

            string jsonPath = QuestTableExcelToJson.ConvertWithDefaultPaths();
            if (string.IsNullOrEmpty(jsonPath))
            {
                Debug.LogError("[QuestTable] 엑셀을 찾지 못해 중단했습니다.");
                return;
            }

            GenerateAllFromJson(jsonPath);
            Debug.Log("[QuestTable] ===== 통합 실행 완료 =====");
        }

        public static void GenerateAllFromJson(string jsonPath)
        {
            List<QuestTableRow> rows = LoadJson(jsonPath);
            if (rows == null)
                return;

            QuestDatabaseSO database = AssetDatabase.LoadAssetAtPath<QuestDatabaseSO>(QuestDatabasePath);
            if (database == null)
                Debug.LogWarning($"[QuestTable] QuestDatabaseSO를 찾을 수 없습니다: {QuestDatabasePath}. allQuests 자동 등록을 건너뜁니다.");

            Dictionary<string, ItemDefinitionSO> itemLookup = BuildItemLookup();
            Dictionary<string, QuestDefinitionSO> existingQuests = BuildExistingQuestLookup();
            List<QuestDefinitionSO> databaseList = database != null
                ? new List<QuestDefinitionSO>(database.allQuests)
                : null;

            EnsureAssetFolder(NewQuestFolder);

            int created = 0, updated = 0, skipped = 0, registered = 0;

            foreach (QuestTableRow row in rows)
            {
                string id = (row.questId ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id))
                {
                    skipped++;
                    continue;
                }

                QuestDefinitionSO asset;
                if (existingQuests.TryGetValue(id, out asset))
                {
                    updated++;
                }
                else
                {
                    string assetPath = CombineAssetPath(NewQuestFolder, SanitizeFileName(id) + ".asset");
                    UnityEngine.Object other = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (other != null)
                    {
                        Debug.LogError($"[QuestTable] '{id}' 경로에 QuestDefinitionSO가 아닌 에셋이 이미 있습니다: {assetPath}");
                        skipped++;
                        continue;
                    }

                    asset = ScriptableObject.CreateInstance<QuestDefinitionSO>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                    existingQuests[id] = asset;
                    created++;
                }

                ApplyRow(asset, row, itemLookup);
                EditorUtility.SetDirty(asset);

                if (databaseList != null && !databaseList.Contains(asset))
                {
                    databaseList.Add(asset);
                    registered++;
                }
            }

            if (database != null && databaseList != null)
            {
                database.allQuests = databaseList.ToArray();
                EditorUtility.SetDirty(database);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[QuestTable] SO 생성/갱신 완료. 신규 {created}개, 갱신 {updated}개, 건너뜀 {skipped}개, " +
                      $"QuestDatabase에 새로 등록 {registered}개");
        }

        private static void ApplyRow(QuestDefinitionSO asset, QuestTableRow row, Dictionary<string, ItemDefinitionSO> itemLookup)
        {
            asset.questId = row.questId;
            asset.questName = row.questName;
            asset.description = row.description;
            asset.conditions = ParseConditions(row.conditions, row.questId);
            asset.rewardGold = row.rewardGold;
            asset.rewardItemCount = Mathf.Max(1, row.rewardItemCount);

            string rewardItemId = (row.rewardItemId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(rewardItemId))
            {
                asset.rewardItem = null;
            }
            else if (itemLookup.TryGetValue(rewardItemId, out ItemDefinitionSO rewardItem))
            {
                asset.rewardItem = rewardItem;
            }
            else
            {
                Debug.LogWarning($"[QuestTable] '{row.questId}'의 rewardItemId '{rewardItemId}'에 해당하는 ItemDefinitionSO를 찾지 못했습니다. 아이템 보상 없이 둡니다.");
                asset.rewardItem = null;
            }
        }

        /// <summary>
        /// "조건타입:대상ID:목표수치:설명"을 ';'로 여러 개 이어붙인 문자열을 QuestConditionDefinition[]로 바꾼다.
        /// 설명에 ':'가 들어있어도 깨지지 않게 최대 4조각까지만 나눈다.
        /// </summary>
        private static QuestConditionDefinition[] ParseConditions(string raw, string questId)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<QuestConditionDefinition>();

            var result = new List<QuestConditionDefinition>();

            foreach (string entry in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = entry.Split(new[] { ':' }, 4);
                if (parts.Length < 3)
                {
                    Debug.LogWarning($"[QuestTable] '{questId}'의 conditions 항목 형식이 잘못됐습니다: '{entry.Trim()}' " +
                                     "(\"조건타입:대상ID:목표수치:설명\" 형태여야 함). 해당 항목은 건너뜁니다.");
                    continue;
                }

                if (!Enum.TryParse(parts[0].Trim(), true, out QuestConditionType conditionType))
                {
                    Debug.LogWarning($"[QuestTable] '{questId}'의 conditions에 알 수 없는 조건타입이 있습니다: '{parts[0].Trim()}'. 해당 항목은 건너뜁니다.");
                    continue;
                }

                if (!int.TryParse(parts[2].Trim(), out int requiredCount) || requiredCount < 1)
                {
                    Debug.LogWarning($"[QuestTable] '{questId}'의 conditions 목표수치가 올바르지 않습니다: '{parts[2].Trim()}'. 해당 항목은 건너뜁니다.");
                    continue;
                }

                result.Add(new QuestConditionDefinition
                {
                    conditionType = conditionType,
                    targetId = parts[1].Trim(),
                    requiredCount = requiredCount,
                    description = parts.Length > 3 ? parts[3].Trim() : string.Empty
                });
            }

            return result.ToArray();
        }

        /// <summary>itemId -> ItemDefinitionSO. 보상 아이템 참조를 문자열 ID로 적을 수 있게 해준다.</summary>
        private static Dictionary<string, ItemDefinitionSO> BuildItemLookup()
        {
            var lookup = new Dictionary<string, ItemDefinitionSO>();

            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinitionSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
                if (item != null && !string.IsNullOrEmpty(item.itemId))
                    lookup[item.itemId] = item;
            }

            return lookup;
        }

        /// <summary>questId -> 이미 존재하는 QuestDefinitionSO. 파일 위치/이름과 무관하게 questId로 찾는다.</summary>
        private static Dictionary<string, QuestDefinitionSO> BuildExistingQuestLookup()
        {
            var lookup = new Dictionary<string, QuestDefinitionSO>();

            foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinitionSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var quest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(path);
                if (quest != null && !string.IsNullOrEmpty(quest.questId))
                    lookup[quest.questId] = quest;
            }

            return lookup;
        }

        private static List<QuestTableRow> LoadJson(string jsonPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[QuestTable] JSON file not found: {absoluteJsonPath}");
                return null;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            List<QuestTableRow> rows = JsonConvert.DeserializeObject<List<QuestTableRow>>(json);
            if (rows == null)
                Debug.LogError("[QuestTable] JSON parse failed.");

            return rows;
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

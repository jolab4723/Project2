using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// item_drop_table.json(ItemDropTableExcelToJson 결과물)을 읽어서 ItemDropTableSO를 갱신한다.
    /// ItemDropTableSO(SW 제작)의 enemyDropRules/itemKindWeights는 private [SerializeField]라
    /// 그 파일은 그대로 두고 SerializedObject/SerializedProperty로 직접 써넣는다.
    /// </summary>
    public static class ItemDropTableSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/ItemData/2. JSONFile";
        private const string DefaultOutputAssetPath = "Assets/WJ_TestPlace/Script/Item/Data/ItemData/ItemDropTable.asset";

        [MenuItem("DataLoader/Item Drop Table/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select item drop table JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Import(jsonPath, DefaultOutputAssetPath);
        }

        public static void Import(string jsonPath, string outputAssetPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[ItemDropTable] JSON file not found: {absoluteJsonPath}");
                return;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            ItemDropTableJsonData data = JsonConvert.DeserializeObject<ItemDropTableJsonData>(json);
            if (data == null)
            {
                Debug.LogError("[ItemDropTable] JSON parse failed.");
                return;
            }

            ItemDropTableSO so = GetOrCreateAsset(outputAssetPath);
            if (so == null)
                return;

            SerializedObject serialized = new SerializedObject(so);
            int ruleCount = WriteEnemyDropRules(serialized.FindProperty("enemyDropRules"), data);
            int kindCount = WriteItemKindWeights(serialized.FindProperty("itemKindWeights"), data.itemKindWeights);
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ItemDropTable] SO 갱신 완료: {outputAssetPath}\n" +
                      $"EnemyDropRules: {ruleCount}, ItemKindWeights: {kindCount}");
        }

        private static int WriteEnemyDropRules(SerializedProperty rulesProp, ItemDropTableJsonData data)
        {
            rulesProp.ClearArray();

            int index = 0;
            foreach (EnemyDropRuleRow ruleRow in data.enemyDropRules)
            {
                if (!TryParseEnum(ruleRow.enemyGrade, out EnemyGrade grade))
                {
                    Debug.LogWarning($"[ItemDropTable] EnemyGrade 파싱 실패, 행 건너뜀: {ruleRow.enemyGrade}");
                    continue;
                }

                rulesProp.InsertArrayElementAtIndex(index);
                SerializedProperty ruleElement = rulesProp.GetArrayElementAtIndex(index);
                ruleElement.FindPropertyRelative("enemyGrade").enumValueIndex = (int)grade;
                ruleElement.FindPropertyRelative("itemDropChance").floatValue = ruleRow.itemDropChance;

                SerializedProperty weightsProp = ruleElement.FindPropertyRelative("rarityWeights");
                weightsProp.ClearArray();

                List<RarityWeightRow> matchingWeights = data.rarityWeights
                    .Where(w => string.Equals(w.enemyGrade, ruleRow.enemyGrade, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                int weightIndex = 0;
                foreach (RarityWeightRow weightRow in matchingWeights)
                {
                    if (!TryParseEnum(weightRow.rarity, out ItemRarity rarity))
                    {
                        Debug.LogWarning($"[ItemDropTable] ItemRarity 파싱 실패, 행 건너뜀: {weightRow.rarity} ({weightRow.enemyGrade})");
                        continue;
                    }

                    weightsProp.InsertArrayElementAtIndex(weightIndex);
                    SerializedProperty weightElement = weightsProp.GetArrayElementAtIndex(weightIndex);
                    weightElement.FindPropertyRelative("rarity").enumValueIndex = (int)rarity;
                    weightElement.FindPropertyRelative("weight").floatValue = weightRow.weight;
                    weightIndex++;
                }

                index++;
            }

            return index;
        }

        private static int WriteItemKindWeights(SerializedProperty kindsProp, List<ItemKindWeightRow> rows)
        {
            kindsProp.ClearArray();

            int index = 0;
            foreach (ItemKindWeightRow row in rows)
            {
                if (!TryParseEnum(row.itemKind, out ItemDropTypes.ItemDropKind kind))
                {
                    Debug.LogWarning($"[ItemDropTable] ItemDropKind 파싱 실패, 행 건너뜀: {row.itemKind}");
                    continue;
                }

                kindsProp.InsertArrayElementAtIndex(index);
                SerializedProperty element = kindsProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("itemKind").enumValueIndex = (int)kind;
                element.FindPropertyRelative("weight").floatValue = row.weight;
                index++;
            }

            return index;
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

        private static ItemDropTableSO GetOrCreateAsset(string path)
        {
            ItemDropTableSO asset = AssetDatabase.LoadAssetAtPath<ItemDropTableSO>(path);
            if (asset != null)
                return asset;

            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing != null)
            {
                Debug.LogError($"[ItemDropTable] Asset already exists but type is not ItemDropTableSO: {path}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<ItemDropTableSO>();
            string directory = Path.GetDirectoryName(path).Replace("\\", "/");
            EnsureAssetFolder(directory);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
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

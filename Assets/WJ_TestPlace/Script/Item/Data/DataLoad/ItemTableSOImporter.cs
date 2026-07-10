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
    /// item_table_structured.json(ItemTableExcelToJson 결과물)을 읽어서 서브스탯 풀 2종 +
    /// 원소 보너스 설정 1종을 ScriptableObject로 생성/갱신한다.
    /// 아이템(ItemDefinitionSO) 생성 로직은 서브스탯 변환 케이스로 범위를 좁히면서 제거함.
    /// </summary>
    public static class ItemTableSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/ItemData/JSONFile";
        private const string DefaultOutputRoot = "Assets/Resources/DataFiles/ItemData/GeneratedAssets";

        private const string CombatPoolAssetName = "CombatStatPool";
        private const string UtilityPoolAssetName = "UtilityStatPool";
        private const string ElementBonusConfigAssetName = "ElementBonusConfig";

        [MenuItem("DataLoader/Item Table/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select item table JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            Import(jsonPath, DefaultOutputRoot);
        }

        public static void Import(string jsonPath, string outputRoot)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[ItemTable] JSON file not found: {absoluteJsonPath}");
                return;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            ItemTableJsonData data = JsonConvert.DeserializeObject<ItemTableJsonData>(json);
            if (data == null)
            {
                Debug.LogError("[ItemTable] JSON parse failed.");
                return;
            }

            EnsureAssetFolder(outputRoot);

            SubStatPoolSO combatPool = CreateSubStatPool(data, outputRoot, "Combat", CombatPoolAssetName);
            SubStatPoolSO utilityPool = CreateSubStatPool(data, outputRoot, "Utility", UtilityPoolAssetName);
            ElementalBonusConfigSO elementConfig = CreateElementalBonusConfig(data, outputRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ItemTable] SO generation complete. "
                + $"Combat options: {(combatPool != null ? combatPool.options.Count : 0)}, "
                + $"Utility options: {(utilityPool != null ? utilityPool.options.Count : 0)}, "
                + $"ElementBonusConfig: {(elementConfig != null ? "생성됨" : "없음")}\nOutput: {outputRoot}");
        }

        /// <summary>statPoolType(Combat/Utility)이 일치하는 행들을 모아 하나의 SubStatPoolSO로 만든다.</summary>
        private static SubStatPoolSO CreateSubStatPool(ItemTableJsonData data, string outputFolder, string poolType, string assetName)
        {
            List<SubStatPoolRow> rows = data.subStatPools
                .Where(row => string.Equals(row.statPoolType, poolType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rows.Count == 0)
            {
                Debug.LogWarning($"[ItemTable] {poolType} 타입 서브스탯 행이 없습니다. {assetName} 생성을 건너뜁니다.");
                return null;
            }

            SubStatPoolSO pool = GetOrCreateAsset<SubStatPoolSO>(outputFolder, assetName);
            if (pool == null)
                return null;

            pool.options.Clear();

            foreach (SubStatPoolRow row in rows)
            {
                if (!TryParseEnum(row.statType, out StatType statType))
                {
                    Debug.LogWarning($"[ItemTable] 서브스탯 옵션 건너뜀 (StatType 파싱 실패): {row.statType} / {row.optionName}");
                    continue;
                }

                pool.options.Add(new RandomStatOption
                {
                    statType = statType,
                    minValue = row.minValue,
                    maxValue = row.maxValue
                });
            }

            EditorUtility.SetDirty(pool);
            return pool;
        }

        private static ElementalBonusConfigSO CreateElementalBonusConfig(ItemTableJsonData data, string outputFolder)
        {
            if (data.elementalBonusConfig == null)
            {
                Debug.LogWarning("[ItemTable] elementalBonusConfig 데이터가 없습니다.");
                return null;
            }

            ElementalBonusConfigSO asset = GetOrCreateAsset<ElementalBonusConfigSO>(outputFolder, ElementBonusConfigAssetName);
            if (asset == null)
                return null;

            asset.elementBonusValue = data.elementalBonusConfig.elementBonusValue;
            asset.atkFallbackValue = data.elementalBonusConfig.atkFallbackValue;
            asset.missChance = Mathf.Clamp01(data.elementalBonusConfig.missChance);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>에셋 경로를 고정된 이름(assetName)으로 결정한다. 같은 이름이면 항상 같은 에셋을 갱신한다.</summary>
        private static T GetOrCreateAsset<T>(string folder, string assetName) where T : ScriptableObject
        {
            string fileName = SanitizeFileName(assetName);
            string path = CombineAssetPath(folder, fileName + ".asset");
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing != null)
            {
                Debug.LogError($"[ItemTable] Asset already exists but type is not {typeof(T).Name}: {path}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
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

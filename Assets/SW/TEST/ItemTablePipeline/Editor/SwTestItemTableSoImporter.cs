using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace SW.Test.ItemTablePipeline
{
    public static class SwTestItemTableSoImporter
    {
        private const string DefaultJsonFolder = "Assets/SW/TEST/ItemTablePipeline/GeneratedJson";
        private const string DefaultOutputRoot = "Assets/SW/TEST/ItemTablePipeline/GeneratedAssets";

        [MenuItem("SW/TEST/Item Table/2. Generate SO From JSON")]
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
                Debug.LogError($"[SW TEST ItemTable] JSON file not found: {absoluteJsonPath}");
                return;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            SwTestItemTableJsonData data = JsonConvert.DeserializeObject<SwTestItemTableJsonData>(json);
            if (data == null)
            {
                Debug.LogError("[SW TEST ItemTable] JSON parse failed.");
                return;
            }

            EnsureAssetFolder(outputRoot);
            string itemFolder = CombineAssetPath(outputRoot, "Items");
            string poolFolder = CombineAssetPath(outputRoot, "SubStatPools");
            string elementalFolder = CombineAssetPath(outputRoot, "ElementalBonusConfigs");
            EnsureAssetFolder(itemFolder);
            EnsureAssetFolder(poolFolder);
            EnsureAssetFolder(elementalFolder);

            Dictionary<string, ElementalBonusConfigSO> elementalConfigs = CreateElementalConfigs(data, elementalFolder);
            Dictionary<string, SubStatPoolSO> pools = CreateSubStatPools(data, poolFolder);
            int itemCount = CreateItemDefinitions(data, itemFolder, elementalConfigs, pools);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SW TEST ItemTable] SO generation complete. Items: {itemCount}, Pools: {pools.Count}, ElementalConfigs: {elementalConfigs.Count}\nOutput: {outputRoot}");
        }

        private static Dictionary<string, ElementalBonusConfigSO> CreateElementalConfigs(SwTestItemTableJsonData data, string outputFolder)
        {
            Dictionary<string, ElementalBonusConfigSO> result = new Dictionary<string, ElementalBonusConfigSO>();

            foreach (SwTestElementalBonusConfigRow row in data.elementalBonusConfigs)
            {
                if (string.IsNullOrWhiteSpace(row.elementalConfigId))
                    continue;

                ElementalBonusConfigSO asset = GetOrCreateAsset<ElementalBonusConfigSO>(outputFolder, row.elementalConfigId, row.elementalConfigId);
                if (asset == null)
                    continue;

                asset.elementBonusValue = row.elementBonusValue;
                asset.atkFallbackValue = row.atkFallbackValue;
                asset.missChance = Mathf.Clamp01(row.missChance);
                EditorUtility.SetDirty(asset);
                result[row.elementalConfigId] = asset;
            }

            return result;
        }

        private static Dictionary<string, SubStatPoolSO> CreateSubStatPools(SwTestItemTableJsonData data, string outputFolder)
        {
            Dictionary<string, SwTestOptionDefinitionRow> optionsById = data.optionDefinitions
                .Where(row => !string.IsNullOrWhiteSpace(row.optionId))
                .GroupBy(row => row.optionId)
                .ToDictionary(group => group.Key, group => group.First());

            Dictionary<string, List<SwTestSubStatPoolRow>> poolRowsById = data.subStatPools
                .Where(row => row.enabled && !string.IsNullOrWhiteSpace(row.poolId))
                .GroupBy(row => row.poolId)
                .ToDictionary(group => group.Key, group => group.ToList());

            Dictionary<string, SubStatPoolSO> result = new Dictionary<string, SubStatPoolSO>();

            foreach (KeyValuePair<string, List<SwTestSubStatPoolRow>> pair in poolRowsById)
            {
                SubStatPoolSO pool = GetOrCreateAsset<SubStatPoolSO>(outputFolder, pair.Key, pair.Key);
                if (pool == null)
                    continue;

                pool.options.Clear();

                foreach (SwTestSubStatPoolRow poolRow in pair.Value)
                {
                    if (!optionsById.TryGetValue(poolRow.optionId, out SwTestOptionDefinitionRow optionRow))
                    {
                        Debug.LogWarning($"[SW TEST ItemTable] Missing optionId in SubStatPools: {poolRow.optionId}");
                        continue;
                    }

                    if (!TryParseEnum(optionRow.statType, out StatType statType))
                    {
                        Debug.LogWarning($"[SW TEST ItemTable] Option skipped. Invalid or empty StatType: {optionRow.optionId} / {optionRow.optionName}");
                        continue;
                    }

                    float min = optionRow.minValue;
                    float max = optionRow.maxValue;
                    if (Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 0f))
                        TryReadRangeFromText(optionRow.optionName, out min, out max);

                    pool.options.Add(new RandomStatOption
                    {
                        statType = statType,
                        minValue = min,
                        maxValue = max
                    });
                }

                EditorUtility.SetDirty(pool);
                result[pair.Key] = pool;
            }

            return result;
        }

        private static int CreateItemDefinitions(
            SwTestItemTableJsonData data,
            string outputFolder,
            Dictionary<string, ElementalBonusConfigSO> elementalConfigs,
            Dictionary<string, SubStatPoolSO> pools)
        {
            Dictionary<string, SwTestWeaponDefinitionRow> weaponsById = data.weaponDefinitions
                .Where(row => !string.IsNullOrWhiteSpace(row.itemId))
                .GroupBy(row => row.itemId)
                .ToDictionary(group => group.Key, group => group.First());

            Dictionary<string, SwTestArmorDefinitionRow> armorsById = data.armorDefinitions
                .Where(row => !string.IsNullOrWhiteSpace(row.itemId))
                .GroupBy(row => row.itemId)
                .ToDictionary(group => group.Key, group => group.First());

            int count = 0;

            foreach (SwTestItemDefinitionRow row in data.itemDefinitions)
            {
                if (!row.enabled || string.IsNullOrWhiteSpace(row.itemId))
                    continue;

                ItemDefinitionSO asset = GetOrCreateAsset<ItemDefinitionSO>(outputFolder, row.itemId, row.itemName);
                if (asset == null)
                    continue;

                asset.itemId = row.itemId;
                asset.itemName = row.itemName;
                asset.category = ParseEnumOrDefault(row.category, ItemCategory.Weapon);
                asset.rarity = ParseEnumOrDefault(row.rarity, ItemRarity.Common);
                asset.sellPrice = row.sellPrice;
                asset.itemWidth = Mathf.Max(1, row.itemWidth);
                asset.itemHeight = Mathf.Max(1, row.itemHeight);
                asset.upgradeBonusPerLevel = row.upgradeBonusPerLevel;
                asset.weaponEnchantElement = ParseEnumOrDefault(row.weaponEnchantElement, ItemSystem.ElementType.None);
                asset.mainOptions = BuildMainOptions(row).ToArray();

                asset.icon = LoadSprite(row.iconKey);
                asset.elementalBonusConfig = GetValueOrNull(elementalConfigs, row.elementalConfigId);
                asset.combatPool = GetValueOrNull(pools, row.combatPoolId);
                asset.utilityPool = GetValueOrNull(pools, row.utilityPoolId);
                asset.uniqueEffect = FindAssetByNameOrPath<UniqueEffectSO>(row.uniqueEffectId);

                if (asset.category == ItemCategory.Weapon && weaponsById.TryGetValue(row.itemId, out SwTestWeaponDefinitionRow weaponRow))
                {
                    asset.characterClass = ParseEnumOrDefault(weaponRow.characterClass, CharacterClass.Fighter);
                    asset.weaponType = ParseEnumOrDefault(weaponRow.weaponType, WeaponType.Greatsword);
                    asset.weaponEnchantElement = ParseEnumOrDefault(weaponRow.elementType, asset.weaponEnchantElement);
                }

                if (asset.category == ItemCategory.Armor && armorsById.TryGetValue(row.itemId, out SwTestArmorDefinitionRow armorRow))
                {
                    asset.armorType = ParseEnumOrDefault(armorRow.armorType, ArmorType.None);
                }

                EditorUtility.SetDirty(asset);
                count++;
            }

            return count;
        }

        private static List<FixedStatValue> BuildMainOptions(SwTestItemDefinitionRow row)
        {
            List<FixedStatValue> result = new List<FixedStatValue>();
            AddMainOption(result, row.mainStat1Type, row.mainStat1Value);
            AddMainOption(result, row.mainStat2Type, row.mainStat2Value);
            return result;
        }

        private static void AddMainOption(List<FixedStatValue> list, string statTypeText, float value)
        {
            if (string.IsNullOrWhiteSpace(statTypeText))
                return;

            if (!TryParseEnum(statTypeText, out StatType statType))
            {
                Debug.LogWarning($"[SW TEST ItemTable] Invalid main stat type skipped: {statTypeText}");
                return;
            }

            list.Add(new FixedStatValue
            {
                statType = statType,
                value = value
            });
        }

        private static T GetOrCreateAsset<T>(string folder, string id, string displayName) where T : ScriptableObject
        {
            string fileName = SanitizeFileName(string.IsNullOrWhiteSpace(displayName) ? id : id + "_" + displayName);
            string path = CombineAssetPath(folder, fileName + ".asset");
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing != null)
            {
                Debug.LogError($"[SW TEST ItemTable] Asset already exists but type is not {typeof(T).Name}: {path}");
                return null;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T FindAssetByNameOrPath<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (key.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<T>(key);

            string[] guids = AssetDatabase.FindAssets(key + " t:" + typeof(T).Name);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    return asset;
            }

            return null;
        }

        private static Sprite LoadSprite(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            Sprite sprite = FindAssetByNameOrPath<Sprite>(key);
            if (sprite != null)
                return sprite;

            Texture2D texture = FindAssetByNameOrPath<Texture2D>(key);
            if (texture == null)
                return null;

            string path = AssetDatabase.GetAssetPath(texture);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static T GetValueOrNull<T>(Dictionary<string, T> dictionary, string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            return dictionary.TryGetValue(key, out T value) ? value : null;
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

        private static bool TryReadRangeFromText(string text, out float min, out float max)
        {
            min = 0f;
            max = 0f;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            Match match = Regex.Match(text, @"([-+]?\d+(?:\.\d+)?)\s*~\s*([-+]?\d+(?:\.\d+)?)");
            if (!match.Success)
                return false;

            bool minOk = float.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out min);
            bool maxOk = float.TryParse(match.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out max);
            return minOk && maxOk;
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

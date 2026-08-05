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
    /// item_data_table.json(ItemDataTableExcelToJson 결과물)을 읽어서 무기/방어구/포션/(추후 유물)
    /// ItemDefinitionSO를 카테고리별로 각각 생성/갱신한다.
    ///
    /// 파일명은 "{itemId}_{itemName}" 형태. itemName이 바뀌면 새 에셋을 만드는 대신
    /// 같은 폴더에서 itemId가 일치하는 기존 에셋을 찾아 AssetDatabase.RenameAsset으로 이름만 바꾼다
    /// (GUID가 유지되므로 기존 참조가 안 끊김).
    /// </summary>
    public static class ItemDataTableSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/ItemData/2. JSONFile";
        private const string DefaultOutputRoot = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items";

        private const string ItemDatabasePath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/DropTableConfig/AllItems.asset";

        private const string CombatPoolPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/SubStatPoolData/CombatStatPool.asset";
        private const string UtilityPoolPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/SubStatPoolData/UtilityStatPool.asset";
        private const string ElementalConfigPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/SubStatPoolData/ElementBonusConfig.asset";

        private const string IconFolder = "Assets/Resources/Images/Item";
        private const string UniqueEffectFolder = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/UniqueEffectPool";

        [MenuItem("DataLoader/Item Data Table/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select item data table JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            GenerateAllFromJson(jsonPath, DefaultOutputRoot);
        }

        /// <summary>4개 카테고리를 전부 순서대로 생성/갱신한다.</summary>
        public static void GenerateAllFromJson(string jsonPath, string outputRoot)
        {
            ItemDataTableJsonData data = LoadJson(jsonPath);
            if (data == null)
                return;

            EnsureAssetFolder(outputRoot);

            SubStatPoolSO combatPool = AssetDatabase.LoadAssetAtPath<SubStatPoolSO>(CombatPoolPath);
            SubStatPoolSO utilityPool = AssetDatabase.LoadAssetAtPath<SubStatPoolSO>(UtilityPoolPath);
            ElementalBonusConfigSO elementalConfig = AssetDatabase.LoadAssetAtPath<ElementalBonusConfigSO>(ElementalConfigPath);

            if (combatPool == null)
                Debug.LogWarning($"[ItemDataTable] CombatStatPool을 찾을 수 없습니다: {CombatPoolPath}");
            if (utilityPool == null)
                Debug.LogWarning($"[ItemDataTable] UtilityStatPool을 찾을 수 없습니다: {UtilityPoolPath}");
            if (elementalConfig == null)
                Debug.LogWarning($"[ItemDataTable] ElementBonusConfig를 찾을 수 없습니다: {ElementalConfigPath}");

            ItemDatabaseSO database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(ItemDatabasePath);
            if (database == null)
                Debug.LogWarning($"[ItemDataTable] ItemDatabaseSO를 찾을 수 없습니다: {ItemDatabasePath}. allItems 자동 등록을 건너뜁니다.");

            int registeredCount = 0;

            int armorCount = CreateOrUpdateArmorDefinitions(data, outputRoot, combatPool, utilityPool, elementalConfig, database, ref registeredCount);
            int weaponCount = CreateOrUpdateWeaponDefinitions(data, outputRoot, combatPool, utilityPool, elementalConfig, database, ref registeredCount);
            int potionCount = CreateOrUpdatePotionDefinitions(data, outputRoot, database, ref registeredCount);
            int relicCount = CreateOrUpdateRelicDefinitions(data, outputRoot, database, ref registeredCount);

            if (database != null && registeredCount > 0)
                EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ItemDataTable] 1단계 완료. Armor {armorCount}, Weapon {weaponCount}, Potion {potionCount}, Relic {relicCount} " +
                      $"(ItemDatabaseSO에 {registeredCount}개 새로 등록됨)");
        }

        /// <summary>방어구만 생성/갱신한다. 다른 카테고리와 독립적으로 호출 가능.</summary>
        public static int CreateOrUpdateArmorDefinitions(
            ItemDataTableJsonData data, string outputFolder,
            SubStatPoolSO combatPool, SubStatPoolSO utilityPool, ElementalBonusConfigSO elementalConfig,
            ItemDatabaseSO database, ref int registeredCount)
        {
            int count = 0;

            foreach (ArmorDefinitionRow row in data.armorDefinitions)
            {
                ItemDefinitionSO asset = CreateOrUpdateBase(outputFolder, row.itemId, row.itemName, ItemCategory.Armor,
                    row.rarity, row.description, row.itemPrice, row.itemWidth, row.itemHeight,
                    row.mainStat1Type, row.mainStat1Value, row.mainStat2Type, row.mainStat2Value,
                    row.uniqueEffectId, combatPool, utilityPool, elementalConfig);
                if (asset == null)
                    continue;

                asset.armorType = ParseEnumOrDefault(row.armorType, ArmorType.None);
                FinalizeAsset(asset, database, ref registeredCount);
                count++;
            }

            return count;
        }

        /// <summary>무기만 생성/갱신한다. 다른 카테고리와 독립적으로 호출 가능.</summary>
        public static int CreateOrUpdateWeaponDefinitions(
            ItemDataTableJsonData data, string outputFolder,
            SubStatPoolSO combatPool, SubStatPoolSO utilityPool, ElementalBonusConfigSO elementalConfig,
            ItemDatabaseSO database, ref int registeredCount)
        {
            int count = 0;

            foreach (WeaponDefinitionRow row in data.weaponDefinitions)
            {
                ItemDefinitionSO asset = CreateOrUpdateBase(outputFolder, row.itemId, row.itemName, ItemCategory.Weapon,
                    row.rarity, row.description, row.sellPrice, row.itemWidth, row.itemHeight,
                    row.mainStat1Type, row.mainStat1Value, row.mainStat2Type, row.mainStat2Value,
                    row.uniqueEffectId, combatPool, utilityPool, elementalConfig);
                if (asset == null)
                    continue;

                asset.characterClass = ParseEnumOrDefault(row.characterClass, CharacterClass.Fighter);
                asset.weaponType = ParseEnumOrDefault(row.weaponType, WeaponType.Greatsword);
                asset.weaponEnchantElement = ParseEnumOrDefault(row.EnchantedElement, ElementType.None);
                FinalizeAsset(asset, database, ref registeredCount);
                count++;
            }

            return count;
        }

        /// <summary>
        /// 포션만 생성/갱신한다. 다른 카테고리와 독립적으로 호출 가능.
        /// 포션은 메인/서브 옵션 없이 uniqueEffect만 가지므로 스탯 인자와 옵션 풀을 모두 비워서 넘긴다.
        /// </summary>
        public static int CreateOrUpdatePotionDefinitions(
            ItemDataTableJsonData data, string outputFolder,
            ItemDatabaseSO database, ref int registeredCount)
        {
            int count = 0;

            foreach (PotionDefinitionRow row in data.potionDefinitions)
            {
                ItemDefinitionSO asset = CreateOrUpdateBase(outputFolder, row.itemId, row.itemName, ItemCategory.Potion,
                    row.rarity, row.description, row.itemPrice, row.itemWidth, row.itemHeight,
                    null, 0f, null, 0f,
                    row.uniqueEffectId, null, null, null);
                if (asset == null)
                    continue;

                FinalizeAsset(asset, database, ref registeredCount);
                count++;
            }

            return count;
        }

        /// <summary>
        /// 유물만 생성/갱신한다. 다른 카테고리와 독립적으로 호출 가능.
        /// 유물은 메인/서브 옵션 없이 보유만으로 uniqueEffect가 상시 적용되므로 스탯 인자와 옵션 풀을
        /// 모두 비워서 넘긴다 (포션과 동일한 형태).
        /// </summary>
        public static int CreateOrUpdateRelicDefinitions(
            ItemDataTableJsonData data, string outputFolder,
            ItemDatabaseSO database, ref int registeredCount)
        {
            int count = 0;

            foreach (RelicDefinitionRow row in data.relicDefinitions)
            {
                ItemDefinitionSO asset = CreateOrUpdateBase(outputFolder, row.itemId, row.itemName, ItemCategory.Relic,
                    row.rarity, row.description, row.itemPrice, row.itemWidth, row.itemHeight,
                    null, 0f, null, 0f,
                    row.uniqueEffectId, null, null, null);
                if (asset == null)
                    continue;

                FinalizeAsset(asset, database, ref registeredCount);
                count++;
            }

            return count;
        }

        private static ItemDefinitionSO CreateOrUpdateBase(
            string outputFolder, string itemId, string itemName, ItemCategory category,
            string rarityText, string description, int price, int width, int height,
            string mainStat1Type, float mainStat1Value, string mainStat2Type, float mainStat2Value,
            string uniqueEffectId,
            SubStatPoolSO combatPool, SubStatPoolSO utilityPool, ElementalBonusConfigSO elementalConfig)
        {
            // itemId는 엑셀 셀에 이미 "item.weapon.greatsword.basic" 같은 네임스페이스 문자열로 들어있는 고유키.
            // 숫자로 변환하지 않고 그대로 쓴다 (구 8자리 Category+Class+Index 스킴에서 전환됨).
            string idText = (itemId ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(idText) || idText.Contains(" "))
                Debug.LogWarning($"[ItemDataTable] itemId가 비어있거나 공백을 포함합니다: '{idText}' ({itemName}). 'item.category.type.name' 형식을 확인해주세요.");

            ItemDefinitionSO asset = GetOrCreateAsset<ItemDefinitionSO>(outputFolder, idText, itemName);
            if (asset == null)
                return null;

            asset.itemId = idText;
            asset.itemName = itemName;
            asset.category = category;
            asset.rarity = ParseEnumOrDefault(rarityText, ItemRarity.Common);
            asset.description = description;
            asset.sellPrice = price;
            asset.itemWidth = Mathf.Max(1, width);
            asset.itemHeight = Mathf.Max(1, height);
            asset.uniqueEffectId = uniqueEffectId;

            List<FixedStatValue> mainOptions = new List<FixedStatValue>();
            AddMainOption(mainOptions, mainStat1Type, mainStat1Value);
            AddMainOption(mainOptions, mainStat2Type, mainStat2Value);
            asset.mainOptions = mainOptions.ToArray();

            asset.combatPool = combatPool;
            asset.utilityPool = utilityPool;
            asset.elementalBonusConfig = elementalConfig;

            return asset;
        }

        private static void FinalizeAsset(ItemDefinitionSO asset, ItemDatabaseSO database, ref int registeredCount)
        {
            EditorUtility.SetDirty(asset);

            if (database != null && !database.allItems.Contains(asset))
            {
                database.allItems.Add(asset);
                registeredCount++;
            }
        }

        private static void AddMainOption(List<FixedStatValue> list, string statTypeText, float value)
        {
            if (string.IsNullOrWhiteSpace(statTypeText))
                return;

            if (!TryParseEnum(statTypeText, out StatType statType))
            {
                Debug.LogWarning($"[ItemDataTable] Invalid main stat type skipped: {statTypeText}");
                return;
            }

            list.Add(new FixedStatValue { statType = statType, value = value });
        }

        /// <summary>
        /// itemId와 파일명(확장자 제외)이 정확히 일치하는 스프라이트를 아이콘으로 연결한다.
        /// (느슨한 이름 검색이 아니라 경로 정확 매칭이라 오검색 위험이 없음.)
        /// </summary>
        [MenuItem("DataLoader/Item Data Table/3. Insert Icons")]
        public static void InsertItemIcons()
        {
            ItemDatabaseSO database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(ItemDatabasePath);
            if (database == null)
            {
                Debug.LogWarning($"[ItemDataTable] ItemDatabaseSO를 찾을 수 없습니다: {ItemDatabasePath}");
                return;
            }

            int matched = 0;
            int missing = 0;
            int effectIconsCopied = 0;

            foreach (ItemDefinitionSO asset in database.allItems)
            {
                if (asset == null)
                    continue;

                Sprite icon = LoadIconSprite(asset.itemId);
                if (icon == null)
                {
                    missing++;
                    continue;
                }

                asset.icon = icon;
                EditorUtility.SetDirty(asset);
                matched++;

                // 고유 효과 아이콘은 그 효과가 붙은 아이템의 아이콘을 그대로 쓴다.
                // 한 효과가 여러 아이템에 붙어 있으면 마지막에 처리된 아이템 것이 남으므로 경고를 남긴다.
                if (asset.uniqueEffect != null)
                {
                    if (asset.uniqueEffect.icon != null && asset.uniqueEffect.icon != icon)
                    {
                        Debug.LogWarning($"[ItemDataTable] 고유 효과 '{asset.uniqueEffect.name}'가 아이콘이 서로 다른 여러 아이템에 붙어 있습니다. " +
                                         $"'{asset.itemId}'의 아이콘으로 덮어씁니다.");
                    }

                    asset.uniqueEffect.icon = icon;
                    EditorUtility.SetDirty(asset.uniqueEffect);
                    effectIconsCopied++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemDataTable] 아이콘 연결 완료. 성공 {matched}, 실패 {missing} " +
                      $"(고유 효과에 복사 {effectIconsCopied}건)");
        }

        /// <summary>png/jpg 순서로 시도. 파일은 있는데 Sprite로 안 읽히면(Texture Type 설정 문제) 별도 경고.</summary>
        private static Sprite LoadIconSprite(string itemId)
        {
            string[] extensions = { "png", "jpg", "jpeg" };

            foreach (string ext in extensions)
            {
                string iconPath = CombineAssetPath(IconFolder, itemId + "." + ext);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (sprite != null)
                    return sprite;

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                if (texture != null)
                {
                    Debug.LogWarning($"[ItemDataTable] {iconPath} 파일은 있는데 Sprite로 못 읽었습니다. Texture Type을 'Sprite (2D and UI)'로 바꿔주세요.");
                    return null;
                }
            }

            Debug.LogWarning($"[ItemDataTable] 아이콘을 못 찾았습니다: {IconFolder}/{itemId}.(png|jpg|jpeg)");
            return null;
        }

        /// <summary>
        /// 고유 효과 엑셀을 먼저 SO로 변환한 뒤, ItemDefinitionSO.uniqueEffectId 기준으로
        /// 같은 이름(확장자 제외)의 UniqueEffectSO를 찾아 연결한다.
        ///
        /// !! 변환을 먼저 하는 이유: 연결만 하면 엑셀에서 새로 추가·수정한 고유 효과가 아직 SO로
        ///    만들어지지 않아 "고유효과를 못 찾았습니다"로 실패한다. 변환을 앞에 두면
        ///    이 단계 하나만 돌려도 항상 엑셀 최신 상태가 반영된다.
        /// </summary>
        [MenuItem("DataLoader/Item Data Table/4. Insert Unique Effects")]
        public static void InsertUniqueEffects()
        {
            Debug.Log("[ItemDataTable] 고유효과 엑셀 -> SO 변환 먼저 수행합니다.");
            if (!UniqueEffectTableSOImporter.RunExcelToSoWithDefaultPaths())
            {
                Debug.LogWarning("[ItemDataTable] 고유효과 변환을 건너뛰었습니다. " +
                                 "이미 만들어져 있는 SO만으로 연결을 시도합니다.");
            }

            ItemDatabaseSO database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(ItemDatabasePath);
            if (database == null)
            {
                Debug.LogWarning($"[ItemDataTable] ItemDatabaseSO를 찾을 수 없습니다: {ItemDatabasePath}");
                return;
            }

            int matched = 0;
            int missing = 0;
            int skipped = 0;

            foreach (ItemDefinitionSO asset in database.allItems)
            {
                if (asset == null)
                    continue;

                if (string.IsNullOrWhiteSpace(asset.uniqueEffectId))
                {
                    skipped++;
                    continue;
                }

                string effectPath = CombineAssetPath(UniqueEffectFolder, asset.uniqueEffectId + ".asset");
                UniqueEffectSO effect = AssetDatabase.LoadAssetAtPath<UniqueEffectSO>(effectPath);

                if (effect == null)
                {
                    Debug.LogWarning($"[ItemDataTable] 고유효과를 못 찾았습니다: {effectPath} ({asset.itemName})");
                    missing++;
                    continue;
                }

                asset.uniqueEffect = effect;
                EditorUtility.SetDirty(asset);
                matched++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemDataTable] 고유효과 연결 완료. 성공 {matched}, 실패 {missing}, 대상 없음(스킵) {skipped}");
        }

        /// <summary>
        /// 1~4단계를 한 번에 실행한다. 엑셀·JSON 모두 사전 설정된 기본 경로를 우선 사용하며,
        /// 기본 엑셀이 없을 때만 파일 선택 대화상자로 넘어간다(개별 단계 메뉴와 동일한 방침).
        /// </summary>
        [MenuItem("DataLoader/Item Data Table/0. Run All Steps")]
        public static void RunAllSteps()
        {
            Debug.Log("[ItemDataTable] ===== 통합 실행 시작 =====");

            Debug.Log("[ItemDataTable] 1/4: Excel -> JSON 변환 중...");
            string jsonPath = ItemDataTableExcelToJson.ConvertPreferringDefaultPaths();
            if (string.IsNullOrEmpty(jsonPath))
            {
                Debug.LogError("[ItemDataTable] 엑셀을 정하지 못해 통합 실행을 중단했습니다.");
                return;
            }

            Debug.Log("[ItemDataTable] 2/4: JSON -> SO 생성/갱신 중...");
            GenerateAllFromJson(jsonPath, DefaultOutputRoot);

            Debug.Log("[ItemDataTable] 3/4: 아이콘 연결 중...");
            InsertItemIcons();

            // 이 단계가 내부에서 고유효과 엑셀 -> SO 변환을 먼저 수행한 뒤 연결한다.
            Debug.Log("[ItemDataTable] 4/4: 고유효과 변환 및 연결 중...");
            InsertUniqueEffects();

            Debug.Log("[ItemDataTable] ===== 통합 실행 완료 =====");
        }

        private static ItemDataTableJsonData LoadJson(string jsonPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[ItemDataTable] JSON file not found: {absoluteJsonPath}");
                return null;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            ItemDataTableJsonData data = JsonConvert.DeserializeObject<ItemDataTableJsonData>(json);
            if (data == null)
                Debug.LogError("[ItemDataTable] JSON parse failed.");

            return data;
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
                        Debug.LogWarning($"[ItemDataTable] 에셋 리네임 실패 ({path} -> {desiredFileName}): {error}");
                }

                return existing;
            }

            UnityEngine.Object existingAtPath = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(desiredPath);
            if (existingAtPath != null)
            {
                Debug.LogError($"[ItemDataTable] Asset already exists but type is not {typeof(T).Name}: {desiredPath}");
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

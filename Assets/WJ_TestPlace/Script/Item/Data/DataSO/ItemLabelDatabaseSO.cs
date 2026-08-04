using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 아이템 이름/설명 표시 라벨(한/영/일/중) 데이터베이스.
    /// ItemDataLabel.xlsx -> JSON(ItemLabelExcelToJson) -> 이 SO(ItemLabelSOImporter) 순서로 생성된다.
    /// itemId는 ItemDefinitionSO.itemId(예: "item.weapon.greatsword.basic")와 1:1로 맞춰져 있다.
    ///
    /// !! 표시 언어는 이 에셋이 들고 있지 않는다. 인자 없는 오버로드는 YJ_LanguageManager.CurrentLanguage를
    ///    따라가므로, 언어를 바꾸려면 그 매니저의 SetLanguage를 부르면 된다(런타임 전환 가능).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemLabelDatabase", menuName = "Item/Item Label Database")]
    public class ItemLabelDatabaseSO : ScriptableObject
    {
        [Serializable]
        public class ItemLabelEntry
        {
            public string itemId;
            public string name;
            public string description;
        }

        [SerializeField] private List<ItemLabelEntry> korLabels = new List<ItemLabelEntry>();
        [SerializeField] private List<ItemLabelEntry> engLabels = new List<ItemLabelEntry>();
        [SerializeField] private List<ItemLabelEntry> jpnLabels = new List<ItemLabelEntry>();
        [SerializeField] private List<ItemLabelEntry> chnLabels = new List<ItemLabelEntry>();

        private Dictionary<GameLanguage, Dictionary<string, ItemLabelEntry>> lookupCache;

        /// <summary>YJ_LanguageManager가 아직 없으면(테스트 씬 등) KOR로 취급한다.</summary>
        private static GameLanguage CurrentLanguage =>
            YJ_LanguageManager.Instance != null ? YJ_LanguageManager.Instance.CurrentLanguage : GameLanguage.KOR;

        public string GetName(string itemId)
        {
            return GetName(itemId, CurrentLanguage);
        }

        public string GetName(string itemId, GameLanguage language)
        {
            ItemLabelEntry entry = GetEntry(itemId, language);
            return entry != null && !string.IsNullOrEmpty(entry.name) ? entry.name : itemId;
        }

        public string GetDescription(string itemId)
        {
            return GetDescription(itemId, CurrentLanguage);
        }

        public string GetDescription(string itemId, GameLanguage language)
        {
            ItemLabelEntry entry = GetEntry(itemId, language);
            return entry != null ? entry.description : string.Empty;
        }

        /// <summary>DB에 itemId가 없으면 false를 반환한다. GetName과 달리 itemId를 대체값으로 쓰지 않아서,
        /// 호출부가 definition.itemName 같은 자기 자신의 기본값으로 정확히 폴백할 수 있다.</summary>
        public bool TryGetName(string itemId, out string name)
        {
            return TryGetName(itemId, CurrentLanguage, out name);
        }

        public bool TryGetName(string itemId, GameLanguage language, out string name)
        {
            ItemLabelEntry entry = GetEntry(itemId, language);
            name = entry != null ? entry.name : null;
            return entry != null && !string.IsNullOrEmpty(name);
        }

        /// <summary>해당 언어에 항목이 없으면 KOR로 폴백한다(번역이 아직 안 채워진 항목 대비).</summary>
        private ItemLabelEntry GetEntry(string itemId, GameLanguage language)
        {
            if (GetOrBuildLookup(language).TryGetValue(itemId, out ItemLabelEntry entry))
                return entry;

            if (language != GameLanguage.KOR
                && GetOrBuildLookup(GameLanguage.KOR).TryGetValue(itemId, out ItemLabelEntry korEntry))
            {
                return korEntry;
            }

            return null;
        }

        private Dictionary<string, ItemLabelEntry> GetOrBuildLookup(GameLanguage language)
        {
            lookupCache ??= new Dictionary<GameLanguage, Dictionary<string, ItemLabelEntry>>();

            if (lookupCache.TryGetValue(language, out var cached))
                return cached;

            var built = new Dictionary<string, ItemLabelEntry>();
            foreach (ItemLabelEntry entry in GetEntries(language))
            {
                if (entry != null && !string.IsNullOrEmpty(entry.itemId))
                    built[entry.itemId] = entry;
            }

            lookupCache[language] = built;
            return built;
        }

        private List<ItemLabelEntry> GetEntries(GameLanguage language)
        {
            return language switch
            {
                GameLanguage.ENG => engLabels,
                GameLanguage.JPN => jpnLabels,
                GameLanguage.CHN => chnLabels,
                _ => korLabels,
            };
        }

        private void OnValidate()
        {
            lookupCache = null;
        }
    }
}

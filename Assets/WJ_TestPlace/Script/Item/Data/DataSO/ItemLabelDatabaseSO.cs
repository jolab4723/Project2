using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 아이템 이름/설명 표시 라벨(한/영) 데이터베이스.
    /// ItemDataLabel.xlsx -> JSON(ItemLabelExcelToJson) -> 이 SO(ItemLabelSOImporter) 순서로 생성된다.
    /// itemId는 ItemDefinitionSO.itemId(예: "item.weapon.greatsword.basic")와 1:1로 맞춰져 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemLabelDatabase", menuName = "Item/Item Label Database")]
    public class ItemLabelDatabaseSO : ScriptableObject
    {
        public enum Language
        {
            KOR,
            ENG
        }

        [Serializable]
        public class ItemLabelEntry
        {
            public string itemId;
            public string name;
            public string description;
        }

        [Header("표시 언어")]
        [SerializeField] private Language currentLanguage = Language.KOR;

        [SerializeField] private List<ItemLabelEntry> korLabels = new List<ItemLabelEntry>();
        [SerializeField] private List<ItemLabelEntry> engLabels = new List<ItemLabelEntry>();

        private Dictionary<string, ItemLabelEntry> korLookup;
        private Dictionary<string, ItemLabelEntry> engLookup;

        public string GetName(string itemId)
        {
            return GetName(itemId, currentLanguage);
        }

        public string GetName(string itemId, Language language)
        {
            ItemLabelEntry entry = GetEntry(itemId, language);
            return entry != null ? entry.name : itemId;
        }

        public string GetDescription(string itemId)
        {
            return GetDescription(itemId, currentLanguage);
        }

        public string GetDescription(string itemId, Language language)
        {
            ItemLabelEntry entry = GetEntry(itemId, language);
            return entry != null ? entry.description : string.Empty;
        }

        /// <summary>DB에 itemId가 없으면 false를 반환한다. GetName과 달리 itemId를 대체값으로 쓰지 않아서,
        /// 호출부가 definition.itemName 같은 자기 자신의 기본값으로 정확히 폴백할 수 있다.</summary>
        public bool TryGetName(string itemId, out string name)
        {
            return TryGetName(itemId, currentLanguage, out name);
        }

        public bool TryGetName(string itemId, Language language, out string name)
        {
            ItemLabelEntry entry = GetEntry(itemId, language);
            name = entry != null ? entry.name : null;
            return entry != null;
        }

        private ItemLabelEntry GetEntry(string itemId, Language language)
        {
            Dictionary<string, ItemLabelEntry> lookup = language == Language.KOR ? GetOrBuildLookup(ref korLookup, korLabels) : GetOrBuildLookup(ref engLookup, engLabels);
            return lookup.TryGetValue(itemId, out ItemLabelEntry entry) ? entry : null;
        }

        private static Dictionary<string, ItemLabelEntry> GetOrBuildLookup(ref Dictionary<string, ItemLabelEntry> cache, List<ItemLabelEntry> entries)
        {
            if (cache != null)
                return cache;

            cache = new Dictionary<string, ItemLabelEntry>();
            foreach (ItemLabelEntry entry in entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.itemId))
                    cache[entry.itemId] = entry;
            }

            return cache;
        }

        private void OnValidate()
        {
            korLookup = null;
            engLookup = null;
        }
    }
}

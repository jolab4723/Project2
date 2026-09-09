using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 아이템 툴팁/강화 팝업 등에서 쓰는 등급·속성·분류·직업·무기·방어구·스탯 이름의 다국어 데이터베이스.
    /// QuestLabelDatabaseSO/UILabelDatabaseSO와 같은 범용 key→문구 구조를 그대로 쓴다.
    /// `ItemDisplayNames`(같은 폴더)의 각 Dictionary 프로퍼티가 내부적으로 이 DB를 조회해서
    /// 만들어주므로, TooltipUI.cs/UpgradeController.cs 등 기존 사용처는 전혀 바뀌지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDisplayNameDatabase", menuName = "Item/Item Display Name Database")]
    public class ItemDisplayNameDatabaseSO : ScriptableObject
    {
        [Serializable]
        public class ItemDisplayNameEntry
        {
            public string key;
            public string label;
        }

        [SerializeField] private List<ItemDisplayNameEntry> korLabels = new List<ItemDisplayNameEntry>();
        [SerializeField] private List<ItemDisplayNameEntry> engLabels = new List<ItemDisplayNameEntry>();
        [SerializeField] private List<ItemDisplayNameEntry> jpnLabels = new List<ItemDisplayNameEntry>();
        [SerializeField] private List<ItemDisplayNameEntry> chnLabels = new List<ItemDisplayNameEntry>();

        private Dictionary<GameLanguage, Dictionary<string, string>> lookupCache;

        /// <summary>현재 언어(YJ_LanguageManager 기준)의 문구를 반환한다. 없으면 한국어로, 한국어에도
        /// 없으면 key를 그대로 반환한다(ItemDisplayNames 쪽에서 이 반환값을 그대로 못 쓰는 경우는
        /// 호출부가 자체적으로 value.ToString() 등으로 다시 폴백한다).</summary>
        public string GetLabel(string key) => GetLabel(key, CurrentLanguage);

        public string GetLabel(string key, GameLanguage language)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            var lookup = GetOrBuildLookup(language);
            if (lookup.TryGetValue(key, out string label))
                return label;

            if (language != GameLanguage.KOR)
            {
                var korLookup = GetOrBuildLookup(GameLanguage.KOR);
                if (korLookup.TryGetValue(key, out string korLabel))
                    return korLabel;
            }

            return null;
        }

        private static GameLanguage CurrentLanguage =>
            YJ_LanguageManager.Instance != null ? YJ_LanguageManager.Instance.CurrentLanguage : GameLanguage.KOR;

        private Dictionary<string, string> GetOrBuildLookup(GameLanguage language)
        {
            lookupCache ??= new Dictionary<GameLanguage, Dictionary<string, string>>();

            if (lookupCache.TryGetValue(language, out var lookup))
                return lookup;

            lookup = new Dictionary<string, string>();
            foreach (var entry in GetEntries(language))
            {
                if (!string.IsNullOrEmpty(entry.key))
                    lookup[entry.key] = entry.label;
            }

            lookupCache[language] = lookup;
            return lookup;
        }

        private List<ItemDisplayNameEntry> GetEntries(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.ENG: return engLabels;
                case GameLanguage.JPN: return jpnLabels;
                case GameLanguage.CHN: return chnLabels;
                default: return korLabels;
            }
        }

        private void OnValidate() => lookupCache = null;
    }
}

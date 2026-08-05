using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 고유 효과 이름/설명 표시 라벨(한/영/일/중) 데이터베이스.
    /// UniqueEffectLabel.xlsx -> JSON(UniqueEffectLabelExcelToJson) -> 이 SO(UniqueEffectLabelSOImporter) 순서로 생성된다.
    /// uniqueEffectId는 UniqueEffectSO 에셋의 파일명(예: "UE_CyberneticCore")과 1:1로 맞춰져 있다
    /// (UniqueEffectTableSOImporter가 그 이름으로 에셋을 생성하므로 asset.name이 곧 uniqueEffectId다).
    ///
    /// !! 표시 언어는 이 에셋이 들고 있지 않는다. 인자 없는 오버로드는 YJ_LanguageManager.CurrentLanguage를
    ///    따라가므로, 언어를 바꾸려면 그 매니저의 SetLanguage를 부르면 된다(런타임 전환 가능).
    ///
    /// description은 ItemLabelDatabaseSO와 달리 완성 문구가 아니라 {0},{1}... 자리를 가진 "템플릿"이다.
    /// 실제 표시 문구는 GetDescription(id, coefficients)로 UniqueEffectSO.coefficients를 대입해서 얻는다
    /// (수치는 언어와 무관하므로 UniqueEffectSO가 그대로 들고 있는 값을 재사용한다).
    /// </summary>
    [CreateAssetMenu(fileName = "UniqueEffectLabelDatabase", menuName = "Item/Unique Effect Label Database")]
    public class UniqueEffectLabelDatabaseSO : ScriptableObject
    {
        [Serializable]
        public class UniqueEffectLabelEntry
        {
            public string uniqueEffectId;
            public string name;

            /// <summary>완성 문구가 아니라 {0},{1}... 자리를 가진 템플릿.</summary>
            public string description;
        }

        [SerializeField] private List<UniqueEffectLabelEntry> korLabels = new List<UniqueEffectLabelEntry>();
        [SerializeField] private List<UniqueEffectLabelEntry> engLabels = new List<UniqueEffectLabelEntry>();
        [SerializeField] private List<UniqueEffectLabelEntry> jpnLabels = new List<UniqueEffectLabelEntry>();
        [SerializeField] private List<UniqueEffectLabelEntry> chnLabels = new List<UniqueEffectLabelEntry>();

        private Dictionary<GameLanguage, Dictionary<string, UniqueEffectLabelEntry>> lookupCache;

        /// <summary>YJ_LanguageManager가 아직 없으면(테스트 씬 등) KOR로 취급한다.</summary>
        private static GameLanguage CurrentLanguage =>
            YJ_LanguageManager.Instance != null ? YJ_LanguageManager.Instance.CurrentLanguage : GameLanguage.KOR;

        public string GetName(string uniqueEffectId)
        {
            return GetName(uniqueEffectId, CurrentLanguage);
        }

        public string GetName(string uniqueEffectId, GameLanguage language)
        {
            UniqueEffectLabelEntry entry = GetEntry(uniqueEffectId, language);
            return entry != null && !string.IsNullOrEmpty(entry.name) ? entry.name : uniqueEffectId;
        }

        /// <summary>DB에 uniqueEffectId가 없으면 false를 반환한다. 호출부가 effect.EffectName 같은
        /// 자기 자신의 기본값으로 정확히 폴백할 수 있게 하기 위함(GetName처럼 id를 대체값으로 쓰지 않음).</summary>
        public bool TryGetName(string uniqueEffectId, out string name)
        {
            return TryGetName(uniqueEffectId, CurrentLanguage, out name);
        }

        public bool TryGetName(string uniqueEffectId, GameLanguage language, out string name)
        {
            UniqueEffectLabelEntry entry = GetEntry(uniqueEffectId, language);
            name = entry != null ? entry.name : null;
            return entry != null && !string.IsNullOrEmpty(name);
        }

        /// <summary>coefficients를 대입하지 않은 원본 설명 템플릿. 필요할 때만 직접 쓰면 된다.</summary>
        public string GetDescriptionTemplate(string uniqueEffectId)
        {
            return GetDescriptionTemplate(uniqueEffectId, CurrentLanguage);
        }

        public string GetDescriptionTemplate(string uniqueEffectId, GameLanguage language)
        {
            UniqueEffectLabelEntry entry = GetEntry(uniqueEffectId, language);
            return entry != null ? entry.description : string.Empty;
        }

        /// <summary>coefficients를 대입해 완성한 최종 설명 문구. 툴팁 등 실제 표시에는 이걸 쓰면 된다.</summary>
        public string GetDescription(string uniqueEffectId, float[] coefficients)
        {
            return GetDescription(uniqueEffectId, coefficients, CurrentLanguage);
        }

        public string GetDescription(string uniqueEffectId, float[] coefficients, GameLanguage language)
        {
            string template = GetDescriptionTemplate(uniqueEffectId, language);
            return UniqueEffectSO.FormatDescription(template, coefficients, uniqueEffectId);
        }

        /// <summary>해당 언어에 항목이 없으면 KOR로 폴백한다(번역이 아직 안 채워진 항목 대비).</summary>
        private UniqueEffectLabelEntry GetEntry(string uniqueEffectId, GameLanguage language)
        {
            if (GetOrBuildLookup(language).TryGetValue(uniqueEffectId, out UniqueEffectLabelEntry entry))
                return entry;

            if (language != GameLanguage.KOR
                && GetOrBuildLookup(GameLanguage.KOR).TryGetValue(uniqueEffectId, out UniqueEffectLabelEntry korEntry))
            {
                return korEntry;
            }

            return null;
        }

        private Dictionary<string, UniqueEffectLabelEntry> GetOrBuildLookup(GameLanguage language)
        {
            lookupCache ??= new Dictionary<GameLanguage, Dictionary<string, UniqueEffectLabelEntry>>();

            if (lookupCache.TryGetValue(language, out var cached))
                return cached;

            var built = new Dictionary<string, UniqueEffectLabelEntry>();
            foreach (UniqueEffectLabelEntry entry in GetEntries(language))
            {
                if (entry != null && !string.IsNullOrEmpty(entry.uniqueEffectId))
                    built[entry.uniqueEffectId] = entry;
            }

            lookupCache[language] = built;
            return built;
        }

        private List<UniqueEffectLabelEntry> GetEntries(GameLanguage language)
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

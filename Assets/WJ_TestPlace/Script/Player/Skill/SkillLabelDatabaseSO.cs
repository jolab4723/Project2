using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 기본 설명 + 진화 1~3 설명 + 강화 1~3 설명 라벨(한/영/일/중) 데이터베이스.
/// SkillDataLabel.xlsx -> JSON(SkillLabelExcelToJson) -> 이 SO(SkillLabelSOImporter) 순서로 생성된다.
/// skillId는 ActiveSkillId enum 이름(예: "FighterHalfCircleSlash")과 1:1로 맞춰져 있다.
///
/// !! 표시 언어는 이 에셋이 들고 있지 않는다. 인자 없는 Get* 메서드는 YJ_LanguageManager.CurrentLanguage를
///    따라가므로, 언어를 바꾸려면 그 매니저의 SetLanguage를 부르면 된다(런타임 전환 가능).
/// </summary>
[CreateAssetMenu(fileName = "SkillLabelDatabase", menuName = "Data/Skill Label Database")]
public class SkillLabelDatabaseSO : ScriptableObject
{
    [Serializable]
    public class SkillLabelEntry
    {
        public string skillId;
        public string skillName;
        public string skillDescription;
        public string evolution1Description;
        public string evolution2Description;
        public string evolution3Description;
        public string enhancement1Description;
        public string enhancement2Description;
        public string enhancement3Description;
    }

    [SerializeField] private List<SkillLabelEntry> korLabels = new List<SkillLabelEntry>();
    [SerializeField] private List<SkillLabelEntry> engLabels = new List<SkillLabelEntry>();
    [SerializeField] private List<SkillLabelEntry> jpnLabels = new List<SkillLabelEntry>();
    [SerializeField] private List<SkillLabelEntry> chnLabels = new List<SkillLabelEntry>();

    private Dictionary<GameLanguage, Dictionary<string, SkillLabelEntry>> lookupCache;

    /// <summary>현재 표시 언어 기준으로 스킬 이름을 반환한다. 매칭 실패 시 빈 문자열(호출부에서 SkillDefinitionSO.skillName으로 폴백해야 함).</summary>
    public string GetSkillName(ActiveSkillId skillId) =>
        GetEntry(skillId, CurrentLanguage)?.skillName ?? string.Empty;

    /// <summary>현재 표시 언어 기준으로 기본 스킬 설명을 반환한다. 매칭 실패 시 빈 문자열.</summary>
    public string GetSkillDescription(ActiveSkillId skillId) =>
        GetEntry(skillId, CurrentLanguage)?.skillDescription ?? string.Empty;

    /// <summary>현재 표시 언어 기준으로 진화 설명을 반환한다. None이거나 해당 진화가 없으면 빈 문자열.</summary>
    public string GetEvolutionDescription(ActiveSkillId skillId, SkillEvolutionId evolution)
    {
        SkillLabelEntry entry = GetEntry(skillId, CurrentLanguage);
        if (entry == null)
            return string.Empty;

        return evolution switch
        {
            SkillEvolutionId.Evolution1 => entry.evolution1Description ?? string.Empty,
            SkillEvolutionId.Evolution2 => entry.evolution2Description ?? string.Empty,
            SkillEvolutionId.Evolution3 => entry.evolution3Description ?? string.Empty,
            _ => string.Empty,
        };
    }

    /// <summary>현재 표시 언어 기준으로 강화 설명을 반환한다. None이면 빈 문자열.</summary>
    public string GetEnhancementDescription(ActiveSkillId skillId, SkillEnhancementId enhancement)
    {
        SkillLabelEntry entry = GetEntry(skillId, CurrentLanguage);
        if (entry == null)
            return string.Empty;

        return enhancement switch
        {
            SkillEnhancementId.Enhance1 => entry.enhancement1Description ?? string.Empty,
            SkillEnhancementId.Enhance2 => entry.enhancement2Description ?? string.Empty,
            SkillEnhancementId.Enhance3 => entry.enhancement3Description ?? string.Empty,
            _ => string.Empty,
        };
    }

    /// <summary>YJ_LanguageManager가 아직 없으면(테스트 씬 등) KOR로 취급한다.</summary>
    private static GameLanguage CurrentLanguage =>
        YJ_LanguageManager.Instance != null ? YJ_LanguageManager.Instance.CurrentLanguage : GameLanguage.KOR;

    /// <summary>지정한 언어에 항목이 없으면 KOR로 폴백한다.</summary>
    private SkillLabelEntry GetEntry(ActiveSkillId skillId, GameLanguage language)
    {
        string key = skillId.ToString();

        if (GetOrBuildLookup(language).TryGetValue(key, out SkillLabelEntry entry))
            return entry;

        if (language != GameLanguage.KOR && GetOrBuildLookup(GameLanguage.KOR).TryGetValue(key, out SkillLabelEntry korEntry))
            return korEntry;

        return null;
    }

    private Dictionary<string, SkillLabelEntry> GetOrBuildLookup(GameLanguage language)
    {
        lookupCache ??= new Dictionary<GameLanguage, Dictionary<string, SkillLabelEntry>>();

        if (lookupCache.TryGetValue(language, out var cached))
            return cached;

        var built = new Dictionary<string, SkillLabelEntry>();
        foreach (SkillLabelEntry entry in GetEntries(language))
        {
            if (entry != null && !string.IsNullOrEmpty(entry.skillId))
                built[entry.skillId] = entry;
        }

        lookupCache[language] = built;
        return built;
    }

    private List<SkillLabelEntry> GetEntries(GameLanguage language)
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

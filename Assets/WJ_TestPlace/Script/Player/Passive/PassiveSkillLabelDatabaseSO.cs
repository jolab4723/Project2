using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

/// <summary>
/// 패시브 스킬 이름/설명 표시 라벨(한/영/일/중) 데이터베이스.
/// PassiveSkillDataLabel.xlsx -> JSON(PassiveSkillLabelExcelToJson) -> 이 SO(PassiveSkillLabelSOImporter) 순서로 생성된다.
/// passiveId는 Core.PassiveSkillId enum 이름(예: "MaxHealth")과 1:1로 맞춰져 있다.
///
/// !! 표시 언어는 이 에셋이 들고 있지 않는다. 인자 없는 Get* 메서드는 YJ_LanguageManager.CurrentLanguage를
///    따라가므로, 언어를 바꾸려면 그 매니저의 SetLanguage를 부르면 된다(런타임 전환 가능).
/// </summary>
[CreateAssetMenu(fileName = "PassiveSkillLabelDatabase", menuName = "Data/Passive Skill Label Database")]
public class PassiveSkillLabelDatabaseSO : ScriptableObject
{
    [Serializable]
    public class PassiveSkillLabelEntry
    {
        public string passiveId;
        public string name;
        [TextArea] public string description;
    }

    [SerializeField] private List<PassiveSkillLabelEntry> korLabels = new List<PassiveSkillLabelEntry>();
    [SerializeField] private List<PassiveSkillLabelEntry> engLabels = new List<PassiveSkillLabelEntry>();
    [SerializeField] private List<PassiveSkillLabelEntry> jpnLabels = new List<PassiveSkillLabelEntry>();
    [SerializeField] private List<PassiveSkillLabelEntry> chnLabels = new List<PassiveSkillLabelEntry>();

    private Dictionary<GameLanguage, Dictionary<string, PassiveSkillLabelEntry>> lookupCache;

    /// <summary>현재 표시 언어 기준으로 이름을 반환한다. 매칭 실패 시 passiveId를 그대로 반환.</summary>
    public string GetName(PassiveSkillId id) => GetName(id.ToString(), CurrentLanguage);

    /// <summary>현재 표시 언어 기준으로 이름을 반환한다. 매칭 실패 시 passiveId를 그대로 반환.</summary>
    public string GetName(string passiveId) => GetName(passiveId, CurrentLanguage);

    /// <summary>
    /// 지정한 언어의 이름을 반환한다. 해당 언어에 항목이 없으면 KOR로 폴백하고,
    /// 그래도 없으면 passiveId를 그대로 반환한다.
    /// </summary>
    public string GetName(string passiveId, GameLanguage language)
    {
        PassiveSkillLabelEntry entry = GetEntry(passiveId, language);
        return entry != null && !string.IsNullOrEmpty(entry.name) ? entry.name : passiveId;
    }

    /// <summary>현재 표시 언어 기준으로 설명을 반환한다. 매칭 실패 시 빈 문자열(호출부에서 PassiveSkillDefinition.description으로 폴백해야 함).</summary>
    public string GetDescription(PassiveSkillId id) => GetDescription(id.ToString(), CurrentLanguage);

    /// <summary>현재 표시 언어 기준으로 설명을 반환한다. 매칭 실패 시 빈 문자열.</summary>
    public string GetDescription(string passiveId) => GetDescription(passiveId, CurrentLanguage);

    /// <summary>지정한 언어의 설명을 반환한다. 해당 언어에 항목이 없으면 KOR로 폴백하고, 그래도 없으면 빈 문자열.</summary>
    public string GetDescription(string passiveId, GameLanguage language)
    {
        PassiveSkillLabelEntry entry = GetEntry(passiveId, language);
        return entry != null && !string.IsNullOrEmpty(entry.description) ? entry.description : string.Empty;
    }

    /// <summary>YJ_LanguageManager가 아직 없으면(테스트 씬 등) KOR로 취급한다.</summary>
    private static GameLanguage CurrentLanguage =>
        YJ_LanguageManager.Instance != null ? YJ_LanguageManager.Instance.CurrentLanguage : GameLanguage.KOR;

    /// <summary>지정한 언어에 항목이 없으면 KOR로 폴백한다.</summary>
    private PassiveSkillLabelEntry GetEntry(string passiveId, GameLanguage language)
    {
        if (GetOrBuildLookup(language).TryGetValue(passiveId, out PassiveSkillLabelEntry entry))
            return entry;

        if (language != GameLanguage.KOR
            && GetOrBuildLookup(GameLanguage.KOR).TryGetValue(passiveId, out PassiveSkillLabelEntry korEntry))
        {
            return korEntry;
        }

        return null;
    }

    private Dictionary<string, PassiveSkillLabelEntry> GetOrBuildLookup(GameLanguage language)
    {
        lookupCache ??= new Dictionary<GameLanguage, Dictionary<string, PassiveSkillLabelEntry>>();

        if (lookupCache.TryGetValue(language, out var cached))
            return cached;

        var built = new Dictionary<string, PassiveSkillLabelEntry>();
        foreach (PassiveSkillLabelEntry entry in GetEntries(language))
        {
            if (entry != null && !string.IsNullOrEmpty(entry.passiveId))
                built[entry.passiveId] = entry;
        }

        lookupCache[language] = built;
        return built;
    }

    private List<PassiveSkillLabelEntry> GetEntries(GameLanguage language)
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

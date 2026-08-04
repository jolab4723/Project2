using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스탯 표시 라벨(한/영/일/중) 데이터베이스.
/// StatDataLabel.xlsx -> JSON(StatLabelExcelToJson) -> 이 SO(StatLabelSOImporter) 순서로 생성된다.
/// statKey는 PlayerStat.cs의 실제 필드명과 1:1로 맞춰져 있다 (예: "attackPower", "critRate").
///
/// !! 표시 언어는 이 에셋이 들고 있지 않는다. 인자 없는 GetLabel은 YJ_LanguageManager.CurrentLanguage를
///    따라가므로, 언어를 바꾸려면 그 매니저의 SetLanguage를 부르면 된다(런타임 전환 가능).
/// </summary>
[CreateAssetMenu(fileName = "StatLabelDatabase", menuName = "Data/Stat Label Database")]
public class StatLabelDatabaseSO : ScriptableObject
{
    [Serializable]
    public class StatLabelEntry
    {
        public string statKey;
        public string label;
    }

    [SerializeField] private List<StatLabelEntry> korLabels = new List<StatLabelEntry>();
    [SerializeField] private List<StatLabelEntry> engLabels = new List<StatLabelEntry>();
    [SerializeField] private List<StatLabelEntry> jpnLabels = new List<StatLabelEntry>();
    [SerializeField] private List<StatLabelEntry> chnLabels = new List<StatLabelEntry>();

    private Dictionary<GameLanguage, Dictionary<string, string>> lookupCache;

    /// <summary>현재 표시 언어 기준으로 라벨을 반환한다. 매칭 실패 시 statKey를 그대로 반환.</summary>
    public string GetLabel(string statKey)
    {
        return GetLabel(statKey, CurrentLanguage);
    }

    /// <summary>
    /// 지정한 언어의 라벨을 반환한다. 해당 언어에 항목이 없으면 KOR로 폴백하고,
    /// 그래도 없으면 statKey를 그대로 반환한다.
    /// </summary>
    public string GetLabel(string statKey, GameLanguage language)
    {
        if (GetOrBuildLookup(language).TryGetValue(statKey, out string label) && !string.IsNullOrEmpty(label))
            return label;

        if (language != GameLanguage.KOR
            && GetOrBuildLookup(GameLanguage.KOR).TryGetValue(statKey, out string korLabel)
            && !string.IsNullOrEmpty(korLabel))
        {
            return korLabel;
        }

        return statKey;
    }

    /// <summary>YJ_LanguageManager가 아직 없으면(테스트 씬 등) KOR로 취급한다.</summary>
    private static GameLanguage CurrentLanguage =>
        YJ_LanguageManager.Instance != null ? YJ_LanguageManager.Instance.CurrentLanguage : GameLanguage.KOR;

    private Dictionary<string, string> GetOrBuildLookup(GameLanguage language)
    {
        lookupCache ??= new Dictionary<GameLanguage, Dictionary<string, string>>();

        if (lookupCache.TryGetValue(language, out var cached))
            return cached;

        var built = new Dictionary<string, string>();
        foreach (StatLabelEntry entry in GetEntries(language))
        {
            if (entry != null && !string.IsNullOrEmpty(entry.statKey))
                built[entry.statKey] = entry.label;
        }

        lookupCache[language] = built;
        return built;
    }

    private List<StatLabelEntry> GetEntries(GameLanguage language)
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

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스탯 표시 라벨(한/영) 데이터베이스.
/// StatDataLabel.xlsx -> JSON(StatLabelExcelToJson) -> 이 SO(StatLabelSOImporter) 순서로 생성된다.
/// statKey는 PlayerStat.cs의 실제 필드명과 1:1로 맞춰져 있다 (예: "attackPower", "critRate").
/// </summary>
[CreateAssetMenu(fileName = "StatLabelDatabase", menuName = "Data/Stat Label Database")]
public class StatLabelDatabaseSO : ScriptableObject
{
    public enum Language
    {
        KOR,
        ENG
    }

    [Serializable]
    public class StatLabelEntry
    {
        public string statKey;
        public string label;
    }

    [Header("표시 언어")]
    [SerializeField] private Language currentLanguage = Language.KOR;

    [SerializeField] private List<StatLabelEntry> korLabels = new List<StatLabelEntry>();
    [SerializeField] private List<StatLabelEntry> engLabels = new List<StatLabelEntry>();

    private Dictionary<string, string> korLookup;
    private Dictionary<string, string> engLookup;

    /// <summary>currentLanguage 기준으로 statKey에 대응하는 라벨을 반환한다. 매칭 실패 시 statKey를 그대로 반환.</summary>
    public string GetLabel(string statKey)
    {
        return GetLabel(statKey, currentLanguage);
    }

    public string GetLabel(string statKey, Language language)
    {
        Dictionary<string, string> lookup = language == Language.KOR ? GetOrBuildLookup(ref korLookup, korLabels) : GetOrBuildLookup(ref engLookup, engLabels);
        return lookup.TryGetValue(statKey, out string label) ? label : statKey;
    }

    private static Dictionary<string, string> GetOrBuildLookup(ref Dictionary<string, string> cache, List<StatLabelEntry> entries)
    {
        if (cache != null)
            return cache;

        cache = new Dictionary<string, string>();
        foreach (StatLabelEntry entry in entries)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.statKey))
                cache[entry.statKey] = entry.label;
        }

        return cache;
    }

    private void OnValidate()
    {
        korLookup = null;
        engLookup = null;
    }
}

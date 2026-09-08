using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면에 고정으로 표시되는 UI 문구(탭 이름, 버튼 라벨, 옵션 라벨 등)를 위한 범용 key→문구 다국어
/// 데이터베이스. QuestLabelDatabaseSO와 완전히 같은 구조(KOR/ENG/JPN/CHN + 캐시)를 따르되, 퀘스트에
/// 종속된 편의 메서드(GetQuestName 등) 없이 GetLabel(key)만 제공한다 - 특정 화면 전용이 아니라 여러
/// 화면(UILabelText를 붙인 아무 TextMeshProUGUI)이 공유해서 쓰는 범용 DB로 쓰기 위함이다.
/// </summary>
[CreateAssetMenu(fileName = "UILabelDatabase", menuName = "Data/UI Label Database")]
public class UILabelDatabaseSO : ScriptableObject
{
    [Serializable]
    public class UILabelEntry
    {
        public string key;
        public string label;
    }

    [SerializeField] private List<UILabelEntry> korLabels = new List<UILabelEntry>();
    [SerializeField] private List<UILabelEntry> engLabels = new List<UILabelEntry>();
    [SerializeField] private List<UILabelEntry> jpnLabels = new List<UILabelEntry>();
    [SerializeField] private List<UILabelEntry> chnLabels = new List<UILabelEntry>();

    private Dictionary<GameLanguage, Dictionary<string, string>> lookupCache;

    /// <summary>현재 언어(YJ_LanguageManager 기준)의 문구를 반환한다. 해당 언어에 없으면 한국어로,
    /// 한국어에도 없으면 key를 그대로 반환한다.</summary>
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

        return key;
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

    private List<UILabelEntry> GetEntries(GameLanguage language)
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

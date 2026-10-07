using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버프 이름 표시 라벨(한/영/일/중) 데이터베이스.
/// BuffLabel.xlsx -> JSON(BuffLabelExcelToJson) -> 이 SO(BuffLabelSOImporter) 순서로 생성된다.
///
/// HUD 버프 아이콘 툴팁의 스탯 줄은 여기 없다 - BuffTextComposer가 StatEffects(증감 스탯과 수치)로 직접
/// 조립하고, 스탯 이름은 이미 다국어인 ItemDisplayNames.StatNames를 쓴다.
/// 2026-10-01: P 버프 팝업은 스탯 줄 대신 상세 효과 문장만 보여주게 바뀌어서, 그 문장(description)을 함께 담는다.
/// (고유효과 버프는 이름·설명 모두 UniqueEffectLabelDatabase에 이미 번역돼 있어 이 DB를 쓰지 않는다.)
///
/// !! 표시 언어는 이 에셋이 들고 있지 않는다. 인자 없는 GetName은 YJ_LanguageManager.CurrentLanguage를
///    따라가므로, 언어를 바꾸려면 그 매니저의 SetLanguage를 부르면 된다(런타임 전환 가능).
/// </summary>
[CreateAssetMenu(fileName = "BuffLabelDatabase", menuName = "Data/Buff Label Database")]
public class BuffLabelDatabaseSO : ScriptableObject
{
    [Serializable]
    public class BuffLabelEntry
    {
        public string buffId;
        public string buffName;
        [TextArea] public string description;
    }

    [SerializeField] private List<BuffLabelEntry> korLabels = new List<BuffLabelEntry>();
    [SerializeField] private List<BuffLabelEntry> engLabels = new List<BuffLabelEntry>();
    [SerializeField] private List<BuffLabelEntry> jpnLabels = new List<BuffLabelEntry>();
    [SerializeField] private List<BuffLabelEntry> chnLabels = new List<BuffLabelEntry>();

    private Dictionary<GameLanguage, Dictionary<string, BuffLabelEntry>> lookupCache;

    /// <summary>현재 표시 언어 기준 버프 이름. 매칭 실패 시 false를 반환한다(호출부가 원본 이름으로 폴백하도록).</summary>
    public bool TryGetName(string buffId, out string buffName)
    {
        return TryGetName(buffId, CurrentLanguage, out buffName);
    }

    /// <summary>지정한 언어의 버프 이름. 해당 언어에 항목이 없으면 KOR로 폴백한다.</summary>
    public bool TryGetName(string buffId, GameLanguage language, out string buffName)
    {
        return TryGetText(buffId, language, entry => entry.buffName, out buffName);
    }

    /// <summary>현재 표시 언어 기준 상세 효과 문장. 해당 언어가 비어 있으면 KOR로, 그것도 없으면 false.</summary>
    public bool TryGetDescription(string buffId, out string description)
    {
        return TryGetText(buffId, CurrentLanguage, entry => entry.description, out description);
    }

    private bool TryGetText(string buffId, GameLanguage language, Func<BuffLabelEntry, string> select, out string text)
    {
        if (!string.IsNullOrEmpty(buffId))
        {
            if (GetOrBuildLookup(language).TryGetValue(buffId, out BuffLabelEntry entry)
                && !string.IsNullOrEmpty(text = select(entry)))
            {
                return true;
            }

            if (language != GameLanguage.KOR
                && GetOrBuildLookup(GameLanguage.KOR).TryGetValue(buffId, out entry)
                && !string.IsNullOrEmpty(text = select(entry)))
            {
                return true;
            }
        }

        text = null;
        return false;
    }

    /// <summary>YJ_LanguageManager가 아직 없으면(테스트 씬 등) KOR로 취급한다.</summary>
    private static GameLanguage CurrentLanguage =>
        YJ_LanguageManager.Instance != null ? YJ_LanguageManager.Instance.CurrentLanguage : GameLanguage.KOR;

    private Dictionary<string, BuffLabelEntry> GetOrBuildLookup(GameLanguage language)
    {
        lookupCache ??= new Dictionary<GameLanguage, Dictionary<string, BuffLabelEntry>>();

        if (lookupCache.TryGetValue(language, out var cached))
            return cached;

        var built = new Dictionary<string, BuffLabelEntry>();
        foreach (BuffLabelEntry entry in GetEntries(language))
        {
            if (entry != null && !string.IsNullOrEmpty(entry.buffId))
                built[entry.buffId] = entry;
        }

        lookupCache[language] = built;
        return built;
    }

    private List<BuffLabelEntry> GetEntries(GameLanguage language)
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

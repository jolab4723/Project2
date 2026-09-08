using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 관련 표시 문구(한/영/일/중) 데이터베이스. StatLabelDatabaseSO와 같은 범용 key/label 구조를
/// 그대로 따른다(퀘스트마다 고정 필드를 두는 EnemyLabelDatabaseSO 방식 대신, 키 하나로 퀘스트 콘텐츠와
/// 팝업 UI 고정 문구를 전부 커버).
/// QuestLabel.xlsx -> JSON(QuestLabelExcelToJson) -> 이 SO(QuestLabelSOImporter) 순서로 생성된다.
///
/// key 규칙(QuestLabelRow 참고):
///  - 퀘스트 콘텐츠: "{questId}.name", "{questId}.description", "{questId}.condition.{index}"
///  - UI 고정 문구: "quest_ui.*"
///
/// !! 표시 언어는 이 에셋이 들고 있지 않는다. 인자 없는 GetLabel은 YJ_LanguageManager.CurrentLanguage를
///    따라가므로, 언어를 바꾸려면 그 매니저의 SetLanguage를 부르면 된다(런타임 전환 가능).
/// </summary>
[CreateAssetMenu(fileName = "QuestLabelDatabase", menuName = "Data/Quest Label Database")]
public class QuestLabelDatabaseSO : ScriptableObject
{
    [Serializable]
    public class QuestLabelEntry
    {
        public string key;
        public string label;
    }

    [SerializeField] private List<QuestLabelEntry> korLabels = new List<QuestLabelEntry>();
    [SerializeField] private List<QuestLabelEntry> engLabels = new List<QuestLabelEntry>();
    [SerializeField] private List<QuestLabelEntry> jpnLabels = new List<QuestLabelEntry>();
    [SerializeField] private List<QuestLabelEntry> chnLabels = new List<QuestLabelEntry>();

    private Dictionary<GameLanguage, Dictionary<string, string>> lookupCache;

    /// <summary>현재 표시 언어 기준으로 라벨을 반환한다. 매칭 실패 시 key를 그대로 반환.</summary>
    public string GetLabel(string key)
    {
        return GetLabel(key, CurrentLanguage);
    }

    /// <summary>
    /// 지정한 언어의 라벨을 반환한다. 해당 언어에 항목이 없으면 KOR로 폴백하고,
    /// 그래도 없으면 key를 그대로 반환한다.
    /// </summary>
    public string GetLabel(string key, GameLanguage language)
    {
        if (GetOrBuildLookup(language).TryGetValue(key, out string label) && !string.IsNullOrEmpty(label))
            return label;

        if (language != GameLanguage.KOR
            && GetOrBuildLookup(GameLanguage.KOR).TryGetValue(key, out string korLabel)
            && !string.IsNullOrEmpty(korLabel))
        {
            return korLabel;
        }

        return key;
    }

    /// <summary>퀘스트 이름. 없으면 questId를 그대로 반환.</summary>
    public string GetQuestName(string questId) => GetLabel(questId + ".name");

    /// <summary>퀘스트 설명.</summary>
    public string GetQuestDescription(string questId) => GetLabel(questId + ".description");

    /// <summary>조건 하나(0부터)의 설명. QuestConditionDefinition.description 대신 이걸 쓴다.</summary>
    public string GetConditionDescription(string questId, int conditionIndex) =>
        GetLabel(questId + ".condition." + conditionIndex);

    /// <summary>YJ_LanguageManager가 아직 없으면(테스트 씬 등) KOR로 취급한다.</summary>
    private static GameLanguage CurrentLanguage =>
        YJ_LanguageManager.Instance != null ? YJ_LanguageManager.Instance.CurrentLanguage : GameLanguage.KOR;

    private Dictionary<string, string> GetOrBuildLookup(GameLanguage language)
    {
        lookupCache ??= new Dictionary<GameLanguage, Dictionary<string, string>>();

        if (lookupCache.TryGetValue(language, out var cached))
            return cached;

        var built = new Dictionary<string, string>();
        foreach (QuestLabelEntry entry in GetEntries(language))
        {
            if (entry != null && !string.IsNullOrEmpty(entry.key))
                built[entry.key] = entry.label;
        }

        lookupCache[language] = built;
        return built;
    }

    private List<QuestLabelEntry> GetEntries(GameLanguage language)
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

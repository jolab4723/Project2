using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 이름 표시 라벨(한/영/일/중) 데이터베이스.
/// EnemyDataLabel.xlsx -> JSON(EnemyLabelExcelToJson) -> 이 SO(EnemyLabelSOImporter) 순서로 생성된다.
/// enemyId는 EnemyData.xlsx의 enemyId(예: "enemy.normal.ranged.patrol_drone")와 1:1로 맞춰져 있다.
///
/// !! 표시 언어는 이 에셋이 들고 있지 않는다. 인자 없는 GetName은 YJ_LanguageManager.CurrentLanguage를
///    따라가므로, 언어를 바꾸려면 그 매니저의 SetLanguage를 부르면 된다(런타임 전환 가능).
/// </summary>
[CreateAssetMenu(fileName = "EnemyLabelDatabase", menuName = "Data/Enemy Label Database")]
public class EnemyLabelDatabaseSO : ScriptableObject
{
    [Serializable]
    public class EnemyLabelEntry
    {
        public string enemyId;
        public string name;
    }

    [SerializeField] private List<EnemyLabelEntry> korLabels = new List<EnemyLabelEntry>();
    [SerializeField] private List<EnemyLabelEntry> engLabels = new List<EnemyLabelEntry>();
    [SerializeField] private List<EnemyLabelEntry> jpnLabels = new List<EnemyLabelEntry>();
    [SerializeField] private List<EnemyLabelEntry> chnLabels = new List<EnemyLabelEntry>();

    private Dictionary<GameLanguage, Dictionary<string, string>> lookupCache;

    /// <summary>현재 표시 언어 기준으로 이름을 반환한다. 매칭 실패 시 enemyId를 그대로 반환.</summary>
    public string GetName(string enemyId)
    {
        return GetName(enemyId, CurrentLanguage);
    }

    /// <summary>
    /// 지정한 언어의 이름을 반환한다. 해당 언어에 항목이 없으면 KOR로 폴백하고,
    /// 그래도 없으면 enemyId를 그대로 반환한다.
    /// </summary>
    public string GetName(string enemyId, GameLanguage language)
    {
        if (GetOrBuildLookup(language).TryGetValue(enemyId, out string name) && !string.IsNullOrEmpty(name))
            return name;

        if (language != GameLanguage.KOR
            && GetOrBuildLookup(GameLanguage.KOR).TryGetValue(enemyId, out string korName)
            && !string.IsNullOrEmpty(korName))
        {
            return korName;
        }

        return enemyId;
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
        foreach (EnemyLabelEntry entry in GetEntries(language))
        {
            if (entry != null && !string.IsNullOrEmpty(entry.enemyId))
                built[entry.enemyId] = entry.name;
        }

        lookupCache[language] = built;
        return built;
    }

    private List<EnemyLabelEntry> GetEntries(GameLanguage language)
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

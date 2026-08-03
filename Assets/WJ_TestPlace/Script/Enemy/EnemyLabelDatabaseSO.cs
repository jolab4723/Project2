using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 이름 표시 라벨(한/영) 데이터베이스.
/// EnemyDataLabel.xlsx -> JSON(EnemyLabelExcelToJson) -> 이 SO(EnemyLabelSOImporter) 순서로 생성된다.
/// enemyId는 EnemyData.xlsx의 enemyId(예: "enemy.normal.ranged.patrol_drone")와 1:1로 맞춰져 있다.
/// </summary>
[CreateAssetMenu(fileName = "EnemyLabelDatabase", menuName = "Data/Enemy Label Database")]
public class EnemyLabelDatabaseSO : ScriptableObject
{
    public enum Language
    {
        KOR,
        ENG
    }

    [Serializable]
    public class EnemyLabelEntry
    {
        public string enemyId;
        public string name;
    }

    [Header("표시 언어")]
    [SerializeField] private Language currentLanguage = Language.KOR;

    [SerializeField] private List<EnemyLabelEntry> korLabels = new List<EnemyLabelEntry>();
    [SerializeField] private List<EnemyLabelEntry> engLabels = new List<EnemyLabelEntry>();

    private Dictionary<string, string> korLookup;
    private Dictionary<string, string> engLookup;

    /// <summary>currentLanguage 기준으로 enemyId에 대응하는 이름을 반환한다. 매칭 실패 시 enemyId를 그대로 반환.</summary>
    public string GetName(string enemyId)
    {
        return GetName(enemyId, currentLanguage);
    }

    public string GetName(string enemyId, Language language)
    {
        Dictionary<string, string> lookup = language == Language.KOR ? GetOrBuildLookup(ref korLookup, korLabels) : GetOrBuildLookup(ref engLookup, engLabels);
        return lookup.TryGetValue(enemyId, out string name) ? name : enemyId;
    }

    private static Dictionary<string, string> GetOrBuildLookup(ref Dictionary<string, string> cache, List<EnemyLabelEntry> entries)
    {
        if (cache != null)
            return cache;

        cache = new Dictionary<string, string>();
        foreach (EnemyLabelEntry entry in entries)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.enemyId))
                cache[entry.enemyId] = entry.name;
        }

        return cache;
    }

    private void OnValidate()
    {
        korLookup = null;
        engLookup = null;
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using ItemSystem;

/// <summary>
/// 레벨에 따른 캐릭터(파이터) 기본 스탯을 JSON에서 읽어와 StatType 형식으로 변환해주는 매니저.
/// JSON 스키마(level/maxHP/maxMP/baseATK/baseDEF/baseCritRate/baseCritMult/baseMS/baseAS/baseCDR/baseMPRegen/basePen)를
/// StatType enum에 매핑한다. JSON에 대응하는 필드가 없는 StatType은 전부 0으로 채워진다.
/// </summary>
public class PlayerLevelManager : MonoBehaviour
{
    [Tooltip("레벨별 스탯 원본 데이터 (예: FighterStatData.json). 다른 클래스로 바꿀 때는 같은 필드 구조의 JSON을 여기 연결하면 됨.")]
    [SerializeField] private TextAsset statDataJson;

    [Header("테스트")]
    [Tooltip("L키를 눌렀을 때 조회할 레벨")]
    [SerializeField] private int testLevel = 1;

    /// <summary>
    /// JSON 필드 이름 -> StatType 매핑.
    /// 실제 데이터 스키마가 바뀜면 이 딜셔너리만 고치면 됨.
    /// </summary>
    private static readonly Dictionary<string, StatType> JsonFieldToStatType = new Dictionary<string, StatType>
    {
        { "maxHP", StatType.healthFlat },
        { "maxMP", StatType.mpMaxFlat },
        { "baseATK", StatType.attackPowerFlat },
        { "baseDEF", StatType.defensePowerFlat },
        { "baseCritRate", StatType.critRateFlat },
        { "baseCritMult", StatType.critMultFlat },
        { "baseMS", StatType.moveSpeedFlat },
        { "baseAS", StatType.attackSpeedFlat },
        { "baseCDR", StatType.cdrFlat },
        { "baseMPRegen", StatType.mpRegenFlat },
        { "basePen", StatType.penetrationFlat },
        { "baseSkillRange", StatType.skillRangeFlat },
    };

    private List<FighterLevelStatData> levelStatList;

    private void Awake()
    {
        Load();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            TestPrintStats();
    }

    /// <summary>statDataJson을 파싱해서 레벨별 데이터 목록을 채운다.</summary>
    public void Load()
    {
        if (statDataJson == null)
        {
            Debug.LogWarning("[PlayerLevelManager] statDataJson이 연결되지 않았습니다.");
            levelStatList = new List<FighterLevelStatData>();
            return;
        }

        // JsonUtility는 최상위가 배열인 JSON을 바로 파싱하지 못해서 객체로 감싸서 처리한다.
        string wrapped = "{\"items\":" + statDataJson.text + "}";
        var parsed = JsonUtility.FromJson<FighterLevelStatDataListWrapper>(wrapped);
        levelStatList = parsed != null && parsed.items != null
            ? parsed.items
            : new List<FighterLevelStatData>();
    }

    /// <summary>
    /// 지정한 레벨의 스탯을 StatType 전체 기준으로 반환한다.
    /// JSON에 매핑되지 않은 StatType은 0으로 채워진다.
    /// </summary>
    public Dictionary<StatType, float> GetStatsForLevel(int level)
    {
        var result = new Dictionary<StatType, float>();
        foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            result[statType] = 0f;

        var row = FindRow(level);
        if (row == null)
            return result;

        ApplyField(result, "maxHP", row.maxHP);
        ApplyField(result, "maxMP", row.maxMP);
        ApplyField(result, "baseATK", row.baseATK);
        ApplyField(result, "baseDEF", row.baseDEF);
        ApplyField(result, "baseCritRate", row.baseCritRate);
        ApplyField(result, "baseCritMult", row.baseCritMult);
        ApplyField(result, "baseMS", row.baseMS);
        ApplyField(result, "baseAS", row.baseAS);
        ApplyField(result, "baseCDR", row.baseCDR);
        ApplyField(result, "baseMPRegen", row.baseMPRegen);
        ApplyField(result, "basePen", row.basePen);
        ApplyField(result, "baseSkillRange", row.baseSkillRange);

        return result;
    }

    private void ApplyField(Dictionary<StatType, float> result, string fieldName, float value)
    {
        if (JsonFieldToStatType.TryGetValue(fieldName, out var statType))
            result[statType] = value;
    }

    /// <summary>
    /// level과 정확히 일치하는 데이터를 찾는다. 없으면 가장 가까운 레벨로 대체하고 경고를 남긴다.
    /// </summary>
    private FighterLevelStatData FindRow(int level)
    {
        if (levelStatList == null || levelStatList.Count == 0)
        {
            Debug.LogWarning("[PlayerLevelManager] 로드된 레벨 스탯 데이터가 없습니다.");
            return null;
        }

        foreach (var row in levelStatList)
        {
            if (row.level == level)
                return row;
        }

        FighterLevelStatData closest = levelStatList[0];
        int closestDiff = Mathf.Abs(closest.level - level);

        foreach (var row in levelStatList)
        {
            int diff = Mathf.Abs(row.level - level);
            if (diff < closestDiff)
            {
                closest = row;
                closestDiff = diff;
            }
        }

        Debug.LogWarning($"[PlayerLevelManager] 레벨 {level}에 해당하는 데이터가 없습니다. 가장 가까운 레벨 {closest.level}로 대체합니다.");
        return closest;
    }

    /// <summary>
    /// 지정한 레벨에서 다음 레벨로 올라가는 데 필요한 경험치. 데이터에 없는 레벨(마지막 레벨 등)이거나
    /// expToNextLevel이 0이면 더 이상 레벨업할 수 없다는 뜻으로 0을 반환한다.
    /// </summary>
    public float GetExpToNextLevel(int level)
    {
        var row = FindRow(level);
        return row != null ? row.expToNextLevel : 0f;
    }

    /// <summary>테스트용: testLevel의 스탯 전체를 콘솔에 출력.</summary>
    [ContextMenu("레벨 스탯 테스트 출력")]
    private void TestPrintStats()
    {
        var stats = GetStatsForLevel(testLevel);

        var sb = new StringBuilder();
        sb.AppendLine($"===== 레벨 {testLevel} 스탯 =====");
        foreach (var kvp in stats)
            sb.AppendLine($"  {kvp.Key} : {kvp.Value}");

        Debug.Log(sb.ToString());
    }
}

/// <summary>FighterStatData.json 한 행(레벨 하나)에 대응하는 데이터.</summary>
[Serializable]
public class FighterLevelStatData
{
    public int level;
    public float maxHP;
    public float maxMP;
    public float baseATK;
    public float baseDEF;
    public float baseCritRate;
    public float baseCritMult;
    public float baseMS;
    public float baseAS;
    public float baseCDR;
    public float baseMPRegen;
    public float basePen;
    public float baseSkillRange;
    public float expToNextLevel;
}

/// <summary>JsonUtility로 최상위 배열 JSON을 파싱하기 위한 래퍼.</summary>
[Serializable]
internal class FighterLevelStatDataListWrapper
{
    public List<FighterLevelStatData> items;
}


using System;
using System.Collections.Generic;
using ItemSystem;

/// <summary>
/// 후보 아이템의 메인 스탯이 현재 장비보다
/// 증가했는지, 감소했는지, 동일한지를 나타낸다.
/// </summary>
public enum MainStatComparisonDirection
{
    Decrease = -1,
    Equal = 0,
    Increase = 1
}

/// <summary>
/// 메인 스탯 한 종류에 대한 비교 결과.
/// UI 문자열이나 색상은 보관하지 않고 숫자 정보만 보관한다.
/// </summary>
public sealed class MainStatComparisonResult
{
    public StatType StatType { get; }
    public float CandidateValue { get; }
    public float EquippedValue { get; }
    public float Delta { get; }
    public MainStatComparisonDirection Direction { get; }

    public MainStatComparisonResult(
        StatType statType,
        float candidateValue,
        float equippedValue,
        float delta,
        MainStatComparisonDirection direction)
    {
        StatType = statType;
        CandidateValue = candidateValue;
        EquippedValue = equippedValue;
        Delta = delta;
        Direction = direction;
    }
}

/// <summary>
/// 두 아이템 전체의 메인 스탯 비교 결과.
/// </summary>
public sealed class ItemMainStatComparisonResult
{
    public ItemInstance CandidateItem { get; }
    public ItemInstance EquippedItem { get; }
    public IReadOnlyList<MainStatComparisonResult> Rows { get; }

    public bool HasComparableStats => Rows.Count > 0;

    public bool HasAnyDifference
    {
        get
        {
            foreach (MainStatComparisonResult row in Rows)
            {
                if (row.Direction != MainStatComparisonDirection.Equal)
                    return true;
            }

            return false;
        }
    }

    public ItemMainStatComparisonResult(
        ItemInstance candidateItem,
        ItemInstance equippedItem,
        IReadOnlyList<MainStatComparisonResult> rows)
    {
        CandidateItem = candidateItem;
        EquippedItem = equippedItem;
        Rows = rows;
    }
}

/// <summary>
/// 두 ItemInstance의 강화 적용 메인 스탯을 비교한다.
///
/// 이 클래스는 다음 책임을 갖지 않는다.
/// - 장비 슬롯 판정
/// - 현재 장비 조회
/// - UI 문자열 생성
/// - 색상 결정
/// </summary>
public static class ItemMainStatComparer
{
    // 강화 계산 시 발생하는 부동소수점 오차를 동일 값으로 취급한다.
    private const float ComparisonEpsilon = 0.0001f;

    /// <summary>
    /// 후보 아이템과 현재 장착 아이템의 메인 스탯을 비교한다.
    ///
    /// 반환값:
    /// true  = 입력이 유효하여 비교 결과를 생성함
    /// false = 아이템이나 definition이 유효하지 않음
    /// </summary>
    public static bool TryCompare(
        ItemInstance candidateItem,
        ItemInstance equippedItem,
        out ItemMainStatComparisonResult result)
    {
        result = null;

        if (!IsValid(candidateItem) || !IsValid(equippedItem))
            return false;

        // 후보 아이템에 존재하는 스탯을 먼저 배치하고,
        // 장착 아이템에만 존재하는 스탯을 뒤에 추가한다.
        var orderedStatTypes = new List<StatType>();
        var discoveredStatTypes = new HashSet<StatType>();

        Dictionary<StatType, float> candidateValues =
            BuildMainStatTotals(
                candidateItem,
                orderedStatTypes,
                discoveredStatTypes);

        Dictionary<StatType, float> equippedValues =
            BuildMainStatTotals(
                equippedItem,
                orderedStatTypes,
                discoveredStatTypes);

        var rows = new List<MainStatComparisonResult>();

        foreach (StatType statType in orderedStatTypes)
        {
            float candidateValue =
                GetValueOrZero(candidateValues, statType);

            float equippedValue =
                GetValueOrZero(equippedValues, statType);

            float delta = candidateValue - equippedValue;

            MainStatComparisonDirection direction =
                GetDirection(delta);

            // 아주 작은 부동소수점 오차는 0으로 정리한다.
            if (direction == MainStatComparisonDirection.Equal)
                delta = 0f;

            rows.Add(
                new MainStatComparisonResult(
                    statType,
                    candidateValue,
                    equippedValue,
                    delta,
                    direction));
        }

        result = new ItemMainStatComparisonResult(
            candidateItem,
            equippedItem,
            rows.ToArray());

        return true;
    }

    private static bool IsValid(ItemInstance item)
    {
        return item != null && item.definition != null;
    }

    /// <summary>
    /// GetEffectiveMainOptions()를 사용하므로 강화 레벨이 반영된 값이다.
    ///
    /// 같은 StatType이 여러 번 들어 있다면 하나로 합산한다.
    /// </summary>
    private static Dictionary<StatType, float> BuildMainStatTotals(
        ItemInstance item,
        List<StatType> orderedStatTypes,
        HashSet<StatType> discoveredStatTypes)
    {
        var totals = new Dictionary<StatType, float>();

        List<RolledSubStat> mainOptions =
            item.GetEffectiveMainOptions();

        foreach (RolledSubStat option in mainOptions)
        {
            if (option == null)
                continue;

            if (totals.TryGetValue(
                    option.statType,
                    out float currentValue))
            {
                totals[option.statType] =
                    currentValue + option.value;
            }
            else
            {
                totals.Add(option.statType, option.value);
            }

            if (discoveredStatTypes.Add(option.statType))
                orderedStatTypes.Add(option.statType);
        }

        return totals;
    }

    private static float GetValueOrZero(
        Dictionary<StatType, float> values,
        StatType statType)
    {
        return values.TryGetValue(
            statType,
            out float value)
            ? value
            : 0f;
    }

    private static MainStatComparisonDirection GetDirection(
        float delta)
    {
        if (Math.Abs(delta) <= ComparisonEpsilon)
            return MainStatComparisonDirection.Equal;

        return delta > 0f
            ? MainStatComparisonDirection.Increase
            : MainStatComparisonDirection.Decrease;
    }
}
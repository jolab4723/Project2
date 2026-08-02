using System;
using System.Collections.Generic;
using ItemSystem;

/// <summary>
/// 후보 장비 착용 후의 최종 스탯이 현재 최종 스탯보다
/// 증가하는지, 감소하는지, 동일한지를 나타낸다.
/// </summary>
public enum MainStatComparisonDirection
{
    Decrease = -1,
    Equal = 0,
    Increase = 1
}

/// <summary>
/// 장비 슬롯에 대응하는 최종 스탯 한 종류의 비교 결과를 보관한다.
/// 스탯 종류와 두 값의 차이만 제공하며 UI 문자열과 색상은 결정하지 않는다.
/// </summary>
public sealed class MainStatComparisonResult
{
    public StatType StatType { get; }
    public float Delta { get; }
    public MainStatComparisonDirection Direction { get; }

    public MainStatComparisonResult(
        StatType statType,
        float delta,
        MainStatComparisonDirection direction)
    {
        StatType = statType;
        Delta = delta;
        Direction = direction;
    }
}

/// <summary>
/// 후보 아이템과 현재 장착 아이템 사이의 최종 스탯 비교 결과 묶음이다.
/// 현재 정책은 슬롯마다 대표 최종 스탯 한 행만 생성한다.
/// </summary>
public sealed class ItemMainStatComparisonResult
{
    public ItemInstance CandidateItem { get; }
    public ItemInstance EquippedItem { get; }
    public IReadOnlyList<MainStatComparisonResult> Rows { get; }

    public bool HasComparableStats => Rows.Count > 0;

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
/// 실제 장비 상태를 변경하지 않고 후보 아이템 착용 전후의 최종 스탯을 비교한다.
/// 무기는 공격력, 투구와 상의는 방어력을 대표값으로 사용한다.
/// 부츠는 후보 아이템의 메인 옵션에 맞춰 이동속도 또는 방어력을 선택한다.
///
/// 이 클래스는 다음 책임을 갖지 않는다.
/// - 장비 슬롯 판정
/// - 현재 장비 조회
/// - 플레이어 최종 스탯 계산 공식
/// - UI 문자열 생성
/// - 색상 결정
/// </summary>
public static class ItemMainStatComparer
{
    /// <summary>
    /// 부동소수점 계산 과정에서 생기는 미세한 오차를 동일 값으로 처리하기 위한 허용 범위다.
    /// </summary>
    private const float ComparisonEpsilon = 0.0001f;

    /// <summary>
    /// 현재 장비 구성과 해당 슬롯을 후보 아이템으로 교체한 구성을 각각 계산한 뒤,
    /// 슬롯에 맞는 대표 최종 스탯의 값과 증감량을 비교 결과로 만든다.
    /// 실제 장비 데이터와 플레이어의 현재 스탯은 변경하지 않는다.
    /// </summary>
    /// <returns>입력과 슬롯이 유효하고 두 구성의 최종 스탯을 계산했으면 true.</returns>
    public static bool TryCompare(
        ItemInstance candidateItem,
        ItemInstance equippedItem,
        EquipSlotType comparisonSlot,
        PlayerStatManager playerStatManager,
        out ItemMainStatComparisonResult result)
    {
        result = null;

        if (!IsValid(candidateItem) || !IsValid(equippedItem) || playerStatManager == null)
            return false;

        if (!playerStatManager.TryCalculateStatsAfterReplacing(
                comparisonSlot,
                candidateItem,
                out PlayerStat currentStats,
                out PlayerStat candidateStats))
        {
            return false;
        }

        StatType displayStatType;
        float currentValue;
        float candidateValue;

        switch (comparisonSlot)
        {
            case EquipSlotType.Weapon:
                displayStatType = StatType.attackPowerFlat;
                currentValue = currentStats.attackPower;
                candidateValue = candidateStats.attackPower;
                break;

            case EquipSlotType.Helmet:
            case EquipSlotType.Chest:
                displayStatType = StatType.defensePowerFlat;
                currentValue = currentStats.defensePower;
                candidateValue = candidateStats.defensePower;
                break;

            case EquipSlotType.Boots:
                if (UsesMoveSpeedAsMainStat(candidateItem))
                {
                    displayStatType = StatType.moveSpeedFlat;
                    currentValue = currentStats.moveSpeed;
                    candidateValue = candidateStats.moveSpeed;
                }
                else
                {
                    displayStatType = StatType.defensePowerFlat;
                    currentValue = currentStats.defensePower;
                    candidateValue = candidateStats.defensePower;
                }
                break;

            default:
                return false;
        }

        float delta = candidateValue - currentValue;

        MainStatComparisonDirection direction = GetDirection(delta);

        if (direction == MainStatComparisonDirection.Equal)
            delta = 0f;

        var row =
            new MainStatComparisonResult(
                displayStatType,
                delta,
                direction);

        result =
            new ItemMainStatComparisonResult(
                candidateItem,
                equippedItem,
                new[] { row });

        return true;
    }

    /// <summary>
    /// 비교에 필요한 아이템 인스턴스와 원본 정의가 모두 존재하는지 확인한다.
    /// </summary>
    private static bool IsValid(ItemInstance item)
    {
        return item != null && item.definition != null;
    }

    /// <summary>
    /// 부츠의 강화 적용 메인 옵션에 이동속도 고정값 또는 비율값이 있는지 확인한다.
    /// 이동속도형 부츠와 방어력형 부츠가 함께 존재하므로 슬롯만으로 비교 스탯을 고정하지 않는다.
    /// </summary>
    private static bool UsesMoveSpeedAsMainStat(ItemInstance item)
    {
        if (!IsValid(item))
            return false;

        foreach (RolledSubStat option in item.GetEffectiveMainOptions())
        {
            if (option == null)
                continue;

            if (option.statType == StatType.moveSpeedFlat ||
                option.statType == StatType.moveSpeedPercent)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 계산된 차이를 허용 오차와 비교해 증가, 감소, 동일 중 하나로 분류한다.
    /// </summary>
    private static MainStatComparisonDirection GetDirection(float delta)
    {
        if (Math.Abs(delta) <= ComparisonEpsilon)
            return MainStatComparisonDirection.Equal;

        return delta > 0f
            ? MainStatComparisonDirection.Increase
            : MainStatComparisonDirection.Decrease;
    }
}

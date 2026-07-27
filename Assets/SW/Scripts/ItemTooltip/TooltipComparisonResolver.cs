using ItemSystem;

/// <summary>
/// 후보 아이템과 비교해야 할 현재 장비를 찾고,
/// ItemMainStatComparer를 호출하는 연결 계층.
/// </summary>
public static class TooltipComparisonResolver
{
    /// <summary>
    /// 후보 아이템과 동일한 장비 슬롯의 현재 장비를 찾아
    /// 메인 스탯 비교 결과를 만든다.
    ///
    /// false가 반환되는 경우:
    /// - 후보 아이템이 유효하지 않음
    /// - 무기나 방어구가 아님
    /// - 현재 장착된 동일 부위 장비가 없음
    /// - 후보와 현재 장비가 같은 아이템임
    /// - 비교 가능한 메인 스탯이 없음
    /// </summary>
    public static bool TryResolveComparison(
        ItemInstance candidateItem,
        EquipmentSystem equipmentSystem,
        out ItemMainStatComparisonResult comparisonResult)
    {
        comparisonResult = null;

        if (candidateItem == null ||
            candidateItem.definition == null)
        {
            return false;
        }

        if (equipmentSystem == null)
            return false;

        if (!EquipSlotRules.TryGetComparisonSlot(
                candidateItem.definition,
                out EquipSlotType comparisonSlot))
        {
            return false;
        }

        if (!equipmentSystem.TryGetEquippedItemInstance(
                comparisonSlot,
                out ItemInstance equippedItem))
        {
            return false;
        }

        if (IsSameItem(candidateItem, equippedItem))
            return false;

        if (!ItemMainStatComparer.TryCompare(
                candidateItem,
                equippedItem,
                out comparisonResult))
        {
            comparisonResult = null;
            return false;
        }

        if (!comparisonResult.HasComparableStats)
        {
            comparisonResult = null;
            return false;
        }

        return true;
    }

    private static bool IsSameItem(
        ItemInstance first,
        ItemInstance second)
    {
        if (ReferenceEquals(first, second))
            return true;

        if (first == null || second == null)
            return false;

        if (string.IsNullOrEmpty(first.instanceId) ||
            string.IsNullOrEmpty(second.instanceId))
        {
            return false;
        }

        return first.instanceId == second.instanceId;
    }
}
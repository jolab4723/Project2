using System.Collections.Generic;
using System.Linq;
using ItemSystem;

// 버프 목록 정렬 규칙
public static class KY_BuffSortRule
{
    public static List<BuffInstance> Sort(IEnumerable<BuffInstance> buffs)
    {
        // 현재 규칙: 디버프 먼저, 그다음 일반 버프. 각 그룹 내에서는 원래 순서 유지.
        return buffs
            .OrderBy(b => IsDebuff(b) ? 0 : 1)
            .ToList();
    }

    static bool IsDebuff(BuffInstance buff)
    {
        // 임시
        return false;
    }
}
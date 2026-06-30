namespace ItemSystem
{
    /// <summary>
    /// 마스터 변수 시트 기준 스탯 목록.
    /// 이름 끝의 Flat/Percent 접미사로 수치 타입을 자동 판별하므로,
    /// 새 스탯을 추가할 때도 이 접미사 규칙만 지키면 됨.
    /// </summary>
    public enum StatType
    {
        healthFlat,
        healthPercent,
        attackPowerFlat,
        attackPowerPercent,
        defensePowerFlat,
        defensePowerPercent,
        moveSpeedFlat,
        moveSpeedPercent,
        attackSpeedFlat,
        attackSpeedPercent,
        critRateFlat,
        critMultFlat,
        cdrFlat,
        mpRegenFlat,
        mpRegenPercent,
        penetrationFlat,
        skillRangeFlat,
        fireBonusFlat,
        iceBonusFlat,
        electricBonusFlat,
    }

    public static class StatTypeUtility
    {
        public static bool IsPercent(StatType type) => type.ToString().EndsWith("Percent");
    }
}

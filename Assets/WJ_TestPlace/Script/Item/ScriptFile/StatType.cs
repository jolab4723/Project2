namespace ItemSystem
{
    /// <summary>
    /// 마스터 변수 시트 기준 스탯 목록.
    /// 이름 끝의 Flat/Percent 접미사로 수치 타입을 자동 판별하므로,
    /// 새 스탯을 추가할 때도 이 접미사 규칙만 지키면 됨.
    ///
    /// !! 새 값을 추가할 때는 반드시 맨 끝에 추가할 것.
    /// Unity는 enum을 정수(선언 순서)로 직렬화하기 때문에, 중간에 끼워넣으면
    /// 그 뒤 값들의 정수가 하나씩 밀리면서 이미 저장된 에셋(ItemDefinitionSO,
    /// SubStatPoolSO 등)의 StatType 참조가 전부 엉뚱한 값으로 바뀐다.
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
        mpMaxFlat, // 마나 최대량. 기존 값 정수 안 밀리게 맨 끝에 추가.
    }

    public static class StatTypeUtility
    {
        public static bool IsPercent(StatType type) => type.ToString().EndsWith("Percent");
    }
}

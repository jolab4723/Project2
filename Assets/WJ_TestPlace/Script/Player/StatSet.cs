using System;

/// <summary>
/// 장비 또는 버프 한 레이어에서 발생하는 스탯 가산치를 담는 구조체.
/// Flat: 고정값 가산 / Percent: 비율 가산 (3 = +3%, 소수가 아니라 퍼센트 숫자 그대로. PlayerStat.CalcFinal에서 100으로 나눠 적용함)
/// 여러 StatSet을 + 연산자로 합산하여 레이어별 총합을 구할 수 있음.
/// ※ 추정 코드이므로 실제 기존 구현과 필드/방식이 다를 수 있습니다.
/// </summary>
[Serializable]
public struct StatSet
{
    // 기본 전투 스탯
    public float maxHealthFlat;
    public float maxHealthPercent;

    public float attackPowerFlat;
    public float attackPowerPercent;

    public float defensePowerFlat;
    public float defensePowerPercent;

    // 속도 계열
    public float moveSpeedFlat;
    public float moveSpeedPercent;

    public float attackSpeedFlat;
    public float attackSpeedPercent;

    // 치명타 계열
    public float critRateFlat;   // 플랫 합연산 전용 (Percent 레이어 미적용)
    public float critMultFlat;
    public float critMultPercent;

    // 쿨타임/자원
    public float cdrFlat;        // 플랫 합연산 전용 (Percent 레이어 미적용)
    public float mpRegenFlat;
    public float mpRegenPercent;
    public float maxManaFlat;    // PlayerStatManager 작업 때 추가 (StatType.mpMaxFlat 대응). 단순 합산이라 Percent 없음.

    // 관통/범위
    public float penFlat;
    public float penPercent;

    public float skillRangeFlat;
    public float skillRangePercent;

    // 피해 배율 계열 - 기준값이 없는 순수 증감이라 Percent만 있다.
    // 일반공격(WBH_AttackType.Normal)과 스킬(Skill)에 각각 적용된다.
    public float normalDamagePercent;
    public float skillDamagePercent;

    // 속성 보너스
    public float fireBonusFlat;
    public float fireBonusPercent;

    public float iceBonusFlat;
    public float iceBonusPercent;

    public float electricBonusFlat;
    public float electricBonusPercent;

    public static StatSet Zero => new StatSet();

    public static StatSet operator +(StatSet a, StatSet b)
    {
        return new StatSet
        {
            maxHealthFlat = a.maxHealthFlat + b.maxHealthFlat,
            maxHealthPercent = a.maxHealthPercent + b.maxHealthPercent,

            attackPowerFlat = a.attackPowerFlat + b.attackPowerFlat,
            attackPowerPercent = a.attackPowerPercent + b.attackPowerPercent,

            defensePowerFlat = a.defensePowerFlat + b.defensePowerFlat,
            defensePowerPercent = a.defensePowerPercent + b.defensePowerPercent,

            moveSpeedFlat = a.moveSpeedFlat + b.moveSpeedFlat,
            moveSpeedPercent = a.moveSpeedPercent + b.moveSpeedPercent,

            attackSpeedFlat = a.attackSpeedFlat + b.attackSpeedFlat,
            attackSpeedPercent = a.attackSpeedPercent + b.attackSpeedPercent,

            critRateFlat = a.critRateFlat + b.critRateFlat,
            critMultFlat = a.critMultFlat + b.critMultFlat,
            critMultPercent = a.critMultPercent + b.critMultPercent,

            cdrFlat = a.cdrFlat + b.cdrFlat,
            mpRegenFlat = a.mpRegenFlat + b.mpRegenFlat,
            mpRegenPercent = a.mpRegenPercent + b.mpRegenPercent,
            maxManaFlat = a.maxManaFlat + b.maxManaFlat,

            penFlat = a.penFlat + b.penFlat,
            penPercent = a.penPercent + b.penPercent,

            skillRangeFlat = a.skillRangeFlat + b.skillRangeFlat,
            skillRangePercent = a.skillRangePercent + b.skillRangePercent,

            normalDamagePercent = a.normalDamagePercent + b.normalDamagePercent,
            skillDamagePercent = a.skillDamagePercent + b.skillDamagePercent,

            fireBonusFlat = a.fireBonusFlat + b.fireBonusFlat,
            fireBonusPercent = a.fireBonusPercent + b.fireBonusPercent,

            iceBonusFlat = a.iceBonusFlat + b.iceBonusFlat,
            iceBonusPercent = a.iceBonusPercent + b.iceBonusPercent,

            electricBonusFlat = a.electricBonusFlat + b.electricBonusFlat,
            electricBonusPercent = a.electricBonusPercent + b.electricBonusPercent,
        };
    }
}
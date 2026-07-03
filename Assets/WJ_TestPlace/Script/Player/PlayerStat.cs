using System;
using UnityEngine;

/// <summary>
/// 플레이어의 "최종" 스탯 값만 들고 있는 컨테이너 클래스.
/// 실제 계산 로직(데미지 등)은 포함하지 않으며, 외부(장비/레벨업/버프 시스템)에서
/// Recalculate()를 호출해주면 3단 공식으로 최종값을 갱신하고 OnStatChanged를 발행한다.
///
/// ※ 추정 코드 - 아래 가정이 실제와 다를 수 있습니다:
///   1) "캐릭터" 레이어는 StatSet이 아니라 Flat 필드만 사용한다고 가정 (Percent 없음)
///   2) 장비/버프 레이어는 여러 소스를 미리 합산한 StatSet 하나씩으로 전달받는다고 가정
///      (개별 장비 N개를 합산하는 책임은 EquipmentManager 등 외부에 있다고 가정)
///   3) 구독 방식은 아직 실제 이벤트 소스(장비/레벨/버프 매니저)를 몰라서,
///      외부에서 Recalculate()를 호출해주는 형태로 작성. 실제 매니저 클래스가 정해지면
///      그 클래스의 이벤트에 OnEquipmentChanged 등을 연결하면 됨.
/// </summary>
[Serializable]
public class PlayerStat
{
    // ----- 기본 (직접값, 공식 미적용) -----
    public int currentHealth;
    public int currentLevel;
    public float currentExp;

    // ----- 최종 (캐릭터+장비+버프 3단 공식 합산 결과) -----
    public int maxHealth;
    public int attackPower;
    public int defensePower;
    public float moveSpeed;
    public float attackSpeed;
    public float critRate;   // 플랫 합연산 전용, 0~100 클램프
    public float critMult;
    public float cdr;        // 플랫 합연산 전용, 0~70 클램프
    public float mpRegen;
    public int pen;
    public float skillRange;
    public float fireBonus;
    public float iceBonus;
    public float electricBonus;

    private const float CritRateMin = 0f;
    private const float CritRateMax = 100f;
    private const float CdrMin = 0f;
    private const float CdrMax = 70f;

    /// <summary>스탯이 갱신될 때마다 발행. UI 등에서 구독해서 갱신.</summary>
    public event Action OnStatChanged;

    public PlayerStat(int startLevel = 1, int startHealth = 0, float startExp = 0f)
    {
        currentLevel = startLevel;
        currentHealth = startHealth;
        currentExp = startExp;
    }

    /// <summary>
    /// 장비 변경 / 레벨 업 / 버프 발생 시 외부에서 호출.
    /// character: 캐릭터 기본 스탯(Flat만 사용), equipment: 장비 레이어 합산값, buff: 버프 레이어 합산값
    /// </summary>
    public void Recalculate(StatSet character, StatSet equipment, StatSet buff)
    {
        maxHealth = Mathf.RoundToInt(CalcFinal(
            character.maxHealthFlat, equipment.maxHealthFlat, equipment.maxHealthPercent,
            buff.maxHealthPercent, buff.maxHealthFlat));

        attackPower = Mathf.RoundToInt(CalcFinal(
            character.attackPowerFlat, equipment.attackPowerFlat, equipment.attackPowerPercent,
            buff.attackPowerPercent, buff.attackPowerFlat));

        defensePower = Mathf.RoundToInt(CalcFinal(
            character.defensePowerFlat, equipment.defensePowerFlat, equipment.defensePowerPercent,
            buff.defensePowerPercent, buff.defensePowerFlat));

        moveSpeed = CalcFinal(
            character.moveSpeedFlat, equipment.moveSpeedFlat, equipment.moveSpeedPercent,
            buff.moveSpeedPercent, buff.moveSpeedFlat);

        attackSpeed = CalcFinal(
            character.attackSpeedFlat, equipment.attackSpeedFlat, equipment.attackSpeedPercent,
            buff.attackSpeedPercent, buff.attackSpeedFlat);

        // critRate, cdr: 3단 공식 미적용. 모든 소스의 Flat을 단순 합산 후 클램프.
        critRate = Mathf.Clamp(
            character.critRateFlat + equipment.critRateFlat + buff.critRateFlat,
            CritRateMin, CritRateMax);

        cdr = Mathf.Clamp(
            character.cdrFlat + equipment.cdrFlat + buff.cdrFlat,
            CdrMin, CdrMax);

        critMult = CalcFinal(
            character.critMultFlat, equipment.critMultFlat, equipment.critMultPercent,
            buff.critMultPercent, buff.critMultFlat);

        mpRegen = CalcFinal(
            character.mpRegenFlat, equipment.mpRegenFlat, equipment.mpRegenPercent,
            buff.mpRegenPercent, buff.mpRegenFlat);

        pen = Mathf.RoundToInt(CalcFinal(
            character.penFlat, equipment.penFlat, equipment.penPercent,
            buff.penPercent, buff.penFlat));

        skillRange = CalcFinal(
            character.skillRangeFlat, equipment.skillRangeFlat, equipment.skillRangePercent,
            buff.skillRangePercent, buff.skillRangeFlat);

        fireBonus = CalcFinal(
            character.fireBonusFlat, equipment.fireBonusFlat, equipment.fireBonusPercent,
            buff.fireBonusPercent, buff.fireBonusFlat);

        iceBonus = CalcFinal(
            character.iceBonusFlat, equipment.iceBonusFlat, equipment.iceBonusPercent,
            buff.iceBonusPercent, buff.iceBonusFlat);

        electricBonus = CalcFinal(
            character.electricBonusFlat, equipment.electricBonusFlat, equipment.electricBonusPercent,
            buff.electricBonusPercent, buff.electricBonusFlat);

        // maxHealth가 줄어들어 currentHealth가 초과 상태가 되지 않도록 보정
        if (currentHealth > maxHealth)
            currentHealth = maxHealth;

        OnStatChanged?.Invoke();
    }

    /// <summary>3단 공식: (캐릭터 + 장비고정) × (1+장비%) × (1+버프%) + 버프고정</summary>
    private static float CalcFinal(float characterFlat, float equipFlat, float equipPercent, float buffPercent, float buffFlat)
    {
        return (characterFlat + equipFlat) * (1f + equipPercent) * (1f + buffPercent) + buffFlat;
    }

    // ----- 구독용 핸들러 예시 (실제 매니저 이벤트 시그니처에 맞춰 연결 필요) -----
    public void OnEquipmentChanged(StatSet character, StatSet equipment, StatSet buff) => Recalculate(character, equipment, buff);
    public void OnLevelUp(StatSet character, StatSet equipment, StatSet buff) => Recalculate(character, equipment, buff);
    public void OnBuffApplied(StatSet character, StatSet equipment, StatSet buff) => Recalculate(character, equipment, buff);

    public void GainExp(float amount)
    {
        currentExp += amount;
        OnStatChanged?.Invoke();
    }

    public void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnStatChanged?.Invoke();
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnStatChanged?.Invoke();
    }
}

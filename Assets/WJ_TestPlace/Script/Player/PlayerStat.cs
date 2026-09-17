using System;
using UnityEngine;

/// <summary>
/// 플레이어의 "최종" 스탯 값만 들고 있는 컨테이너 클래스.
/// 실제 계산 로직(데미지 등)은 포함하지 않으며, 외부(장비/레벨업/버프 시스템)에서
/// Recalculate()를 호출해주면 3단 공식으로 최종값을 갱신하고 OnStatChanged를 발행한다.
///
/// !! maxHealth/attackPower/defensePower/maxMana/pen은 원래 int였는데 float로 전환함.
///    (크레딧/레벨 제외 전부 float로 통일) 계산 결과 자체는 Mathf.Ceil로 올림 처리해서
///    항상 정수 값을 갖지만, 타입은 float라 나중에 소수 보너스가 들어와도 안전함.
/// </summary>
[Serializable]
public class PlayerStat
{
    // ----- 기본 (직접값, 공식 미적용) -----
    public int currentLevel; // 레벨은 이산값이라 그대로 int
    public float currentExp;

    // ----- 최종 (캐릭터+장비+버프 3단 공식 합산 결과) -----
    public float maxHealth;
    public float attackPower;
    public float defensePower;
    public float moveSpeed;
    public float attackSpeed;
    public float critRate;   // 플랫 합연산 전용, 0~100 클램프
    public float critMult;
    public float cdr;        // 플랫 합연산 전용, 0~70 클램프
    public float mpRegen;
    public float maxMana;
    public float pen;
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

    /// <summary>
    /// 외부 상태 소유자가 최종 수치를 일괄 반영한 뒤 변경을 한 번 알린다.
    /// 수치를 다시 계산하거나 경험치·패시브를 변경하지 않는다.
    /// </summary>
    public void NotifyValuesChanged()
    {
        OnStatChanged?.Invoke();
    }

    public PlayerStat(int startLevel = 1, float startExp = 0f)
    {
        currentLevel = startLevel;
        currentExp = startExp;
    }

    /// <summary>
    /// 장비 변경 / 레벨 업 / 버프 발생 / 패시브 스킬 변경 시 외부에서 호출.
    /// character: 캐릭터 기본 스탯(Flat만 사용), equipment: 장비 레이어 합산값, buff: 버프 레이어 합산값,
    /// passive: 패시브 스킬트리 레이어 합산값(PassiveSkillManager.GetStatSet())
    /// </summary>
    public void Recalculate(StatSet character, StatSet equipment, StatSet buff, StatSet passive)
    {
        maxHealth = Mathf.Max(0f, Mathf.Ceil(CalcFinal(
            character.maxHealthFlat, equipment.maxHealthFlat, equipment.maxHealthPercent,
            buff.maxHealthPercent, buff.maxHealthFlat, passive.maxHealthPercent, passive.maxHealthFlat)));

        attackPower = Mathf.Max(0f, Mathf.Ceil(CalcFinal(
            character.attackPowerFlat, equipment.attackPowerFlat, equipment.attackPowerPercent,
            buff.attackPowerPercent, buff.attackPowerFlat, passive.attackPowerPercent, passive.attackPowerFlat)));

        defensePower = Mathf.Max(0f, Mathf.Ceil(CalcFinal(
            character.defensePowerFlat, equipment.defensePowerFlat, equipment.defensePowerPercent,
            buff.defensePowerPercent, buff.defensePowerFlat, passive.defensePowerPercent, passive.defensePowerFlat)));

        moveSpeed = Mathf.Max(0f, CalcFinal(
            character.moveSpeedFlat, equipment.moveSpeedFlat, equipment.moveSpeedPercent,
            buff.moveSpeedPercent, buff.moveSpeedFlat, passive.moveSpeedPercent, passive.moveSpeedFlat));

        attackSpeed = Mathf.Max(0f, CalcFinal(
            character.attackSpeedFlat, equipment.attackSpeedFlat, equipment.attackSpeedPercent,
            buff.attackSpeedPercent, buff.attackSpeedFlat, passive.attackSpeedPercent, passive.attackSpeedFlat));

        // critRate, cdr: 3단 공식 미적용. 모든 소스의 Flat을 단순 합산 후 클램프.
        critRate = Mathf.Clamp(
            character.critRateFlat + equipment.critRateFlat + buff.critRateFlat + passive.critRateFlat,
            CritRateMin, CritRateMax);

        cdr = Mathf.Clamp(
            character.cdrFlat + equipment.cdrFlat + buff.cdrFlat + passive.cdrFlat,
            CdrMin, CdrMax);

        critMult = Mathf.Max(0f, CalcFinal(
            character.critMultFlat, equipment.critMultFlat, equipment.critMultPercent,
            buff.critMultPercent, buff.critMultFlat, passive.critMultPercent, passive.critMultFlat));

        mpRegen = Mathf.Max(0f, CalcFinal(
            character.mpRegenFlat, equipment.mpRegenFlat, equipment.mpRegenPercent,
            buff.mpRegenPercent, buff.mpRegenFlat, passive.mpRegenPercent, passive.mpRegenFlat));

        // maxMana: 3단 공식 미적용. mpMaxFlat 계열은 단순 합산.
        maxMana = Mathf.Max(0f, Mathf.Ceil(character.maxManaFlat + equipment.maxManaFlat + buff.maxManaFlat + passive.maxManaFlat));

        pen = Mathf.Max(0f, Mathf.Ceil(CalcFinal(
            character.penFlat, equipment.penFlat, equipment.penPercent,
            buff.penPercent, buff.penFlat, passive.penPercent, passive.penFlat)));

        skillRange = Mathf.Max(0f, CalcFinal(
            character.skillRangeFlat, equipment.skillRangeFlat, equipment.skillRangePercent,
            buff.skillRangePercent, buff.skillRangeFlat, passive.skillRangePercent, passive.skillRangeFlat));

        fireBonus = Mathf.Max(0f, CalcFinal(
            character.fireBonusFlat, equipment.fireBonusFlat, equipment.fireBonusPercent,
            buff.fireBonusPercent, buff.fireBonusFlat, passive.fireBonusPercent, passive.fireBonusFlat));

        iceBonus = Mathf.Max(0f, CalcFinal(
            character.iceBonusFlat, equipment.iceBonusFlat, equipment.iceBonusPercent,
            buff.iceBonusPercent, buff.iceBonusFlat, passive.iceBonusPercent, passive.iceBonusFlat));

        electricBonus = Mathf.Max(0f, CalcFinal(
            character.electricBonusFlat, equipment.electricBonusFlat, equipment.electricBonusPercent,
            buff.electricBonusPercent, buff.electricBonusFlat, passive.electricBonusPercent, passive.electricBonusFlat));

        OnStatChanged?.Invoke();
    }

    /// <summary>
    /// 스탯 최종값. 두 단계로 나눠서 계산한다.
    ///
    ///   기본 스펙 = (캐릭터 + 장비고정) × (1+장비%) × (1+패시브%) + 패시브고정
    ///   최종      = 기본 스펙 × (1+버프%) + 버프고정
    ///
    /// "레벨 + 장비 + 패시브 = 내 기본 스펙, 버프는 그 위에 얹히는 일시 효과"라는 개념을 그대로 옮긴 것이다.
    /// 예전엔 버프%와 패시브%를 같은 자리에서 연달아 곱했는데, 곱셈은 순서를 바꿔도 결과가 같아서
    /// **퍼센트만 있는 스탯(공격력/체력/방어력/속도 등)은 수치가 한 자리도 바뀌지 않는다.**
    /// 달라지는 건 패시브 **고정값**뿐이다 - 예전엔 맨 끝에 더해져 버프 %가 안 곱해졌는데,
    /// 이제 기본 스펙에 포함되므로 버프 %의 영향을 받는다(치명타 배율·속성 보너스가 해당).
    ///
    /// !! equipPercent/buffPercent/passivePercent는 "3"이 오면 3%를 의미하는 퍼센트 숫자 그대로다
    /// (0.03 같은 소수 분수가 아님 - 아이템 서브옵션/툴팁 표시와 동일한 스케일). 그래서 여기서 100으로 나눈다.
    /// </summary>
    private static float CalcFinal(float characterFlat, float equipFlat, float equipPercent, float buffPercent, float buffFlat, float passivePercent, float passiveFlat)
    {
        float baseSpec = CalcBaseSpec(characterFlat, equipFlat, equipPercent, passivePercent, passiveFlat);
        return baseSpec * (1f + buffPercent / 100f) + buffFlat;
    }

    /// <summary>버프를 뺀 "내 기본 스펙"(레벨 + 장비 + 패시브). 스탯 UI의 기본값 칸도 이 값을 쓴다.</summary>
    private static float CalcBaseSpec(float characterFlat, float equipFlat, float equipPercent, float passivePercent, float passiveFlat)
    {
        return (characterFlat + equipFlat) * (1f + equipPercent / 100f) * (1f + passivePercent / 100f) + passiveFlat;
    }

    /// <summary>
    /// UI에서 스탯 한 줄을 "캐릭터/장비/버프" 3단으로 나눠 보여줄 때 쓰는 분해 계산.
    /// 장비%/패시브%/버프%가 곱연산으로 얽혀 있어 레이어별 기여분을 딱 나눌 수 없으므로,
    /// 레이어를 하나씩 순서대로 켜가며 그 차이를 해당 레이어의 몫으로 본다(텔레스코핑 방식).
    ///
    /// !! 패시브는 **버프가 아니라 캐릭터(기본값) 몫에 포함**한다. 패시브 스킬트리는 세이브 프로필에
    ///    붙은 영구 스펙이라 "레벨 + 장비 + 패시브 = 내 기본 스펙"으로 보는 게 맞다.
    ///    예전엔 UI가 3단뿐이라는 이유로 패시브를 버프 몫에 합쳐 넣었는데, 패시브 스킬트리가
    ///    실제 값을 내기 시작하면서 **아이템을 다 벗어도 "버프로 공격력 증가"가 뜨는** 문제가 됐다.
    ///
    /// base+equip+buff를 더하면 항상 CalcFinal(전체)와 정확히 같다.
    /// </summary>
    public static void CalcBreakdown(
        float characterFlat, float equipFlat, float equipPercent,
        float buffPercent, float buffFlat, float passivePercent, float passiveFlat,
        out float baseValue, out float equipValue, out float buffValue)
    {
        CalcBreakdown(characterFlat, equipFlat, equipPercent, buffPercent, buffFlat, passivePercent, passiveFlat,
            out baseValue, out equipValue, out float passiveValue, out buffValue);

        // 3단만 쓰는 화면(미러 테스트 등)에서는 패시브를 캐릭터 몫에 합쳐 보여준다.
        baseValue += passiveValue;
    }

    /// <summary>
    /// 캐릭터/장비/패시브/버프 4단 분해. 스탯 상세 화면이 네 갈래를 각각 다른 색으로 보여줄 때 쓴다.
    /// 텔레스코핑 순서는 실제 계산 순서(캐릭터 → 패시브 → 장비 → 버프)를 따르며,
    /// base+equip+passive+buff를 더하면 항상 CalcFinal(전체)와 정확히 같다.
    /// </summary>
    public static void CalcBreakdown(
        float characterFlat, float equipFlat, float equipPercent,
        float buffPercent, float buffFlat, float passivePercent, float passiveFlat,
        out float baseValue, out float equipValue, out float passiveValue, out float buffValue)
    {
        baseValue = characterFlat;
        float withPassive = CalcBaseSpec(characterFlat, 0f, 0f, passivePercent, passiveFlat);
        float withEquip = CalcBaseSpec(characterFlat, equipFlat, equipPercent, passivePercent, passiveFlat);
        float final = CalcFinal(characterFlat, equipFlat, equipPercent, buffPercent, buffFlat, passivePercent, passiveFlat);

        passiveValue = withPassive - baseValue;
        equipValue = withEquip - withPassive;
        buffValue = final - withEquip;
    }

    /// <summary>
    /// critRate/cdr처럼 퍼센트 배율 없이 Flat만 클램프해서 쓰는 스탯의 3단 분해.
    /// 클램프 때문에 캐릭터+장비 단계에서 이미 상한/하한에 걸리면 버프 몫이 0으로 보일 수 있는데,
    /// 이는 실제로 클램프에 막혀 화면에 반영되지 않는 값이라 의도된 동작이다.
    /// </summary>
    public static void CalcBreakdownClampedFlat(
        float characterFlat, float equipFlat, float buffFlat, float passiveFlat,
        float min, float max,
        out float baseValue, out float equipValue, out float buffValue)
    {
        CalcBreakdownClampedFlat(characterFlat, equipFlat, buffFlat, passiveFlat, min, max,
            out baseValue, out equipValue, out float passiveValue, out buffValue);

        baseValue += passiveValue;
    }

    /// <summary>critRate/cdr처럼 Flat만 합산·클램프하는 스탯의 4단 분해.</summary>
    public static void CalcBreakdownClampedFlat(
        float characterFlat, float equipFlat, float buffFlat, float passiveFlat,
        float min, float max,
        out float baseValue, out float equipValue, out float passiveValue, out float buffValue)
    {
        baseValue = Mathf.Clamp(characterFlat, min, max);
        float withPassive = Mathf.Clamp(characterFlat + passiveFlat, min, max);
        float withEquip = Mathf.Clamp(characterFlat + passiveFlat + equipFlat, min, max);
        float final = Mathf.Clamp(characterFlat + passiveFlat + equipFlat + buffFlat, min, max);

        passiveValue = withPassive - baseValue;
        equipValue = withEquip - withPassive;
        buffValue = final - withEquip;
    }

    // ----- 구독용 핸들러 예시 (실제 매니저 이벤트 시그니처에 맞춰 연결 필요) -----
    public void OnEquipmentChanged(StatSet character, StatSet equipment, StatSet buff, StatSet passive) => Recalculate(character, equipment, buff, passive);
    public void OnLevelUp(StatSet character, StatSet equipment, StatSet buff, StatSet passive) => Recalculate(character, equipment, buff, passive);
    public void OnBuffApplied(StatSet character, StatSet equipment, StatSet buff, StatSet passive) => Recalculate(character, equipment, buff, passive);

    public void GainExp(float amount)
    {
        currentExp += amount;
        OnStatChanged?.Invoke();
    }
}

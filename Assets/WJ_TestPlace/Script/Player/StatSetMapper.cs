using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// StatType 값 하나를 StatSet의 대응 필드에 더하는 공용 로직.
    /// PlayerEquipManager, PlayerBuffManager 등 "스탯을 합산해서 StatSet으로 내보내는" 소스들이
    /// 전부 이걸 공유해서, 매핑 규칙이 여러 군데 중복되지 않게 한다.
    ///
    /// !! StatType과 StatSet의 필드 이름/구성이 완전히 1:1은 아니라서 명시적으로 매핑함:
    ///    - healthFlat/Percent -> maxHealthFlat/Percent (이름만 다름, 같은 의미)
    ///    - penetrationFlat -> penFlat (StatType엔 Percent 버전이 없음)
    ///    - mpMaxFlat -> maxManaFlat (StatSet에 원래 없어서 PlayerStatManager 작업 때 추가함)
    /// </summary>
    public static class StatSetMapper
    {
        public static void AddStat(ref StatSet s, StatType type, float value)
        {
            switch (type)
            {
                case StatType.healthFlat: s.maxHealthFlat += value; break;
                case StatType.healthPercent: s.maxHealthPercent += value; break;
                case StatType.attackPowerFlat: s.attackPowerFlat += value; break;
                case StatType.attackPowerPercent: s.attackPowerPercent += value; break;
                case StatType.defensePowerFlat: s.defensePowerFlat += value; break;
                case StatType.defensePowerPercent: s.defensePowerPercent += value; break;
                case StatType.moveSpeedFlat: s.moveSpeedFlat += value; break;
                case StatType.moveSpeedPercent: s.moveSpeedPercent += value; break;
                case StatType.attackSpeedFlat: s.attackSpeedFlat += value; break;
                case StatType.attackSpeedPercent: s.attackSpeedPercent += value; break;
                case StatType.critRateFlat: s.critRateFlat += value; break;
                case StatType.critMultFlat: s.critMultFlat += value; break;
                case StatType.cdrFlat: s.cdrFlat += value; break;
                case StatType.mpRegenFlat: s.mpRegenFlat += value; break;
                case StatType.mpRegenPercent: s.mpRegenPercent += value; break;
                case StatType.mpMaxFlat: s.maxManaFlat += value; break;
                case StatType.penetrationFlat: s.penFlat += value; break;
                case StatType.skillRangeFlat: s.skillRangeFlat += value; break;
                case StatType.fireBonusFlat: s.fireBonusFlat += value; break;
                case StatType.iceBonusFlat: s.iceBonusFlat += value; break;
                case StatType.electricBonusFlat: s.electricBonusFlat += value; break;
                default:
                    Debug.LogWarning($"[StatSetMapper] 매핑되지 않은 StatType: {type}");
                    break;
            }
        }
    }
}

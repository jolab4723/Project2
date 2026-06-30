using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>개발자가 직접 세팅하는 고정 스탯 값 (메인 옵션 등, 랜덤 없음)</summary>
    [Serializable]
    public class FixedStatValue
    {
        public StatType statType;
        public float value;
        public bool IsPercent => StatTypeUtility.IsPercent(statType);
    }

    /// <summary>풀에서 랜덤으로 뽑힐 수 있는 옵션 정의 (값 범위)</summary>
    [Serializable]
    public class RandomStatOption
    {
        public StatType statType;
        public float minValue;
        public float maxValue;
        public bool IsPercent => StatTypeUtility.IsPercent(statType);
    }

    /// <summary>실제로 굴려진 결과값 (런타임 전용, ScriptableObject 아님)</summary>
    [Serializable]
    public class RolledSubStat
    {
        public StatType statType;
        public float value;
        public bool IsPercent => StatTypeUtility.IsPercent(statType);
    }

    /// <summary>
    /// 컴뱃/유틸 옵션 풀. 등급마다 별도로 만들지 않고
    /// CombatStatPool.asset / UtilityStatPool.asset 두 개를 모든 아이템이 공유 참조한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/SubStatPool")]
    public class SubStatPoolSO : ScriptableObject
    {
        public List<RandomStatOption> options = new List<RandomStatOption>();
    }

    /// <summary>
    /// 속성 보너스(Flat)/공격력%(Percent) 대체 슬롯에 쓰이는 수치 설정.
    /// 시트 M5의 5%/3%는 아직 확정 전이라 기본값 0으로 비워둠 — 기획 확정 후 채울 것.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/ElementalBonusConfig")]
    public class ElementalBonusConfigSO : ScriptableObject
    {
        public float elementBonusValue = 0f;   // 예: fireBonusFlat/iceBonusFlat/electricBonusFlat 값
        public float atkFallbackValue = 0f;    // 예: attackPowerPercent 대체값
        [Range(0f, 1f)] public float missChance = 0.25f; // 방어구 한정, 속성 미당첨 확률 (확정 전 임시값)
    }
}

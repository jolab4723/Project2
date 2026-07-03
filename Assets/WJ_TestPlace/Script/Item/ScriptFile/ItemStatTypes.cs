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
}
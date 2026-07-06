using System;

namespace ItemSystem
{
    /// <summary>
    /// 실제로 적용된 버프 1건의 런타임 상태. BuffDefinitionSO는 정의(설계값), 이건 인스턴스(적용 상태).
    /// </summary>
    [Serializable]
    public class BuffInstance
    {
        public BuffDefinitionSO definition;

        /// <summary>남은 지속시간(초). definition.IsPermanent면 의미 없음(항상 갱신되지 않음).</summary>
        public float remainingTime;

        /// <summary>현재 스택 수. RefreshDuration/Ignore 방식 버프는 항상 1.</summary>
        public int stackCount = 1;

        public bool IsExpired => !definition.IsPermanent && remainingTime <= 0f;

        public BuffInstance(BuffDefinitionSO def)
        {
            definition = def;
            remainingTime = def.duration;
            stackCount = 1;
        }
    }
}

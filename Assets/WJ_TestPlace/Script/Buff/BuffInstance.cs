using System;

namespace ItemSystem
{
    /// <summary>
    /// 실제로 적용된 버프 1건의 런타임 상태. IBuffSource는 정의(설계값), 이건 인스턴스(적용 상태).
    ///
    /// !! source는 값 비교가 아니라 **참조 비교**로 버프를 식별한다 (PlayerBuffManager 참고).
    /// </summary>
    [Serializable]
    public class BuffInstance
    {
        /// <summary>이 버프를 건 정의. 고유 효과(UniqueEffectSO) 또는 BuffDefinitionSO.</summary>
        public IBuffSource source;

        /// <summary>남은 지속시간(초). source.IsPermanent면 의미 없음(감소시키지 않음).</summary>
        public float remainingTime;

        /// <summary>현재 스택 수. RefreshDuration/Ignore 방식 버프는 항상 1.</summary>
        public int stackCount = 1;

        public bool IsExpired => source != null && !source.IsPermanent && remainingTime <= 0f;

        public BuffInstance(IBuffSource source)
        {
            this.source = source;
            remainingTime = source != null ? source.Duration : 0f;
            stackCount = 1;
        }
    }
}

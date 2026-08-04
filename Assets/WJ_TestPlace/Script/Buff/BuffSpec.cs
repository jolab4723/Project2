using System;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// "버프가 실제로 하는 일"만 담는 값 묶음. 에셋(ScriptableObject)이 아니라
    /// 다른 SO 안에 인라인으로 들어가는 직렬화 클래스다.
    ///
    /// 아이템 고유 효과(UniqueEffectSO)는 별도 버프 에셋을 참조하지 않고 이걸 직접 들고 있어서,
    /// 효과 이름·설명·발동 조건과 실제 효과 수치를 한 에셋에서 다 설정할 수 있다.
    /// 표시용 이름/아이콘은 이 안에 두지 않는다 - 그건 이걸 들고 있는 쪽(고유 효과)의 것을 쓴다.
    /// </summary>
    [Serializable]
    public class BuffSpec
    {
        [Tooltip("적용할 스탯 효과. 디버프는 음수 값을 넣으면 됨. 스택 시 이 값 × 스택 수만큼 적용됨.")]
        public FixedStatValue[] statEffects;

        [Header("지속시간")]
        [Tooltip("0 이하면 영구 지속 (수동으로 제거하기 전까지 유지)")]
        public float duration = 10f;

        [Header("중첩 규칙")]
        public BuffStackBehavior stackBehavior = BuffStackBehavior.RefreshDuration;

        [Tooltip("stackBehavior가 Stack일 때 최대 스택 수. 0 이하면 무제한.")]
        public int maxStack = 0;

        public bool IsPermanent => duration <= 0f;
    }
}

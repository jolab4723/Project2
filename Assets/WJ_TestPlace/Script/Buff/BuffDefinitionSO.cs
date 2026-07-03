using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 버프를 다시 적용했을 때 처리 방식.
    /// </summary>
    public enum BuffStackBehavior
    {
        /// <summary>스택은 그대로, 지속시간만 duration으로 리셋</summary>
        RefreshDuration,
        /// <summary>스택이 쌓여서 효과가 배가됨 (maxStack까지)</summary>
        Stack,
        /// <summary>이미 걸려있으면 재적용 무시</summary>
        Ignore,
    }

    /// <summary>
    /// 버프 하나의 정의. ItemDefinitionSO와 같은 역할(설계 데이터)을 하고,
    /// 실제 적용 상태는 BuffInstance(런타임)가 따로 들고 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Buff/BuffDefinition")]
    public class BuffDefinitionSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string buffId;
        public string buffName;
        public Sprite icon;
        [TextArea] public string description;

        [Header("지속시간")]
        [Tooltip("0 이하면 영구 지속 (수동으로 RemoveBuff 하기 전까지 유지)")]
        public float duration = 10f;

        [Header("중첩 규칙")]
        public BuffStackBehavior stackBehavior = BuffStackBehavior.RefreshDuration;
        [Tooltip("stackBehavior가 Stack일 때 최대 스택 수. 0 이하면 무제한.")]
        public int maxStack = 0;

        [Header("스탯 효과")]
        [Tooltip("디버프는 여기 음수 값을 넣으면 됨. 스택 시 이 값 * 스택 수만큼 적용됨.")]
        public FixedStatValue[] statEffects;

        public bool IsPermanent => duration <= 0f;
    }
}

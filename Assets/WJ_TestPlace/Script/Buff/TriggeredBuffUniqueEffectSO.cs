using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 특정 조건을 만족했을 때(OnTrigger 호출 시)만 적용되는 고유 효과.
    /// 보통 지속시간이 있는 임시 버프로 설정해서 쓴다.
    /// 언제 OnTrigger를 부를지(치명타 시, 피격 시 등)는 전투 시스템 쪽 책임.
    ///
    /// 별도 버프 에셋을 참조하지 않고 buffSpec을 직접 들고 있어서, 효과 이름·설명·발동 조건·수치를
    /// 이 에셋 하나에서 전부 설정한다. 버프 식별 키는 이 에셋 자신이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/TriggeredBuff")]
    public class TriggeredBuffUniqueEffectSO : UniqueEffectSO, IBuffSource
    {
        [Header("발동 조건")]
        [Tooltip("이 효과가 반응하는 발동 조건. ItemTriggerManager가 이 값과 일치하는 실제 게임 이벤트가 발생했을 때 OnTrigger를 불러준다.")]
        public TriggerCondition triggerCondition = TriggerCondition.None;

        [Header("적용할 버프")]
        [Tooltip("조건 만족 시 적용될 효과. 보통 duration을 양수로 둬서 일정 시간만 유지되게 한다.")]
        public BuffSpec buffSpec = new BuffSpec();

        public override void OnTrigger(ItemInstance ownerItem)
        {
            if (PlayerBuffManager.Instance == null)
            {
                Debug.LogWarning("[TriggeredBuffUniqueEffectSO] PlayerBuffManager.Instance가 없습니다.");
                return;
            }

            PlayerBuffManager.Instance.ApplyBuff(this);
        }

        // ----- IBuffSource -----
        public string BuffDisplayName => string.IsNullOrEmpty(effectName) ? name : effectName;
        public Sprite BuffIcon => icon;
        public FixedStatValue[] StatEffects => buffSpec?.statEffects;
        public float Duration => buffSpec != null ? buffSpec.duration : 0f;
        public BuffStackBehavior StackBehavior => buffSpec != null ? buffSpec.stackBehavior : BuffStackBehavior.RefreshDuration;
        public int MaxStack => buffSpec != null ? buffSpec.maxStack : 0;
        public bool IsPermanent => buffSpec == null || buffSpec.IsPermanent;
    }
}

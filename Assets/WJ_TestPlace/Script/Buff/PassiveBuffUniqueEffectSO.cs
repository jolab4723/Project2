using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 장착하면 상시 적용되는 고유 효과. 장착 시 ApplyBuff, 해제 시 RemoveBuff 한다.
    /// buffSpec.duration을 0 이하로 두면 영구 지속(해제 전까지 유지)로 동작한다.
    ///
    /// 별도 버프 에셋을 참조하지 않고 buffSpec을 직접 들고 있어서, 효과 이름·설명·수치를
    /// 이 에셋 하나에서 전부 설정한다. 버프 식별 키는 이 에셋 자신이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/PassiveBuff")]
    public class PassiveBuffUniqueEffectSO : UniqueEffectSO, IBuffSource
    {
        [Header("적용할 버프")]
        [Tooltip("장착 중 상시 적용될 효과. duration을 0 이하로 두면 영구 지속.")]
        public BuffSpec buffSpec = new BuffSpec { duration = 0f, stackBehavior = BuffStackBehavior.Ignore };

        public override void OnEquip(ItemInstance ownerItem)
        {
            if (PlayerBuffManager.Instance == null)
            {
                Debug.LogWarning("[PassiveBuffUniqueEffectSO] PlayerBuffManager.Instance가 없습니다.");
                return;
            }

            PlayerBuffManager.Instance.ApplyBuff(this);
        }

        public override void OnUnequip(ItemInstance ownerItem)
        {
            if (PlayerBuffManager.Instance == null)
                return;

            PlayerBuffManager.Instance.RemoveBuff(this);
        }

        // ----- IBuffSource -----
        public string BuffDisplayName => string.IsNullOrEmpty(effectName) ? name : effectName;
        public Sprite BuffIcon => icon;
        public FixedStatValue[] StatEffects => buffSpec?.statEffects;
        public float Duration => buffSpec != null ? buffSpec.duration : 0f;
        public BuffStackBehavior StackBehavior => buffSpec != null ? buffSpec.stackBehavior : BuffStackBehavior.Ignore;
        public int MaxStack => buffSpec != null ? buffSpec.maxStack : 0;
        public bool IsPermanent => buffSpec == null || buffSpec.IsPermanent;
    }
}

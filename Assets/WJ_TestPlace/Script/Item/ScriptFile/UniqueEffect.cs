using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 고유 효과는 등급과 무관하게 아이템별로 있을 수도, 없을 수도 있다.
    /// 상시 효과(OnEquip/OnUnequip)와 조건부 발동 효과(OnTrigger) 둘 다 지원한다.
    /// - OnEquip/OnUnequip: 장착/해제 시 ItemUI에서 자동으로 호출됨.
    /// - OnTrigger: 특정 조건(치명타, 피격 등)을 만족했을 때 외부(전투 시스템 등)에서 직접 호출해야 함.
    ///   (언제 발동할지의 조건 판정 자체는 이 시스템 범위 밖)
    /// </summary>
    public interface IUniqueEffect
    {
        string EffectDescription { get; }

        /// <summary>장착 시 호출 (상시 효과용). 조건부 효과는 비워두면 됨.</summary>
        void OnEquip(ItemInstance ownerItem);

        /// <summary>해제 시 호출 (상시 효과 해제용).</summary>
        void OnUnequip(ItemInstance ownerItem);

        /// <summary>특정 조건 만족 시 외부(전투 시스템 등)에서 호출 (조건부 효과용).</summary>
        void OnTrigger(ItemInstance ownerItem);
    }

    public abstract class UniqueEffectSO : ScriptableObject, IUniqueEffect
    {
        [TextArea] public string effectDescription;
        public string EffectDescription => effectDescription;

        // 기본값은 전부 아무것도 안 함 - 서브클래스는 필요한 것만 오버라이드
        public virtual void OnEquip(ItemInstance ownerItem) { }
        public virtual void OnUnequip(ItemInstance ownerItem) { }
        public virtual void OnTrigger(ItemInstance ownerItem) { }
    }

    /// <summary>
    /// 장착하면 상시 적용되는 고유 효과. 지정된 버프를 장착 시 ApplyBuff, 해제 시 RemoveBuff 한다.
    /// buffToApply.duration을 0 이하로 두면 영구 지속(해제 전까지 유지)로 동작함.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/PassiveBuff")]
    public class PassiveBuffUniqueEffectSO : UniqueEffectSO
    {
        [Tooltip("장착 중 상시 적용될 버프. duration을 0 이하로 두면 영구 지속.")]
        public BuffDefinitionSO buffToApply;

        public override void OnEquip(ItemInstance ownerItem)
        {
            if (buffToApply == null)
                return;

            if (PlayerBuffManager.Instance == null)
            {
                Debug.LogWarning("[PassiveBuffUniqueEffectSO] PlayerBuffManager.Instance가 없습니다.");
                return;
            }

            PlayerBuffManager.Instance.ApplyBuff(buffToApply);
        }

        public override void OnUnequip(ItemInstance ownerItem)
        {
            if (buffToApply == null || PlayerBuffManager.Instance == null)
                return;

            PlayerBuffManager.Instance.RemoveBuff(buffToApply);
        }
    }

    /// <summary>
    /// 특정 조건을 만족했을 때(OnTrigger 호출 시)만 적용되는 고유 효과.
    /// 보통 지속시간이 있는 임시 버프를 연결해서 쓴다.
    /// 언제 OnTrigger를 부를지(치명타 시, 피격 시 등)는 전투 시스템 쪽 책임.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/TriggeredBuff")]
    public class TriggeredBuffUniqueEffectSO : UniqueEffectSO
    {
        [Tooltip("조건 만족 시 적용될 버프")]
        public BuffDefinitionSO buffToApply;

        public override void OnTrigger(ItemInstance ownerItem)
        {
            if (buffToApply == null)
                return;

            if (PlayerBuffManager.Instance == null)
            {
                Debug.LogWarning("[TriggeredBuffUniqueEffectSO] PlayerBuffManager.Instance가 없습니다.");
                return;
            }

            PlayerBuffManager.Instance.ApplyBuff(buffToApply);
        }
    }
}

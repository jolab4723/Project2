using UnityEngine;

namespace ItemSystem
{
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
}

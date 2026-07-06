using UnityEngine;

namespace ItemSystem
{
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

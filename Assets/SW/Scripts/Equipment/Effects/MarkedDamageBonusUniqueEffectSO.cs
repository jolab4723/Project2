using UnityEngine;

namespace ItemSystem
{
    /// <summary>SW 추가: 표식(Marked)이 걸린 적에게 주는 모든 피해에 공격자 쪽 배율을 더한다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Marked Damage Bonus")]
    public sealed class MarkedDamageBonusUniqueEffectSO : UniqueEffectSO
    {
        [Header("표식 대상 추가 피해")]
        [Tooltip("추가 피해 비율. 0.2면 +20%.")]
        [Min(0f)] public float bonusFraction = 0.2f;
    }
}

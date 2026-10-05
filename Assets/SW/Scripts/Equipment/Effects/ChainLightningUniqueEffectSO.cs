using UnityEngine;

namespace ItemSystem
{
    /// <summary>직접 기본 공격 뒤 가까운 적에게 전기 후속 피해를 순차 전달하는 설정이다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Chain Lightning")]
    public sealed class ChainLightningUniqueEffectSO : UniqueEffectSO
    {
        // SW 수정 : 크루세이더는 실제 치명 적중에서만 발동하며 기존 아크 블레이드의 기본값은 유지한다.
        public bool requiresCritical;

        [Header("연쇄 피해")]
        [Min(0.001f)] public float firstDamageMultiplier = 0.25f;
        [Range(0f, 1f)] public float subsequentDamageMultiplier = 0.8f;
        [Min(0.1f)] public float jumpRadius = 4f;
        [Range(1, 16)] public int maxAdditionalTargets = 3;
        [Min(0f)] public float cooldownSeconds = 0.8f;
    }
}

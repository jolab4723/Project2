using UnityEngine;

namespace ItemSystem
{
    /// <summary>유리빛 궤도 라이플 탄이 유효하게 적중했을 때 적용할 비치명 냉기 추가타 설정이다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Glass Rail Extra Hit")]
    public sealed class GlassRailExtraHitUniqueEffectSO : UniqueEffectSO
    {
        [Header("냉기 추가타")]
        [Min(0.001f)] public float damageMultiplier = 0.15f;
    }
}

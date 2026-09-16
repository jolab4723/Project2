using UnityEngine;

namespace ItemSystem
{
    /// <summary>Fighter 근접 기본 공격이 적중한 같은 대상에게 즉시 화염 후속 피해를 주는 설정이다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Inferno Extra Hit")]
    public sealed class InfernoExtraHitUniqueEffectSO : UniqueEffectSO
    {
        [Header("화염 추가타")]
        [Min(0.001f)] public float damageMultiplier = 0.2f;
    }
}

using UnityEngine;

namespace ItemSystem
{
    [CreateAssetMenu(menuName = "Item/Unique Effect/Solar Grace Shield")]
    public sealed class SolarGraceShieldUniqueEffectSO : UniqueEffectSO
    {
        [Range(0f, 1f)] public float minimumHealthFraction = 0.8f;
        [Min(0f)] public float undamagedSeconds = 8f;
        [Range(0f, 1f)] public float shieldFraction = 0.15f;
        [Min(0f)] public float shieldDurationSeconds = 12f;
    }
}

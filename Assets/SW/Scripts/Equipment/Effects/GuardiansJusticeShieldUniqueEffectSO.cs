using UnityEngine;

namespace ItemSystem
{
    [CreateAssetMenu(menuName = "Item/Unique Effect/Guardian Justice Shield")]
    public sealed class GuardiansJusticeShieldUniqueEffectSO : UniqueEffectSO
    {
        [Range(0f, 1f)] public float lossToEnergyFraction = 0.5f;
        [Range(0f, 1f)] public float maximumHealthFraction = 0.12f;
        [Min(0f)] public float energyDurationSeconds = 5f;
        [Min(0f)] public float shieldDurationSeconds = 5f;
        [Min(0f)] public float cooldownSeconds = 8f;
    }
}

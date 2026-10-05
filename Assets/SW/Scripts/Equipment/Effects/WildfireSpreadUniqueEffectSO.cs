using UnityEngine;

namespace ItemSystem
{
    [CreateAssetMenu(menuName = "Item/Unique Effect/Wildfire Spread")]
    public sealed class WildfireSpreadUniqueEffectSO : UniqueEffectSO
    {
        [Min(0.1f)] public float radius = 4f;
        [Min(1)] public int maxTargets = 3;
        [Min(0f)] public float cooldownSeconds = 1f;
    }
}

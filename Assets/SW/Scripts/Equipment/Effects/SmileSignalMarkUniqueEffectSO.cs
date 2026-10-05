using UnityEngine;

namespace ItemSystem
{
    [CreateAssetMenu(menuName = "Item/Unique Effect/Smile Signal Mark")]
    public sealed class SmileSignalMarkUniqueEffectSO : UniqueEffectSO
    {
        [Min(0.1f)] public float durationSeconds = 3f;
        [Min(0f)] public float damageMultiplier = 0.25f;
        [Min(1)] public int maxTargets = 3;
        [Min(0f)] public float targetRecoverySeconds = 1f;
    }
}

using UnityEngine;

namespace ItemSystem
{
    [CreateAssetMenu(menuName = "Item/Unique Effect/World Ender Charged Blast")]
    public sealed class WorldEnderChargedBlastUniqueEffectSO : UniqueEffectSO
    {
        [Min(0.1f)] public float rechargeSeconds = 8f;
        [Min(0.1f)] public float radius = 5f;
        [Min(0f)] public float damageMultiplier = 1.5f;
        [Min(1)] public int maxTargets = 8;
    }
}

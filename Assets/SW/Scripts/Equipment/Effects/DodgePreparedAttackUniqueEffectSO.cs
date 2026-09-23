using UnityEngine;

namespace ItemSystem
{
    [CreateAssetMenu(menuName = "Item/Unique Effect/Dodge Prepared Attack")]
    public sealed class DodgePreparedAttackUniqueEffectSO : UniqueEffectSO
    {
        [Min(1f)] public float damageMultiplier = 1.4f;
    }
}

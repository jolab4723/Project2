using UnityEngine;

namespace ItemSystem
{
    [CreateAssetMenu(menuName = "Item/Unique Effect/Ninja Dodge Attack")]
    public sealed class NinjaDodgeAttackUniqueEffectSO : UniqueEffectSO
    {
        [Min(0f)] public float bonusFraction = 0.2f;
        [Min(0f)] public float preparationSeconds = 3f;
        [Min(0f)] public float cooldownSeconds = 6f;
    }
}

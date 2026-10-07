using UnityEngine;

namespace ItemSystem
{
    [CreateAssetMenu(menuName = "Item/Unique Effect/Echo Vault Replay")]
    public sealed class EchoVaultReplayUniqueEffectSO : UniqueEffectSO
    {
        [Min(0.01f)] public float delaySeconds = 0.45f;
        [Min(0f)] public float damageMultiplier = 0.35f;
        [Min(1)] public int maxTargets = 6;
        [Min(0f)] public float cooldownSeconds = 1.5f;
        [Min(1)] public int maxPendingReplays = 2;
    }
}

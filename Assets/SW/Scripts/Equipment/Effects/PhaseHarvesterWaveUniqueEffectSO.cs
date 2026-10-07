using UnityEngine;

namespace ItemSystem
{
    /// <summary>SW 수정: Fighter 기본 공격 처치 뒤 싱글·서버가 즉시 확정하는 전방 처형 파동의 설정이다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Phase Harvester Wave")]
    public sealed class PhaseHarvesterWaveUniqueEffectSO : UniqueEffectSO
    {
        [Min(0.1f)] public float length = 6f;
        [Min(0.1f)] public float width = 2f;
        [Min(0.001f)] public float damageMultiplier = 0.6f;
        [Range(1, 16)] public int maxTargets = 6;
        [Min(0f)] public float cooldownSeconds = 1f;
    }
}

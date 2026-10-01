using UnityEngine;

namespace ItemSystem
{
    /// <summary>SW 수정: Gunner 기본 Shotgun의 최초 근거리 명중 뒤 싱글·서버가 즉시 확정하는 Fire 폭발 설정이다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Star Breacher Explosion")]
    public sealed class StarBreacherExplosionUniqueEffectSO : UniqueEffectSO
    {
        [Min(0.1f)] public float triggerDistance = 3f;
        [Min(0.1f)] public float radius = 2.5f;
        [Min(0.001f)] public float damageMultiplier = 0.35f;
        [Range(1, 16)] public int maxTargets = 5;
        [Min(0f)] public float cooldownSeconds = 1.2f;
    }
}

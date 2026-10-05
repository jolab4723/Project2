using UnityEngine;

namespace ItemSystem
{
    /// <summary>SW 수정 : 같은 적의 서로 다른 기본 적중을 누적해 기존 적 방어 약화를 적용하는 코어 브레이커 설정이다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Core Breaker Expose")]
    public sealed class CoreBreakerExposeUniqueEffectSO : UniqueEffectSO
    {
        [Min(1)] public int requiredHits = 3;
        [Min(0.1f)] public float hitWindowSeconds = 3f;
        [Min(0.1f)] public float exposeDurationSeconds = 4f;
        [Range(0f, 1f)] public float defenseReduction = 0.2f;
        [Range(0f, 1f)] public float bossDefenseReduction = 0.1f;
    }
}

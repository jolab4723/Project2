using UnityEngine;

namespace ItemSystem
{
    /// <summary>SW 수정 : 기본 적중으로 둔화·냉각을 누적하고 대상 공통 재준비 제한을 지켜 짧게 빙결하는 초냉매 설정이다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Super Refrigerant Freeze")]
    public sealed class SuperRefrigerantFreezeUniqueEffectSO : UniqueEffectSO
    {
        [Min(1)] public int requiredHits = 3;
        [Min(0.1f)] public float hitWindowSeconds = 3f;
        [Min(0.1f)] public float slowDurationSeconds = 1f;
        [Range(0f, 1f)] public float slowMultiplier = 0.85f;
        [Min(0.1f)] public float freezeDurationSeconds = 0.6f;
        [Min(0.1f)] public float freezeRecoverySeconds = 5f;
    }
}

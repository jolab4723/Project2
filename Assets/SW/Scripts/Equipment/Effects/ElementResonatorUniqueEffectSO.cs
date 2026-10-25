using UnityEngine;

namespace ItemSystem
{
    /// <summary>SW 추가: 화상·빙결·감전 중 하나 이상이 걸린 적을 적중하면 그 자리에 무속성 비치명 폭발을 일으킨다. 서로 다른 두 가지 이상이면 피해가 커진다. 대상마다 재발동 대기시간이 있다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Element Resonator")]
    public sealed class ElementResonatorUniqueEffectSO : UniqueEffectSO
    {
        [Header("원소 과부하 폭발")]
        [Min(0.1f)] public float radius = 3f;
        [Tooltip("공격력 대비 피해 배율. 0.8이면 80%.")]
        [Min(0.001f)] public float damageMultiplier = 0.8f;
        [Tooltip("같은 대상이 다시 폭발 기점이 되기까지의 시간(초).")]
        [Min(0f)] public float perTargetCooldownSeconds = 3f;
        [Range(1, 16)] public int maxTargets = 8;
    }
}

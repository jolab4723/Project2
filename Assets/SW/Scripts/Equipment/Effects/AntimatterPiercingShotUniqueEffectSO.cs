using UnityEngine;

namespace ItemSystem
{
    /// <summary>SW 수정: 기본 라이플 탄의 서로 다른 적 관통 상한과 비치명 후속 피해 계수를 설정한다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Antimatter Piercing Shot")]
    public sealed class AntimatterPiercingShotUniqueEffectSO : UniqueEffectSO
    {
        [Range(1, 3)] public int maxTargets = 3;
        [Range(0.001f, 1f)] public float secondDamageMultiplier = 0.7f;
        [Range(0.001f, 1f)] public float thirdDamageMultiplier = 0.5f;

        /// <summary>SW 수정: 첫 유효 적은 원래 직접 피해 계수, 두 번째와 세 번째는 설정된 관통 계수를 사용한다.</summary>
        public float GetDamageMultiplier(int hitIndex)
            => hitIndex == 0 ? 1f : hitIndex == 1 ? secondDamageMultiplier : thirdDamageMultiplier;
    }
}

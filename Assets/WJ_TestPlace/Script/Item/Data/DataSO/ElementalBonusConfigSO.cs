using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 속성 보너스(Flat)/공격력%(Percent) 대체 슬롯에 쓰이는 수치 설정
    /// </summary>
    [CreateAssetMenu(menuName = "Item/StatPool/ElementalBonusConfig")]
    public class ElementalBonusConfigSO : ScriptableObject
    {
        [Tooltip("속성 보너스 값")]
        public float elementBonusValue = 0f;

        [Tooltip("대체 공격력 값")]
        public float atkFallbackValue = 0f;

        [Tooltip("방어구 한정, 속성 미당첨 확률")]
        [Range(0f, 1f)] public float missChance = 0.25f;
    }
}

using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 속성 보너스(Flat)/공격력%(Percent) 대체 슬롯에 쓰이는 수치 설정.
    /// 시트 M5의 5%/3%는 아직 확정 전이라 기본값 0으로 비워둠 — 기획 확정 후 채울 것.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/ElementalBonusConfig")]
    public class ElementalBonusConfigSO : ScriptableObject
    {
        public float elementBonusValue = 0f;   // 예: fireBonusFlat/iceBonusFlat/electricBonusFlat 값
        public float atkFallbackValue = 0f;    // 예: attackPowerPercent 대체값
        [Range(0f, 1f)] public float missChance = 0.25f; // 방어구 한정, 속성 미당첨 확률 (확정 전 임시값)
    }
}

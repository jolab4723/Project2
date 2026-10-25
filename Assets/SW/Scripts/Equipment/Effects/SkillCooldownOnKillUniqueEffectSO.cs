using UnityEngine;

namespace ItemSystem
{
    /// <summary>SW 추가: 적 처치 시 지정 슬롯 스킬의 남은 쿨타임(스택형은 충전)을 줄인다. 과부하 심장·충전 회로가 같은 유형을 쓴다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Skill Cooldown On Kill")]
    public sealed class SkillCooldownOnKillUniqueEffectSO : UniqueEffectSO
    {
        [Header("처치 시 쿨타임 감소")]
        [Tooltip("대상 스킬 슬롯(1~4). 슬롯 의미는 클래스마다 다르다(파이터 4번 각성, 거너 4번 융단폭격).")]
        [Range(1, 4)] public int skillSlot = 4;
        [Min(0.01f)] public float reduceSeconds = 1f;
        [Tooltip("광역 처치 한 번에 여러 번 깎이지 않도록 하는 내부 쿨타임(초).")]
        [Min(0f)] public float cooldownSeconds = 0.5f;
    }
}

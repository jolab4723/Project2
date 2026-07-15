using System;
using System.Collections.Generic;

namespace Core
{
    /// <summary>
    /// 패시브 스킬트리 - 게임 세션과 무관하게 캐릭터에게 항상 적용되는 영구 강화.
    /// SkillTreeSaveData(액티브 스킬, 게임플레이 데이터 소속)와는 다른 개념 - 이건 플레이어 프로필 소속.
    /// 스킬별 이름/효과 수치/해금 비용은 세이브 데이터가 아니라 PassiveSkillDatabase(디자인 데이터)에 있음.
    /// </summary>
    [Serializable]
    public class PassiveSkillTreeData
    {
        public List<PassiveSkillEntry> learnedSkills = new List<PassiveSkillEntry>();
    }

    /// <summary>
    /// unlockedLevel: 골드를 써서 해금한 최고 레벨 (한 번 해금하면 절대 줄어들지 않음).
    /// currentLevel: 실제로 적용 중인 레벨 (0~unlockedLevel 범위는 무료로 자유롭게 증감 가능,
    /// 실제 스탯/효과 계산에는 이 값을 사용함). btn_SkillClear는 currentLevel만 0으로 되돌린다.
    /// </summary>
    [Serializable]
    public class PassiveSkillEntry
    {
        public PassiveSkillId id;
        public int unlockedLevel;
        public int currentLevel;
    }

    /// <summary>총 12개 패시브 스킬의 식별자. 효과/레벨당 수치는 PassiveSkillDatabase 참고.</summary>
    public enum PassiveSkillId
    {
        MaxHealth,          // 최대 체력 증가 (5단계, 3~15%)
        AttackPower,        // 공격력 증가 (5단계, 3~15%)
        DefensePower,       // 방어력 증가 (5단계, 3~15%)
        AllSpeed,           // 이동속도+공격속도 증가 (5단계, 3~15%)
        CritRate,           // 크리티컬 확률 증가 (3단계, 5~15%)
        CritDamage,         // 크리티컬 피해 증가 (3단계, 10~30%)
        CooldownReduction,  // 스킬 쿨타임 감소 (3단계, 10/15/20%)
        AllElementalBonus,  // 모든 속성 보너스 증가 (3단계, 10/15/25%)
        Revive,             // 부활 1회 활성화 (1단계, 최대체력 20%로 부활)
        CampHealBonus,      // 캠프 회복량 증가 (1단계, 20% -> 40%)
        ShopEnhance,        // 상점 강화 (1단계, 가격 10% 할인 + 리롤 1회 추가)
        Undecided,          // 미정 (1단계, 효과 미정)
    }
}

using System;
using System.Collections.Generic;

namespace Core
{
    /// <summary>
    /// 패시브 스킬트리 - 게임 세션과 무관하게 캐릭터에게 항상 적용되는 영구 강화.
    /// SkillTreeSaveData(액티브 스킬, 게임플레이 데이터 소속)와는 다른 개념 - 이건 플레이어 프로필 소속.
    /// 실제 패시브 스킬 시스템이 아직 없어서 최소 구조만 잡아둠.
    /// TODO: 패시브 스킬 시스템이 생기면 PassiveSkillEntry에 필요한 필드(효과 종류/수치 등) 채우기.
    /// </summary>
    [Serializable]
    public class PassiveSkillTreeData
    {
        /// <summary>아직 안 쓴 스킬 포인트. PlayerProfileData.gold(영구 골드)로 구매해서 늘어남.</summary>
        public int skillPoints;
        public List<PassiveSkillEntry> learnedSkills = new List<PassiveSkillEntry>();
    }

    [Serializable]
    public class PassiveSkillEntry
    {
        public string skillId;
        public int level;
    }
}

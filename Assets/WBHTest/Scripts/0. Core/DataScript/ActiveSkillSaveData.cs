using System;

namespace Core
{
    /// <summary>
    /// 3-5. 액티브 스킬(Skill1~3) 진화/강화 세이브 데이터.
    /// 인덱스 기반(0~2)으로 저장한다 - ISkillController 자체가 인덱스 기준 API(GetEvolution(int) 등)라
    /// 저장 형식도 그대로 맞췄다. 스킬 슬롯의 인덱스↔실제 스킬 매핑이 바뀌지 않는다는 전제.
    /// </summary>
    [Serializable]
    public class ActiveSkillSaveData
    {
        public SkillEvolutionId[] evolutions;
        public SkillEnhancementId[] enhancements;
    }
}

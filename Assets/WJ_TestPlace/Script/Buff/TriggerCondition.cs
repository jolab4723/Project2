namespace ItemSystem
{
    /// <summary>
    /// 발동형 고유효과가 반응하는 조건. 실제 게임 이벤트와의 연결 상태는 각 값 주석 참고.
    /// !! 에셋은 열거형을 정수로 저장하므로 새 값은 반드시 맨 끝에 추가한다.
    /// </summary>
    public enum TriggerCondition
    {
        None,

        /// <summary>피격 시. PlayerHealthManager.OnDamageTaken으로 실제 연결됨 (ItemTriggerManager).</summary>
        OnHitTaken,

        /// <summary>치명타 적중 시. PlayerItemEffectState.FireDamageDealt에서 실제 연결됨. 피해 출처는 hitSource로 거른다.</summary>
        OnCrit,

        /// <summary>적 처치 시. 싱글은 WBHEnemyItemDropAdapter, 멀티는 NetworkEnemyAuthority에서 실제 연결됨.</summary>
        OnKill,

        /// <summary>공격 적중(피해를 줬을 때) 시. PlayerItemEffectState.FireDamageDealt에서 실제 연결됨. 피해 출처는 hitSource로 거른다.</summary>
        OnDamageDealt,

        /// <summary>
        /// 회피(기본 Space) 사용 시. WBH_PlayerStateMachine.OnEnterState에서 Dodge 상태 진입을
        /// 감지해 실제 연결됨 (ItemTriggerManager). 대시·백스탭 스킬(SkillDefinitionSO.CountsAsDodge)도 회피로 친다.
        /// </summary>
        OnDodge,

        /// <summary>
        /// SW 추가: 스킬 시전이 확정됐을 때(쿨타임·스택 소모 시점). ISkillController.SkillCast로 연결되며
        /// 슬롯은 skillSlot으로 거른다.
        /// </summary>
        OnSkillCast,
    }

    /// <summary>SW 추가: 적중·치명타 트리거가 어떤 피해에 반응할지. 빈 칸은 Basic이라 기존 행의 동작이 바뀌지 않는다.</summary>
    public enum HitSourceFilter
    {
        Basic,
        Skill,
        Any,
    }

    /// <summary>SW 추가: 발동 시점의 피해 출처와 스킬 슬롯(1~4, 0은 해당 없음)을 효과 판정에 전달한다.</summary>
    public readonly struct TriggerContext
    {
        public readonly bool IsSkillHit;
        public readonly int SkillSlot;

        public TriggerContext(bool isSkillHit, int skillSlot)
        {
            IsSkillHit = isSkillHit;
            SkillSlot = skillSlot;
        }

        public static TriggerContext BasicHit => new(false, 0);
    }
}

namespace ItemSystem
{
    /// <summary>
    /// 발동형 고유효과가 반응하는 조건. 실제 게임 이벤트와의 연결 상태는 각 값 주석 참고.
    /// </summary>
    public enum TriggerCondition
    {
        None,

        /// <summary>피격 시. PlayerHealthManager.OnDamageTaken으로 실제 연결됨 (ItemTriggerManager).</summary>
        OnHitTaken,

        /// <summary>치명타 적중 시. 전투 시스템이 생기면 연결 예정 (아직 발동 안 됨).</summary>
        OnCrit,

        /// <summary>적 처치 시. 전투 시스템이 생기면 연결 예정 (아직 발동 안 됨).</summary>
        OnKill,

        /// <summary>공격 적중(피해를 줬을 때) 시. 전투 시스템이 생기면 연결 예정 (아직 발동 안 됨).</summary>
        OnDamageDealt,
    }
}

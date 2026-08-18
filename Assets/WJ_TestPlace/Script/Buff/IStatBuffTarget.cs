namespace ItemSystem
{
    /// <summary>
    /// EnemyBuffManager처럼 BuffTracker가 합산한 StatSet을 그대로 받아 반영할 수 있는 대상의 계약.
    /// WBH_EnemyStatus/TrainingDummyStatus가 구현한다 - 실제 적과 허수아비를 같은 EnemyBuffManager
    /// 하나로 다루기 위해 분리했다.
    /// </summary>
    public interface IStatBuffTarget
    {
        void ApplyBuffStatSet(StatSet statSet);
    }
}

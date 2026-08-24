namespace ItemSystem
{
    /// <summary>
    /// 버프를 걸고 뗄 수 있는 대상의 공통 계약. BuffFieldZone처럼 대상이 플레이어인지 적인지
    /// 몰라도 되는 코드에서 공용으로 다룰 때 쓴다. PlayerBuffManager/EnemyBuffManager가 구현한다.
    /// </summary>
    public interface IBuffTarget
    {
        void ApplyBuff(IBuffSource source);
        void RemoveBuff(IBuffSource source);
    }
}

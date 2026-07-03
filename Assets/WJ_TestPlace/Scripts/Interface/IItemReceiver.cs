namespace ItemSystem
{
    /// <summary>
    /// 아이템을 받을 수 있는 대상(인벤토리 등)의 계약.
    /// ItemAcquisition이 특정 인벤토리 구현(InventoryController)에 직접 의존하지 않게 하기 위한 추상화.
    /// 멀티플레이/인벤토리 구조 변경 시에도 이 인터페이스만 구현하면 획득 로직을 재사용할 수 있다.
    /// </summary>
    public interface IItemReceiver
    {
        /// <summary>아이템을 받는다. 성공하면 true, (인벤토리 꽉 참 등) 실패하면 false.</summary>
        bool AddItem(ItemInstance item);
    }
}

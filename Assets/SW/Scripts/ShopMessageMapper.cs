public static class ShopMessageMapper
{
    public static string GetMessage(
        TradeResult result,
        string itemName,
        bool isBuying)
    {
        switch (result)
        {
            case TradeResult.Success:
                return isBuying
                    ? $"{itemName}을 구매했습니다."
                    : $"{itemName}을 판매했습니다.";

            case TradeResult.NotEnoughGold:
                return $"골드가 부족해 {itemName} 구매에 실패했습니다.";

            case TradeResult.NoSpace:
                return "인벤토리에 공간이 없습니다.";

            case TradeResult.InvalidItem:
                return "유효하지 않은 아이템입니다.";

            case TradeResult.TransferFailed:
                return $"{itemName} 이동 중 거래에 실패했습니다.";

            default:
                return "거래 처리에 실패했습니다.";
        }
    }
}
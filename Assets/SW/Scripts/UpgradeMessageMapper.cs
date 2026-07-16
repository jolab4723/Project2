public static class UpgradeMessageMapper
{
    public const string SelectionRequired =
        "강화할 아이템을 선택하세요.";

    public static string GetMessage(
        UpgradeResult result,
        string itemName,
        int upgradeLevel)
    {
        switch (result)
        {
            case UpgradeResult.Success:
                return $"{itemName} +{upgradeLevel} 강화 성공";

            case UpgradeResult.NotEnoughGold:
                return "골드가 부족합니다.";

            case UpgradeResult.InvalidItem:
                return "강화할 수 없는 아이템입니다.";

            case UpgradeResult.InvalidCost:
                return "강화 비용 설정이 올바르지 않습니다.";

            case UpgradeResult.WalletUnavailable:
                return "강화 처리 중 문제가 발생했습니다.";

            default:
                return "강화에 실패했습니다.";
        }
    }
}
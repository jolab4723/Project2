public static class UpgradeMessageMapper
{
    /// <summary>다국어 DB가 있으면 그 문구를, 없으면 기존 하드코딩 한국어 문구를 반환한다.</summary>
    public static string GetSelectionRequired(UILabelDatabaseSO labels) =>
        GetLabel(labels, "upgrade_ui.selection_required", "강화할 아이템을 선택하세요.");

    public static string GetMessage(
        UpgradeResult result,
        string itemName,
        int upgradeLevel,
        UILabelDatabaseSO labels)
    {
        switch (result)
        {
            case UpgradeResult.Success:
                return string.Format(
                    GetLabel(labels, "upgrade_ui.result_success", "{0} +{1} 강화 성공"),
                    itemName, upgradeLevel);

            case UpgradeResult.NotEnoughGold:
                return GetLabel(labels, "upgrade_ui.result_not_enough_gold", "골드가 부족합니다.");

            case UpgradeResult.InvalidItem:
                return GetLabel(labels, "upgrade_ui.result_invalid_item", "강화할 수 없는 아이템입니다.");

            case UpgradeResult.InvalidCost:
                return GetLabel(labels, "upgrade_ui.result_invalid_cost", "강화 비용 설정이 올바르지 않습니다.");

            case UpgradeResult.WalletUnavailable:
                return GetLabel(labels, "upgrade_ui.result_wallet_unavailable", "강화 처리 중 문제가 발생했습니다.");

            default:
                return GetLabel(labels, "upgrade_ui.result_failed", "강화에 실패했습니다.");
        }
    }

    private static string GetLabel(UILabelDatabaseSO labels, string key, string fallback) =>
        labels != null ? labels.GetLabel(key) : fallback;
}

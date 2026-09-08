using UnityEngine;

public static class ShopMessageMapper
{
    private static UILabelDatabaseSO labelsCache;

    private static UILabelDatabaseSO Labels =>
        labelsCache ??= Resources.Load<UILabelDatabaseSO>("DataFiles/UIData/3. GeneratedAssets/UILabelDatabase");

    private static string GetLabel(string key, string fallback)
    {
        string label = Labels != null ? Labels.GetLabel(key) : null;
        return string.IsNullOrEmpty(label) ? fallback : label;
    }

    public static string GetMessage(
        TradeResult result,
        string itemName,
        bool isBuying)
    {
        switch (result)
        {
            case TradeResult.Success:
                return isBuying
                    ? string.Format(GetLabel("chat_ui.shop_buy_success", "{0}을 구매했습니다."), itemName)
                    : string.Format(GetLabel("chat_ui.shop_sell_success", "{0}을 판매했습니다."), itemName);

            case TradeResult.NotEnoughGold:
                return string.Format(GetLabel("chat_ui.shop_not_enough_gold", "골드가 부족해 {0} 구매에 실패했습니다."), itemName);

            case TradeResult.NoSpace:
                return isBuying
                    ? GetLabel("chat_ui.shop_no_space_buy", "인벤토리에 공간이 없습니다.")
                    : GetLabel("chat_ui.shop_no_space_sell", "상점에 판매 아이템을 놓을 공간이 없습니다.");

            case TradeResult.InvalidItem:
                return GetLabel("chat_ui.shop_invalid_item", "유효하지 않은 아이템입니다.");

            case TradeResult.TransferFailed:
                return string.Format(GetLabel("chat_ui.shop_transfer_failed", "{0} 이동 중 거래에 실패했습니다."), itemName);

            case TradeResult.StockUpdateFailed:
                return string.Format(GetLabel("chat_ui.shop_stock_update_failed", "{0} 재고 처리에 실패했습니다."), itemName);

            default:
                return GetLabel("chat_ui.shop_failed", "거래 처리에 실패했습니다.");
        }
    }
}

using UnityEngine;

public static class ChatMessageMapper
{
    private static UILabelDatabaseSO labelsCache;

    private static UILabelDatabaseSO Labels =>
        labelsCache ??= Resources.Load<UILabelDatabaseSO>("DataFiles/UIData/3. GeneratedAssets/UILabelDatabase");

    private static string GetLabel(string key, string fallback)
    {
        string label = Labels != null ? Labels.GetLabel(key) : null;
        return string.IsNullOrEmpty(label) ? fallback : label;
    }

    public static bool TryMap(MirrorInventoryRequestCompleted result, out ChatEntry entry)
    {
        entry = default;
        bool pickup = result.Operation == MirrorInventoryOperation.PickupWorldItem;
        bool drop = result.Operation == MirrorInventoryOperation.DropInventoryItem;
        bool equip = result.Operation == MirrorInventoryOperation.ChangeEquipment;
        bool upgrade = result.Operation == MirrorInventoryOperation.UpgradeItem;
        if ((!pickup && !drop && !equip && !upgrade) ||
            result.Result == MirrorInventoryRequestResult.DuplicateRequest)
            return false;
        if (result.Result == MirrorInventoryRequestResult.Success)
        {
            if (!pickup) return false;
            entry = new ChatEntry(ChatKind.Acquisition,
                string.IsNullOrEmpty(result.FeedbackText) ? GetLabel("chat_ui.pickup_default", "아이템을 획득했습니다.") : result.FeedbackText);
            return true;
        }

        string text = result.FeedbackText;
        if (string.IsNullOrEmpty(text))
        {
            string unknownItem = GetLabel("upgrade_ui.unknown_item", "아이템");
            text = result.Result switch
            {
                MirrorInventoryRequestResult.InventoryFull => InventoryMessageMapper.GetMessage(InventoryAddResult.NoSpace, unknownItem, 0, 0),
                MirrorInventoryRequestResult.InventoryUnavailable => InventoryMessageMapper.GetMessage(InventoryAddResult.GridUnavailable, unknownItem, 0, 0),
                MirrorInventoryRequestResult.NotEnoughGold => UpgradeMessageMapper.GetMessage(UpgradeResult.NotEnoughGold, unknownItem, 0, null),
                MirrorInventoryRequestResult.UpgradeUnavailable => UpgradeMessageMapper.GetMessage(UpgradeResult.InvalidItem, unknownItem, 0, null),
                MirrorInventoryRequestResult.OutOfRange => GetLabel("chat_ui.out_of_range", "아이템에 더 가까이 이동해 주세요."),
                MirrorInventoryRequestResult.AlreadyClaimed => GetLabel("chat_ui.already_claimed", "이미 획득된 아이템입니다."),
                MirrorInventoryRequestResult.PickupUnavailable => GetLabel("chat_ui.pickup_unavailable", "더 이상 획득할 수 없는 아이템입니다."),
                MirrorInventoryRequestResult.ItemUnavailable => GetLabel("chat_ui.item_unavailable", "대상 아이템을 찾을 수 없습니다."),
                MirrorInventoryRequestResult.StaleRevision => GetLabel("chat_ui.stale_revision", "아이템 상태가 바뀌었습니다. 다시 시도해 주세요."),
                MirrorInventoryRequestResult.RecoveryFailed when drop =>
                    GetLabel("chat_ui.recovery_failed", "아이템 드랍과 인벤토리 복구에 실패했습니다. 인벤토리 상태를 확인해 주세요."),
                _ => equip ? EquipMessageMapper.GetMessage(EquipResult.Failed) :
                    upgrade ? GetLabel("chat_ui.upgrade_process_failed", "강화를 처리하지 못했습니다. 다시 시도해 주세요.") :
                    drop ? GetLabel("chat_ui.drop_failed", "아이템을 필드에 놓지 못했습니다. 다시 시도해 주세요.") :
                    GetLabel("chat_ui.acquisition_failed", "아이템을 획득하지 못했습니다. 다시 시도해 주세요."),
            };
        }
        entry = new ChatEntry(ChatKind.Warning, text);
        return true;
    }

    public static bool TryMap(MirrorShopRequestCompleted result, out ChatEntry entry)
    {
        entry = default;
        if ((result.Operation != MirrorShopOperation.Buy && result.Operation != MirrorShopOperation.Sell) ||
            result.Result == MirrorShopRequestResult.Success || result.Result == MirrorShopRequestResult.DuplicateRequest)
            return false;
        bool buying = result.Operation == MirrorShopOperation.Buy;
        string unknownItem = GetLabel("upgrade_ui.unknown_item", "아이템");
        string text = result.Result switch
        {
            MirrorShopRequestResult.NotEnoughGold => ShopMessageMapper.GetMessage(TradeResult.NotEnoughGold, unknownItem, buying),
            MirrorShopRequestResult.InventoryFull => ShopMessageMapper.GetMessage(TradeResult.NoSpace, unknownItem, true),
            MirrorShopRequestResult.ShopFull => ShopMessageMapper.GetMessage(TradeResult.NoSpace, unknownItem, false),
            MirrorShopRequestResult.ItemUnavailable => ShopMessageMapper.GetMessage(TradeResult.InvalidItem, unknownItem, buying),
            MirrorShopRequestResult.ShopStateChanged => GetLabel("chat_ui.shop_state_changed", "상점 상태가 바뀌었습니다. 다시 선택해 주세요."),
            MirrorShopRequestResult.InventoryStateChanged => GetLabel("chat_ui.inventory_state_changed", "인벤토리 상태가 바뀌었습니다. 다시 시도해 주세요."),
            _ => GetLabel("chat_ui.trade_failed", "거래를 처리하지 못했습니다. 다시 시도해 주세요."),
        };
        entry = new ChatEntry(ChatKind.Warning, text);
        return true;
    }
}

public static class ChatMessageMapper
{
    public static bool TryMap(MirrorTestInventoryRequestCompleted result, out ChatEntry entry)
    {
        entry = default;
        bool pickup = result.Operation == MirrorTestInventoryOperation.PickupWorldItem;
        bool drop = result.Operation == MirrorTestInventoryOperation.DropInventoryItem;
        bool equip = result.Operation == MirrorTestInventoryOperation.ChangeEquipment;
        bool upgrade = result.Operation == MirrorTestInventoryOperation.UpgradeItem;
        if ((!pickup && !drop && !equip && !upgrade) ||
            result.Result == MirrorTestInventoryRequestResult.DuplicateRequest)
            return false;
        if (result.Result == MirrorTestInventoryRequestResult.Success)
        {
            if (!pickup) return false;
            entry = new ChatEntry(ChatKind.Acquisition,
                string.IsNullOrEmpty(result.FeedbackText) ? "아이템을 획득했습니다." : result.FeedbackText);
            return true;
        }

        string text = result.FeedbackText;
        if (string.IsNullOrEmpty(text))
        {
            text = result.Result switch
            {
                MirrorTestInventoryRequestResult.InventoryFull => InventoryMessageMapper.GetMessage(InventoryAddResult.NoSpace, "아이템", 0, 0),
                MirrorTestInventoryRequestResult.InventoryUnavailable => InventoryMessageMapper.GetMessage(InventoryAddResult.GridUnavailable, "아이템", 0, 0),
                MirrorTestInventoryRequestResult.NotEnoughGold => UpgradeMessageMapper.GetMessage(UpgradeResult.NotEnoughGold, "아이템", 0),
                MirrorTestInventoryRequestResult.UpgradeUnavailable => UpgradeMessageMapper.GetMessage(UpgradeResult.InvalidItem, "아이템", 0),
                MirrorTestInventoryRequestResult.OutOfRange => "아이템에 더 가까이 이동해 주세요.",
                MirrorTestInventoryRequestResult.AlreadyClaimed => "이미 획득된 아이템입니다.",
                MirrorTestInventoryRequestResult.PickupUnavailable => "더 이상 획득할 수 없는 아이템입니다.",
                MirrorTestInventoryRequestResult.ItemUnavailable => "대상 아이템을 찾을 수 없습니다.",
                MirrorTestInventoryRequestResult.StaleRevision => "아이템 상태가 바뀌었습니다. 다시 시도해 주세요.",
                MirrorTestInventoryRequestResult.RecoveryFailed when drop =>
                    "아이템 드랍과 인벤토리 복구에 실패했습니다. 인벤토리 상태를 확인해 주세요.",
                _ => equip ? EquipMessageMapper.GetMessage(EquipResult.Failed) :
                    upgrade ? "강화를 처리하지 못했습니다. 다시 시도해 주세요." :
                    drop ? "아이템을 필드에 놓지 못했습니다. 다시 시도해 주세요." :
                    "아이템을 획득하지 못했습니다. 다시 시도해 주세요.",
            };
        }
        entry = new ChatEntry(ChatKind.Warning, text);
        return true;
    }

    public static bool TryMap(MirrorTestShopRequestCompleted result, out ChatEntry entry)
    {
        entry = default;
        if ((result.Operation != MirrorTestShopOperation.Buy && result.Operation != MirrorTestShopOperation.Sell) ||
            result.Result == MirrorTestShopRequestResult.Success || result.Result == MirrorTestShopRequestResult.DuplicateRequest)
            return false;
        bool buying = result.Operation == MirrorTestShopOperation.Buy;
        string text = result.Result switch
        {
            MirrorTestShopRequestResult.NotEnoughGold => ShopMessageMapper.GetMessage(TradeResult.NotEnoughGold, "아이템", buying),
            MirrorTestShopRequestResult.InventoryFull => ShopMessageMapper.GetMessage(TradeResult.NoSpace, "아이템", true),
            MirrorTestShopRequestResult.ShopFull => ShopMessageMapper.GetMessage(TradeResult.NoSpace, "아이템", false),
            MirrorTestShopRequestResult.ItemUnavailable => ShopMessageMapper.GetMessage(TradeResult.InvalidItem, "아이템", buying),
            MirrorTestShopRequestResult.ShopStateChanged => "상점 상태가 바뀌었습니다. 다시 선택해 주세요.",
            MirrorTestShopRequestResult.InventoryStateChanged => "인벤토리 상태가 바뀌었습니다. 다시 시도해 주세요.",
            _ => "거래를 처리하지 못했습니다. 다시 시도해 주세요.",
        };
        entry = new ChatEntry(ChatKind.Warning, text);
        return true;
    }
}

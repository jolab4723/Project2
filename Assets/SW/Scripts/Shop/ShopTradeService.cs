using UnityEngine;

internal sealed class ShopTradeService
{
    private readonly PlayerWallet playerWallet;
    private readonly ShopStockService stockService;

    public ShopTradeService(PlayerWallet playerWallet, ShopStockService stockService)
    {
        this.playerWallet = playerWallet;
        this.stockService = stockService;
    }

    public TradeResult TryBuy(
        InventoryItem item,
        InventoryGrid shopGrid,
        InventoryGrid playerGrid,
        InventoryPlacementSnapshot targetPlacement)
    {
        if (item?.itemData?.definition == null ||
            shopGrid == null ||
            playerGrid == null ||
            playerWallet == null ||
            stockService == null)
        {
            return TradeResult.InvalidItem;
        }

        string instanceId = item.itemData.instanceId;

        // 상점 그리드에 보이더라도 재고 서비스에 등록되지 않은 아이템은 구매할 수 없다.
        if (string.IsNullOrWhiteSpace(instanceId) ||
            !stockService.TryGetEntry(instanceId, out ShopStockEntry stockEntry) ||
            stockEntry.Item != item)
        {
            return TradeResult.StockUpdateFailed;
        }

        if (!targetPlacement.IsValid ||
            !playerGrid.CanPlaceItem(
                targetPlacement.Rect.X,
                targetPlacement.Rect.Y,
                targetPlacement.Rect.Width,
                targetPlacement.Rect.Height))
        {
            return TradeResult.NoSpace;
        }

        // 상점 강화 패시브 할인을 적용한 값. 툴팁 표시도 같은 ShopPricing을 쓴다.
        // 플레이어 판매 재고는 지급했던 매입가보다 싸게 되팔지 않는다.
        int price = ShopPricing.GetStockBuyPrice(stockEntry);

        if (!playerWallet.TrySpendGold(price))
            return TradeResult.NotEnoughGold;

        // 이동 전에 재고에서 제거한다.
        if (!stockService.RemoveStock(instanceId))
        {
            playerWallet.AddGold(price);
            return TradeResult.StockUpdateFailed;
        }

        if (!TryTransferItem(
                item,
                shopGrid,
                playerGrid,
                targetPlacement))
        {
            // 아이템 이동에 실패했으므로 차감한 골드를 되돌린다.
            playerWallet.AddGold(price);

            // 제거했던 재고 항목도 원래 출처 그대로 복구
            if (!TryRestoreStock(stockEntry))
            {
                Debug.LogError(
                    "[ShopTradeService] 구매 실패 후 상점 재고 복구에도 실패했습니다.");

                return TradeResult.StockUpdateFailed;
            }
            return TradeResult.TransferFailed;
        }

        return TradeResult.Success;
    }

    public TradeResult TrySell(
        InventoryItem item,
        InventoryGrid playerGrid,
        InventoryGrid shopGrid,
        InventoryPlacementSnapshot targetPlacement)
    {
        if (item?.itemData?.definition == null ||
            playerGrid == null ||
            shopGrid == null ||
            playerWallet == null ||
            stockService == null)
        {
            return TradeResult.InvalidItem;
        }

        if (!targetPlacement.IsValid ||
            !shopGrid.CanPlaceItem(
                targetPlacement.Rect.X,
                targetPlacement.Rect.Y,
                targetPlacement.Rect.Width,
                targetPlacement.Rect.Height))
        {
            return TradeResult.NoSpace;
        }

        string instanceId = item.itemData.instanceId;

        // ID가 없거나 이미 등록된 아이템이면 거래를 시작하지 않는다.
        if (string.IsNullOrWhiteSpace(instanceId) ||
            stockService.TryGetEntry(instanceId, out _))
        {
            return TradeResult.StockUpdateFailed;
        }

        // 구매와 같은 할인율을 적용해 할인 구매 → 판매 왕복으로 골드가 늘지 않게 한다.
        int price = ShopPricing.GetSellPrice(item.itemData.definition);

        if (!TryTransferItem(
                item,
                playerGrid,
                shopGrid,
                targetPlacement))
        {
            return TradeResult.TransferFailed;
        }

        // 아이템 이동 후 재고 등록
        if (!stockService.RegisterPlayerSoldItem(item, price))
        {
            // 상점 그리드에서 다시 제거해야
            // ShopController가 원래 인벤토리 위치로 복구할 수 있다.
            if (!shopGrid.TryRemoveItem(item))
            {
                Debug.LogError(
                    "[ShopTradeService] 재고 등록 실패 후 상점 Grid에서도 아이템을 제거하지 못했습니다.");
            }

            return TradeResult.StockUpdateFailed;
        }

        // 아이템 이동과 재고 등록이 모두 성공한 뒤에만 지급한다.
        playerWallet.AddGold(price);

        return TradeResult.Success;
    }

    private bool TryRestoreStock(ShopStockEntry entry)
    {
        if (entry == null || stockService == null)
            return false;

        switch (entry.Source)
        {
            case ShopItemSource.Generated:
                return stockService.RegisterGeneratedItem(entry.Item);

            case ShopItemSource.PlayerSold:
                return stockService.RegisterPlayerSoldItem(
                    entry.Item,
                    entry.PricePaidToPlayer);

            default:
                return false;
        }
    }
    private static bool TryTransferItem(
        InventoryItem item,
        InventoryGrid sourceGrid,
        InventoryGrid targetGrid,
        InventoryPlacementSnapshot targetPlacement)
    {
        // 우클릭 거래라면 아직 sourceGrid에 있고,
        // 드래그 거래라면 ItemUI가 이미 제거했을 수 있다.
        bool removedByService = sourceGrid.ContainsItem(item);

        InventoryPlacementSnapshot sourcePlacement =
            removedByService
                ? InventoryPlacementSnapshot.Capture(sourceGrid, item)
                : default;

        if (removedByService &&
            !sourceGrid.TryRemoveItem(item))
        {
            Debug.LogError(
                "[ShopTradeService] 거래 원본 Grid에서 아이템을 제거하지 못했습니다.");
            return false;
        }

        // 원본 Grid는 기존 방향으로 제거한 뒤, 목표 Grid에 배치하기 직전에만 회전 상태를 바꾼다.
        item.isRotated = targetPlacement.IsRotated;

        if (targetGrid.TryPlaceItem(
                item,
                targetPlacement.Rect.X,
                targetPlacement.Rect.Y))
            return true;

        Debug.LogError(
            "[ShopTradeService] 거래 대상 Grid에 아이템을 배치하지 못했습니다.");

        // 서비스가 직접 제거한 경우에만 서비스가 복구한다.
        // 드래그 중 이미 제거된 경우에는 ItemUI가 원래 위치로 복구한다.
        if (removedByService &&
            !TryRestoreGridPlacement(
                item,
                sourceGrid,
                sourcePlacement))
        {
            Debug.LogError(
                "[ShopTradeService] 거래 실패 후 원본 Grid 복구에도 실패했습니다.");
        }

        return false;
    }

    private static bool TryRestoreGridPlacement(
        InventoryItem item,
        InventoryGrid sourceGrid,
        InventoryPlacementSnapshot sourcePlacement)
    {
        if (item == null ||
            sourceGrid == null ||
            !sourcePlacement.IsValid)
        {
            return false;
        }

        item.isRotated = sourcePlacement.IsRotated;

        return sourceGrid.TryPlaceItem(
            item,
            sourcePlacement.Rect.X,
            sourcePlacement.Rect.Y);
    }
}

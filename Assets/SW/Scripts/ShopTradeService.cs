using UnityEngine;
public class ShopTradeService
{
    private readonly PlayerWallet playerWallet;

    public ShopTradeService(PlayerWallet playerWallet)
    {
        this.playerWallet = playerWallet;
    }

    public TradeResult TryBuy(
        InventoryItem item,
        InventoryGrid shopGrid,
        InventoryGrid playerGrid,
        int targetX,
        int targetY)
    {
        if (item?.itemData?.definition == null ||
            shopGrid == null ||
            playerGrid == null ||
            playerWallet == null)
        {
            return TradeResult.InvalidItem;
        }

        if (!playerGrid.CanPlaceItem(
                targetX,
                targetY,
                item.CurrentWidth,
                item.CurrentHeight))
        {
            return TradeResult.NoSpace;
        }

        int price = item.itemData.definition.sellPrice;

        if (!playerWallet.TrySpendGold(price))
            return TradeResult.NotEnoughGold;

        if (!TryTransferItem(
                item,
                shopGrid,
                playerGrid,
                targetX,
                targetY))
        {
            // 아이템 이동에 실패했으므로 차감한 골드를 되돌린다.
            playerWallet.AddGold(price);
            return TradeResult.TransferFailed;
        }

        return TradeResult.Success;
    }

    public TradeResult TrySell(
        InventoryItem item,
        InventoryGrid playerGrid,
        InventoryGrid shopGrid,
        int targetX,
        int targetY)
    {
        if (item?.itemData?.definition == null ||
            playerGrid == null ||
            shopGrid == null ||
            playerWallet == null)
        {
            return TradeResult.InvalidItem;
        }

        if (!shopGrid.CanPlaceItem(
                targetX,
                targetY,
                item.CurrentWidth,
                item.CurrentHeight))
        {
            return TradeResult.NoSpace;
        }

        int price = item.itemData.definition.sellPrice;

        if (!TryTransferItem(
                item,
                playerGrid,
                shopGrid,
                targetX,
                targetY))
        {
            return TradeResult.TransferFailed;
        }

        // 아이템 이동이 확정된 후에만 골드를 지급한다.
        playerWallet.AddGold(price);

        return TradeResult.Success;
    }

    private static bool TryTransferItem(
        InventoryItem item,
        InventoryGrid sourceGrid,
        InventoryGrid targetGrid,
        int targetX,
        int targetY)
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

        if (targetGrid.TryPlaceItem(item, targetX, targetY))
            return true;

        Debug.LogError(
            "[ShopTradeService] 거래 대상 Grid에 아이템을 배치하지 못했습니다.");

        // 서비스가 직접 제거한 경우에만 서비스가 복구한다.
        // 드래그 중 이미 제거된 경우에는 ItemUI가 원래 위치로 복구한다.
        if (removedByService &&
            !TryRestoreItem(
                item,
                sourceGrid,
                sourcePlacement))
        {
            Debug.LogError(
                "[ShopTradeService] 거래 실패 후 원본 Grid 복구에도 실패했습니다.");
        }

        return false;
    }

    private static bool TryRestoreItem(
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
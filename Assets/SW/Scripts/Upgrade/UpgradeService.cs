using ItemSystem;

public sealed class UpgradeService
{
    private readonly PlayerWallet playerWallet;

    public UpgradeService(PlayerWallet playerWallet)
    {
        this.playerWallet = playerWallet;
    }

    public UpgradeResult TryUpgrade(
        ItemInstance item,
        int cost)
    {
        if (!CanUpgrade(item))
            return UpgradeResult.InvalidItem;

        if (cost <= 0)
            return UpgradeResult.InvalidCost;

        if (playerWallet == null)
            return UpgradeResult.WalletUnavailable;

        if (!playerWallet.TrySpendGold(cost))
            return UpgradeResult.NotEnoughGold;

        item.upgradeLevel++;

        return UpgradeResult.Success;
    }

    public static bool CanUpgrade(ItemInstance item)
    {
        return item?.definition != null &&
               item.definition.category != ItemCategory.Relic &&
               item.definition.mainOptions != null &&
               item.definition.mainOptions.Length > 0;
    }
}

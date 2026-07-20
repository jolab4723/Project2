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
        if (item?.definition?.mainOptions == null ||
            item.definition.mainOptions.Length == 0)
        {
            return UpgradeResult.InvalidItem;
        }

        if (cost <= 0)
            return UpgradeResult.InvalidCost;

        if (playerWallet == null)
            return UpgradeResult.WalletUnavailable;

        if (!playerWallet.TrySpendGold(cost))
            return UpgradeResult.NotEnoughGold;

        item.upgradeLevel++;

        return UpgradeResult.Success;
    }
}
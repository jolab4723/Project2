using System;
using ItemSystem;

public sealed class UpgradeService
{
    private const int BaseUpgradeCost = 500;
    private const double UpgradeCostMultiplier = 1.15d;
    private const int CostRoundingUnit = 10;

    private readonly PlayerWallet playerWallet;

    /// <summary>
    /// 지정한 플레이어 지갑을 사용하는 강화 서비스를 생성한다.
    /// </summary>
    public UpgradeService(PlayerWallet playerWallet)
    {
        this.playerWallet = playerWallet;
    }

    /// <summary>
    /// 공용 강화 규칙으로 비용을 계산하고 결제한 뒤 아이템 강화 수치를 증가시킨다.
    /// </summary>
    public UpgradeResult TryUpgrade(ItemInstance item)
    {
        if (!CanUpgrade(item))
            return UpgradeResult.InvalidItem;

        if (!TryGetUpgradeCost(item, out int cost))
            return UpgradeResult.InvalidCost;

        if (playerWallet == null)
            return UpgradeResult.WalletUnavailable;

        if (!playerWallet.TrySpendGold(cost))
            return UpgradeResult.NotEnoughGold;

        item.upgradeLevel++;

        return UpgradeResult.Success;
    }

    /// <summary>
    /// 현재 강화 단계에 해당하는 비용을 10골드 단위로 올림해 반환한다.
    /// 손상되었거나 계산 범위를 벗어난 아이템 데이터는 false를 반환한다.
    /// </summary>
    public static bool TryGetUpgradeCost(ItemInstance item, out int cost)
    {
        cost = 0;
        if (!CanUpgrade(item) || item.upgradeLevel < 0 || item.upgradeLevel == int.MaxValue)
            return false;

        double rawCost = BaseUpgradeCost * Math.Pow(UpgradeCostMultiplier, item.upgradeLevel);
        if (double.IsNaN(rawCost) || double.IsInfinity(rawCost) || rawCost <= 0d)
        {
            return false;
        }

        double roundedCost = Math.Ceiling(rawCost / CostRoundingUnit) * CostRoundingUnit;
        if (roundedCost > int.MaxValue)
            return false;

        cost = (int)roundedCost;
        return true;
    }

    /// <summary>
    /// 아이템 정의와 메인 옵션을 기준으로 강화 가능한 아이템인지 확인한다.
    /// </summary>
    public static bool CanUpgrade(ItemInstance item)
    {
        return item?.definition != null &&
               item.definition.category != ItemCategory.Relic &&
               item.definition.mainOptions != null &&
               item.definition.mainOptions.Length > 0;
    }
}

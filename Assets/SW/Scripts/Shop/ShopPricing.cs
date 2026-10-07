using Core;
using ItemSystem;
using UnityEngine;

/// <summary>
/// 상점 구매가를 계산하는 한 곳. **차감(ShopTradeService)과 표시(TooltipUI)가 반드시 같은 값을 써야 해서**
/// 계산을 여기로 모았다 - 예전엔 양쪽이 각각 definition.sellPrice를 그대로 읽어서,
/// 할인을 한쪽에만 넣으면 "툴팁엔 100인데 90이 빠지는" 불일치가 난다.
///
/// 할인 출처는 패시브 스킬트리의 "상점 강화"(PassiveSkillId.ShopEnhance)다.
/// 판매 지급액에도 판매자 본인의 할인율을 같은 계산으로 적용한다 - 예전엔 판매가에 할인을 빼서
/// "90에 구매 → 100에 판매"를 반복하면 골드가 계속 늘었다(최종 리뷰 R03, 팀 결정으로 판매에도 할인 적용).
/// 플레이어가 판 재고는 지급했던 매입가보다 싸게 되팔지 않는다 - 할인 없는 참가자가 판 물건을
/// 할인 참가자가 싸게 사서 돌려 파는 파티 단위 차익도 막는다.
/// </summary>
public static class ShopPricing
{
    /// <summary>싱글과 서버에 같은 할인 상한을 적용해 전부 공짜가 되는 상황을 막습니다.</summary>
    private const float MaxDiscountRatio = 0.95f;

    /// <summary>지금 적용 중인 상점 할인 비율(0~0.95). 패시브가 없거나 프로필이 없으면 0.</summary>
    public static float DiscountRatio
    {
        get
        {
            PassiveSkillManager passive = PassiveSkillManager.Instance;
            if (passive == null)
                return 0f;

            // !! PassiveSkillManager.ShopDiscountPercent는 "10"처럼 퍼센트 숫자를 돌려준다(0.1이 아님).
            //    공통 GetBuyPrice는 비율을 받으므로 여기서 100으로 나눠 미러 서버와 단위를 맞춘다.
            return Mathf.Clamp(passive.ShopDiscountPercent / 100f, 0f, MaxDiscountRatio);
        }
    }

    /// <summary>상점에서 이 아이템을 살 때 실제로 내는 금액.</summary>
    public static int GetBuyPrice(ItemDefinitionSO definition)
    {
        if (definition == null)
            return 0;

        return GetBuyPrice(definition.sellPrice);
    }

    /// <summary>정가에 현재 할인을 적용한다. 올림이라 1골드짜리가 0원이 되지 않는다.</summary>
    public static int GetBuyPrice(int basePrice)
    {
        return GetBuyPrice(basePrice, DiscountRatio);
    }

    /// <summary>전달된 플레이어의 할인율로 구매가를 계산합니다. 서버는 검증한 참가자 할인율을 전달합니다.</summary>
    public static int GetBuyPrice(int basePrice, float discountRatio)
    {
        if (float.IsNaN(discountRatio) || float.IsInfinity(discountRatio))
            discountRatio = 0f;
        float discounted = Mathf.Max(0, basePrice) * (1f - Mathf.Clamp(discountRatio, 0f, MaxDiscountRatio));
        return Mathf.Max(0, Mathf.CeilToInt(discounted));
    }

    /// <summary>재고 출처까지 반영한 구매가. 플레이어 판매 재고는 지급했던 매입가가 하한이다.</summary>
    public static int GetStockBuyPrice(int basePrice, float discountRatio, ShopItemSource source, int pricePaidToPlayer)
    {
        int price = GetBuyPrice(basePrice, discountRatio);
        return source == ShopItemSource.PlayerSold ? Mathf.Max(price, pricePaidToPlayer) : price;
    }

    /// <summary>싱글 상점 재고 항목의 실제 구매가. 차감(ShopTradeService)과 툴팁이 같이 쓴다.</summary>
    public static int GetStockBuyPrice(ShopStockEntry entry)
    {
        ItemDefinitionSO definition = entry?.Item?.itemData?.definition;
        if (definition == null)
            return 0;

        return GetStockBuyPrice(definition.sellPrice, DiscountRatio, entry.Source, entry.PricePaidToPlayer);
    }

    /// <summary>판매자 할인율을 적용한 판매 지급액. 같은 할인율의 구매가와 같아 왕복 차익이 생기지 않는다.</summary>
    public static int GetSellPrice(int basePrice, float discountRatio)
    {
        return GetBuyPrice(basePrice, discountRatio);
    }

    /// <summary>싱글 플레이어가 이 아이템을 상점에 팔 때 받는 금액.</summary>
    public static int GetSellPrice(ItemDefinitionSO definition)
    {
        return definition == null ? 0 : GetSellPrice(definition.sellPrice, DiscountRatio);
    }

    /// <summary>
    /// 이 아이템이 지금 상점 재고에 올라와 있는지. 툴팁이 할인가를 보여줄지 판단할 때 쓴다 -
    /// 인벤토리에 이미 들어온 아이템에까지 할인가를 표시하면 살 수 있는 값처럼 오해된다.
    /// </summary>
    public static bool IsShopItem(ItemInstance itemData)
    {
        if (itemData == null || string.IsNullOrWhiteSpace(itemData.instanceId))
            return false;

        ShopController shop = ShopController.Instance;
        return shop != null && shop.IsInStock(itemData.instanceId);
    }

    /// <summary>상점 재고에 있는 아이템의 재고 항목. 툴팁이 출처별 구매가를 계산할 때 쓴다.</summary>
    public static bool TryGetShopEntry(ItemInstance itemData, out ShopStockEntry entry)
    {
        entry = null;
        ShopController shop = ShopController.Instance;
        return itemData != null && shop != null && shop.TryGetStockEntry(itemData.instanceId, out entry);
    }
}

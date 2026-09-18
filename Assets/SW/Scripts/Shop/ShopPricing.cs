using Core;
using ItemSystem;
using UnityEngine;

/// <summary>
/// 상점 구매가를 계산하는 한 곳. **차감(ShopTradeService)과 표시(TooltipUI)가 반드시 같은 값을 써야 해서**
/// 계산을 여기로 모았다 - 예전엔 양쪽이 각각 definition.sellPrice를 그대로 읽어서,
/// 할인을 한쪽에만 넣으면 "툴팁엔 100인데 90이 빠지는" 불일치가 난다.
///
/// 할인 출처는 패시브 스킬트리의 "상점 강화"(PassiveSkillId.ShopEnhance)다.
/// 판매가(TrySell)에는 적용하지 않는다 - "상점 가격 하락"은 사는 쪽이 싸지는 것이고,
/// 파는 값까지 내리면 플레이어에게 손해다.
/// </summary>
public static class ShopPricing
{
    /// <summary>할인 상한. 전부 공짜가 되는 상황을 막는다(미러 테스트의 CalculateBuyPrice와 같은 값).</summary>
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
            //    미러 테스트 쪽 CalculateBuyPrice는 비율을 받으므로 여기서 100으로 나눠 단위를 맞춘다.
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
        float discounted = Mathf.Max(0, basePrice) * (1f - DiscountRatio);
        return Mathf.Max(0, Mathf.CeilToInt(discounted));
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
}

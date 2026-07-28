using UnityEngine;

public static class WBH_StatusEffectFactory
{
    public static WBH_IStatusEffect Create(WBH_StatusEffectController controller, WBH_StatusEffectData data)
    {
        //return data.Type switch
        //{
        //    WBH_StatusEffectType.Burn => new WBH_BurnEffect(controller, data),
        //    WBH_StatusEffectType.Freeze => new WBH_FreezeEffect(controller, data),
        //    WBH_StatusEffectType.Electric => new WBH_ElectricEffect(controller, data),
        //    WBH_StatusEffectType.Slow => new WBH_SlowEffect(controller, data),
        //    WBH_StatusEffectType.KnockBack => new WBH_KnockbackEffect(controller, data),
        //    WBH_StatusEffectType.Airborne => new WBH_AirborneEffect(controller, data),
        //    WBH_StatusEffectType.Stun => new WBH_StunEffect(controller, data),
        //    _ => null
        //};

        switch (data.Type)
        {
            case WBH_StatusEffectType.Burn:
                return new WBH_BurnEffect(controller, data);

            case WBH_StatusEffectType.Freeze:
                return new WBH_FreezeEffect(controller, data);

            case WBH_StatusEffectType.Electric:
                return new WBH_ElectricEffect(controller, data);

            case WBH_StatusEffectType.Slow:
                return new WBH_SlowEffect(controller, data);

            case WBH_StatusEffectType.KnockBack:
                return new WBH_KnockbackEffect(controller, data);

            case WBH_StatusEffectType.Airborne:
                return new WBH_AirborneEffect(controller, data);

            case WBH_StatusEffectType.Stun:
                return new WBH_StunEffect(controller, data);

            default:
                Log.Error($"지원하지 않는 상태이상입니다. ({data.Type})");
                return null;
        }
    }
}

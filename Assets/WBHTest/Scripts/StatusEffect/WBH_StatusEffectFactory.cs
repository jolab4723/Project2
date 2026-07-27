using UnityEngine;

public class WBH_StatusEffectFactory
{
    public static WBH_IStatusEffect Create(WBH_StatusEffectController controller, WBH_StatusEffectData data)
    {
        return data.Type switch
        {
            WBH_StatusEffectType.Burn      =>  new WBH_BurnEffect(controller, data),
            WBH_StatusEffectType.Freeze    =>  new WBH_FreezeEffect(data, controller),
            WBH_StatusEffectType.Electric  =>  new WBH_ElectricEffect(data, controller),
            WBH_StatusEffectType.Slow      =>  new WBH_SlowEffect(data, controller),
            WBH_StatusEffectType.KnockBack =>  new WBH_KnockbackEffect(data, controller),
            WBH_StatusEffectType.Airborne  =>  new WBH_AirborneEffect(data, controller),
            WBH_StatusEffectType.Stun      =>  new WBH_StunEffect(data, controller),
            _ => null
        };
    }
}

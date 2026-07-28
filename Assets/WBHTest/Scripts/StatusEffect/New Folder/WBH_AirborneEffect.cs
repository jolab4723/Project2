using UnityEngine;

public class WBH_AirborneEffect : WBH_StatusEffectBase
{
    public WBH_AirborneEffect(WBH_StatusEffectController controller, WBH_StatusEffectData data) : base(controller, data) { }

    public override void Apply()
    {
        controller.SetControlEnable(false);

        controller.ApplyAirborne(data.Force, data.Duration);

        controller.PlayStatusEffect(EffectType);
        controller.PlayStatusSound(EffectType);
    }

    public override void Remove()
    {
        controller.SetControlEnable(true);

        controller.StopStatusEffect(EffectType);
    }
}

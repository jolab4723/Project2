using UnityEngine;

public class WBH_StunEffect : WBH_StatusEffectBase
{
    public WBH_StunEffect(WBH_StatusEffectController controller, WBH_StatusEffectData data) : base(controller, data) { }

    public override void Apply()
    {
        controller.SetControlEnable(false);

        controller.PlayStatusEffect(EffectType);
        controller.PlayStatusSound(EffectType);
    }

    public override void Remove()
    {
        controller.SetControlEnable(true);

        controller.StopStatusEffect(EffectType);
    }
}

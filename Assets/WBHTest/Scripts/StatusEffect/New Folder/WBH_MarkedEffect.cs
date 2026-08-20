using UnityEngine;

public class WBH_MarkedEffect : WBH_StatusEffectBase
{
    public WBH_MarkedEffect(WBH_StatusEffectController controller, WBH_StatusEffectData data) : base(controller, data) { }

    public override void Apply()
    {
        controller.SetDamageTakenModifier(this, data.Value);

        controller.PlayStatusEffect(EffectType);
        controller.PlayStatusSound(EffectType);
    }

    public override void Remove()
    {
        controller.RemoveDamageTakenModifier(this);

        controller.StopStatusEffect(EffectType);
    }
}

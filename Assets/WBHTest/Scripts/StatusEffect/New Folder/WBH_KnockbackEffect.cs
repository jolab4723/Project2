using UnityEngine;

public class WBH_KnockbackEffect : WBH_StatusEffectBase
{
    public WBH_KnockbackEffect(WBH_StatusEffectController controller, WBH_StatusEffectData data) : base(controller, data) { }

    public override void Apply()
    {
        controller.ApplyKnockback(data.Direction, data.Force, data.Duration);

        controller.PlayStatusEffect(EffectType);
        controller.PlayStatusSound(EffectType);
    }

    public override void Remove()
    {
        controller.StopStatusEffect(EffectType);
    }
}

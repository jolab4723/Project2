using UnityEngine;

public class WBH_DefenseDownEffect : WBH_StatusEffectBase
{
    public WBH_DefenseDownEffect(WBH_StatusEffectController controller, WBH_StatusEffectData data) : base(controller, data) { }

    public override void Apply()
    {
        controller.SetDefenseModifier(this, data.Value);

        controller.PlayStatusEffect(EffectType);
        controller.PlayStatusSound(EffectType);
    }

    public override void Remove()
    {
        controller.RemoveDefenseModifier(this);

        controller.StopStatusEffect(EffectType);
    }
}

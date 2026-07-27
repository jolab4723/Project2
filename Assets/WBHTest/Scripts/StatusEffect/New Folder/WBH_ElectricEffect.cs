using UnityEngine;

public class WBH_ElectricEffect : WBH_StatusEffectBase
{
    public WBH_ElectricEffect(WBH_StatusEffectController controller, WBH_StatusEffectData data) : base(controller, data) { }

    public override void Apply()
    {
        controller.SetMoveSpeedModifier(this, data.Value);

        controller.PlayStatusEffect(EffectType);
        controller.PlayStatusSound(EffectType);
    }

    public override void Remove()
    {
        controller.RemoveAttackModifier(this);

        controller.StopStatusEffect(EffectType);
    }
}

using UnityEngine;

public class WBH_SlowEffect : WBH_StatusEffectBase
{
    public WBH_SlowEffect(WBH_StatusEffectController controller, WBH_StatusEffectData data) : base(controller, data) { }

    public override void Apply()
    {
        controller.SetMoveSpeedModifier(this, data.Value);

        controller.PlayStatusEffect(EffectType);
        controller.PlayStatusSound(EffectType);
    }

    public override void Remove()
    {
        controller.RemoveMoveSpeedModifier(this);
        Log.Print("Remove Slow");

        controller.StopStatusEffect(EffectType);
    }
}

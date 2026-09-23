using UnityEngine;

public class WBH_FreezeEffect : WBH_StatusEffectBase
{
    public WBH_FreezeEffect(WBH_StatusEffectController controller, WBH_StatusEffectData data) : base(controller, data) { }

    public override void Apply()
    {
        controller.SetMoveSpeedModifier(this, data.Value);
        controller.SetAttackSpeedModifier(this, data.Value);

        controller.PlayStatusEffect(EffectType);
        controller.PlayStatusSound(EffectType);
    }

    public override void Remove()
    {
        controller.RemoveMoveSpeedModifier(this);
        controller.RemoveAttackSpeedModifier(this);

        controller.StopStatusEffect(EffectType);
    }

    public override void Refresh(WBH_StatusEffectData data)
    {
        base.Refresh(data);
        controller.SetMoveSpeedModifier(this, data.Value);
        controller.SetAttackSpeedModifier(this, data.Value);
    }
}

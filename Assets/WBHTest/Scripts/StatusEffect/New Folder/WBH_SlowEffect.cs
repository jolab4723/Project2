using UnityEngine;

public class WBH_SlowEffect : WBH_StatusEffectBase
{
    private float slowMultiplier;

    //public override WBH_StatusEffectType EffectType => WBH_StatusEffectType.Slow;

    public WBH_SlowEffect(WBH_StatusEffectController owner, WBH_StatusEffectData data) : base(owner, data) { }

    public override void Apply()
    {
        {
            slowMultiplier = 1f - data.Value;
            //owner.Status.MultiplyMoveSpeed(slowMultiplier);
            //owner.PlayEffect(EffectType);
        }
    }

    public override void Remove()
    {
        //owner.Status.MultiplyMoveSpeed(1f / slowMultiplier);
        //owner.StopEffect(EffectType);
    }

    public override void Refresh(WBH_StatusEffectData data)
    {
        base.Refresh(data);
    }
}

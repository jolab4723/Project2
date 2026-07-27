using UnityEngine;

public class WBH_BurnEffect : WBH_StatusEffectBase
{
    private float tickTimer;

    public WBH_BurnEffect(WBH_StatusEffectController controller, WBH_StatusEffectData data) : base(controller, data) { }

    public override void Apply()
    {
        tickTimer = 0f;

        controller.PlayStatusEffect(EffectType);
        controller.PlayStatusSound(EffectType);
    }

    protected override void OnTick(float deltaTime)
    {
        tickTimer += deltaTime;

        if (tickTimer < data.Interval)
            return;

        tickTimer -= data.Interval;
        float damage = controller.GetMaxHealth() * data.Value;
        controller.ApplyDotDamage(damage);
    }

    public override void Remove()
    {
        controller.StopStatusEffect(EffectType);
    }

    public override void Refresh(WBH_StatusEffectData data)
    {
        base.Refresh(data);

        tickTimer = 0f;
    }
}

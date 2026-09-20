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

    /// <summary>SW 수정: 긴 프레임이어도 실제 남은 지속시간 안의 틱만 처리합니다.</summary>
    public override void Tick(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f)
            return;
        base.Tick(Mathf.Min(deltaTime, remainingTime));
    }

    protected override void OnTick(float deltaTime)
    {
        tickTimer += deltaTime;

        // SW 수정: 한 프레임에 여러 주기가 지나도 빠진 틱을 처리합니다.
        while (data.Interval > 0f && tickTimer >= data.Interval)
        {
            tickTimer = Mathf.Max(0f, tickTimer - data.Interval);
            float damage = controller.GetMaxHealth() * data.Value;
            controller.ApplyDotDamage(damage, data);
        }
    }

    public override void Remove()
    {
        controller.StopStatusEffect(EffectType);
    }

    /// <summary>
    /// SW 수정: 중첩하지 않고 마지막 적용의 강도·출처·지속시간으로 갱신합니다.
    /// 틱 시계는 유지하므로 연속 공격으로 화상 피해가 계속 뒤로 밀리지 않습니다.
    /// </summary>
    public override void Refresh(WBH_StatusEffectData data)
    {
        base.Refresh(data);
    }
}

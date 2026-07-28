using UnityEngine;

public abstract class WBH_StatusEffectBase : WBH_IStatusEffect
{
    protected readonly WBH_StatusEffectController controller; // 상태이상이 적용된 대상
    protected WBH_StatusEffectData data;

    protected float remainingTime; // 감소용 타이머 변수

    public WBH_StatusEffectType EffectType => data.Type;

    public float RemainingTime => remainingTime;
    public bool IsFinished => remainingTime <= 0f;

    protected WBH_StatusEffectBase(WBH_StatusEffectController controller, WBH_StatusEffectData data)
    {
        this.controller = controller;
        this.data = data;

        remainingTime = data.Duration;
    }

    public abstract void Apply();

    // 타이머 감소
    public virtual void Tick(float deltaTime)
    {
        remainingTime -= deltaTime;

        if (remainingTime < 0f)
            remainingTime = 0f;

        OnTick(deltaTime);
    }

    protected virtual void OnTick(float deltaTime) { }

    public abstract void Remove();

    // 지속시간 갱신
    public virtual void Refresh(WBH_StatusEffectData data)
    {
        this.data = data;
        remainingTime = data.Duration;
    }
}

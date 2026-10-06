using UnityEngine;

public class WBH_FreezeEffect : WBH_StatusEffectBase
{
    public WBH_FreezeEffect(WBH_StatusEffectController controller, WBH_StatusEffectData data) : base(controller, data) { }

    public override void Apply()
    {
        controller.SetMoveSpeedModifier(this, data.Value);
        controller.SetAttackSpeedModifier(this, data.Value);
        // SW 수정: 기존 약한 냉기 둔화는 유지하고 강도 0인 짧은 완전 빙결만 기존 행동 제어 경계로 막는다.
        controller.SetStatusControlBlock(EffectType, data.Value <= 0f);

        controller.PlayStatusEffect(EffectType);
        controller.PlayStatusSound(EffectType);
    }

    public override void Remove()
    {
        controller.RemoveMoveSpeedModifier(this);
        controller.RemoveAttackSpeedModifier(this);
        controller.SetStatusControlBlock(EffectType, false);

        controller.StopStatusEffect(EffectType);
    }

    public override void Refresh(WBH_StatusEffectData data)
    {
        base.Refresh(data);
        controller.SetMoveSpeedModifier(this, data.Value);
        controller.SetAttackSpeedModifier(this, data.Value);
        controller.SetStatusControlBlock(EffectType, data.Value <= 0f);
    }
}

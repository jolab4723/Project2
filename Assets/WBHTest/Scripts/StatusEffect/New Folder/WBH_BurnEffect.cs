using UnityEngine;

public class WBH_BurnEffect : WBH_StatusEffectBase
{
    private float tickTimer;
    public WBH_StatusEffectData Source => data;

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
            // SW 수정: 사망 시 상태 목록이 지워져도 이번 틱의 실제 Burn 출처와 사망 전 위치를 보존한다.
            WBH_StatusEffectData source = data;
            var enemy = controller.GetComponent<WBH_EnemyController>();
            var status = enemy != null ? enemy.Status : null;
            bool wasAlive = status != null && !status.IsDead;
            Vector3 position = controller.transform.position;
            controller.ApplyDotDamage(damage, data);
            if (wasAlive && status.IsDead)
            {
                var player = (source.Attacker as T_PlayerController)?.GetComponent<PlayerContext>();
                player?.Effects.FireWildfireKill(source, position, source.AttackId);
                break;
            }
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

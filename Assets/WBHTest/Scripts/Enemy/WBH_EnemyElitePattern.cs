using UnityEngine;

public class WBH_EnemyElitePattern : WBH_IEnemyPattern
{
    private WBH_EnemyPattern owner;

    private float dashCooldown;
    private float shootCooldown;

    private const float DashCooldown = 5f;
    private const float ShootCooldown = 7f;

    private float dashRange = 10f;
    private float dashMinRange = 4f;
    protected float dashHitRadius = 3f;

    public void Initialize(WBH_EnemyPattern owner)
    {
        this.owner = owner;

        dashHitRadius = 3f;
    }

    public void Tick(float deltaTime)
    {
        dashCooldown -= deltaTime;
        shootCooldown -= deltaTime;

        if (owner.Combat.IsActionInProgress)
            return;

        float distance = owner.Distance;

        // 기본 공격
        if(distance <= owner.AttackRange)
        {
            owner.Combat.TryAttack();
            return;
        }

        // 돌진
        if(distance <= dashRange && distance >= dashMinRange && dashCooldown <= 0f)
        {
            if(owner.Combat.TryDashAttack(dashRange, 0.4f, owner.IndicatorSpawner, dashHitRadius * 2f, 1f))
            {
                dashCooldown = DashCooldown;
            }
            return;
        }

        // 연사
        if(distance > dashRange && shootCooldown <= 0f)
        {
            if(owner.Combat.TryShootBurst(5))
            {
                shootCooldown = ShootCooldown;
            }
            return;
        }

        owner.Movement.Move(owner.Target.position);
    }
}

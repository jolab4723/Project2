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

        float distance = owner.Distance;

        // 기본 공격
        if(distance <= owner.AttackRange)
        {
            owner.Combat.Attack();
            return;
        }

        // 돌진
        if(distance <= dashRange && distance >= dashMinRange && dashCooldown <= 0f)
        {
            dashCooldown = DashCooldown;

            owner.Indicator.SetSize(dashHitRadius, dashRange);

            owner.Combat.DashAttack(dashRange, 0.4f, owner.Indicator);

            return;
        }

        // 돌진
        if(distance > dashRange && shootCooldown <= 0f)
        {
            shootCooldown = ShootCooldown;

            owner.Combat.ShootBurst(5);

            return;
        }

        owner.Movement.Move(owner.Target.position);
    }
}

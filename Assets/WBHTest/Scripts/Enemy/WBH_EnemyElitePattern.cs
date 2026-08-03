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

    public void Initialize(WBH_EnemyPattern owner)
    {
        this.owner = owner;
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

            owner.Combat.DashAttack(dashRange);

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

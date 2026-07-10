using UnityEngine;

[RequireComponent(typeof(WBH_EnemyMovement))]
[RequireComponent(typeof(WBH_EnemyCombat))]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyAnimation))]
[RequireComponent(typeof(WBH_EffectSpawner))]
[RequireComponent(typeof(WBH_ProjectileSpawner))]
public class WBH_EnemyPattern : MonoBehaviour
{
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform meleeEffectPoint;
    [SerializeField] private WBH_EffectData normalMeleeEffect;

    private WBH_EnemyController controller;
    private WBH_EnemyMovement movement;
    private WBH_EnemyCombat combat;
    private WBH_EnemyStatus status;
    private WBH_EnemyAnimation enemyAnimation;

    private WBH_EffectSpawner effectSpawner;

    private WBH_ProjectileSpawner projectileSpawner;

    private Transform target;

    private float distance = 0;

    public virtual void Initialize(WBH_EnemyController controller)
    {
        this.controller = controller;
        movement = GetComponent<WBH_EnemyMovement>();
        combat = GetComponent<WBH_EnemyCombat>();
        status = GetComponent<WBH_EnemyStatus>();
        enemyAnimation = GetComponent<WBH_EnemyAnimation>();
        effectSpawner = GetComponent<WBH_EffectSpawner>();
        projectileSpawner = GetComponent<WBH_ProjectileSpawner>();
    }

    protected virtual void Update()
    {
        if(status.IsDead)
        {
            Die();
            return;
        }

        if (target == null)
            return;

        distance = Vector3.Distance(transform.position, target.position);

        UpdateMove(distance);
        UpdateAttack(distance);
    }

    protected virtual void UpdateMove(float distance)
    {
        if(distance > combat.AttackRange)
        {
            movement.Move(target.position);
            enemyAnimation.SetMove(true);
        }
        else
        {
            movement.Stop();
            enemyAnimation.SetMove(false);
        }
    }

    protected virtual void UpdateAttack(float distance)
    {
        if (distance > combat.AttackRange)
            return;

        combat.Attack();
    }

    public virtual void Hit()
    {
        enemyAnimation.PlayHit();
    }

    public virtual void Die()
    {
        movement.Stop();
        enemyAnimation.PlayDie();
        enabled = false;
    }

    public virtual void ExecuteAttack()
    {
        switch(controller.Info.enemyType)
        {
            case EnemyType.Melee:
                MeleeAttack();
                break;
            case EnemyType.Ranged:
                RangedAttack();
                break;
        }
    }

    protected virtual void MeleeAttack()
    {
        SectorAttack(combat.AttackRange, 120f, status.Attack);
        effectSpawner.SpawnEffect(normalMeleeEffect, meleeEffectPoint);
    }

    protected virtual void RangedAttack()
    {
        Vector3 targetPos = target.position + Vector3.up;

        Vector3 direction = (targetPos - firePoint.position).normalized;

        projectileSpawner.FireProjectile(ProjectileType.NormalEnemy, firePoint.position, direction, status.Attack, status.ProjectileSpeed, combat.AttackRange, playerLayer);
    }

    // player 공격 코드 재활용
    // 전방 부채꼴 범위 공격
    private void SectorAttack(float range, float angle, float damage)
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, range, playerLayer);

        foreach (Collider target in targets)
        {
            Vector3 dirToTarget = (target.transform.position - transform.position).normalized;

            dirToTarget.y = 0;
            float targetAngle = Vector3.Angle(transform.forward, dirToTarget);
            if (targetAngle <= angle * 0.5f)
            {
                if (target.TryGetComponent<T_IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(damage);
                }
            }
        }
    }

    // 멀티플레이 감안. 타겟 설정.
    // Spawner 에서 호출 예정.
    public void SetTarget(Transform target)
    {
        this.target = target;
    }
}

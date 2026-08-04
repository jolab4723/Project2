using System.Collections;
using UnityEngine;

[RequireComponent(typeof(WBH_EnemyController))]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyAnimation))]

public class WBH_EnemyCombat : MonoBehaviour
{
    private WBH_EnemyController controller;
    private WBH_EnemyStatus status;
    private WBH_EnemyAnimation enemyAnimation;
    private WBH_EnemyPattern pattern;
    private WBH_EnemyMovement movement;
    private WBH_ProjectileSpawner projectileSpawner;

    private float attackTimer = 0f;
    private bool isDashAttack;
    private bool hasHitTarget;


    private void Awake()
    {
        controller = GetComponent<WBH_EnemyController>();
        status = GetComponent<WBH_EnemyStatus>();
        enemyAnimation = GetComponent<WBH_EnemyAnimation>();
        pattern = GetComponent<WBH_EnemyPattern>();
        movement = GetComponent<WBH_EnemyMovement>();
        projectileSpawner = GetComponent<WBH_ProjectileSpawner>();

    }

    private void OnEnable()
    {
        movement.OnDashUpdate += CheckDashHit;
    }
    private void OnDisable()
    {
        movement.OnDashUpdate -= CheckDashHit;
    }

    private void Update()
    {
        if(attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
        }
        attackTimer = Mathf.Max(attackTimer, 0f);
    }

    public void Initialize(WBH_EnemyInfo info)
    {
        attackTimer = 0f;
    }

    public bool CanAttack()
    {
        return attackTimer <= 0f;
    }

    public void Attack()
    {
        // 움직일 수 없는 상태가 아니거나 공격 쿨타임이 돌지 않았다면 return
        if (!CanAttack() || !movement.CanControl)
            return;

        ResetAttackCoolTime();

        enemyAnimation.PlayAttack(pattern.ExecuteAttack);
    }

    public void ResetAttackCoolTime()
    {
        attackTimer = controller.Info.attackCoolTime / status.AttackSpeed;
    }

    // 투사체 외
    public WBH_DamageRequest CreateDamageRequest(WBH_ICombat target, WBH_AttackType atkType, ItemSystem.ElementType elementType, float damageMult)
    {
        return new WBH_DamageRequest(controller, target, atkType, elementType, damageMult);
    }

    // 투사체는 타겟이 충돌 시 결정되기에 null 로 비워둠.
    public WBH_DamageRequest CreateDamageRequest(WBH_AttackType atkType, ItemSystem.ElementType elementType, float damageMult)
    {
        return new WBH_DamageRequest(controller, null, atkType, elementType, damageMult);
    }

    public void DashAttack(float distance, float duration)
    {
        isDashAttack = true;
        movement.Dash(transform.forward, distance, duration);
    }

    public void ShootBurst(int count)
    {
        StartCoroutine(CoShootBurst(count));
    }

    private IEnumerator CoShootBurst(int count)
    {
        for(int i = 0; i < count; i++)
        {
            FireProjectile();

            yield return new WaitForSeconds(0.15f);
        }
    }

    private void FireProjectile()
    {
        Vector3 dir = (pattern.Target.position - pattern.FirePoint.position).normalized;

        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1);

        projectileSpawner.FireProjectile(ProjectileType.NormalEnemy, pattern.FirePoint.position, dir, request, status.ProjectileSpeed, 12f, pattern.PlayerLayer);
    }    

    private void CheckDashHit()
    {
        if (hasHitTarget)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, pattern.DashHitRadius, pattern.PlayerLayer);

        foreach(Collider hit in hits)
        {
            if (!hit.TryGetComponent<WBH_ICombat>(out var target))
                continue;

            hasHitTarget = true;

            WBH_CombatManager.ProcessDamage(CreateDamageRequest(target, WBH_AttackType.Normal, ItemSystem.ElementType.None, 1f));

            break;
        }
    }
}

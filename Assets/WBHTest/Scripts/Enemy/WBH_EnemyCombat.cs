using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms.GameCenter;

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
    private bool hasHitTarget;
    public bool IsActionInProgress { get; private set; }


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

        hasHitTarget = false;
        IsActionInProgress = false;
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
        hasHitTarget = false;
        IsActionInProgress = false;
    }

    public bool CanAttack()
    {
        return attackTimer <= 0f;
    }

    public bool TryAttack()
    {
        // 현재 다른 행동 중이 아니거나 움직일 수 없는 상태가 아니거나 공격 쿨타임이 돌지 않았다면 return
        if (IsActionInProgress || !CanAttack() || !movement.CanControl)
            return false;

        BeginAction();
        ResetAttackCoolTime();

        enemyAnimation.PlayAttack(pattern.ExecuteAttack, EndAction);
        return true;
    }

    public void ResetAttackCoolTime()
    {
        attackTimer = controller.Info.attackCoolTime / status.AttackSpeed;
    }

    // 투사체 외 데미지 요청
    public WBH_DamageRequest CreateDamageRequest(WBH_ICombat target, WBH_AttackType atkType, ItemSystem.ElementType elementType, float damageMult)
    {
        return new WBH_DamageRequest(controller, target, atkType, elementType, damageMult);
    }

    // 투사체는 타겟이 충돌 시 결정되기에 null 로 비워둠.
    public WBH_DamageRequest CreateDamageRequest(WBH_AttackType atkType, ItemSystem.ElementType elementType, float damageMult)
    {
        return new WBH_DamageRequest(controller, null, atkType, elementType, damageMult);
    }

    // 현재 일반 공격 및 패턴 동작 여부. 각 패턴 시작과 종료에 추가
    private void BeginAction()
    {
        IsActionInProgress = true;
    }
    private void EndAction()
    {
        IsActionInProgress = false;
    }

    //------- 엘리트 등 특수 패턴용 메서드

    // 돌진
    public bool TryDashAttack(float distance, float duration, WBH_Indicator indicator = null)
    {
        if (IsActionInProgress)
            return false;

        BeginAction();
        transform.LookAt(pattern.Target);

        StartCoroutine(CoDashAttack(distance, duration,indicator));
        return true;
    }

    private IEnumerator CoDashAttack(float distance, float duration, WBH_Indicator indicator)
    {
        indicator.Show();

        yield return new WaitForSeconds(1f);

        indicator.Hide();

        enemyAnimation.PlayDash();

        movement.Dash(transform.forward, distance, duration, EndAction);
    }

    // 돌진 중 플레이어 충돌 체크
    private void CheckDashHit()
    {
        if (hasHitTarget)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, pattern.DashHitRadius, pattern.PlayerLayer);

        foreach (Collider hit in hits)
        {
            if (!hit.TryGetComponent<WBH_ICombat>(out var target))
                continue;

            hasHitTarget = true;

            WBH_CombatManager.ProcessDamage(CreateDamageRequest(target, WBH_AttackType.Normal, ItemSystem.ElementType.None, 1f));

            break;
        }
    }

    // 연발 사격
    public bool TryShootBurst(int count)
    {
        if (IsActionInProgress)
            return false;

        BeginAction();
        transform.LookAt(pattern.Target);

        enemyAnimation.PlayShootBurst();
        StartCoroutine(CoShootBurst(count));
        return true;
    }

    private IEnumerator CoShootBurst(int count)
    {
        for(int i = 0; i < count; i++)
        {
            FireProjectile();

            if(i < count -1)
                yield return new WaitForSeconds(0.15f);
        }
        EndAction();
    }

    private void FireProjectile()
    {
        Vector3 dir = (pattern.Target.position - pattern.FirePoint.position).normalized;

        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1);

        projectileSpawner.FireProjectile(ProjectileType.NormalEnemy, pattern.FirePoint.position, dir, request, status.ProjectileSpeed, 12f, pattern.PlayerLayer);
    }    

    public bool TryBarrage(int projectileCount, float spreadAngle, float maxDistance, float actionDuration)
    {
        if (IsActionInProgress || pattern.Target == null)
            return false;

        BeginAction();
        FaceTarget(pattern.Target);

        Vector3 dir = (pattern.Target.position - pattern.FirePoint.position).normalized;

        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1f);

        projectileSpawner.FireMultipleProjectile(ProjectileType.NormalEnemy, 
                                                 pattern.FirePoint.position, 
                                                 dir, 
                                                 request, 
                                                 status.ProjectileSpeed, 
                                                 maxDistance, 
                                                 pattern.PlayerLayer, 
                                                 projectileCount, 
                                                 spreadAngle); // !@ 투사체 종류 수정 필요

        StartCoroutine(CoEndAction(actionDuration));
        return true;
    }
    
    public bool TryMissile(IReadOnlyList<Vector3> impactPoints, float explosionRadius, float warningDuration, float recoveryDuration, WBH_IndicatorSpawner indicatorSpawner)
    {
        if (IsActionInProgress || impactPoints == null || impactPoints.Count == 0 || indicatorSpawner == null)
            return false;

        BeginAction();

        StartCoroutine(CoMissile(impactPoints, explosionRadius, warningDuration, recoveryDuration, indicatorSpawner));

        return true;
    }

    private IEnumerator CoMissile(IReadOnlyList<Vector3> impactPoints, float explosionRadius, float warningDuration, float recoveryDuration, WBH_IndicatorSpawner indicatorSpawner)
    {
        foreach(Vector3 impactPoint in impactPoints)
        {
            indicatorSpawner.ShowCircle(impactPoint, explosionRadius, warningDuration, growOverTime: true);
        }
        yield return new WaitForSeconds(warningDuration);

        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1f);

        foreach(Vector3 impactPoint in impactPoints)
        {
            projectileSpawner.FireGrenade(ProjectileType.Missile, 
                                          pattern.FirePoint.position, 
                                          impactPoint, 
                                          request, 
                                          status.ProjectileSpeed, 
                                          100f, 
                                          explosionRadius, 
                                          pattern.PlayerLayer);
        }
        yield return new WaitForSeconds(recoveryDuration);
        EndAction();
    }

    public bool TryJumpAttack(Vector3 landingPos, float damageRadius, float jumpDuration, float recoveryDuration)
    {
        if(IsActionInProgress || !movement.CanJumpTo(landingPos))
            return false;

        BeginAction();
        FacePosition(landingPos);

        StartCoroutine(CoJumpAttack(landingPos, damageRadius, jumpDuration, recoveryDuration));
        return true;
    }

    private IEnumerator CoJumpAttack(Vector3 landingPos, float damageRadius, float jumpDuration, float recoveryDuration)
    {
        bool landed = false;

        movement.JumpTo(landingPos, jumpDuration, () => landed = true);

        yield return new WaitUntil(() => landed);

        ApplyAreaDamage(landingPos, damageRadius);
        EndAction();
    }

    private IEnumerator CoEndAction(float duration)
    {
        yield return new WaitForSeconds(duration);
        EndAction();
    }

    private void FaceTarget(Transform target)
    {
        FacePosition(target.position);
    }

    private void FacePosition(Vector3 position)
    {
        Vector3 dir = position - transform.position;
        dir.y = 0f;

        if(dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    private void ApplyAreaDamage(Vector3 center, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, pattern.PlayerLayer);

        foreach (Collider hit in hits)
        {
            if (!hit.TryGetComponent<WBH_ICombat>(out var target))
                continue;

            WBH_CombatManager.ProcessDamage(CreateDamageRequest(target, WBH_AttackType.Normal, ItemSystem.ElementType.None, 1f));
        }
    }
}

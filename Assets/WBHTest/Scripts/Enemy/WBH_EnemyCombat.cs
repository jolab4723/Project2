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
    private float missileMaxDistance = 100f;
    private float minMissileFlightTime = 1f;

    private readonly HashSet<WBH_ICombat> dashHitTargets = new();
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
        
        dashHitTargets.Clear();
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
        dashHitTargets.Clear();
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
    public bool TryDashAttack(float distance, float duration, WBH_IndicatorSpawner indicator, float indicatorWidth, float readyDuration)
    {
        if (IsActionInProgress || pattern.Target == null || indicator == null)
            return false;

        BeginAction();
        dashHitTargets.Clear();

        FaceTarget(pattern.Target);

        StartCoroutine(CoDashAttack(distance, duration,indicator, indicatorWidth, readyDuration));
        return true;
    }

    private IEnumerator CoDashAttack(float distance, float duration, WBH_IndicatorSpawner indicator, float indicatorWidth, float readyDuration)
    {
        indicator.ShowRect(transform.position, transform.forward, indicatorWidth,distance,readyDuration);

        yield return new WaitForSeconds(readyDuration);

        enemyAnimation.PlaySkill(1); // 돌진 스킬 번호

        movement.Dash(transform.forward, distance, duration, EndAction);
    }

    // 돌진 중 플레이어 충돌 체크
    private void CheckDashHit()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, pattern.DashHitRadius, pattern.PlayerLayer);

        foreach (Collider hit in hits)
        {
            if (!hit.TryGetComponent<WBH_ICombat>(out var target))
                continue;

            if (!dashHitTargets.Add(target))
                continue;

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

        enemyAnimation.PlaySkill(2); // 연발 사격 스킬번호
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
    
    public bool TryMissile(IReadOnlyList<Vector3> impactPoints, 
                           float explosionRadius, 
                           float warningDuration, 
                           float recoveryDuration, 
                           WBH_IndicatorSpawner indicatorSpawner, 
                           System.Action onCompleted = null)
    {
        if (IsActionInProgress || impactPoints == null || impactPoints.Count == 0 || indicatorSpawner == null)
            return false;

        BeginAction();

        StartCoroutine(CoMissile(impactPoints, explosionRadius, warningDuration, recoveryDuration, indicatorSpawner, onCompleted));

        return true;
    }

    // 미사일 패턴(인디케이터 O)
    private IEnumerator CoMissile(IReadOnlyList<Vector3> impactPoints, 
                                  float explosionRadius, 
                                  float warningDuration, 
                                  float recoveryDuration, 
                                  WBH_IndicatorSpawner indicatorSpawner, 
                                  System.Action onCompleted = null)
    {
        Vector3 spawnPos = pattern.GrenadePoint.position;

        float[] flightTimes = new float[impactPoints.Count];
        float impactTime = warningDuration;

        // 모든 미사일의 착탄 시점을 가장 긴 비행시간 기준으로 동일화
        for(int i = 0; i < impactPoints.Count; i++)
        {
            float distance = Mathf.Min(Vector3.Distance(spawnPos, impactPoints[i]), missileMaxDistance);

            float flightTime = Mathf.Max(minMissileFlightTime, distance / status.ProjectileSpeed);

            flightTimes[i] = flightTime;
            impactTime = Mathf.Max(impactTime, flightTime);
        }

        for(int i = 0; i < impactPoints.Count; i++)
        {
            indicatorSpawner.ShowCircle(impactPoints[i], explosionRadius, impactTime, growOverTime: true);

            float launchDelay = impactTime - flightTimes[i];

            StartCoroutine(CoLaunchMissileAfter(launchDelay, spawnPos, impactPoints[i], explosionRadius));
        }

        yield return new WaitForSeconds(impactTime + recoveryDuration);
        onCompleted?.Invoke();
        EndAction();
    }

    // 미사일 실제 발사 메서드
    private IEnumerator CoLaunchMissileAfter(float delay, Vector3 spawnPos, Vector3 impactPos, float explosionRadius)
    {
        yield return new WaitForSeconds(delay);

        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1f);

        projectileSpawner.FireGrenade(ProjectileType.Missile, spawnPos, impactPos, request, status.ProjectileSpeed, missileMaxDistance, explosionRadius, pattern.PlayerLayer);
    }


    public bool TryJumpAttack(Vector3 landingPos, float damageRadius, float jumpDuration, float recoveryDuration, WBH_IndicatorSpawner indicatorSpawner)
    {
        if(IsActionInProgress || indicatorSpawner == null || !movement.CanJumpTo(landingPos))
            return false;

        BeginAction();
        FacePosition(landingPos);

        StartCoroutine(CoJumpAttack(landingPos, damageRadius, jumpDuration, recoveryDuration, indicatorSpawner));
        return true;
    }

    private IEnumerator CoJumpAttack(Vector3 landingPos, float damageRadius, float jumpDuration, float recoveryDuration, WBH_IndicatorSpawner indicatorSpawner)
    {
        indicatorSpawner.ShowCircle(landingPos, damageRadius, jumpDuration, growOverTime: true);

        bool landed = false;

        movement.JumpTo(landingPos, jumpDuration, () => landed = true);

        yield return new WaitUntil(() => landed);

        ApplyAreaDamage(landingPos, damageRadius);

        yield return new WaitForSeconds(recoveryDuration);
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

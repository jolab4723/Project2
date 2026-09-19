using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WBH_EnemyController))]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyAnimation))]

public class WBH_EnemyCombat : MonoBehaviour
{
    [SerializeField] private WBH_EffectData missileEffect;
    [SerializeField] private Transform grabPoint;

    private WBH_EnemyController controller;
    private WBH_EnemyStatus status;
    private WBH_EnemyAnimation enemyAnimation;
    private WBH_EnemyPattern pattern;
    private WBH_EnemyMovement movement;
    private WBH_ProjectileSpawner projectileSpawner;

    private float attackTimer = 0f;
    private float missileMaxDistance = 100f;
    private float minMissileFlightTime = 1f;

    private bool isGrabDash;
    private float grabCollisionRadius;
    private float grabReleaseRadius = 1.5f;


    private readonly HashSet<WBH_ICombat> dashHitTargets = new(); // 대쉬 피해 시, 플레이어가 여러 번 충돌하더라도 데미지 1번만 받도록 하기 위한 변수
    private readonly HashSet<WBH_ICombat> areaHitTargets = new(); // 범위 피해 시, 플레이어가 여러 컬라이더 가져도 데미지 1번만
    private readonly HashSet<T_PlayerController> grabbedPlayers = new();

    public bool IsActionInProgress { get; private set; }


    private void Awake()
    {
        controller = GetComponent<WBH_EnemyController>();
        status = GetComponent<WBH_EnemyStatus>();
        enemyAnimation = GetComponent<WBH_EnemyAnimation>();
        pattern = GetComponent<WBH_EnemyPattern>();
        movement = GetComponent<WBH_EnemyMovement>();
    }

    private void OnEnable()
    {
        movement.OnDashUpdate += CheckDashHit;
    }
    private void OnDisable()
    {
        movement.OnDashUpdate -= CheckDashHit;

        CancelCurrentAction();
    }

    private void Update()
    {
        if(attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
        }
        attackTimer = Mathf.Max(attackTimer, 0f);
    }

    public void Initialize(WBH_EnemyInfo info, WBH_ProjectileSpawner projectileSpawner)
    {
        this.projectileSpawner = projectileSpawner;

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

    #region 엘리트 등 특수 패턴용 메서드
    // 패턴 취소
    public void CancelCurrentAction()
    {
        StopAllCoroutines();

        movement.CancelForcedMovement();

        isGrabDash = false;

        ReleaseGrabbedPlayers();

        dashHitTargets.Clear();
        areaHitTargets.Clear();

        IsActionInProgress = false;
    }

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

    public bool TryDashAttackWithRangeVisual(float distance, float duration, float indicatorWidth, float readyDuration, Color indicatorColor)
    {
        if (IsActionInProgress || pattern.Target == null)
            return false;

        BeginAction();
        dashHitTargets.Clear();

        FaceTarget(pattern.Target);

        Vector3 dashDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        if(dashDir.sqrMagnitude < 0.001f)
        {
            EndAction();
            return false;
        }

        SkillRangeVisual.ShowLine(transform.position, dashDir, distance, indicatorWidth, indicatorColor, readyDuration);

        StartCoroutine(CoDashAttackWithRangeVisual(dashDir, distance, duration, readyDuration));
        return true;
    }

    private IEnumerator CoDashAttackWithRangeVisual(Vector3 dashDir, float distance, float duration, float readyDuration)
    {
        yield return new WaitForSeconds(readyDuration);

        movement.Dash(dashDir, distance, duration, EndAction);
    }

    // 돌진 중 플레이어 충돌 체크
    private void CheckDashHit()
    {
        if(isGrabDash) // 잡기 전용 돌진일 경우
        {
            CheckGrabHit();
            HoldGrabbedPlayers();
            return;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, pattern.DashHitRadius, pattern.PlayerLayer, QueryTriggerInteraction.Collide);

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

    // 돌진 도중 부딪히는 플레이어를 감지해서 grabbedPlayers 에 추가
    private void CheckGrabHit()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, grabCollisionRadius, pattern.PlayerLayer, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            T_PlayerController player = hit.GetComponentInParent<T_PlayerController>();

            if (player == null || grabbedPlayers.Contains(player))
                continue;

            if (!player.TryBeginGrab())
                continue;

            grabbedPlayers.Add(player);
        }
    }

    // 돌진 잡기 패턴
    public bool TryGrabAndSlam(Transform dashTarget,
                               float roarDuration,
                               float maxDashDistance,
                               float dashDuration,
                               float slamHitDelay,
                               float slamRecoveryDuration,
                               float collisionRadius,
                               float damageMul,
                               Color indicatorColor)
    {
        if (IsActionInProgress || dashTarget == null || !movement.CanControl)
            return false;

        BeginAction();

        grabbedPlayers.Clear();
        grabCollisionRadius = collisionRadius;

        FaceTarget(dashTarget);

        Vector3 toTarget = Vector3.ProjectOnPlane(dashTarget.position - transform.position, Vector3.up);

        Vector3 dashDir;
        
        if(toTarget.sqrMagnitude < 0.001f)
        { 
            dashDir = transform.forward; 
        }
        else
            dashDir = toTarget.normalized;

        float dashDistance = Mathf.Min(toTarget.magnitude + 1.5f, maxDashDistance);

        transform.rotation = Quaternion.LookRotation(dashDir);

        SkillRangeVisual.ShowLine(transform.position, dashDir, dashDistance, collisionRadius * 2f, indicatorColor, roarDuration);

        StartCoroutine(CoGrabAndSlam(dashTarget, dashDir, dashDistance, roarDuration, dashDuration, slamHitDelay, slamRecoveryDuration, damageMul));
        return true;
    }

    // 돌진 잡기 패턴 코루틴. 애니메이션 종료까지의 타이밍을 float 으로 직접 받음
    private IEnumerator CoGrabAndSlam(Transform dashTarget,
                                      Vector3 dashDir,
                                      float dashDistance,
                                      float roarDuration,
                                      float dashDuration,
                                      float slamHitDelay,
                                      float slamRecoveryDuration,
                                      float damageMul)
    {
        // 1. 포효
        yield return new WaitForSeconds(roarDuration);

        if(dashTarget == null || !dashTarget.gameObject.activeInHierarchy)
        {
            ReleaseGrabbedPlayers();
            EndAction();
            yield break;
        }

        transform.rotation = Quaternion.LookRotation(dashDir);

        // 2. 돌진 및 충돌 플레이어 잡기
        isGrabDash = true;

        bool dashFinished = false;

        movement.Dash(dashDir, dashDistance, dashDuration, () => dashFinished = true);

        while(!dashFinished)
        {
            HoldGrabbedPlayers();
            yield return null;
        }

        isGrabDash = false;
        HoldGrabbedPlayers();

        //3. 내려찍기
        float elapsed = 0f;

        while(elapsed < slamHitDelay)
        {
            elapsed += Time.deltaTime;
            HoldGrabbedPlayers();
            yield return null;
        }

        // 4. 피해 후 포획 해제
        DamageGrabbedPlayers(damageMul);
        ReleaseGrabbedPlayers();

        yield return new WaitForSeconds(slamRecoveryDuration);

        EndAction();
    }

    // 플레이어에게 잡힘 상태 부여
    private void HoldGrabbedPlayers()
    {
        Vector3 holdPos = grabPoint != null ? grabPoint.position : transform.position + Vector3.up;

        foreach (T_PlayerController player in grabbedPlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                continue;

            player.SetGrabPosition(holdPos);
        }
    }

    private void DamageGrabbedPlayers(float damageMul)
    {
        foreach (T_PlayerController player in grabbedPlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                continue;

            WBH_DamageRequest request = CreateDamageRequest(player, WBH_AttackType.Normal, ItemSystem.ElementType.None, damageMul);

            WBH_CombatManager.ProcessDamage(request);
        }
    }

    // 잡기 해제
    private void ReleaseGrabbedPlayers()
    {
        int count = grabbedPlayers.Count;
        int index = 0;

        foreach (T_PlayerController player in grabbedPlayers)
        {
            if (player == null)
                continue;

            float angle = count > 0 ? 360f * index / count : 0f;

            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            Vector3 releasPos = transform.position + dir * grabReleaseRadius;

            player.EndGrab(releasPos);
            index++;
        }
        grabbedPlayers.Clear();
    }

    // 연발 사격
    public bool TryShootBurst(int count)
    {
        if (IsActionInProgress || pattern.Target == null)
            return false;

        BeginAction();
        FaceTarget(pattern.Target);

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
        Vector3 dir = GetFlatFireDirection();

        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1);

        projectileSpawner.FireProjectile(ProjectileType.NormalEnemy, pattern.FirePoint.position, dir, request, status.ProjectileSpeed, 12f, pattern.PlayerLayer);
    }    

    // 일제사격 탄막
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

        projectileSpawner.FireGrenade(ProjectileType.Missile, spawnPos, impactPos, request, status.ProjectileSpeed, missileMaxDistance, explosionRadius, pattern.PlayerLayer, missileEffect);
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

    public void ApplyAreaDamage(Vector3 center, float radius, float damageMultiplier = 1f)
    {
        areaHitTargets.Clear();

        Collider[] hits = Physics.OverlapSphere(center, radius, pattern.PlayerLayer);

        foreach (Collider hit in hits)
        {
            T_PlayerController player = hit.GetComponent<T_PlayerController>();

            if (player == null)
                continue;

            if (!areaHitTargets.Add(player))
                continue;

            WBH_DamageRequest request = CreateDamageRequest(player, WBH_AttackType.Normal, ItemSystem.ElementType.None, damageMultiplier);

            WBH_CombatManager.ProcessDamage(request);
        }
    }

    // 적 방향으로 회전하면서 일정 간격으로 사격
    public bool TryTrackingFire(int count, float interval, float turnSpeed, float maxDistance)
    {
        if (IsActionInProgress || pattern.Target == null)
            return false;

        BeginAction();
        StartCoroutine(CoTrackingFire(count, interval, turnSpeed, maxDistance));
        return true;
    }

    private IEnumerator CoTrackingFire(int count, float interval, float turnSpeed,float maxDistance)
    {
        for(int i = 0; i < count; i ++)
        {
            if (pattern.Target == null)
                break;

            if(i > 0)
            {
                float elapsed = 0f;

                while(elapsed < interval)
                {
                    elapsed += Time.deltaTime;
                    RotateTowardsTarget(turnSpeed);
                    yield return null;
                }
            }

            RotateTowardsTarget(turnSpeed);

            Vector3 dir = (pattern.Target.position + Vector3.up - pattern.FirePoint.position).normalized;
            WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1f);

            projectileSpawner.FireProjectile(ProjectileType.NormalEnemy, pattern.FirePoint.position, dir, request, status.ProjectileSpeed, maxDistance, pattern.PlayerLayer);
        }
        yield return new WaitForSeconds(0.25f);
        EndAction();
    }

    // 타겟 방향으로 회전
    private void RotateTowardsTarget(float turnSpeed)
    {
        if (pattern.Target == null)
            return;

        Vector3 dir = pattern.Target.position - transform.position;
        dir.y = 0;

        if (dir.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(dir.normalized);

        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    // 부채꼴 단발 공격
    public bool TrySectorAttack(float range, float angle, float damageMul, float hitDelay = 0.35f, float recoveryDuration = 0.4f)
    {
        if (IsActionInProgress || pattern.Target == null)
            return false;

        BeginAction();
        FaceTarget(pattern.Target);

        StartCoroutine(CoSectorAttack(range, angle, damageMul, hitDelay, recoveryDuration));

        return true;
    }

    private IEnumerator CoSectorAttack(float range, float angle, float damageMul, float hitDelay, float recoverDuration)
    {
        yield return new WaitForSeconds(hitDelay);

        ApplySectorDamage(range, angle, damageMul);

        yield return new WaitForSeconds(recoverDuration);
        EndAction();
    }

    private void ApplySectorDamage(float range, float angle, float damageMul)
    {
        areaHitTargets.Clear();

        Collider[] hits = Physics.OverlapSphere(transform.position, range, pattern.PlayerLayer, QueryTriggerInteraction.Ignore);

        foreach(Collider hit in hits)
        {
            T_PlayerController player = hit.GetComponent<T_PlayerController>();

            if (player == null || !areaHitTargets.Add(player))
                continue;

            Vector3 dir = player.transform.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.001f)
                continue;

            if (Vector3.Angle(transform.forward, dir) > angle * 0.5f)
                continue;

            WBH_CombatManager.ProcessDamage(CreateDamageRequest(player, WBH_AttackType.Normal, ItemSystem.ElementType.None, damageMul));
        }
    }

    // rotationCount 바퀴 회전하며 투사체 발사
    public bool TrySpinBarrage(int rotationCount, float duration, int bulletCount, float bulletRange, System.Action onCompleted)
    {
        if(IsActionInProgress || bulletCount <= 0 )
            return false;

        BeginAction();

        StartCoroutine(CoSpinBarrage(rotationCount, duration, bulletCount, bulletRange, onCompleted));

        return true;
    }

    private IEnumerator CoSpinBarrage(int rotationCount, float duration, int bulletCount, float bulletRange, System.Action onCompleted)
    {
        Quaternion startRotation = transform.rotation;
        float shotInterval = duration / bulletCount;

        float elapsed = 0f;
        int firedCount = 0;

        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            float angle = 360f * rotationCount * progress;

            transform.rotation = startRotation * Quaternion.Euler(0f, angle, 0f);

            while(firedCount < bulletCount && elapsed >= firedCount * shotInterval)
            {
                WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1f);

                projectileSpawner.FireProjectile(ProjectileType.NormalEnemy, pattern.FirePoint.position, transform.forward, request, status.ProjectileSpeed, bulletRange, pattern.PlayerLayer);

                firedCount++;
            }
            yield return null;
        }
        onCompleted?.Invoke();
        EndAction();
    }

    // 부채꼴 범위 틱데미지
    public bool TryFlameThrow(float range, float angle, float duration, float damageInterval, float damageMul = 0.25f)
    {
        if (IsActionInProgress || pattern.Target == null)
            return false;
        
        BeginAction();
        FaceTarget(pattern.Target);

        StartCoroutine(CoFlameThrow(range, angle, duration, damageInterval, damageMul));

        return true;
    }

    private IEnumerator CoFlameThrow(float range, float angle, float duration, float damageInterval, float damageMul)
    {
        float elapsed = 0f;
        float damageTimer = 0f;

        while(elapsed < duration)
        {
            elapsed += Time.deltaTime; ;
            damageTimer -= Time.deltaTime;

            if(damageTimer <= 0f)
            {
                damageTimer = damageInterval;
                ApplySectorDamage(range, angle, damageMul);
            }
            yield return null;
        }
        EndAction();
    }

    // 원형 범위 데미지 + 상태이상
    public bool TryAreaDamageAndStatus(Vector3 center, float radius, float damageMul, WBH_StatusEffectData statusEffect, float startDelay, float hitDelay = 0.5f, float recoveryDuration = 0.4f, System.Action onStarted = null)
    {
        if(IsActionInProgress)
            return false;

        BeginAction();

        StartCoroutine(CoAreaDamageAndStatus(center, radius, damageMul, statusEffect, startDelay, hitDelay, recoveryDuration, onStarted));

        return true;
    }

    private IEnumerator CoAreaDamageAndStatus(Vector3 center, float radius, float damageMul, WBH_StatusEffectData statusEffect, float startDelay, float hitDelay = 0.5f, float recoveryDuration = 0.4f, System.Action onStarted = null)
    {
        if(startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        onStarted?.Invoke(); // 포효 후 Attack3 가 시작되는 시점에 인디케이터 표시

        if (hitDelay > 0f)
            yield return new WaitForSeconds(hitDelay);

        areaHitTargets.Clear();

        Collider[] hits = Physics.OverlapSphere(center, radius, pattern.PlayerLayer, QueryTriggerInteraction.Collide);

        foreach(Collider hit in hits)
        {
            T_PlayerController player = hit.GetComponentInParent<T_PlayerController>();

            if (player == null || !areaHitTargets.Add(player))
                continue;

            WBH_CombatManager.ProcessDamage(CreateDamageRequest(player, WBH_AttackType.Normal, ItemSystem.ElementType.None, damageMul));

            player.AddStatusEffect(statusEffect);
        }

        if(recoveryDuration > 0f)
            yield return new WaitForSeconds(recoveryDuration);

        EndAction();
    }

    public bool TrySummonSelfDestruct(WBH_BossMinionSpawner spawner, int count, Transform initialTarget, float spawnDelay = 0.8f, float recoveryDuration = 0.5f)
    {
        if (IsActionInProgress || spawner == null || count <= 0)
            return false;

        BeginAction();

        StartCoroutine(CoSummonSelfDestruct(spawner, count, initialTarget, spawnDelay, recoveryDuration));

        return true;
    }

    private IEnumerator CoSummonSelfDestruct(WBH_BossMinionSpawner spawner, int count, Transform initialTarget, float spawnDelay, float recoveryDuration)
    {
        yield return new WaitForSeconds(spawnDelay);

        int spawnedCount = spawner.Spawn(count, initialTarget);

        yield return new WaitForSeconds(recoveryDuration);
        EndAction();
    }

    private Vector3 GetFlatFireDirection()
    {
        Vector3 dir = pattern.Target != null ? pattern.Target.position - pattern.FirePoint.position
                                             : transform.forward;
        dir.y = 0;

        if(dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
    }
    #endregion
}

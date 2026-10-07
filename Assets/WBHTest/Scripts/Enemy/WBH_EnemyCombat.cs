using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
    private float SkillRangeFlashDuration = 0.3f;

    private readonly Color DefaultRangeColor = new Color(1f, 0.15f, 0.1f, 0.35f);

    private readonly HashSet<WBH_ICombat> dashHitTargets = new(); // 대쉬 피해 시, 플레이어가 여러 번 충돌하더라도 데미지 1번만 받도록 하기 위한 변수
    private readonly HashSet<WBH_ICombat> areaHitTargets = new(); // 범위 피해 시, 플레이어가 여러 컬라이더 가져도 데미지 1번만
    private readonly HashSet<T_PlayerController> grabbedPlayers = new();

    private bool actionInProgress;
    public bool IsActionInProgress
    {
        get => actionInProgress || externalActionInProgress?.Invoke() == true;
        private set => actionInProgress = value;
    }
    private System.Func<bool> externalAttack;
    private System.Func<bool> externalActionInProgress;
    private System.Action<Vector3, float> externalProjectile;
    private System.Action<int> externalSkill;
    /// <summary>SW 수정: 착탄 위치·비행 시간·폭발 반경을 받아 외부 서버 권한으로 미사일을 생성하는 콜백이다.</summary>
    private System.Action<Vector3, float, float> externalMissile;
    public System.Func<T_PlayerController, bool> ExternalBeginGrab { get; set; }
    public System.Action<T_PlayerController, Vector3> ExternalHoldGrab { get; set; }
    public System.Action<T_PlayerController, Vector3> ExternalEndGrab { get; set; }

    /// <summary>SW 수정: 패턴과 돌진 판정은 재사용하고 공격·투사체·애니메이션의 권한 경계만 연결합니다.</summary>
    public void BindExternalActions(System.Func<bool> attack, System.Func<bool> actionInProgress,
        System.Action<Vector3, float> projectile, System.Action<int> skill)
    {
        externalAttack = attack;
        externalActionInProgress = actionInProgress;
        externalProjectile = projectile;
        externalSkill = skill;
    }

    /// <summary>SW 수정: 착탄 경고와 발사 시점은 원본에서 계산하고 생성만 외부 서버 권한에 전달한다.</summary>
    public void BindExternalMissile(System.Action<Vector3, float, float> missile) => externalMissile = missile;


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

        if (externalAttack != null) return externalAttack();

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
        act3ActionVersion++;

        if (act3ActionInProgress)
        {
            act3ActionInProgress = false;
            enemyAnimation.ResetSkillAniState();
        }

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

        if (externalSkill != null) externalSkill(1);
        else enemyAnimation.PlaySkill(1); // 돌진 스킬 번호

        ShowDebugLine(transform.position, transform.forward, distance, indicatorWidth);

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

        pattern.IndicatorSpawner?.ShowRect(transform.position, dashDir, indicatorWidth, distance, readyDuration, growOverTime: true);

        StartCoroutine(CoDashAttackWithRangeVisual(dashDir, distance, indicatorWidth, duration, readyDuration));
        return true;
    }

    private IEnumerator CoDashAttackWithRangeVisual(Vector3 dashDir, float distance, float indicatorWidth ,float duration, float readyDuration)
    {
        yield return new WaitForSeconds(readyDuration);

        ShowDebugLine(transform.position, dashDir, distance, indicatorWidth);
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

            if (!(ExternalBeginGrab != null ? ExternalBeginGrab(player) : player.TryBeginGrab()))
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

        pattern.IndicatorSpawner?.ShowRect(transform.position, dashDir, collisionRadius * 2f, dashDistance, roarDuration, growOverTime : true);

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

        ShowDebugLine(transform.position, dashDir, dashDistance, grabCollisionRadius * 2);

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

            if (ExternalHoldGrab != null) ExternalHoldGrab(player, holdPos);
            else player.SetGrabPosition(holdPos);
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

            if (ExternalEndGrab != null) ExternalEndGrab(player, releasPos);
            else player.EndGrab(releasPos);
            index++;
        }
        grabbedPlayers.Clear();
    }

    // 연발 사격
    public bool TryShootBurst(int count, WBH_EnemyEffectCue soundCue = WBH_EnemyEffectCue.None)
    {
        if (IsActionInProgress || pattern.Target == null)
            return false;

        BeginAction();
        FaceTarget(pattern.Target);

        if (externalSkill != null) externalSkill(2);
        else enemyAnimation.PlaySkill(2); // 연발 사격 스킬번호
        StartCoroutine(CoShootBurst(count, soundCue));
        return true;
    }

    private IEnumerator CoShootBurst(int count, WBH_EnemyEffectCue soundCue)
    {
        for(int i = 0; i < count; i++)
        {
            FireProjectile();

            if (soundCue != WBH_EnemyEffectCue.None)
                pattern.EnemyEffect?.PlaySfx(soundCue);

            if(i < count -1)
                yield return new WaitForSeconds(0.15f);
        }
        EndAction();
    }

    private void FireProjectile()
    {
        Vector3 dir = GetFlatFireDirection();
        if (externalProjectile != null)
        {
            externalProjectile(dir, 12f);
            return;
        }

        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1);

        projectileSpawner.FireProjectile(ProjectileType.NormalEnemy, pattern.FirePoint.position, dir, request, status.ProjectileSpeed, 12f, pattern.PlayerLayer);
    }    

    // 일제사격 탄막
    /// <summary>SW 수정: 탄막의 발사 방향을 원본에서 계산하고, 외부 콜백이 있으면 각 투사체 생성을 서버 권한에 전달한다.</summary>
    public bool TryBarrage(int projectileCount, float spreadAngle, float maxDistance, float actionDuration)
    {
        if (IsActionInProgress || pattern.Target == null)
            return false;

        BeginAction();
        FaceTarget(pattern.Target);

        Vector3 dir = (pattern.Target.position - pattern.FirePoint.position).normalized;

        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1f);

        if (externalProjectile != null)
        {
            int projectileTotal = Mathf.Max(1, projectileCount);
            for (int projectileIndex = 0; projectileIndex < projectileTotal; projectileIndex++)
            {
                float spreadRotationDegrees = projectileTotal > 1
                    ? -spreadAngle * 0.5f + spreadAngle * projectileIndex / (projectileTotal - 1)
                    : 0f;
                externalProjectile(Quaternion.Euler(0f, spreadRotationDegrees, 0f) * dir, maxDistance);
            }
        }
        else projectileSpawner.FireMultipleProjectile(ProjectileType.NormalEnemy,
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
                           WBH_EnemyEffectCue impactEffectCue,
                           System.Action onCompleted = null)
    {
        if (IsActionInProgress || impactPoints == null || impactPoints.Count == 0 || indicatorSpawner == null)
            return false;

        BeginAction();

        StartCoroutine(CoMissile(impactPoints, explosionRadius, warningDuration, recoveryDuration, indicatorSpawner, impactEffectCue, onCompleted));

        return true;
    }

    // 미사일 패턴(인디케이터 O)
    private IEnumerator CoMissile(IReadOnlyList<Vector3> impactPoints, 
                                  float explosionRadius, 
                                  float warningDuration, 
                                  float recoveryDuration, 
                                  WBH_IndicatorSpawner indicatorSpawner,
                                  WBH_EnemyEffectCue impactEffectCue,
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

            StartCoroutine(CoLaunchMissileAfter(launchDelay, spawnPos, impactPoints[i], explosionRadius, impactEffectCue));
        }

        yield return new WaitForSeconds(impactTime + recoveryDuration);
        onCompleted?.Invoke();
        EndAction();
    }

    // 미사일 실제 발사 메서드
    /// <summary>SW 수정: 예정된 발사 시점에 외부 콜백으로 미사일 생성을 전달하고, 콜백이 없으면 기존 투사체 생성기를 사용한다.</summary>
    private IEnumerator CoLaunchMissileAfter(float delay, Vector3 spawnPos, Vector3 impactPos, float explosionRadius, WBH_EnemyEffectCue impactEffectCue)
    {
        yield return new WaitForSeconds(delay);

        if (externalMissile != null)
        {
            float travelDistance = Mathf.Min(Vector3.Distance(spawnPos, impactPos), missileMaxDistance);
            float flightTime = Mathf.Max(minMissileFlightTime, travelDistance / Mathf.Max(0.01f, status.ProjectileSpeed));
            externalMissile(impactPos, flightTime, explosionRadius);
            yield break;
        }

        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1f);

        projectileSpawner.FireGrenade(ProjectileType.Missile, spawnPos, impactPos, request, status.ProjectileSpeed, missileMaxDistance, explosionRadius, pattern.PlayerLayer, enemyEffect: pattern.EnemyEffect, impactEffectCue: impactEffectCue);
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

        // 애니메이션 전환 시점과 무관하게 실제 착지 후 한 번 재생한다.
        pattern.EnemyEffect?.PlayEffect(WBH_EnemyEffectCue.Boss_Act1_JumpAttack, Vector3.one);
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

        ShowDebugSector(center, Vector3.forward, radius, 360f);

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

            if (externalProjectile != null) externalProjectile(dir, maxDistance);
            else projectileSpawner.FireProjectile(ProjectileType.NormalEnemy, pattern.FirePoint.position, dir, request, status.ProjectileSpeed, maxDistance, pattern.PlayerLayer);
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

        ShowDebugSector(transform.position, transform.forward, range, angle);
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

                if (externalProjectile != null) externalProjectile(transform.forward, bulletRange);
                else projectileSpawner.FireProjectile(ProjectileType.NormalEnemy, pattern.FirePoint.position, transform.forward, request, status.ProjectileSpeed, bulletRange, pattern.PlayerLayer);

                firedCount++;
            }
            yield return null;
        }
        onCompleted?.Invoke();
        EndAction();
    }

    // 부채꼴 범위 틱데미지
    public bool TryFlameThrow(float range, float angle, float duration, float damageInterval, float readyDuration, WBH_IndicatorSpawner indicatorSpawner, float damageMul = 0.25f)
    {
        if (IsActionInProgress || pattern.Target == null || indicatorSpawner == null ||readyDuration <= 0f)
            return false;
        
        BeginAction();
        FaceTarget(pattern.Target);

        WBH_Effect warning = indicatorSpawner.ShowCone(transform.position, transform.forward, range, angle, readyDuration, growOverTime: true);

        if (externalProjectile == null && (warning == null || !warning.IsPlaying))
        {
            EndAction();
            return false;
        }

        StartCoroutine(CoFlameThrow(range, angle, duration, damageInterval, damageMul, readyDuration));

        return true;
    }

    private IEnumerator CoFlameThrow(float range, float angle, float duration, float damageInterval, float damageMul, float readyDuration)
    {
        yield return new WaitForSeconds(readyDuration);

        float elapsed = 0f;
        float damageTimer = 0f;
        float interval = Mathf.Max(0.01f, damageInterval);

        while(elapsed < duration)
        {
            elapsed += Time.deltaTime; ;
            damageTimer -= Time.deltaTime;

            if(damageTimer <= 0f)
            {
                ShowDebugSector(transform.position, transform.forward, range, angle);

                damageTimer = interval;
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

        ShowDebugSector(center, Vector3.forward, radius, 360f);

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

    #region Act3 보스 패턴

    [SerializeField] private float act3HitHeight = 4f;

    private int act3ActionVersion;
    private bool act3ActionInProgress;

    private bool IsNetworkSession => Mirror.NetworkServer.active || Mirror.NetworkClient.active;

    private bool CanStartAct3Action()
    {
        if(!isActiveAndEnabled || status == null || status.IsDead || pattern == null || movement == null || !movement.CanControl || IsActionInProgress)
            return false;

        if (!IsNetworkSession)
            return true;

        return Mirror.NetworkServer.active && TryGetComponent<Mirror.NetworkIdentity>(out var identity) && identity.isServer;
    }

    // 패턴 y축 이동 없애는 메서드
    private static Vector3 Act3FlatDirection(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    // 플레이어 생존여부 판단 메서드
    private static bool IsAct3LivingPlayer(T_PlayerController player)
    {
        return player != null && player.gameObject.activeInHierarchy && player.Status != null && !player.Status.IsDead;
    }

    private IEnumerable<T_PlayerController> FindAct3Players(Collider[] hits, System.Func<T_PlayerController, bool> contains = null)
    {
        var found = new HashSet<T_PlayerController>();

        foreach(Collider hit in hits)
        {
            T_PlayerController player = hit.GetComponentInParent<T_PlayerController>();

            if (!IsAct3LivingPlayer(player) || !found.Add(player) || (contains != null && !contains(player)))
                continue;

            yield return player;
        }
    }

    private IEnumerable<T_PlayerController> FindAct3SectorPlayers(Vector3 origin, Vector3 forward, float radius, float angle)
    {
        forward = Act3FlatDirection(forward);

        Collider[] hits = Physics.OverlapSphere(origin, radius, pattern.PlayerLayer, QueryTriggerInteraction.Collide);

        return FindAct3Players(hits, player =>
        {
            Vector3 offset = player.transform.position - origin;
            offset.y = 0f;

            if (offset.sqrMagnitude > radius * radius)
                return false;

            return angle >= 360f || offset.sqrMagnitude < 0.0001f || Vector3.Angle(forward, offset) <= angle * 0.5f;
        });
    }

    // 컬라이더에 걸린 플레이어에게 데미지 적용
    private void DamageAct3Players(IEnumerable<T_PlayerController> players, float damageMultiplier)
    {
        if (damageMultiplier <= 0f)
            return;

        foreach(T_PlayerController player in players)
        {
            if (status.IsDead)
                break;

            if (!IsAct3LivingPlayer(player))
                continue;

            WBH_CombatManager.ProcessDamage(CreateDamageRequest(player, WBH_AttackType.Normal, ItemSystem.ElementType.None, damageMultiplier));
        }
    }

    private void ApplyAct3SectorDamage(Vector3 origin, Vector3 forward, float radius, float angle, float damageMultiplier)
    {
        DamageAct3Players(FindAct3SectorPlayers(origin, forward, radius, angle), damageMultiplier);
    }

    private void ApplyAct3RectDamage(Vector3 origin, Vector3 forward, float width, float length, float damageMultiplier)
    {
        forward = Act3FlatDirection(forward);

        Quaternion rotation = Quaternion.LookRotation(forward);
        Quaternion inverseRotation = Quaternion.Inverse(rotation);

        Vector3 center = origin + forward * (length * 0.5f) + Vector3.up * (act3HitHeight * 0.5f);

        Collider[] hits = Physics.OverlapBox(center, new Vector3(width * 0.5f, act3HitHeight * 0.5f, length * 0.5f), rotation, pattern.PlayerLayer, QueryTriggerInteraction.Collide);

        DamageAct3Players(FindAct3Players(hits, player =>
        {
            Vector3 local = inverseRotation * (player.transform.position - origin);

            return Mathf.Abs(local.x) < width * 0.5f && local.z >= 0f && local.z <= length; // 구획 경계에서 컬라이더 크기로 옆 구획까지 맞는 것 방지
        }), 
        damageMultiplier);
    }

    private bool StartAct3Action(IEnumerator routine, System.Action onCompleted = null)
    {
        BeginAction();
        act3ActionInProgress = true;

        int version = ++act3ActionVersion;
        StartCoroutine(CoAct3Action(routine, version, onCompleted));
        return true;
    }

    private IEnumerator CoAct3Action(IEnumerator routine, int version, System.Action onCompleted)
    {
        yield return null;

        bool completed = false;

        try
        {
            while(version == act3ActionVersion && status != null && !status.IsDead)
            {
                if(!routine.MoveNext())
                {
                    completed = true;
                    break;
                }
                yield return routine.Current;
            }
        }
        finally
        {
            (routine as System.IDisposable)?.Dispose();

            if(version == act3ActionVersion)
            {
                ReleaseGrabbedPlayers();
                movement.Stop();
                act3ActionInProgress = false;
                EndAction();
            }
        }

        if(completed && version == act3ActionVersion && status != null && !status.IsDead)
        {
            onCompleted?.Invoke();
        }
    }
    private bool TryAct3WarnedHit(WBH_IndicatorSpawner indicatorSpawner, float warningDuration, float recoveryDuration, System.Func<WBH_Effect> showWarning, System.Action hit, System.Action onCompleted = null)
    {
        if(!CanStartAct3Action() || indicatorSpawner == null || warningDuration <= 0f || recoveryDuration < 0f)
            return false;

        movement.Stop();

        WBH_Effect warning = showWarning();

        if (!IsNetworkSession && (warning == null || !warning.IsPlaying))
            return false;

        return StartAct3Action(CoAct3WarnedHit(Time.time + warningDuration, recoveryDuration, hit), onCompleted);
    }

    private IEnumerator CoAct3WarnedHit(float hitAt, float recoveryDuration, System.Action hit)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, hitAt - Time.time));

        hit();

        if(recoveryDuration > 0f)
            yield return new WaitForSeconds(recoveryDuration);
    }

    // 1. 예고가 있는 부채꼴 공격
    public bool TryWarnedSectorAttack(float range, float angle, float damageMultiplier, float warningDuration, float recoveryDuration,WBH_IndicatorSpawner indicator)
    {
        if (!CanStartAct3Action() || pattern.Target == null ||range <= 0f || angle <= 0f || angle > 360f || damageMultiplier < 0f || indicator == null)
            return false;

        FaceTarget(pattern.Target);

        Vector3 origin = transform.position;
        Vector3 forward = Act3FlatDirection(transform.forward);

        return TryAct3WarnedHit(indicator, warningDuration, recoveryDuration,
                                () => indicator.ShowCone(origin, forward, range, angle, warningDuration, growOverTime: true),
                                () => ApplyAct3SectorDamage(origin, forward, range, angle, damageMultiplier));
    }

    // 2. 원형 공격
    public bool TryCircleAttack(Vector3 center, float radius, float damageMultiplier, float warningDuration, float recoveryDuration, WBH_IndicatorSpawner indicator)
    {
        if (radius <= 0f || damageMultiplier < 0f || indicator == null)
            return false;

        return TryAct3WarnedHit(indicator, warningDuration, recoveryDuration,
                                () => indicator.ShowCircle(center, radius, warningDuration, growOverTime: true),
                                () => ApplyAct3SectorDamage(center, Vector3.forward, radius, 360f, damageMultiplier));
    }

    private bool TrySampleAct3Teleport(Vector3 desiredPosition, out Vector3 position)
    {
        position = default;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();

        if (agent == null ||!agent.isActiveAndEnabled ||!agent.isOnNavMesh)
            return false;

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        if (!NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 0.75f, filter))
            return false;

        position = hit.position;
        return true;
    }

    private bool WarpAct3To(Vector3 position)
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();

        if (agent == null ||!agent.isActiveAndEnabled ||!agent.isOnNavMesh)
            return false;

        movement.Stop();
        return agent.Warp(position);
    }

    // 3. 타겟 인근 순간이동
    public bool TryTeleportNearTarget(Transform target, float distance,Transform mapCenter, Vector2 mapSize)
    {
        if (!CanStartAct3Action() ||
            target == null || !target.gameObject.activeInHierarchy ||
            mapCenter == null || distance <= 0f ||
            mapSize.x <= 0f || mapSize.y <= 0f)
            return false;

        float startAngle = Random.Range(0f, 360f);
        Quaternion inverseMapRotation = Quaternion.Inverse(mapCenter.rotation);

        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, startAngle + i * 45f, 0f) * Vector3.forward;

            Vector3 desired = target.position + direction * distance;

            if (!TrySampleAct3Teleport(desired, out Vector3 position))
                continue;

            Vector3 local = inverseMapRotation * (position - mapCenter.position);

            if (Mathf.Abs(local.x) > mapSize.x * 0.5f - 0.25f ||
                Mathf.Abs(local.z) > mapSize.y * 0.5f - 0.25f)
                continue;

            Vector3 offset = position - target.position;
            offset.y = 0f;

            if (offset.sqrMagnitude < distance * distance * 0.25f)
                continue;

            if (!WarpAct3To(position))
                continue;

            FaceTarget(target);
            return StartAct3Action(CoEndAction(0.35f));
        }

        return false;
    }

    // 4. 제한 시간 동안 추격하고 최대 횟수만큼 부채꼴 공격
    public bool TryChaseSectorAttack(float range, float angle, int maxAttackCount, float maxDuration, float damageMultiplier, float warningDuration, WBH_IndicatorSpawner indicator,
                                     float recoveryDuration = 0.35f)
    {
        if (!CanStartAct3Action() ||
            pattern.Target == null || indicator == null ||
            range <= 0f || angle <= 0f || angle > 360f ||
            maxAttackCount <= 0 || warningDuration <= 0f ||
            recoveryDuration < 0f ||
            maxDuration < warningDuration + recoveryDuration ||
            damageMultiplier < 0f)
            return false;

        return StartAct3Action(CoAct3ChaseSector(range, angle, maxAttackCount, maxDuration,damageMultiplier, warningDuration, recoveryDuration, indicator));
    }

    private IEnumerator CoAct3ChaseSector(float range, float angle, int maxAttackCount, float maxDuration, float damageMultiplier, float warningDuration, float recoveryDuration, 
                                          WBH_IndicatorSpawner indicator)
    {
        float deadline = Time.time + maxDuration;
        int attackCount = 0;

        while (Time.time < deadline && attackCount < maxAttackCount)
        {
            Transform target = pattern.Target;

            if (target == null || !target.gameObject.activeInHierarchy || !movement.CanControl)
                yield break;

            Vector3 offset = target.position - transform.position;
            offset.y = 0f;

            if (offset.sqrMagnitude > range * range)
            {
                movement.Move(target.position);
                yield return null;
                continue;
            }

            // 시작한 공격이 제한 시간을 넘기지 않게 한다.
            if (Time.time + warningDuration + recoveryDuration > deadline)
                yield break;

            movement.Stop();
            FaceTarget(target);

            Vector3 origin = transform.position;
            Vector3 forward = Act3FlatDirection(transform.forward);

            WBH_Effect warning = indicator.ShowCone(origin, forward, range, angle, warningDuration, growOverTime: true);

            if (!IsNetworkSession && (warning == null || !warning.IsPlaying))
                yield break;

            yield return new WaitForSeconds(warningDuration);

            ApplyAct3SectorDamage(origin, forward, range, angle, damageMultiplier);

            attackCount++;

            if (recoveryDuration > 0f)
                yield return new WaitForSeconds(recoveryDuration);
        }
    }

    // 5. 관문에서 무작위 각도로 발사하는 탄막
    public bool TryGateBarrage(WBH_ProjectileSpawner spawner, Vector3 origin, Vector3 forward, int bulletCount, float spreadAngle, float range, float warningDuration, WBH_IndicatorSpawner indicator)
    {
        if (spawner == null || indicator == null ||
            bulletCount <= 0 || spreadAngle <= 0f ||
            spreadAngle > 360f || range <= 0f ||
            status == null || status.ProjectileSpeed <= 0f)
            return false;

        // 기존 외부 발사 콜백은 발사 위치를 받지 않으므로 관문 발사의 멀티플레이 연결은 별도로 진행필요. !@
        if (IsNetworkSession || externalProjectile != null)
            return false;

        forward = Act3FlatDirection(forward);
        Vector3 fireDirection = forward;

        return TryAct3WarnedHit(indicator, warningDuration, 0.5f,
                                () => indicator.ShowCone(origin, fireDirection, range, spreadAngle, warningDuration, growOverTime: true),
                                () =>   {
                                            WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, 1f);

                                            for (int i = 0; i < bulletCount; i++)
                                            {
                                                float angle = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);

                                                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * fireDirection;

                                                spawner.FireProjectile(ProjectileType.NormalEnemy, origin + Vector3.up, direction, request, status.ProjectileSpeed, range, pattern.PlayerLayer);
                                            }
                                        });
    }

    // 6. 관문별 자폭병 소환
    public bool TrySummonAtGates(WBH_BossMinionSpawner spawner,Transform[] gates, int count, Transform initialTarget)
    {
        if (!CanStartAct3Action() ||
            spawner == null || count <= 0 ||
            gates == null || gates.Length == 0)
            return false;

        foreach (Transform gate in gates)
        {
            if (gate == null)
                return false;
        }

        return StartAct3Action(CoAct3SummonAtGates(spawner, (Transform[])gates.Clone(), count, initialTarget));
    }

    private IEnumerator CoAct3SummonAtGates(WBH_BossMinionSpawner spawner, Transform[] gates, int count, Transform initialTarget)
    {
        movement.Stop();
        yield return new WaitForSeconds(0.8f);

        int spawned = spawner.SpawnAtGates(gates, count, initialTarget);

        if (spawned != count)
            Log.Warning($"Act3 자폭병 소환: 요청 {count}, 실제 {spawned}");

        yield return new WaitForSeconds(0.5f);
    }

    // 7. 타겟 뒤 순간이동 → 부채꼴 잡기 → 잡힌 인원 비례 폭발
    public bool TryTeleportGrabAndBurst(Transform target, float grabRange, float grabAngle, float holdDuration, float burstRadius, float damagePerCapturedPlayer, WBH_IndicatorSpawner indicator,
                                        System.Action onMiss)
    {
        if (!CanStartAct3Action() ||
            target == null || !target.gameObject.activeInHierarchy ||
            indicator == null ||
            grabRange <= 0f || grabAngle <= 0f || grabAngle > 360f ||
            holdDuration <= 0f || burstRadius <= 0f ||
            damagePerCapturedPlayer < 0f)
            return false;

        Vector3 behind = target.position - Act3FlatDirection(target.forward) * Mathf.Min(2.5f, grabRange * 0.75f);

        if (!TrySampleAct3Teleport(behind, out Vector3 position) ||!WarpAct3To(position))
            return false;

        FaceTarget(target);

        Vector3 origin = transform.position;
        Vector3 forward = Act3FlatDirection(transform.forward);
        const float grabWarningDuration = 0.6f;

        WBH_Effect warning = indicator.ShowCone(origin, forward, grabRange, grabAngle,grabWarningDuration, growOverTime: true);

        if (!IsNetworkSession && (warning == null || !warning.IsPlaying))
            return false;

        bool missed = false;

        return StartAct3Action(CoAct3GrabAndBurst(origin, forward, grabRange, grabAngle,Time.time + grabWarningDuration, holdDuration, burstRadius, damagePerCapturedPlayer, indicator,
                                                  () => missed = true),
                                () => {
                                          if (missed)
                                              onMiss?.Invoke();
                                      });
    }

    private IEnumerator CoAct3GrabAndBurst(Vector3 origin, Vector3 forward, float grabRange, float grabAngle, float grabAt, float holdDuration, float burstRadius, float damagePerCapturedPlayer,
                                           WBH_IndicatorSpawner indicator, System.Action markMiss)
    {
        movement.Stop();

        yield return new WaitForSeconds(Mathf.Max(0f, grabAt - Time.time));

        foreach (T_PlayerController player in FindAct3SectorPlayers(origin, forward, grabRange, grabAngle))
        {
            bool captured = ExternalBeginGrab != null ? ExternalBeginGrab(player) : player.TryBeginGrab();

            if (captured)
            {
                grabbedPlayers.Add(player);
            }
        }

        int capturedCount = grabbedPlayers.Count;

        if (capturedCount == 0)
        {
            markMiss();
            yield break;
        }

        Vector3 burstCenter = transform.position;

        WBH_Effect warning = indicator.ShowCircle(burstCenter, burstRadius, holdDuration, growOverTime: true);

        if (!IsNetworkSession && (warning == null || !warning.IsPlaying))
            yield break;

        float burstAt = Time.time + holdDuration;

        while (Time.time < burstAt)
        {
            HoldGrabbedPlayers();
            yield return null;
        }

        HoldGrabbedPlayers();

        // 포획된 플레이어와 주변 플레이어 모두 동일한 범위 피해를 받는다.
        // 포획 대상에 별도 피해를 더하지 않아 중복 피해를 방지.
        ApplyAct3SectorDamage(burstCenter, Vector3.forward, burstRadius, 360f, damagePerCapturedPlayer * capturedCount);

        ReleaseGrabbedPlayers();

        yield return new WaitForSeconds(0.5f);
    }

    // 8. 중앙 순간이동 → 3개 구획에 시간차 피해
    public bool TryCenterTeleportAndStrips(Vector3 center, Vector3[] origins, Vector3 forward, float width, float length, float warningDuration, float hitInterval, float damageMultiplier, 
                                           WBH_IndicatorSpawner indicator)
    {
        if (!CanStartAct3Action() ||
            origins == null || origins.Length != 3 ||
            indicator == null ||
            width <= 0f || length <= 0f ||
            warningDuration <= 0f || hitInterval < 0f ||
            damageMultiplier < 0f)
            return false;

        if (!TrySampleAct3Teleport(center, out Vector3 position) || !WarpAct3To(position))
            return false;

        Vector3[] snapshot = (Vector3[])origins.Clone();
        Vector3 direction = Act3FlatDirection(forward);

        for (int i = 0; i < snapshot.Length; i++)
        {
            WBH_Effect warning = indicator.ShowRect(snapshot[i], direction, width, length, warningDuration + hitInterval * i, growOverTime: true);

            if (!IsNetworkSession && (warning == null || !warning.IsPlaying))
                return false;
        }

        return StartAct3Action(CoAct3Strips(snapshot, direction, width, length, Time.time + warningDuration, hitInterval, damageMultiplier));
    }

    private IEnumerator CoAct3Strips(Vector3[] origins, Vector3 forward, float width, float length, float firstHitAt, float hitInterval, float damageMultiplier)
    {
        for (int i = 0; i < origins.Length; i++)
        {
            float hitAt = firstHitAt + hitInterval * i;

            yield return new WaitForSeconds(Mathf.Max(0f, hitAt - Time.time));

            ApplyAct3RectDamage(origins[i], forward, width, length, damageMultiplier);
        }

        yield return new WaitForSeconds(0.5f);
    }

    // 9. 페이즈 전환의 맵 전체 공격
    public bool TryArenaAttack(Vector3 center, Vector2 mapSize, Quaternion rotation, float damageMultiplier, float warningDuration, WBH_IndicatorSpawner indicator,
                               System.Action onCompleted = null)
    {
        if (mapSize.x <= 0f || mapSize.y <= 0f || damageMultiplier < 0f || indicator == null)
            return false;

        Vector3 forward = Act3FlatDirection(rotation * Vector3.forward);

        Vector3 origin = center - forward * (mapSize.y * 0.5f);

        return TryAct3WarnedHit(indicator, warningDuration, 0f,
                                () => indicator.ShowRect(origin, forward, mapSize.x, mapSize.y, warningDuration, growOverTime: true),
                                () => ApplyAct3RectDamage(origin, forward, mapSize.x, mapSize.y, damageMultiplier),
                                onCompleted);
    }


    #endregion

    // 이펙트 및 디버그용 스킬 범위 표시 메서드.
    private void ShowDebugSector(Vector3 center, Vector3 forward, float range, float angle)
    {
        if (pattern == null || !pattern.isShowSkillRange)
            return;

        SkillRangeVisual.ShowSector(center, forward, range, angle, DefaultRangeColor, SkillRangeFlashDuration);
    }
    private void ShowDebugLine(Vector3 origin, Vector3 forward, float length, float width)
    {
        if (pattern == null || !pattern.isShowSkillRange)
            return;

        SkillRangeVisual.ShowLine(origin, forward, length, width, DefaultRangeColor, SkillRangeFlashDuration);
    }
}

using EnemySystem;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BH 일반 적 Pattern의 Mirror 테스트용 서버 AI다.
/// <para>원본과 달리 플레이어를 <c>Find</c>로 검색하지 않고, 서버가 등록한
/// <see cref="MirrorTestNetworkManager.ServerPlayerContexts"/>에서 살아 있는 가장 가까운 대상을 고른다.</para>
/// <para>현재 대상이 죽거나 접속이 끊긴 때에만 다시 고르며 이동과 공격 시작은 서버에서만 실행한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class WBH_EnemyPattern_MirrorTest : MonoBehaviour
{
    private const float PhaseTwoHealthRatio = 0.3f;
    private const float MissileCooldown = 20f;
    private const float MissileExplosionRadius = 3f;
    private const float MissileWarningDuration = 1.5f;
    private const float MissileRecoveryDuration = 0.4f;
    private const float MissileTargetSpreadRadius = 4f;
    private const float MissileMaxDistance = 100f;
    private const float MinimumMissileFlightTime = 1f;
    private const float BarrageCooldown = 15f;
    private const int BarrageBulletCount = 15;
    private const float BarrageSpreadAngle = 180f;
    private const float BarrageMaxDistance = 35f;
    private const float BarrageRecoveryDuration = 0.5f;
    private const float BurstCooldown = 3f;
    private const int BurstBulletCount = 5;
    private const float BurstInterval = 0.15f;
    private const float BurstMaxDistance = 12f;
    private const int TransitionMissilesPerRing = 8;
    private const float TransitionInnerRadius = 7f;
    private const float TransitionOuterRadius = 13f;
    private const float TransitionExplosionRadius = 2f;
    private const float TransitionWarningDuration = 2f;
    private const float TransitionRecoveryDuration = 0.4f;
    private const float TransitionArmorDuration = 3f;
    private const float JumpCooldown = 20f;
    private const float JumpTargetRange = 20f;
    private const float JumpDamageRadius = 5f;
    private const float JumpDuration = 1.5f;
    private const float JumpRecoveryDuration = 1f;
    private const float JumpHeight = 4f;
    private const float BossDestinationSampleDistance = 2f;
    private const float DashCooldown = 15f;
    private const float DashTargetRange = 30f;
    private const float DashMaxDistance = 30f;
    private const float DashDuration = 0.8f;
    private const float DashReadyDuration = 1f;
    private const float DashHitRadius = 3f;

    [SerializeField] private NetworkEnemyAuthority_MirrorTest authority;
    [SerializeField] private WBH_EnemyMovement movement;
    [SerializeField] private WBH_EnemyStatus status;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform grenadePoint;

    private PlayerContext target;
    private Coroutine bossActionRoutine;
    private float missileTimer;
    private float barrageTimer;
    private float burstTimer;
    private float dashTimer;
    private float jumpTimer;
    private bool bossPhaseTransitionStarted;
    private readonly HashSet<PlayerContext> dashTargets = new();

    public PlayerContext Target => target;
    public Transform FirePoint => firePoint != null ? firePoint : transform;
    public Transform GrenadePoint => grenadePoint != null ? grenadePoint : FirePoint;

    private void Awake()
    {
        ResolveReferences();
        enabled = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif

    [Server]
    public void InitializeServer(NetworkEnemyAuthority_MirrorTest owner)
    {
        authority = owner;
        ResolveReferences();
        bossActionRoutine = null;
        bossPhaseTransitionStarted = false;
        missileTimer = 0f;
        barrageTimer = 0f;
        burstTimer = 0f;
        dashTimer = 0f;
        jumpTimer = 0f;
        if (IsBoss)
            authority.ServerSetBossPhase(MirrorAct1BossPhase.PhaseOne);
        enabled = authority != null && authority.isServer;
    }

    [Server]
    public void StopServer()
    {
        StopAllCoroutines();
        bossActionRoutine = null;
        target = null;
        authority?.ServerSetTarget(null);
        StopMovementIfPossible();
        enabled = false;
    }

    private void Update()
    {
        if (authority == null || !authority.isServer || authority.IsDead || status == null)
            return;

        if (!IsCurrentTargetValid())
            target = SelectNearestAlivePlayer();

        authority.ServerSetTarget(target);
        if (IsBoss)
        {
            TickBoss(Time.deltaTime);
            return;
        }

        if (target == null)
        {
            StopMovementIfPossible();
            return;
        }

        Vector3 offset = target.transform.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (distance > status.AttackRange)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                movement.Move(target.transform.position);
            return;
        }

        StopMovementIfPossible();
        if (offset.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(offset.normalized);

        authority.ServerTryBeginAttack(target);
    }

    private bool IsBoss => authority != null && authority.EnemyInfo?.enemyAttackType == EnemyAttackType.Boss;

    [Server]
    private void TickBoss(float deltaTime)
    {
        missileTimer -= deltaTime;
        barrageTimer -= deltaTime;
        burstTimer -= deltaTime;
        dashTimer -= deltaTime;
        jumpTimer -= deltaTime;

        StopMovementIfPossible();
        if (bossActionRoutine != null)
            return;

        float healthRatio = authority.MaxHealth > 0f
            ? authority.CurrentHealth / authority.MaxHealth
            : 0f;
        if (!bossPhaseTransitionStarted && healthRatio <= PhaseTwoHealthRatio)
        {
            if (authority.IsAttackPending)
                return;

            bossPhaseTransitionStarted = true;
            bossActionRoutine = StartCoroutine(CoPhaseTwoTransition());
            return;
        }

        if (!bossPhaseTransitionStarted)
        {
            if (target == null)
                return;

            if (missileTimer <= 0f && !authority.IsAttackPending)
            {
                Vector3 center = target.transform.position;
                Vector3[] impactPoints =
                {
                    center,
                    center + Vector3.right * MissileTargetSpreadRadius,
                    center - Vector3.right * MissileTargetSpreadRadius,
                };
                missileTimer = MissileCooldown;
                bossActionRoutine = StartCoroutine(CoMissileVolley(
                    impactPoints,
                    MissileExplosionRadius,
                    MissileWarningDuration,
                    MissileRecoveryDuration,
                    4));
                return;
            }

            if (barrageTimer <= 0f && !authority.IsAttackPending)
            {
                barrageTimer = BarrageCooldown;
                bossActionRoutine = StartCoroutine(CoBarrage(
                    target.transform.position + Vector3.up));
                return;
            }

            if (burstTimer <= 0f && !authority.IsAttackPending)
            {
                burstTimer = BurstCooldown;
                bossActionRoutine = StartCoroutine(CoBurst());
                return;
            }

            return;
        }

        if (authority.BossPhase != MirrorAct1BossPhase.PhaseTwo)
            return;

        PlayerContext farTarget = SelectFarthestAlivePlayer(JumpTargetRange);
        if (jumpTimer <= 0f && farTarget != null)
        {
            target = farTarget;
            authority.ServerSetTarget(target);
            jumpTimer = JumpCooldown;
            bossActionRoutine = StartCoroutine(CoJumpAttack(target.transform.position));
            return;
        }

        farTarget = SelectFarthestAlivePlayer(DashTargetRange);
        if (dashTimer <= 0f && farTarget != null)
        {
            target = farTarget;
            authority.ServerSetTarget(target);
            dashTimer = DashCooldown;
            bossActionRoutine = StartCoroutine(CoDashAttack(target.transform.position));
            return;
        }

        TryBasicBossAttack();
    }

    [Server]
    private void TryBasicBossAttack()
    {
        if (target == null)
            return;

        Vector3 offset = target.transform.position - transform.position;
        offset.y = 0f;
        if (offset.magnitude > status.AttackRange)
            return;

        if (offset.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(offset.normalized);
        authority.ServerTryBeginAttack(target);
    }

    [Server]
    private IEnumerator CoPhaseTwoTransition()
    {
        authority.ServerSetBossPhase(MirrorAct1BossPhase.TransitionMissiles);
        Vector3 center = transform.position;
        Vector3[] impactPoints = new Vector3[TransitionMissilesPerRing * 2];
        float angleStep = 360f / TransitionMissilesPerRing;
        for (int index = 0; index < TransitionMissilesPerRing; index++)
        {
            Vector3 innerDirection =
                Quaternion.Euler(0f, angleStep * index, 0f) * Vector3.forward;
            Vector3 outerDirection =
                Quaternion.Euler(0f, angleStep * (index + 0.5f), 0f) * Vector3.forward;
            impactPoints[index] = center + innerDirection * TransitionInnerRadius;
            impactPoints[index + TransitionMissilesPerRing] =
                center + outerDirection * TransitionOuterRadius;
        }

        yield return CoMissileVolleyBody(
            impactPoints,
            TransitionExplosionRadius,
            TransitionWarningDuration,
            TransitionRecoveryDuration,
            5);
        authority.ServerSetBossPhase(MirrorAct1BossPhase.TransitionArmor);
        yield return new WaitForSeconds(TransitionArmorDuration);
        authority.ServerSetBossPhase(MirrorAct1BossPhase.PhaseTwo);
        dashTimer = 0f;
        jumpTimer = 0f;
        bossActionRoutine = null;
    }

    [Server]
    private IEnumerator CoMissileVolley(
        Vector3[] impactPoints,
        float explosionRadius,
        float warningDuration,
        float recoveryDuration,
        int skillId)
    {
        yield return CoMissileVolleyBody(
            impactPoints,
            explosionRadius,
            warningDuration,
            recoveryDuration,
            skillId);
        bossActionRoutine = null;
    }

    [Server]
    private IEnumerator CoMissileVolleyBody(
        Vector3[] impactPoints,
        float explosionRadius,
        float warningDuration,
        float recoveryDuration,
        int skillId)
    {
        Transform launchPoint = GrenadePoint;
        float projectileSpeed = Mathf.Max(0.01f, status.ProjectileSpeed);
        float[] flightTimes = new float[impactPoints.Length];
        float commonImpactTime = warningDuration;

        for (int index = 0; index < impactPoints.Length; index++)
        {
            float distance = Mathf.Min(
                Vector3.Distance(launchPoint.position, impactPoints[index]),
                MissileMaxDistance);
            flightTimes[index] = Mathf.Max(MinimumMissileFlightTime, distance / projectileSpeed);
            commonImpactTime = Mathf.Max(commonImpactTime, flightTimes[index]);
        }

        authority.ServerRecordBossMissileVolley();
        authority.ServerPlayBossSkill(skillId);
        for (int index = 0; index < impactPoints.Length; index++)
        {
            authority.ServerShowBossCircleIndicator(
                impactPoints[index],
                explosionRadius,
                commonImpactTime,
                true);
            float launchDelay = commonImpactTime - flightTimes[index];
            StartCoroutine(CoLaunchMissileAfter(
                launchDelay,
                impactPoints[index],
                flightTimes[index],
                explosionRadius));
        }

        yield return new WaitForSeconds(commonImpactTime + recoveryDuration);
    }

    [Server]
    private IEnumerator CoLaunchMissileAfter(
        float delay,
        Vector3 impactPoint,
        float flightDuration,
        float explosionRadius)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (authority != null && !authority.IsDead)
            authority.ServerLaunchBossMissile(impactPoint, flightDuration, explosionRadius);
    }

    [Server]
    private IEnumerator CoBarrage(Vector3 targetPoint)
    {
        Vector3 baseDirection = targetPoint - FirePoint.position;
        if (baseDirection.sqrMagnitude < 0.001f)
            baseDirection = transform.forward;

        Vector3 facing = baseDirection;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(facing.normalized);

        authority.ServerRecordBossBarrage();
        authority.ServerPlayBossSkill(3);
        for (int index = 0; index < BarrageBulletCount; index++)
        {
            float normalizedIndex = BarrageBulletCount > 1
                ? index / (float)(BarrageBulletCount - 1)
                : 0.5f;
            float angle = Mathf.Lerp(
                -BarrageSpreadAngle * 0.5f,
                BarrageSpreadAngle * 0.5f,
                normalizedIndex);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * baseDirection.normalized;
            authority.ServerLaunchBossProjectile(direction, BarrageMaxDistance);
        }

        yield return new WaitForSeconds(BarrageRecoveryDuration);
        bossActionRoutine = null;
    }

    [Server]
    private IEnumerator CoBurst()
    {
        authority.ServerRecordBossBurst();
        authority.ServerPlayBossSkill(2);
        for (int index = 0; index < BurstBulletCount; index++)
        {
            if (!IsCurrentTargetValid())
                target = SelectNearestAlivePlayer();

            if (target != null)
            {
                authority.ServerSetTarget(target);
                Vector3 targetPoint = target.transform.position + Vector3.up;
                Vector3 direction = targetPoint - FirePoint.position;
                Vector3 facing = direction;
                facing.y = 0f;
                if (facing.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(facing.normalized);
                authority.ServerLaunchBossProjectile(direction, BurstMaxDistance);
            }

            if (index < BurstBulletCount - 1)
                yield return new WaitForSeconds(BurstInterval);
        }

        bossActionRoutine = null;
    }

    [Server]
    private IEnumerator CoJumpAttack(Vector3 requestedLandingPoint)
    {
        if (!TryResolveBossDestination(requestedLandingPoint, out Vector3 landingPoint))
        {
            bossActionRoutine = null;
            yield break;
        }

        Vector3 offset = landingPoint - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(offset.normalized);

        authority.ServerRecordBossJump();
        authority.ServerShowBossCircleIndicator(
            landingPoint,
            JumpDamageRadius,
            JumpDuration,
            true);
        authority.ServerPlayBossJumpAnimation();

        Vector3 start = transform.position;
        bool restoreAgent = BeginManualMovement();
        float elapsed = 0f;
        while (elapsed < JumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / JumpDuration);
            Vector3 position = Vector3.Lerp(start, landingPoint, t);
            position.y += 4f * JumpHeight * t * (1f - t);
            transform.position = position;
            yield return null;
        }

        transform.position = landingPoint;
        EndManualMovement(landingPoint, restoreAgent);
        authority.ServerDamagePlayersInRadius(landingPoint, JumpDamageRadius);
        yield return new WaitForSeconds(JumpRecoveryDuration);
        bossActionRoutine = null;
    }

    [Server]
    private IEnumerator CoDashAttack(Vector3 requestedTargetPoint)
    {
        Vector3 start = transform.position;
        Vector3 direction = requestedTargetPoint - start;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            bossActionRoutine = null;
            yield break;
        }

        direction.Normalize();
        float distance = Mathf.Min(
            Vector3.Distance(
                new Vector3(start.x, 0f, start.z),
                new Vector3(requestedTargetPoint.x, 0f, requestedTargetPoint.z)),
            DashMaxDistance);
        if (!TryResolveBossDestination(start + direction * distance, out Vector3 end))
        {
            bossActionRoutine = null;
            yield break;
        }

        distance = Vector3.Distance(
            new Vector3(start.x, 0f, start.z),
            new Vector3(end.x, 0f, end.z));
        if (distance < 0.01f)
        {
            bossActionRoutine = null;
            yield break;
        }

        transform.rotation = Quaternion.LookRotation(direction);
        authority.ServerRecordBossDash();
        authority.ServerShowBossRectIndicator(
            start,
            direction,
            DashHitRadius * 2f,
            distance,
            DashReadyDuration);
        authority.ServerPlayBossSkill(7);
        yield return new WaitForSeconds(DashReadyDuration);

        dashTargets.Clear();
        bool restoreAgent = BeginManualMovement();
        float elapsed = 0f;
        while (elapsed < DashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / DashDuration);
            transform.position = Vector3.Lerp(start, end, t);
            authority.ServerDamagePlayersInRadius(transform.position, DashHitRadius, dashTargets);
            yield return null;
        }

        transform.position = end;
        EndManualMovement(end, restoreAgent);
        yield return null;
        bossActionRoutine = null;
    }

    private bool BeginManualMovement()
    {
        bool restoreAgent = agent != null && agent.enabled && agent.isOnNavMesh;
        if (restoreAgent)
        {
            agent.isStopped = true;
            agent.updatePosition = false;
        }

        return restoreAgent;
    }

    private void EndManualMovement(Vector3 destination, bool restoreAgent)
    {
        if (!restoreAgent || agent == null || !agent.enabled)
            return;

        agent.Warp(destination);
        agent.updatePosition = true;
        agent.isStopped = false;
    }

    private bool TryResolveBossDestination(Vector3 requestedPoint, out Vector3 destination)
    {
        destination = transform.position;
        int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
        if (!NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit startHit,
                BossDestinationSampleDistance,
                areaMask) ||
            !NavMesh.SamplePosition(
                requestedPoint,
                out NavMeshHit destinationHit,
                BossDestinationSampleDistance,
                areaMask))
        {
            return false;
        }

        float maximumHeightDifference = agent != null ? agent.height : 2f;
        if (Mathf.Abs(destinationHit.position.y - startHit.position.y) > maximumHeightDifference)
            return false;

        if (NavMesh.Raycast(startHit.position, destinationHit.position, out _, areaMask))
            return false;

        destination = destinationHit.position;
        return true;
    }

    private bool IsCurrentTargetValid()
    {
        if (target == null || !target.gameObject.activeInHierarchy || target.RuntimeState?.IsDead == true)
            return false;

        MirrorTestNetworkManager manager = NetworkManager.singleton as MirrorTestNetworkManager;
        if (manager == null)
            return false;

        foreach (PlayerContext candidate in manager.ServerPlayerContexts)
        {
            if (candidate == target)
                return true;
        }

        return false;
    }

    private PlayerContext SelectNearestAlivePlayer()
    {
        MirrorTestNetworkManager manager = NetworkManager.singleton as MirrorTestNetworkManager;
        if (manager == null)
            return null;

        PlayerContext nearest = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (PlayerContext candidate in manager.ServerPlayerContexts)
        {
            if (candidate == null ||
                !candidate.gameObject.activeInHierarchy ||
                candidate.RuntimeState?.IsDead == true)
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearest = candidate;
            nearestSqrDistance = sqrDistance;
        }

        return nearest;
    }

    private PlayerContext SelectFarthestAlivePlayer(float maxRange)
    {
        MirrorTestNetworkManager manager = NetworkManager.singleton as MirrorTestNetworkManager;
        if (manager == null)
            return null;

        PlayerContext farthest = null;
        float farthestSqrDistance = -1f;
        float maxSqrDistance = maxRange * maxRange;
        foreach (PlayerContext candidate in manager.ServerPlayerContexts)
        {
            if (candidate == null ||
                !candidate.gameObject.activeInHierarchy ||
                candidate.RuntimeState?.IsDead == true)
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - transform.position;
            offset.y = 0f;
            float sqrDistance = offset.sqrMagnitude;
            if (sqrDistance > maxSqrDistance || sqrDistance <= farthestSqrDistance)
                continue;

            farthest = candidate;
            farthestSqrDistance = sqrDistance;
        }

        return farthest;
    }

    private void StopMovementIfPossible()
    {
        if (movement != null && agent != null && agent.enabled && agent.isOnNavMesh)
            movement.Stop();
    }

    private void ResolveReferences()
    {
        authority ??= GetComponent<NetworkEnemyAuthority_MirrorTest>();
        movement ??= GetComponent<WBH_EnemyMovement>();
        status ??= GetComponent<WBH_EnemyStatus>();
        agent ??= GetComponent<NavMeshAgent>();

        if (firePoint != null && grenadePoint != null)
            return;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (firePoint == null && child.name == "FirePoint")
                firePoint = child;
            else if (grenadePoint == null && child.name == "GrenadePoint")
                grenadePoint = child;

            if (firePoint != null && grenadePoint != null)
                break;
        }
    }
}

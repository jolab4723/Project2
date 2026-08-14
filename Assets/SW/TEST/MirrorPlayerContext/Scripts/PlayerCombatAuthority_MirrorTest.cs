using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;

public enum MirrorCombatRequestResult : byte
{
    None = 0,
    Accepted = 1,
    Hit = 2,
    NoTarget = 3,
    Dead = 4,
    InvalidAim = 5,
    DuplicateRequest = 6,
    AttackOnCooldown = 7,
    InvalidTiming = 8,
}

/// <summary>
/// Fighter의 로컬 공격 연출과 서버의 실제 타격 판정을 분리하는 Mirror 테스트 컴포넌트다.
/// <para>클라이언트는 요청 번호와 조준점만 보내며 대상·데미지·치명타는 보내지 않는다.</para>
/// <para>서버가 공격속도를 반영한 타격 시각에 Physics 범위를 검사하고 같은 적의 여러 Collider를 한 번으로 합친다.</para>
/// <para>AnimationEvent는 시각 효과와 로컬 상태 종료에만 사용하고 실제 피해를 실행하지 않는다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerContext), typeof(T_PlayerCombat))]
public sealed class PlayerCombatAuthority_MirrorTest : NetworkBehaviour
{
    private const float BaseImpactSeconds = 0.6836111f;
    private const float BaseAttackDurationSeconds = 2.175f;
    private const float AnimatorAttackStateSpeed = 2f;
    private const float AttackAngle = 230f;
    private const float MaxAimDistance = 1000f;
    private const double MinimumBackdateSeconds = 0.05d;
    private const double MaximumBackdateSeconds = 0.35d;
    private const double FutureTimeToleranceSeconds = 0.1d;
    private const double CooldownBoundaryToleranceSeconds = 0.02d;

    [SerializeField] private PlayerContext context;
    [SerializeField] private T_PlayerCombat combat;
    [SerializeField] private WBH_PlayerStatus status;
    [SerializeField] private LayerMask enemyLayer = 1 << 10;

    [SyncVar] private MirrorCombatRequestResult lastResult;
    [SyncVar] private uint lastTargetNetId;
    [SyncVar] private float lastDamage;
    [SyncVar] private bool lastHitCritical;
    [SyncVar] private uint acceptedRequestCount;
    [SyncVar] private uint rejectedRequestCount;
    [SyncVar] private float lastRequestBackdateSeconds;
    [SyncVar] private float lastCooldownRemainingSeconds;
    [SyncVar] private bool lastRejectedWhileImpactPending;

    private readonly HashSet<WBH_ICombat> resolvedTargets = new();
    private uint nextLocalRequestId;
    private uint lastServerRequestId;
    private bool attackPending;
    private double impactAt;
    private double nextAttackAt;

    public MirrorCombatRequestResult LastResult => lastResult;
    public uint LastTargetNetId => lastTargetNetId;
    public float LastDamage => lastDamage;
    public bool LastHitCritical => lastHitCritical;
    public uint AcceptedRequestCount => acceptedRequestCount;
    public uint RejectedRequestCount => rejectedRequestCount;
    public float LastRequestBackdateSeconds => lastRequestBackdateSeconds;
    public float LastCooldownRemainingSeconds => lastCooldownRemainingSeconds;
    public bool LastRejectedWhileImpactPending => lastRejectedWhileImpactPending;

    private void Awake()
    {
        context ??= GetComponent<PlayerContext>();
        combat ??= GetComponent<T_PlayerCombat>();
        status ??= GetComponent<WBH_PlayerStatus>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        context ??= GetComponent<PlayerContext>();
        combat ??= GetComponent<T_PlayerCombat>();
        status ??= GetComponent<WBH_PlayerStatus>();
    }
#endif

    private void Update()
    {
        if (isServer && attackPending && NetworkTime.time >= impactAt)
        {
            attackPending = false;
            ResolveServerAttack();
        }
    }

    public bool TryBeginLocalAttack(Vector3 aimPoint)
    {
        if (!isLocalPlayer ||
            !NetworkClient.active ||
            !NetworkClient.ready ||
            context?.RuntimeState?.IsDead == true ||
            !IsFinite(aimPoint))
        {
            return false;
        }

        double localAttackStartedAt = NetworkTime.time;
        combat.TryAttack(aimPoint);
        if (context?.StateMachine == null || !context.StateMachine.Is(PlayerState.Attack))
            return false;

        nextLocalRequestId++;
        if (nextLocalRequestId == 0)
            nextLocalRequestId++;

        CmdRequestAttack(nextLocalRequestId, aimPoint, localAttackStartedAt);
        return true;
    }

    [Command]
    private void CmdRequestAttack(uint requestId, Vector3 aimPoint, double clientAttackStartedAt)
    {
        if (context?.RuntimeState?.IsDead == true || status == null || status.IsDead)
        {
            Reject(MirrorCombatRequestResult.Dead);
            return;
        }

        if (requestId == 0 || requestId <= lastServerRequestId)
        {
            Reject(MirrorCombatRequestResult.DuplicateRequest);
            return;
        }

        // 거절된 요청도 한 번 소비해 같은 번호를 나중에 다시 보내 공격으로 바꾸지 못하게 한다.
        lastServerRequestId = requestId;

        Vector3 lookDirection = aimPoint - transform.position;
        lookDirection.y = 0f;
        if (!IsFinite(aimPoint) ||
            lookDirection.sqrMagnitude < 0.001f ||
            lookDirection.sqrMagnitude > MaxAimDistance * MaxAimDistance)
        {
            Reject(MirrorCombatRequestResult.InvalidAim);
            return;
        }

        double now = NetworkTime.time;
        if (!double.IsFinite(clientAttackStartedAt) ||
            clientAttackStartedAt > now + FutureTimeToleranceSeconds)
        {
            Reject(MirrorCombatRequestResult.InvalidTiming);
            return;
        }

        // Client의 NetworkTime은 서버 시간 추정값이다. 실제 RTT 절반과 작은 프레임 여유만큼만
        // 과거 시작 시각을 인정해 Command 전달 시간이 선딜·공격 대기시간에 다시 더해지지 않게 한다.
        double estimatedOneWaySeconds = connectionToClient != null
            ? connectionToClient.rtt * 0.5d
            : 0d;
        double allowedBackdateSeconds = System.Math.Clamp(
            estimatedOneWaySeconds + MinimumBackdateSeconds,
            MinimumBackdateSeconds,
            MaximumBackdateSeconds);
        double authoritativeStartAt = System.Math.Clamp(
            clientAttackStartedAt,
            now - allowedBackdateSeconds,
            now);
        lastRequestBackdateSeconds = Mathf.Max(0f, (float)(now - clientAttackStartedAt));
        lastCooldownRemainingSeconds = Mathf.Max(0f, (float)(nextAttackAt - authoritativeStartAt));
        lastRejectedWhileImpactPending = attackPending;

        if (attackPending ||
            authoritativeStartAt + CooldownBoundaryToleranceSeconds < nextAttackAt)
        {
            Reject(MirrorCombatRequestResult.AttackOnCooldown);
            return;
        }

        transform.forward = lookDirection.normalized;

        // Fighter Animator의 Attack 상태 자체가 2배속이며 AttackSpeed 파라미터가 여기에 다시 곱해진다.
        // 서버도 같은 최종 재생 속도를 사용해야 타격 시점과 다음 공격 허용 시점이 화면의 애니메이션과 일치한다.
        float effectiveAnimationSpeed = Mathf.Max(0.01f, status.AttackSpeed * AnimatorAttackStateSpeed);
        impactAt = System.Math.Max(now, authoritativeStartAt + BaseImpactSeconds / effectiveAnimationSpeed);
        nextAttackAt = authoritativeStartAt + BaseAttackDurationSeconds / effectiveAnimationSpeed;
        attackPending = true;
        acceptedRequestCount++;
        lastCooldownRemainingSeconds = 0f;
        lastRejectedWhileImpactPending = false;
        lastResult = MirrorCombatRequestResult.Accepted;
    }

    [Server]
    private void ResolveServerAttack()
    {
        if (context?.RuntimeState?.IsDead == true || status == null || status.IsDead)
        {
            lastResult = MirrorCombatRequestResult.Dead;
            return;
        }

        resolvedTargets.Clear();
        lastTargetNetId = 0;
        lastDamage = 0f;
        lastHitCritical = false;

        Collider[] hits = Physics.OverlapSphere(transform.position, status.FighterAttackRange, enemyLayer);
        bool hitAny = false;

        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;

            Vector3 direction = hit.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f ||
                Vector3.Angle(transform.forward, direction.normalized) > AttackAngle * 0.5f)
            {
                continue;
            }

            WBH_ICombat target = FindCombatTarget(hit);
            if (target == null || !resolvedTargets.Add(target))
                continue;

            if (!WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    context,
                    target,
                    status.CurrentElement,
                    1f,
                    WBH_StatusEffectPresets.Slow1,
                    out WBH_DamageResult result))
            {
                continue;
            }

            hitAny = true;
            lastDamage = result.FinalDamage;
            lastHitCritical = result.IsCritical;

            if (target is Component targetComponent &&
                targetComponent.GetComponentInParent<NetworkIdentity>() is NetworkIdentity identity)
            {
                lastTargetNetId = identity.netId;
            }
        }

        lastResult = hitAny
            ? MirrorCombatRequestResult.Hit
            : MirrorCombatRequestResult.NoTarget;
    }

    private static WBH_ICombat FindCombatTarget(Collider hit)
    {
        MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is WBH_ICombat combatTarget)
                return combatTarget;
        }

        return null;
    }

    [Server]
    private void Reject(MirrorCombatRequestResult result)
    {
        rejectedRequestCount++;
        lastResult = result;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) &&
               float.IsFinite(value.y) &&
               float.IsFinite(value.z);
    }
}

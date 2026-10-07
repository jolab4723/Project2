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
    CanceledByMove = 9,
    AnimationNotConfirmed = 10,
    UnsupportedCharacter = 11,
    WeaponChanged = 12,
    ProjectileUnavailable = 13,
    /// <summary>보스 인트로·포탈 도착 대기 중이라 신규 공격을 승인하지 않음.</summary>
    ActionHeld = 14,
}

/// <summary>
/// Fighter와 제한된 Gunner 기본 공격의 로컬 연출과 서버 타격 판정을 분리한다.
/// <para>클라이언트는 요청 번호와 조준점만 보내며 대상·데미지·치명타는 보내지 않는다.</para>
/// <para>서버가 공격속도를 반영한 타격 시각에 Physics 범위를 검사하고 같은 적의 여러 Collider를 한 번으로 합친다.</para>
/// <para>클릭 Command는 공격을 예약할 뿐이며, 실제 공격 클립의 타격 AnimationEvent가 로컬에서 발생해야
/// 서버가 예약을 확정한다. Animator가 Attack 클립에 진입하지 못하면 서버 피해도 발생하지 않는다.</para>
/// <para>이동 취소의 전·후 기준도 추정 시간이 아니라 실제 타격 AnimationEvent다.
/// 이벤트 전 이동은 서버 예약까지 취소하고, 이벤트 후 이동은 다음 공격 가능 시각을 유지한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerContext), typeof(T_PlayerCombat))]
public sealed class PlayerCombatAuthority : NetworkBehaviour
{
    private const float BaseImpactSeconds = 0.6836111f;
    // Fighter_Attack_Short의 AniEvent_EndAttack 시각(1.0833334f). AnimatorAttackStateSpeed(2f) 적용 시 0.5417s
    private const float BaseAttackDurationSeconds = 1.0833334f;
    private const float AnimatorAttackStateSpeed = 2f;
    // 현재 GunnerController_Short / Gunner_Attack_Short의 실행 이벤트와 EndAttack 시각.
    private const float GunnerImpactSeconds = 0.16666667f;
    private const float GunnerAttackDurationSeconds = 0.76666665f;
    private const float MaxAimDistance = 1000f;
    private const double MinimumBackdateSeconds = 0.05d;
    private const double MaximumBackdateSeconds = 0.35d;
    private const double FutureTimeToleranceSeconds = 0.1d;
    private const double CooldownBoundaryToleranceSeconds = 0.05d;
    private const double MinimumImpactConfirmationGraceSeconds = 0.5d;
    private const double MaximumImpactConfirmationGraceSeconds = 2d;

    [SerializeField] private PlayerContext context;
    [SerializeField] private T_PlayerCombat combat;
    [SerializeField] private WBH_PlayerStatus status;
    [SerializeField] private LayerMask enemyLayer = 1 << 10;
    [Header("Gunner basic attack test")]
    [SerializeField] private NetworkEnemyProjectile gunnerProjectilePrefab;
    [SerializeField] private Transform gunnerFirePoint;
    [SerializeField, Min(0.01f)] private float gunnerExplosionRadius = 3f;

    [SyncVar] private MirrorCombatRequestResult lastResult;
    [SyncVar] private uint lastTargetNetId;
    [SyncVar] private float lastDamage;
    [SyncVar] private bool lastHitCritical;
    [SyncVar] private uint acceptedRequestCount;
    [SyncVar] private uint rejectedRequestCount;
    [SyncVar] private uint canceledRequestCount;
    [SyncVar] private uint unconfirmedAttackCount;
    [SyncVar] private float lastRequestBackdateSeconds;
    [SyncVar] private float lastCooldownRemainingSeconds;
    [SyncVar] private bool lastRejectedWhileImpactPending;
    [SyncVar] private uint gunnerShotCount;
    [SyncVar] private GunnerWeaponType lastGunnerWeapon;

    private readonly HashSet<WBH_ICombat> resolvedTargets = new();
    private readonly HashSet<WBH_ICombat> directAttackTargets = new();
    private uint resolvedAttackId;
    private uint directAttackId;
    private uint nextLocalRequestId;
    private uint activeLocalRequestId;
    [SyncVar] private uint lastServerRequestId;
    private uint pendingServerRequestId;
    private bool attackPending;
    private bool attackImpactConfirmed;
    private double impactAt;
    private double expectedClientImpactAt;
    private double impactConfirmationExpiresAt;
    [SyncVar] private double nextAttackAt;
    private double localImpactAt;
    private double localImpactConfirmationExpiresAt;
    private double localNextAttackAt;
    private GunnerWeaponType pendingGunnerWeapon;
    private string pendingGunnerItemId;
    private Vector3 pendingGunnerAim;
    private ElementType pendingGunnerElement;
    private float pendingGunnerRange;
    private float pendingGunnerSpeed;
    private GunnerWeaponType localGunnerWeapon;
    private string localGunnerItemId;

    public MirrorCombatRequestResult LastResult => lastResult;
    public uint LastTargetNetId => lastTargetNetId;
    public float LastDamage => lastDamage;
    public bool LastHitCritical => lastHitCritical;
    public uint AcceptedRequestCount => acceptedRequestCount;
    public uint RejectedRequestCount => rejectedRequestCount;
    public uint CanceledRequestCount => canceledRequestCount;
    public uint UnconfirmedAttackCount => unconfirmedAttackCount;
    public float LastRequestBackdateSeconds => lastRequestBackdateSeconds;
    public float LastCooldownRemainingSeconds => lastCooldownRemainingSeconds;
    public bool LastRejectedWhileImpactPending => lastRejectedWhileImpactPending;
    public bool ServerAttackPending => isServer && attackPending;
    public CharacterClass ResolvedCharacterClass
    {
        get
        {
            if (context?.Equipment?.CurrentCharacterClass.HasValue == true)
                return context.Equipment.CurrentCharacterClass.Value;

            if (gameObject.name.IndexOf("Gunner", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return CharacterClass.Gunner;

            return CharacterClass.Fighter;
        }
    }

    public bool IsGunner => ResolvedCharacterClass == CharacterClass.Gunner;
    public bool SupportsCharacter => true;
    public uint GunnerShotCount => gunnerShotCount;
    public GunnerWeaponType LastGunnerWeapon => lastGunnerWeapon;
    public bool CanContinueGunnerProjectile => IsGunner && !IsUnavailable;
    private float AttackAngle => status != null ? status.FighterAttackAngle : 0f;
    private float ImpactSeconds => IsGunner ? GunnerImpactSeconds : BaseImpactSeconds;
    private float AttackDurationSeconds => IsGunner ? GunnerAttackDurationSeconds : BaseAttackDurationSeconds;
    private bool IsUnavailable => !MirrorNetworkManager.CanPlay(netIdentity) || !SupportsCharacter || GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent == true ||
        context?.RuntimeState?.IsDead == true || status == null || status.IsDead;

    private void EnsureCharacterClass()
    {
        if (context?.Equipment != null && !context.Equipment.CurrentCharacterClass.HasValue)
        {
            context.Equipment.SetActiveCharacterClass(ResolvedCharacterClass);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        EnsureCharacterClass();
        lastServerRequestId = 0;
        pendingServerRequestId = 0;
        attackPending = false;
        attackImpactConfirmed = false;
        resolvedAttackId = 0;
        resolvedTargets.Clear();
        directAttackId = 0;
        directAttackTargets.Clear();
        context?.ItemTriggers?.ResetAttackLifetime();
    }

    public override void OnStopServer()
    {
        ServerCancelForDisconnect();
        base.OnStopServer();
    }

    /// <summary>새 소유 연결의 예측 요청을 초기화하고 서버 쿨다운을 유지한다.</summary>
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        EnsureCharacterClass();
        ClearLocalAttackPrediction();
        if (nextLocalRequestId <= lastServerRequestId)
            nextLocalRequestId = lastServerRequestId;
    }

    /// <summary>소유 연결 종료 시 남은 로컬 공격 예약을 제거한다.</summary>
    public override void OnStopLocalPlayer()
    {
        ClearLocalAttackPrediction();
        base.OnStopLocalPlayer();
    }

    private void ClearLocalAttackPrediction()
    {
        activeLocalRequestId = 0;
        localImpactAt = 0d;
        localImpactConfirmationExpiresAt = 0d;
        localNextAttackAt = nextAttackAt;
    }

    /// <summary>연결 종료로 미완료 공격을 취소한다. 이미 확정한 타격의 쿨다운은 유지한다.</summary>
    [Server]
    public void ServerCancelForDisconnect()
    {
        if (attackPending)
        {
            if (!attackImpactConfirmed) ExpireUnconfirmedAttack();
            else ClearServerAttackReservation();
        }
        // 새 소유 연결은 요청 번호를 1부터 발급한다. 서버 쿨다운은 연결을 넘어 유지한다.
        lastServerRequestId = 0;
        resolvedAttackId = 0;
        resolvedTargets.Clear();
        directAttackId = 0;
        directAttackTargets.Clear();
        context?.ItemTriggers?.ResetAttackLifetime();
    }

    [Server]
    public bool TryRegisterResolvedTarget(uint attackId, WBH_ICombat target)
    {
        if (attackId == 0 || target == null)
            return true;
        if (resolvedAttackId != attackId)
        {
            resolvedAttackId = attackId;
            resolvedTargets.Clear();
        }
        return resolvedTargets.Add(target);
    }

    [Server]
    public bool IsDirectTargetForAttack(uint attackId, WBH_ICombat target)
    {
        return attackId != 0 && attackId == directAttackId && target != null && directAttackTargets.Contains(target);
    }

    private void Awake()
    {
        context ??= GetComponent<PlayerContext>();
        combat ??= GetComponent<T_PlayerCombat>();
        status ??= GetComponent<WBH_PlayerStatus>();
        EnsureCharacterClass();
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
        if (IsUnavailable)
        {
            if (isServer) ServerCancelForDisconnect();
            if (isLocalPlayer) ClearLocalAttackPrediction();
            return;
        }
        // 공격 클립이 재생되지 않아 AnimationEvent도 오지 않은 요청은 로컬에서 영구히 붙잡지 않는다.
        // 이 정리는 서버 판정 권한과 무관하며, 다음 정상 공격의 요청 번호를 덮어쓰지 않게 하는 안전장치다.
        if (isLocalPlayer &&
            activeLocalRequestId != 0 &&
            NetworkTime.time >= localImpactConfirmationExpiresAt)
        {
            activeLocalRequestId = 0;
            localImpactAt = 0d;
            localImpactConfirmationExpiresAt = 0d;
            localNextAttackAt = System.Math.Min(localNextAttackAt, NetworkTime.time);

            // 공격 클립이 시작되지 않았다면 EndAttack 이벤트도 오지 않는다.
            // 미확인 요청을 정리할 때 로컬 상태도 Idle로 복구해야 이후 이동과 공격이 함께 풀린다.
            if (context?.StateMachine?.Is(PlayerState.Attack) == true)
                context.StateMachine.ChangeState(PlayerState.Idle);
        }

        if (!isServer || !attackPending)
            return;

        if (!attackImpactConfirmed)
        {
            if (NetworkTime.time >= impactConfirmationExpiresAt)
                ExpireUnconfirmedAttack();

            return;
        }

        if (NetworkTime.time < impactAt)
            return;

        // 요청 번호는 재전송 검증에만 쓰고, 피해 중복 판정용 번호는 스킬과 같은 발급기에서 받는다.
        // 두 번호 체계가 따로 1부터 시작해 평타와 첫 스킬이 같은 번호로 정상 피해를 거절하던 문제를 막는다.
        uint attackId = combat != null ? combat.CreateAttackId() : pendingServerRequestId;
        ClearServerAttackReservation();
        ResolveServerAttack(attackId);
    }

    public bool TryBeginLocalAttack(Vector3 aimPoint)
    {
        if (!isLocalPlayer ||
            !NetworkClient.active ||
            !NetworkClient.ready ||
            IsUnavailable || !MirrorNetworkManager.CanStartNewAction(netIdentity) ||
            context?.RuntimeState?.HasSnapshot != true ||
            status == null ||
            !IsFinite(aimPoint))
        {
            return false;
        }

        double localAttackStartedAt = NetworkTime.time;
        if (localAttackStartedAt < localNextAttackAt)
            return false;

        // TryAttack은 이미 Attack 상태면 새 모션을 시작하지 않는다.
        // 이때 요청 번호만 갱신하면 진행 중인 AnimationEvent가 다른 서버 예약에 연결된다.
        if (activeLocalRequestId != 0 || context.StateMachine == null ||
            context.StateMachine.Is(PlayerState.Attack))
            return false;

        // 이동이 논리 상태를 먼저 풀어도 이전 Attack/블렌드 중에는 새 클립을 시작할 수 없다.
        Animator animator = GetComponent<Animator>();
        if (animator == null || animator.IsInTransition(0) ||
            animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
            return false;

        combat.TryAttack(aimPoint);
        if (context?.StateMachine == null || !context.StateMachine.Is(PlayerState.Attack))
            return false;

        if (nextLocalRequestId <= lastServerRequestId)
            nextLocalRequestId = lastServerRequestId;

        nextLocalRequestId++;
        if (nextLocalRequestId == 0)
            nextLocalRequestId++;

        // 서버와 같은 애니메이션 속도식을 사용해 로컬에서도 거절될 공격 애니메이션이 먼저 나오지 않게 한다.
        // 이 값은 판정 권한이 아니라 입력 예측값이며, 실제 피해와 최종 허용 여부는 항상 서버가 결정한다.
        float effectiveAnimationSpeed = GetEffectiveAnimationSpeed();
        activeLocalRequestId = nextLocalRequestId;
        localImpactAt = localAttackStartedAt + ImpactSeconds / effectiveAnimationSpeed;
        localImpactConfirmationExpiresAt =
            localImpactAt + GetImpactConfirmationGraceSeconds();
        localNextAttackAt = localAttackStartedAt + AttackDurationSeconds / effectiveAnimationSpeed;
        if (IsGunner) TryGetGunnerWeapon(out localGunnerWeapon, out localGunnerItemId);

        CmdRequestAttack(nextLocalRequestId, aimPoint, localAttackStartedAt);
        return true;
    }

    /// <summary>
    /// 로컬 이동 입력이 들어왔을 때 아직 실제 타격 AnimationEvent가 발생하지 않은 평타만 취소 요청한다.
    /// <para>타격 전 취소는 피해 예약만 제거하고 다음 공격 가능 시각은 유지한다.</para>
    /// <para>타격 AnimationEvent 이후에는 이동만 허용하고 <c>localNextAttackAt</c>은 그대로 두어
    /// 이동을 반복해 평타 간격을 줄이는 후딜 캔슬 악용을 막는다.</para>
    /// </summary>
    public bool TryCancelLocalAttackForMove()
    {
        if (!isLocalPlayer ||
            !NetworkClient.active ||
            !NetworkClient.ready ||
            activeLocalRequestId == 0)
        {
            return false;
        }

        double canceledAt = NetworkTime.time;
        uint canceledRequestId = activeLocalRequestId;
        activeLocalRequestId = 0;
        localImpactAt = 0d;
        localImpactConfirmationExpiresAt = 0d;
        CmdCancelAttackForMove(canceledRequestId, canceledAt);
        return true;
    }

    /// <summary>
    /// 로컬 Fighter 공격 클립의 실제 타격 AnimationEvent가 발생했음을 서버에 알린다.
    /// <para>클릭 입력만으로는 이 메서드가 호출되지 않으므로, Animator가 Attack 클립을 재생하지 못한 요청은 피해를 만들 수 없다.</para>
    /// <para>대상과 피해량은 보내지 않으며 요청 번호와 이벤트 시각만 전달한다. 실제 대상 탐색과 피해 계산은 계속 서버가 담당한다.</para>
    /// </summary>
    public bool TryConfirmLocalAttackImpactFromAnimation()
    {
        if (IsUnavailable || !isLocalPlayer ||
            !NetworkClient.active ||
            !NetworkClient.ready ||
            activeLocalRequestId == 0)
        {
            return false;
        }

        double animationImpactAt = NetworkTime.time;

        // 너무 이른 이벤트는 잘못 연결된 클립이나 남아 있던 이벤트일 수 있으므로 서버에 보내지 않는다.
        // 최종 검증은 서버가 다시 수행하며 이 로컬 검사는 불필요한 Command만 줄인다.
        if (animationImpactAt + CooldownBoundaryToleranceSeconds < localImpactAt)
            return false;

        uint confirmedRequestId = activeLocalRequestId;
        activeLocalRequestId = 0;
        localImpactAt = 0d;
        localImpactConfirmationExpiresAt = 0d;
        // 소유자의 총구 연출은 싱글과 같은 AnimationEvent에서 재생한다. 피해·탄 생성은 서버만 확정한다.
        if (IsGunner && IsGunnerShotCurrent(localGunnerItemId, localGunnerWeapon))
        {
            Vector3 origin = gunnerFirePoint != null ? gunnerFirePoint.position : transform.position;
            Vector3 direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            GunnerCombatPresentation.PlayShot(gameObject, localGunnerItemId, localGunnerWeapon,
                origin, direction, status.GunnerAttackRange);
        }
        CmdConfirmAttackAnimationImpact(confirmedRequestId, animationImpactAt);
        return true;
    }

    [Command]
    private void CmdRequestAttack(uint requestId, Vector3 aimPoint, double clientAttackStartedAt)
    {
        if (!SupportsCharacter)
        {
            Reject(MirrorCombatRequestResult.UnsupportedCharacter);
            return;
        }

        if (IsUnavailable)
        {
            Reject(MirrorCombatRequestResult.Dead);
            return;
        }

        // 보스 인트로·포탈 대기 중의 신규 공격은 서버에서 거절한다. 진행 중인 공격은 기존 정책을 따른다.
        if (!MirrorNetworkManager.CanStartNewAction(netIdentity))
        {
            Reject(MirrorCombatRequestResult.ActionHeld);
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

        if (!TryResolveAuthoritativeClientTime(
                clientAttackStartedAt,
                out double now,
                out double authoritativeStartAt))
        {
            Reject(MirrorCombatRequestResult.InvalidTiming);
            return;
        }

        lastRequestBackdateSeconds = Mathf.Max(0f, (float)(now - clientAttackStartedAt));
        lastCooldownRemainingSeconds = Mathf.Max(0f, (float)(nextAttackAt - authoritativeStartAt));
        lastRejectedWhileImpactPending = attackPending;

        if (attackPending || GetComponent<FighterSkillAuthority>()?.ServerMotionLocked == true ||
            authoritativeStartAt + CooldownBoundaryToleranceSeconds < nextAttackAt)
        {
            Reject(MirrorCombatRequestResult.AttackOnCooldown);
            return;
        }

        if (IsGunner)
        {
            if (!TryGetGunnerWeapon(out pendingGunnerWeapon, out pendingGunnerItemId))
            {
                Reject(MirrorCombatRequestResult.UnsupportedCharacter);
                return;
            }
            if (pendingGunnerWeapon != GunnerWeaponType.Shotgun && gunnerProjectilePrefab == null)
            {
                Reject(MirrorCombatRequestResult.ProjectileUnavailable);
                return;
            }
            pendingGunnerAim = aimPoint;
            pendingGunnerElement = status.CurrentElement;
            pendingGunnerRange = Mathf.Max(0.1f, status.GunnerAttackRange);
            pendingGunnerSpeed = Mathf.Max(0.1f, status.GunnerBulletSpeed);
        }

        transform.forward = lookDirection.normalized;

        // Fighter Animator의 Attack 상태 자체가 2배속이며 AttackSpeed 파라미터가 여기에 다시 곱해진다.
        // 서버도 같은 최종 재생 속도를 사용해야 타격 시점과 다음 공격 허용 시점이 화면의 애니메이션과 일치한다.
        float effectiveAnimationSpeed = GetEffectiveAnimationSpeed();
        expectedClientImpactAt =
            authoritativeStartAt + ImpactSeconds / effectiveAnimationSpeed;
        impactAt = System.Math.Max(now, expectedClientImpactAt);
        impactConfirmationExpiresAt =
            impactAt + GetImpactConfirmationGraceSeconds();
        nextAttackAt = authoritativeStartAt + AttackDurationSeconds / effectiveAnimationSpeed;
        attackPending = true;
        attackImpactConfirmed = false;
        pendingServerRequestId = requestId;
        acceptedRequestCount++;
        lastCooldownRemainingSeconds = 0f;
        lastRejectedWhileImpactPending = false;
        lastResult = MirrorCombatRequestResult.Accepted;
    }

    /// <summary>
    /// 클릭 요청과 같은 번호의 공격 AnimationEvent만 서버 예약을 실제 타격 가능 상태로 확정한다.
    /// 클라이언트는 피해 결과를 결정하지 않으며, 서버가 예상한 타격 시각보다 비정상적으로 빠르거나
    /// 확인 유예 시간이 지난 이벤트는 무시한다.
    /// </summary>
    [Command]
    private void CmdConfirmAttackAnimationImpact(
        uint requestId,
        double clientAnimationImpactAt)
    {
        if (IsUnavailable || !attackPending ||
            attackImpactConfirmed ||
            requestId == 0 ||
            requestId != pendingServerRequestId)
        {
            return;
        }

        if (!TryResolveAuthoritativeClientTime(
                clientAnimationImpactAt,
                out double now,
                out double authoritativeImpactAt))
        {
            Reject(MirrorCombatRequestResult.InvalidTiming);
            return;
        }

        if (now > impactConfirmationExpiresAt ||
            authoritativeImpactAt + CooldownBoundaryToleranceSeconds < expectedClientImpactAt)
        {
            return;
        }

        attackImpactConfirmed = true;
    }

    /// <summary>
    /// 같은 연결에서 먼저 접수된 공격 번호와 정확히 일치하는 타격 전 예약만 취소한다.
    /// 클라이언트가 임의 번호를 보내거나 타격 시각 뒤에 취소해 이미 확정된 피해를 되돌릴 수는 없다.
    /// </summary>
    [Command]
    private void CmdCancelAttackForMove(uint requestId, double clientCanceledAt)
    {
        if (IsUnavailable || !attackPending || requestId == 0 || requestId != pendingServerRequestId)
            return;

        if (!TryResolveAuthoritativeClientTime(
                clientCanceledAt,
                out _,
                out double authoritativeCanceledAt))
        {
            Reject(MirrorCombatRequestResult.InvalidTiming);
            return;
        }

        // 같은 연결의 Command는 순서를 보장한다. AnimationEvent 확인이 먼저 도착했다면 이미 실제 타격이므로
        // 이후 이동은 모션만 끊고 피해와 다음 공격 가능 시각을 취소하지 않는다.
        if (attackImpactConfirmed)
            return;

        ClearServerAttackReservation();
        canceledRequestCount++;
        lastTargetNetId = 0;
        lastDamage = 0f;
        lastHitCritical = false;
        lastCooldownRemainingSeconds = Mathf.Max(0f, (float)(nextAttackAt - authoritativeCanceledAt));
        lastRejectedWhileImpactPending = false;
        lastResult = MirrorCombatRequestResult.CanceledByMove;

        Debug.Assert(
            !attackPending && pendingServerRequestId == 0,
            "[PlayerCombatAuthority] 이동 취소 뒤 서버 공격 예약이 남아 있습니다.",
            this);
    }

    /// <summary>
    /// 서버가 공격 요청은 받았지만 제한 시간 안에 실제 공격 클립의 타격 이벤트를 받지 못한 경우다.
    /// 피해를 만들지 않고 예약과 대기시간을 해제하여, 보이지 않는 공격과 다음 입력 잠김을 함께 막는다.
    /// </summary>
    [Server]
    private void ExpireUnconfirmedAttack()
    {
        ClearServerAttackReservation();
        nextAttackAt = NetworkTime.time;
        unconfirmedAttackCount++;
        lastTargetNetId = 0;
        lastDamage = 0f;
        lastHitCritical = false;
        lastCooldownRemainingSeconds = 0f;
        lastRejectedWhileImpactPending = false;
        lastResult = MirrorCombatRequestResult.AnimationNotConfirmed;
    }

    [Server]
    private void ClearServerAttackReservation()
    {
        attackPending = false;
        attackImpactConfirmed = false;
        pendingServerRequestId = 0;
        impactAt = 0d;
        expectedClientImpactAt = 0d;
        impactConfirmationExpiresAt = 0d;
    }

    /// <summary>SW 수정: 서버가 Fighter 기본 공격의 원점·정면과 직접 대상을 한 번 확정하고 피해 처리 동안에만 고유효과 출처를 유지한다.</summary>
    [Server]
    private void ResolveServerAttack(uint attackId)
    {
        if (IsUnavailable)
        {
            lastResult = MirrorCombatRequestResult.Dead;
            return;
        }

        if (IsGunner)
        {
            ResolveServerGunnerAttack(attackId);
            return;
        }

        lastTargetNetId = 0;
        lastDamage = 0f;
        lastHitCritical = false;

        // SW 수정: 피격·사망 콜백 중 Transform이 바뀌어도 같은 공격은 같은 정면을 사용한다.
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Collider[] hits = Physics.OverlapSphere(origin, status.FighterAttackRange, enemyLayer);
        var targets = new List<WBH_ICombat>();
        var targetColliders = new Dictionary<WBH_ICombat, Collider>();
        bool hitAny = false;

        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;

            Vector3 direction = hit.transform.position - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f ||
                Vector3.Angle(forward, direction.normalized) > AttackAngle * 0.5f)
            {
                continue;
            }

            WBH_ICombat target = FindCombatTarget(hit);
            if (target == null || !targetColliders.TryAdd(target, hit))
                continue;

            targets.Add(target);
        }

        targets.Sort(PlayerItemEffectState.CompareStableIds);
        directAttackId = attackId;
        directAttackTargets.Clear();
        foreach (WBH_ICombat target in targets)
            directAttackTargets.Add(target);

        try
        {
            // SW 수정: 서버가 확정한 근접 원점을 폐열 방출에서도 피격 콜백 이전 값으로 보존한다.
            context.Effects.SetDirectTargets(attackId, targets, forward, fighterAttack: true, attackOrigin: origin);
            WBH_EffectData effectData = null;
            GetComponent<WBH_PlayerEffect>()?.TryGetEffectData(WBH_PlayerEffectCue.F_normal0_evo0_etc0, out effectData);
            foreach (WBH_ICombat target in targets)
            {
                Vector3 hitPosition = targetColliders[target].ClosestPoint(origin);
                var request = new WBH_DamageRequest(context.Controller, target, WBH_AttackType.Normal,
                    status.CurrentElement, 1f, GetStatusEffectForElement(status.CurrentElement), effectData,
                    hitPosition, origin - hitPosition, DamageCause.Direct, attackId);
                if (!WBH_CombatResolver.TryProcessPlayerDamage(context, request, out WBH_DamageResult result))
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
        }
        finally
        {
            context.Effects.SetDirectTargets(0, null);
            directAttackId = 0;
            directAttackTargets.Clear();
        }

        lastResult = hitAny
            ? MirrorCombatRequestResult.Hit
            : MirrorCombatRequestResult.NoTarget;
    }

    public static WBH_StatusEffectData? GetStatusEffectForElement(ElementType element)
    {
        return element switch
        {
            ElementType.Fire => WBH_StatusEffectPresets.Burn1,
            ElementType.Ice => WBH_StatusEffectPresets.Freeze1,
            ElementType.Electric => WBH_StatusEffectPresets.Electric1,
            _ => null,
        };
    }

    internal static WBH_ICombat FindCombatTarget(Collider hit)
    {
        MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is WBH_ICombat combatTarget)
                return combatTarget;
        }

        return null;
    }

    private bool TryGetGunnerWeapon(out GunnerWeaponType weaponType, out string itemId)
    {
        weaponType = combat != null ? combat.currentWeapon : GunnerWeaponType.Rifle;
        itemId = string.Empty;
        if (!IsGunner || context.Equipment == null) return false;
        if (context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance item))
        {
            if (item?.definition == null || item.definition.characterClass != CharacterClass.Gunner) return false;
            itemId = item.definition.itemId ?? string.Empty;
            switch (item.definition.weaponType)
            {
                case WeaponType.Rifle: weaponType = GunnerWeaponType.Rifle; break;
                case WeaponType.Shotgun: weaponType = GunnerWeaponType.Shotgun; break;
                case WeaponType.GrenadeLauncher: weaponType = GunnerWeaponType.GrenadeLauncher; break;
                default: return false;
            }
        }
        return weaponType is GunnerWeaponType.Rifle or GunnerWeaponType.Shotgun or GunnerWeaponType.GrenadeLauncher;
    }

    // 예약 중 교체는 이전 무기의 발사를 취소한다. 이미 발사된 탄은 원본처럼 명중 시점 Stat을 읽는다.
    public bool IsGunnerShotCurrent(string itemId, GunnerWeaponType weaponType)
    {
        return !IsUnavailable && TryGetGunnerWeapon(out GunnerWeaponType currentType, out string currentId) &&
            currentType == weaponType && string.Equals(currentId, itemId ?? string.Empty, System.StringComparison.Ordinal);
    }

    /// <summary>SW 수정: 서버에서 실제 Gunner 발사를 확정하고 Shotgun은 발사 원점 기준 유효 대상·적중점을 고정해 공통 효과 출처를 피해 범위 동안 유지한다.</summary>
    [Server]
    private void ResolveServerGunnerAttack(uint attackId)
    {
        lastTargetNetId = 0;
        lastDamage = 0f;
        lastHitCritical = false;
        if (!IsGunnerShotCurrent(pendingGunnerItemId, pendingGunnerWeapon))
        {
            Reject(MirrorCombatRequestResult.WeaponChanged);
            return;
        }
        if (pendingGunnerWeapon != GunnerWeaponType.Shotgun && gunnerProjectilePrefab == null)
        {
            Reject(MirrorCombatRequestResult.ProjectileUnavailable);
            return;
        }

        Vector3 origin = gunnerFirePoint != null ? gunnerFirePoint.position : transform.position + Vector3.up;
        Vector3 direction = pendingGunnerAim - transform.position;
        direction.y = 0f;
        direction.Normalize();
        lastGunnerWeapon = pendingGunnerWeapon;
        gunnerShotCount++;
        lastResult = MirrorCombatRequestResult.Accepted;
        RpcPresentGunnerShot(pendingGunnerItemId, pendingGunnerWeapon, origin, direction, pendingGunnerRange);

        if (pendingGunnerWeapon == GunnerWeaponType.Shotgun)
        {
            context.Effects.ReserveEchoReplay(attackId, origin, direction, pendingGunnerRange, 90f);
            // SW 수정: 사망·이동 콜백 전에 본체별 가장 가까운 유효 표면과 대상 전체를 고정한다.
            var hitPositions = new Dictionary<WBH_ICombat, Vector3>();
            foreach (Collider hit in Physics.OverlapSphere(origin, pendingGunnerRange, enemyLayer, QueryTriggerInteraction.Collide))
            {
                WBH_ICombat target = FindCombatTarget(hit);
                if (target is not Component component || component.GetComponentInParent<NetworkEnemyAuthority>() == null ||
                    target.Status == null || target.Status.IsDead) continue;
                Vector3 hitPosition = hit.ClosestPoint(origin);
                Vector3 offset = hitPosition - origin;
                Vector3 flat = Vector3.ProjectOnPlane(offset, Vector3.up);
                if (Vector3.Angle(direction, flat) > 45f ||
                    Physics.Linecast(origin, hitPosition, LayerMask.GetMask("Wall", "Prop", "Ground"), QueryTriggerInteraction.Ignore)) continue;
                if (!hitPositions.TryGetValue(target, out Vector3 previous) || offset.sqrMagnitude < (previous - origin).sqrMagnitude)
                    hitPositions[target] = hitPosition;
            }
            var targets = new List<WBH_ICombat>(hitPositions.Keys);
            targets.Sort((left, right) =>
            {
                int byDistance = (hitPositions[left] - origin).sqrMagnitude.CompareTo((hitPositions[right] - origin).sqrMagnitude);
                return byDistance != 0 ? byDistance : PlayerItemEffectState.CompareStableIds(left, right);
            });
            directAttackId = attackId;
            directAttackTargets.Clear();
            directAttackTargets.UnionWith(targets);
            try
            {
                context.Effects.SetDirectTargets(attackId, targets, direction, attackOrigin: origin, shotgunAttack: true);
                foreach (WBH_ICombat target in targets)
                {
                    Vector3 hitPosition = hitPositions[target];
                    var request = new WBH_DamageRequest(context.Controller, target, WBH_AttackType.Normal,
                        pendingGunnerElement, 1f, GetStatusEffectForElement(pendingGunnerElement),
                        GunnerCombatPresentation.GetHitEffectData(gameObject, pendingGunnerWeapon),
                        hitPosition, origin - hitPosition, DamageCause.Direct, attackId);
                    if (WBH_CombatResolver.TryProcessPlayerDamage(context, request, out WBH_DamageResult result))
                    {
                        ServerRecordGunnerHit(target, result);
                        Vector3 impactDirection = (hitPosition - origin).normalized;
                        impactDirection.y = 0f;
                        RpcPresentGunnerImpact(pendingGunnerItemId, pendingGunnerWeapon, hitPosition, -impactDirection);
                    }
                }
            }
            finally
            {
                context.Effects.SetDirectTargets(0, null);
                directAttackId = 0;
                directAttackTargets.Clear();
            }
            if (lastResult != MirrorCombatRequestResult.Hit) lastResult = MirrorCombatRequestResult.NoTarget;
            return;
        }

        NetworkEnemyProjectile projectile = Instantiate(gunnerProjectilePrefab, origin, Quaternion.LookRotation(direction));
        UniqueEffectSO weaponEffect = null;
        if (context != null && context.Equipment != null &&
            context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance weaponInstance) &&
            weaponInstance != null && weaponInstance.definition != null)
        {
            weaponEffect = weaponInstance.definition.uniqueEffect;
        }
        projectile.InitializePlayerServer(context, pendingGunnerWeapon, pendingGunnerItemId, pendingGunnerElement,
            direction, pendingGunnerSpeed, pendingGunnerRange, pendingGunnerAim, gunnerExplosionRadius, attackId,
            weaponEffect);
        NetworkServer.Spawn(projectile.gameObject);
    }

    [Server]
    public void ServerRecordGunnerHit(WBH_ICombat target, WBH_DamageResult result)
    {
        lastResult = MirrorCombatRequestResult.Hit;
        lastDamage = result.FinalDamage;
        lastHitCritical = result.IsCritical;
        if (target is Component component) lastTargetNetId = component.GetComponentInParent<NetworkIdentity>()?.netId ?? 0;
    }

    [ClientRpc]
    private void RpcPresentGunnerShot(string itemId, GunnerWeaponType weaponType, Vector3 origin, Vector3 direction, float range)
    {
        if (isLocalPlayer) return; // 실제 타격 이벤트에서 이미 표시한 소유자 연출을 중복 재생하지 않는다.
        GunnerCombatPresentation.PlayShot(gameObject, itemId, weaponType, origin, direction, range);
    }

    [Server]
    public void ServerPresentGunnerImpact(string itemId, GunnerWeaponType weaponType, Vector3 position, Vector3 direction)
    {
        RpcPresentGunnerImpact(itemId, weaponType, position, direction);
    }

    [ClientRpc]
    private void RpcPresentGunnerImpact(string itemId, GunnerWeaponType weaponType, Vector3 position, Vector3 direction)
    {
        GunnerCombatPresentation.PlayImpact(gameObject, itemId, weaponType, position, direction);
    }

    /// <summary>
    /// 투사체 귀속 정책 도입으로 장착 세대 추적이 불필요해졌다. 검증기·호출부의 호환을 위해 상수 1을 유지한다.
    /// </summary>
    [System.Obsolete("투사체 귀속 정책으로 장착 세대 추적 불필요. 호환 유지 목적의 상수값이며 실제 세대를 반영하지 않는다.")]
    public uint WeaponEquipGeneration => 1u;

    private uint activeHitAttackId;
    private GunnerWeaponType activeHitWeaponType;
    private UniqueEffectSO activeHitUniqueEffect;

    /// <summary>
    /// 단일 동기식 투사체 명중 처리 동안만 해당 AttackId의 출처 정보를 노출한다.
    /// 이미 다른 스코프가 활성화되어 있으면 재진입을 거절하고 false를 반환한다.
    /// </summary>
    public bool TryBeginGunnerHitScope(uint attackId, GunnerWeaponType weaponType, UniqueEffectSO effect, out System.IDisposable scope)
    {
        if (attackId == 0 || activeHitAttackId != 0)
        {
            scope = null;
            return false;
        }

        activeHitAttackId = attackId;
        activeHitWeaponType = weaponType;
        activeHitUniqueEffect = effect;
        scope = new HitScopeDisposable(this, attackId);
        return true;
    }

    public System.IDisposable BeginGunnerHitScope(uint attackId, GunnerWeaponType weaponType, UniqueEffectSO effect)
    {
        return TryBeginGunnerHitScope(attackId, weaponType, effect, out System.IDisposable scope) ? scope : null;
    }

    private sealed class HitScopeDisposable : System.IDisposable
    {
        private readonly PlayerCombatAuthority authority;
        private readonly uint attackId;
        private bool disposed;

        public HitScopeDisposable(PlayerCombatAuthority authority, uint attackId)
        {
            this.authority = authority;
            this.attackId = attackId;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (authority != null && authority.activeHitAttackId == attackId)
            {
                authority.activeHitAttackId = 0;
                authority.activeHitWeaponType = default;
                authority.activeHitUniqueEffect = null;
            }
        }
    }

    public bool TryGetGunnerHitSource(uint attackId, out UniqueEffectSO sourceEffect, out GunnerWeaponType sourceWeapon)
    {
        if (activeHitAttackId != 0 && activeHitAttackId == attackId)
        {
            sourceEffect = activeHitUniqueEffect;
            sourceWeapon = activeHitWeaponType;
            return true;
        }

        sourceEffect = null;
        sourceWeapon = GunnerWeaponType.Rifle;
        return false;
    }

    /// <summary>
    /// <para>투사체 귀속 정책에서 장착 상태는 추가타 자격 판단에 사용되지 않는다.</para>
    /// <para><c>sourceStillEquipped</c>는 항상 false를 반환하며 호환 목적으로만 남아 있다.</para>
    /// </summary>
    public bool TryGetGunnerHitSource(uint attackId, out UniqueEffectSO sourceEffect, out GunnerWeaponType sourceWeapon, out bool sourceStillEquipped)
    {
        sourceStillEquipped = false; // 투사체 귀속 정책: 장착 상태와 무관하게 발사 시점 효과로 판단
        return TryGetGunnerHitSource(attackId, out sourceEffect, out sourceWeapon);
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

    /// <summary>
    /// 클라이언트가 보낸 NetworkTime을 서버가 허용할 수 있는 짧은 과거 구간으로 제한한다.
    /// 공격 시작과 이동 취소가 같은 시간 기준을 사용해야 지연 때문에 한쪽만 과도하게 불리해지지 않는다.
    /// </summary>
    private bool TryResolveAuthoritativeClientTime(
        double clientTime,
        out double now,
        out double authoritativeTime)
    {
        now = NetworkTime.time;
        authoritativeTime = now;

        if (!double.IsFinite(clientTime) || clientTime > now + FutureTimeToleranceSeconds)
            return false;

        double estimatedOneWaySeconds = connectionToClient != null
            ? connectionToClient.rtt * 0.5d
            : 0d;
        double allowedBackdateSeconds = System.Math.Clamp(
            estimatedOneWaySeconds + MinimumBackdateSeconds,
            MinimumBackdateSeconds,
            MaximumBackdateSeconds);
        authoritativeTime = System.Math.Clamp(
            clientTime,
            now - allowedBackdateSeconds,
            now);
        return true;
    }

    private float GetEffectiveAnimationSpeed()
    {
        return Mathf.Max(0.01f, status.AttackSpeed * AnimatorAttackStateSpeed);
    }

    /// <summary>
    /// AnimationEvent 확인 Command가 왕복 지연 때문에 늦게 도착할 수 있는 범위를 서버 연결의 RTT에 맞춰 허용한다.
    /// 유예 시간은 피해 시점을 늦추기 위한 값이 아니라, 정상 이벤트가 도착하기 전에 예약을 폐기하지 않기 위한 상한이다.
    /// </summary>
    private double GetImpactConfirmationGraceSeconds()
    {
        double roundTripSeconds = isServer && connectionToClient != null
            ? connectionToClient.rtt
            : NetworkTime.rtt;
        roundTripSeconds = System.Math.Max(0d, roundTripSeconds);

        return System.Math.Clamp(
            roundTripSeconds + 0.25d,
            MinimumImpactConfirmationGraceSeconds,
            MaximumImpactConfirmationGraceSeconds);
    }
}

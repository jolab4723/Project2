using EnemySystem;
using ItemSystem;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum MirrorAct1BossPhase : byte
{
    Inactive = 0,
    PhaseOne = 1,
    TransitionMissiles = 2,
    TransitionArmor = 3,
    PhaseTwo = 4,
    Dead = 5,
}

/// <summary>
/// 일반 적 한 개체의 체력·타깃·공격·사망·보상을 서버에서 한 번만 확정하는 Mirror 테스트 컴포넌트다.
/// <para>BH 원본 Controller는 초기화 어댑터로만 사용하고 비활성 상태를 유지하므로
/// 원본 <c>OnDead → 로컬 Pool.Return()</c> 경로는 실행되지 않는다.</para>
/// <para>클라이언트에는 지속 상태를 SyncVar로, 공격·피격·사망 연출을 NetworkAnimator/RPC로 전달한다.
/// 파괴 파편은 네트워크 객체로 만들지 않고 각 클라이언트의 기존 로컬 풀에서 한 번만 재생한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(WBH_EnemyController))]
[RequireComponent(typeof(WBH_EnemyEffect))]
public sealed class NetworkEnemyAuthority : NetworkBehaviour
{
    private const float MeleeAngle = 120f;
    private static readonly WBH_StatusEffectType[] StatusTypes =
        (WBH_StatusEffectType[])System.Enum.GetValues(typeof(WBH_StatusEffectType));
    private static readonly WBH_PlayerEffectCue[] PlayerEffectCues =
        (WBH_PlayerEffectCue[])System.Enum.GetValues(typeof(WBH_PlayerEffectCue));

    /// <summary>현재 실행에서 서버가 확정한 적 사망 누적 횟수다.</summary>
    public static uint ServerDeathCount { get; private set; }

    /// <summary>현재 실행에서 서버가 처리한 처치 보상 누적 횟수다.</summary>
    public static uint ServerRewardCount { get; private set; }

    /// <summary>현재 실행에서 서버가 생성한 적 드롭 누적 횟수다.</summary>
    public static uint ServerDropCount { get; private set; }

    /// <summary>현재 프로세스에서 실제 재생한 적 파괴 연출 누적 횟수다.</summary>
    public static uint LocalDeathPresentationCount { get; private set; }

    [Header("일반 적 데이터")]
    [SerializeField, SyncVar] private WBH_EnemyInfo enemyInfo;
    [SerializeField, Min(0.05f)] private float destroyDelay = 0.35f;
    [SerializeField] private bool useAnimatorOnlyDeathPresentation;
    [SerializeField] private LayerMask playerLayer = 1 << 15;

    [Header("서버 투사체")]
    [SerializeField] private NetworkEnemyProjectile projectilePrefab;
    [SerializeField] private NetworkEnemyProjectile bossMissilePrefab;

    [Header("원본 어댑터와 테스트 AI")]
    [SerializeField] private WBH_EnemyController controller;
    [SerializeField] private WBH_EnemyStatus status;
    [SerializeField] private WBH_EnemyMovement movement;
    [SerializeField] private WBH_EnemyCombat originalCombat;
    [SerializeField] private WBH_EnemyPattern originalPattern;
    [SerializeField] private WBH_EnemyAnimation originalAnimation;
    [SerializeField] private WBH_EnemyView originalView;
    [SerializeField] private WBH_EffectSpawner originalEffectSpawner;
    [SerializeField] private WBH_IndicatorSpawner indicatorSpawner;
    [SerializeField] private NetworkEnemyPattern networkPattern;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private NetworkAnimator networkAnimator;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyDestructionLink destructionLink;

    [SyncVar] private float currentHealth;
    [SyncVar] private float maxHealth;
    [SyncVar] private bool isDead;
    [SyncVar(hook = nameof(OnStatusVisualMaskChanged))] private uint statusVisualMask;
    [SyncVar(hook = nameof(OnEffectPlaybackSpeedChanged))] private float effectPlaybackSpeed = 1f;
    [SyncVar] private uint targetNetId;
    [SyncVar] private uint lastAttackerNetId;
    [SyncVar] private uint stateChangeNumber;
    [SyncVar] private float lastDamage;
    [SyncVar] private bool lastDamageCritical;
    [SyncVar] private uint receivedDamagePresentationCount;
    [SyncVar] private uint attackStartCount;
    [SyncVar] private uint hitCount;
    [SyncVar] private uint killRewardCount;
    [SyncVar] private uint dropSpawnCount;
    [SyncVar] private uint destructionPresentationCount;
    [SyncVar] private MirrorAct1BossPhase bossPhase;
    [SyncVar] private int bossPhaseVisualSeed;
    [SyncVar] private uint bossMissileVolleyCount;
    [SyncVar] private uint bossMissileLaunchCount;
    [SyncVar] private uint bossBarrageCount;
    [SyncVar] private uint bossBurstCount;
    [SyncVar] private uint bossBulletLaunchCount;
    [SyncVar] private uint bossDashCount;
    [SyncVar] private uint bossJumpCount;

    private readonly HashSet<PlayerContext> meleeTargets = new();
    private PlayerContext pendingAttackTarget;
    private PlayerContext lastAttackerContext;
    private double attackDeadline;
    private uint attackVersion;
    private bool attackHitResolved;
    private bool attackAnimationEntered;
    private float attackAnimationTimeout = 1f;
    private readonly HashSet<AnimationClip> attackClips = new();
    private readonly List<AnimatorClipInfo> attackClipInfo = new();
    private WBH_Effect remoteAct2TransitionEffect;
    private static readonly int PhaseTransitionHash = Animator.StringToHash("IsPhaseTransition");
    private double nextAttackAt;
    private bool attackPending;
    private bool deathHandled;
    private Vector3 lastImpactPoint;
    private Vector3 lastAttackDirection;
    private bool originalStatusEffectsReady;
    private bool lastDamageWasDot;
    private bool localEffectSpawnerInitialized;

    private WBH_EffectSpawner sharedEffectSpawner;
    private WBH_ProjectileSpawner sharedProjectileSpawner;
    private NetworkEnemyCombatView combatView;
    private bool missingCombatViewReported;
    private WBH_EnemyStatusEffectController statusEffects;

    private bool ResolveSharedSpawners()
    {
        // 적 프리팹의 미연결 Spawner 대신 씬의 공용 Pool과 같은 객체에 있는 Spawner를 사용한다.
        sharedEffectSpawner ??= FindFirstObjectByType<WBH_EffectPoolManager>(FindObjectsInactive.Exclude)
            ?.GetComponent<WBH_EffectSpawner>();

        sharedProjectileSpawner ??=FindFirstObjectByType<WBH_ProjectileSpawner>(FindObjectsInactive.Exclude);

        if (sharedEffectSpawner == null)
            return false;

        GetComponent<WBH_EnemyStatusEffectController>()?.Initialize(sharedEffectSpawner);

        indicatorSpawner?.Initialize(sharedEffectSpawner);

        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDiagnostics()
    {
        ServerDeathCount = 0;
        ServerRewardCount = 0;
        ServerDropCount = 0;
        LocalDeathPresentationCount = 0;
    }

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public uint TargetNetId => targetNetId;
    public uint LastAttackerNetId => lastAttackerNetId;
    public uint StateChangeNumber => stateChangeNumber;
    public float LastDamage => lastDamage;
    public bool LastDamageCritical => lastDamageCritical;
    public uint ReceivedDamagePresentationCount => receivedDamagePresentationCount;
    public uint AttackStartCount => attackStartCount;
    public uint HitCount => hitCount;
    public uint KillRewardCount => killRewardCount;
    public uint DropSpawnCount => dropSpawnCount;
    public uint DestructionPresentationCount => destructionPresentationCount;
    public WBH_EnemyInfo EnemyInfo => enemyInfo;
    public bool UsesAnimatorOnlyDeathPresentation => useAnimatorOnlyDeathPresentation;
    public NetworkEnemyProjectile BossMissilePrefab => bossMissilePrefab;
    public MirrorAct1BossPhase BossPhase => bossPhase;
    public int BossPhaseVisualSeed => bossPhaseVisualSeed;
    public uint BossMissileVolleyCount => bossMissileVolleyCount;
    public uint BossMissileLaunchCount => bossMissileLaunchCount;
    public uint BossBarrageCount => bossBarrageCount;
    public uint BossBurstCount => bossBurstCount;
    public uint BossBulletLaunchCount => bossBulletLaunchCount;
    public uint BossDashCount => bossDashCount;
    public uint BossJumpCount => bossJumpCount;
    public bool IsAttackPending => attackPending;

    [Server]
    public void ServerSetEnemyInfo(WBH_EnemyInfo info)
    {
        if (netId != 0)
        {
            Debug.LogError("[NetworkEnemyAuthority] 적 데이터는 NetworkServer.Spawn 전에 설정해야 합니다.", this);
            return;
        }

        enemyInfo = info?.Clone();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureOriginalDriversForNetwork();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
        destroyDelay = Mathf.Max(0.05f, destroyDelay);
    }
#endif

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResolveReferences();
        ConfigureOriginalDriversForNetwork();

        if (enemyInfo == null || controller == null || status == null || movement == null || networkPattern == null)
        {
            Debug.LogError("[NetworkEnemyAuthority] 적 데이터 또는 필수 컴포넌트가 비어 있습니다.", this);
            NetworkServer.Destroy(gameObject);
            return;
        }

        // 원격 표시용 프리팹에서 꺼진 Agent도 서버의 이동 초기화 전에는 준비되어야 한다.
        if (agent != null)
        {
            agent.enabled = true;
            if (!agent.isOnNavMesh &&
                NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, agent.areaMask))
                agent.Warp(hit.position);
        }

        ResolveSharedSpawners();
        controller.Initialize(enemyInfo, null,sharedEffectSpawner, sharedProjectileSpawner, YJ_SfxPlayer.Instance); // @!@
        ConfigureAttackAnimation();
        //InitializeLocalEffectSpawner(); @!@
        // Gameplay effects do not require a visual spawner on the server.
        originalStatusEffectsReady = GetComponent<WBH_EnemyStatusEffectController>() != null;

        status.OnHpChanged += HandleHealthChanged;
        status.OnDamaged += HandleDamaged;
        status.OnDead += HandleDead;
        GetComponent<WBH_EnemyStatusEffectController>().OnBurnResponse += HandleBurnResponse;
        serverDamageSubscribed = true;

        currentHealth = status.CurrentHp;
        maxHealth = status.MaxHealth;
        effectPlaybackSpeed = status.AttackSpeed;
        isDead = false;
        deathHandled = false;
        stateChangeNumber++;

        networkPattern.InitializeServer(this);
        {
            GetComponent<WBH_EnemyEffect>().CueRequested += ForwardPatternCue;
            if (originalView != null) originalView.SelfDestructFlashRequested += ForwardPatternFlash;
            if (indicatorSpawner != null)
            {
                indicatorSpawner.CircleRequested += ForwardPatternCircle;
                indicatorSpawner.RectRequested += ForwardPatternRect;
                indicatorSpawner.ConeRequested += ForwardPatternCone;
            }
        }
    }

    public bool IsServerDamageHandlingActive => netIdentity != null && netIdentity.isServer && serverDamageSubscribed;
    private bool serverDamageSubscribed;

    private static readonly System.Reflection.FieldInfo ControllerInfoField =
        typeof(WBH_EnemyController).GetField(
            "info", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    public override void OnStartClient()
    {
        base.OnStartClient();
        ResolveReferences();
        //InitializeLocalEffectSpawner(); @!@
        ResolveSharedSpawners(); // @!@
        GetComponent<WBH_EnemyEffect>()?.Initialize(sharedEffectSpawner, YJ_SfxPlayer.Instance);
        if (isServer)
            return;

        if (enemyInfo != null)
        {
            // 원격 Client는 controller.Initialize를 거치지 않아 Info가 비어 미니맵 등 표시 코드가 적을 건너뛴다.
            // AI·전투 초기화 없이 표시용 Info만 BH 원본을 수정하지 않고 연결한다.
            if (controller != null) ControllerInfoField?.SetValue(controller, enemyInfo);
            // 기존 효과가 읽는 표시용 스탯도 초기화한다. 피해·AI 권한은 서버에 유지한다.
            status?.Initialize(enemyInfo);
            OnEffectPlaybackSpeedChanged(effectPlaybackSpeed, effectPlaybackSpeed);
            GetComponent<WBH_EnemyGradeVisual>()?.ApplyGrade(enemyInfo.enemyGrade);
        }

        ConfigureOriginalDriversForNetwork();
        if (agent != null)
            agent.enabled = false;
        if (networkPattern != null)
            networkPattern.enabled = false;
        ApplyStatusVisualMask(statusVisualMask);
    }

    private void LateUpdate()
    {
        if (originalView != null && !originalView.enabled) originalView.TickExternalFlash(Time.deltaTime);
        if (isClient && !isServer) UpdateRemoteAct2TransitionEffect();
        if (!isServer) return;
        if (status != null) effectPlaybackSpeed = status.AttackSpeed;
        statusEffects ??= GetComponent<WBH_EnemyStatusEffectController>();
        uint mask = 0;
        if (!isDead && statusEffects != null)
            foreach (WBH_StatusEffectType type in StatusTypes)
                if (type != WBH_StatusEffectType.None && statusEffects.HasStatusEffect(type))
                    mask |= 1u << (int)type;
        statusVisualMask = mask;
    }

    private void OnStatusVisualMaskChanged(uint _, uint value)
    {
        if (isClient && !isServer) ApplyStatusVisualMask(value);
    }

    private void ApplyStatusVisualMask(uint mask)
    {
        statusEffects ??= GetComponent<WBH_EnemyStatusEffectController>();
        if (statusEffects == null || enemyInfo == null) return;
        foreach (WBH_StatusEffectType type in StatusTypes)
        {
            if (type == WBH_StatusEffectType.None) continue;
            if ((mask & (1u << (int)type)) != 0)
                statusEffects.PlayStatusEffect(type, enemyInfo.enemyGrade);
            else statusEffects.StopStatusEffect(type);
        }
    }

    public override void OnStopClient()
    {
        StopRemoteAct2TransitionEffect();
        if (!isServer) ApplyStatusVisualMask(0);
        base.OnStopClient();
    }

    public override void OnStopServer()
    {
        FinishServerAttack();
        networkPattern?.StopServer();
        GetComponent<WBH_EnemyEffect>().CueRequested -= ForwardPatternCue;
        if (originalView != null) originalView.SelfDestructFlashRequested -= ForwardPatternFlash;
        if (indicatorSpawner != null)
        {
            indicatorSpawner.CircleRequested -= ForwardPatternCircle;
            indicatorSpawner.RectRequested -= ForwardPatternRect;
            indicatorSpawner.ConeRequested -= ForwardPatternCone;
        }
        serverDamageSubscribed = false;
        var statusEffects = GetComponent<WBH_EnemyStatusEffectController>();
        if (statusEffects != null)
            statusEffects.OnBurnResponse -= HandleBurnResponse;
        if (status != null)
        {
            status.OnHpChanged -= HandleHealthChanged;
            status.OnDamaged -= HandleDamaged;
            status.OnDead -= HandleDead;
        }

        base.OnStopServer();
    }

    private void ForwardPatternCue(WBH_EnemyEffectCue cue, bool world, Vector3 position, Quaternion rotation, Vector3 scale)
        => RpcPatternCue(cue, world, position, rotation, scale);
    private void ForwardPatternFlash(bool visible, float duration) => RpcPatternFlash(visible, duration);
    private void ForwardPatternCircle(Vector3 position, float radius, float duration, bool grow)
        => RpcShowBossCircleIndicator(position, radius, duration, grow);
    private void ForwardPatternRect(Vector3 origin, Vector3 forward, float width, float length, float duration, bool grow)
        => RpcPatternRect(origin, forward, width, length, duration, grow);
    private void ForwardPatternCone(Vector3 origin, Vector3 forward, float radius, float angle, float duration, bool grow)
        => RpcPatternCone(origin, forward, radius, angle, duration, grow);

    [ClientRpc]
    private void RpcPatternCone(Vector3 origin, Vector3 forward, float radius, float angle, float duration, bool grow)
    {
        if (!isServer) indicatorSpawner?.ShowCone(origin, forward, radius, angle, duration, grow);
    }

    [ClientRpc]
    private void RpcPatternCue(WBH_EnemyEffectCue cue, bool world, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (isServer) return;
        WBH_EnemyEffect effects = GetComponent<WBH_EnemyEffect>();
        if (world) effects.PlayWorldCue(cue, position, rotation, scale);
        else effects.PlayCue(cue, scale);
    }

    [ClientRpc]
    private void RpcPatternFlash(bool visible, float duration)
    {
        if (isServer || originalView == null) return;
        if (duration > 0f) originalView.SetSelfDestructFlash(visible, duration);
        else originalView.SetSelfDestructFlash(visible);
    }

    private void OnEffectPlaybackSpeedChanged(float _, float value)
    {
        if (!isServer && enemyInfo != null)
            status?.MultiplyAttackSpeed(value / Mathf.Max(0.01f, enemyInfo.attackSpeed));
    }

    // Act1의 폭발·착지는 애니메이션 이벤트가 아닌 실제 서버 충돌 시점에 재생한다.
    // RPC가 Host를 포함한 각 클라이언트에서 한 번 실행하므로 로컬 선재생을 하지 않는다.
    [Server]
    public void ServerPlayBossImpactCue(WBH_EnemyEffectCue cue, Vector3 position, Quaternion rotation)
    {
        RpcBossImpactCue(cue, position, rotation);
    }

    [ClientRpc]
    private void RpcBossImpactCue(WBH_EnemyEffectCue cue, Vector3 position, Quaternion rotation)
    {
        var effects = GetComponent<WBH_EnemyEffect>();
        // 착지는 싱글과 같은 AttachOnce 앵커를 써서 화면에 표시된 보스 위치·크기를 보존한다.
        if (cue == WBH_EnemyEffectCue.Boss_Act1_JumpAttack)
            effects?.PlayEffect(cue, Vector3.one);
        else
            effects?.PlayWorldCue(cue, position, rotation);
    }

    [ClientRpc]
    private void RpcPatternRect(Vector3 origin, Vector3 forward, float width, float length, float duration, bool grow)
    {
        if (!isServer) indicatorSpawner?.ShowRect(origin, forward, width, length, duration, grow);
    }

    /// <summary>정식 Elite 애니메이터의 기존 SkillID/Skill 파라미터를 같은 서버 요청으로 전달합니다.</summary>
    [Server]
    public void ServerPlayPatternSkill(int skillId)
    {
        if (animator == null || animator.runtimeAnimatorController == null || networkAnimator == null) return;
        animator.SetInteger("SkillID", skillId);
        networkAnimator.SetTrigger("Skill");
    }

    private void Update()
    {
        if (!isServer || !attackPending) return;
        // 타격은 원본 AnimationEvent만 처리한다. 중단/종료 누락에서는 피해 없이 잠금만 해제한다.
        bool inAttack = IsPlayingAttackAnimation();
        attackAnimationEntered |= inAttack;
        if (isDead || animator == null || !animator.enabled ||
            (attackAnimationEntered && !inAttack) || NetworkTime.time >= attackDeadline)
            FinishServerAttack();
    }

    private void ConfigureAttackAnimation()
    {
        attackClips.Clear();
        attackAnimationTimeout = 1f;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        // 화면이 없는 서버에서도 싱글과 같은 타격/종료 이벤트가 실행되어야 한다.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            foreach (AnimationEvent evt in clip.events)
                if (evt.functionName == nameof(WBH_EnemyAnimation.OnAttackEvent) ||
                    evt.functionName == nameof(WBH_EnemyAnimation.OnAttackEndEvent))
                {
                    if (attackClips.Add(clip)) attackAnimationTimeout += clip.length * 2f;
                    break;
                }
    }

    private bool IsPlayingAttackAnimation()
    {
        if (animator == null || !animator.enabled || animator.runtimeAnimatorController == null) return false;
        animator.GetCurrentAnimatorClipInfo(0, attackClipInfo);
        foreach (AnimatorClipInfo clip in attackClipInfo)
            if (attackClips.Contains(clip.clip)) return true;
        if (!animator.IsInTransition(0)) return false;
        animator.GetNextAnimatorClipInfo(0, attackClipInfo);
        foreach (AnimatorClipInfo clip in attackClipInfo)
            if (attackClips.Contains(clip.clip)) return true;
        return false;
    }

    private void FinishServerAttack()
    {
        attackVersion++;
        attackPending = false;
        pendingAttackTarget = null;
    }

    private void OnDisable()
    {
        FinishServerAttack();
        if (isServer) networkPattern?.StopServer();
        StopRemoteAct2TransitionEffect();
    }

    private void UpdateRemoteAct2TransitionEffect()
    {
        bool active = enemyInfo?.patternID == 102 && !isDead && animator != null &&
            animator.runtimeAnimatorController != null && animator.GetBool(PhaseTransitionHash);
        if (!active)
        {
            StopRemoteAct2TransitionEffect();
            return;
        }
        // Host의 효과는 원본 Act2 패턴이 소유한다. 동기화된 Animator bool로 원격/늦은 참가만 보완한다.
        if (remoteAct2TransitionEffect != null || originalPattern?.act2TransitionEffect == null) return;
        if (sharedEffectSpawner == null) ResolveSharedSpawners();
        if (sharedEffectSpawner != null)
            remoteAct2TransitionEffect = sharedEffectSpawner.SpawnPersistentEffect(originalPattern.act2TransitionEffect, transform);
    }

    private void StopRemoteAct2TransitionEffect()
    {
        if (remoteAct2TransitionEffect != null) remoteAct2TransitionEffect.StopEffect();
        remoteAct2TransitionEffect = null;
    }

    [Server]
    public void ServerSetTarget(PlayerContext target)
    {
        uint nextNetId = GetNetId(target);
        if (targetNetId == nextNetId)
            return;

        targetNetId = nextNetId;
        stateChangeNumber++;
    }

    public void RegisterClientPrefabs()
    {
        if (!NetworkClient.active || bossMissilePrefab == null ||
            !bossMissilePrefab.TryGetComponent(out NetworkIdentity identity) ||
            identity.assetId == 0 || NetworkClient.GetPrefab(identity.assetId, out _))
        {
            return;
        }

        NetworkClient.RegisterPrefab(bossMissilePrefab.gameObject);
    }

    [Server]
    public void ServerSetBossPhase(MirrorAct1BossPhase phase)
    {
        if (bossPhase == phase)
            return;

        bossPhase = phase;
        if (phase == MirrorAct1BossPhase.TransitionArmor && bossPhaseVisualSeed == 0)
            bossPhaseVisualSeed = unchecked((int)(netId * 397u + stateChangeNumber + 1u));
        stateChangeNumber++;
    }

    [Server]
    public void ServerRecordBossMissileVolley()
    {
        bossMissileVolleyCount++;
        stateChangeNumber++;
    }

    [Server]
    public void ServerRecordBossBarrage()
    {
        bossBarrageCount++;
        stateChangeNumber++;
    }

    [Server]
    public void ServerRecordBossBurst()
    {
        bossBurstCount++;
        stateChangeNumber++;
    }

    [Server]
    public void ServerRecordBossDash()
    {
        bossDashCount++;
        stateChangeNumber++;
    }

    [Server]
    public void ServerRecordBossJump()
    {
        bossJumpCount++;
        stateChangeNumber++;
    }

    [Server]
    public bool ServerLaunchBossMissile(
        Vector3 impactPoint,
        float flightDuration,
        float explosionRadius)
    {
        if (isDead || bossMissilePrefab == null || networkPattern == null)
            return false;

        Transform grenadePoint = networkPattern.GrenadePoint;
        Vector3 launchDirection = impactPoint - grenadePoint.position;
        Quaternion launchRotation = launchDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(launchDirection.normalized)
            : transform.rotation;
        NetworkEnemyProjectile projectile = Instantiate(
            bossMissilePrefab,
            grenadePoint.position,
            launchRotation);
        projectile.InitializeMissileServer(this, impactPoint, flightDuration, explosionRadius);
        NetworkServer.Spawn(projectile.gameObject);
        bossMissileLaunchCount++;
        stateChangeNumber++;
        return true;
    }

    [Server]
    public bool ServerLaunchBossProjectile(Vector3 direction, float maxDistance)
    {
        if (isDead || projectilePrefab == null || networkPattern == null)
            return false;

        if (!SpawnStraightProjectile(direction, maxDistance))
            return false;

        bossBulletLaunchCount++;
        stateChangeNumber++;
        return true;
    }

    [Server]
    public int ServerDamagePlayersInRadius(
        Vector3 center,
        float radius,
        HashSet<PlayerContext> alreadyHit = null)
    {
        if (isDead)
            return 0;

        int damagedCount = 0;
        meleeTargets.Clear();
        Collider[] hits = Physics.OverlapSphere(
            center,
            Mathf.Max(0.01f, radius),
            playerLayer,
            QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            PlayerContext target = hit.GetComponentInParent<PlayerContext>();
            if (!IsAliveTarget(target) || !meleeTargets.Add(target) ||
                (alreadyHit != null && !alreadyHit.Add(target)))
            {
                continue;
            }

            if (ServerDamagePlayer(target))
                damagedCount++;
        }

        return damagedCount;
    }

    [Server]
    public void ServerShowBossCircleIndicator(
        Vector3 position,
        float radius,
        float duration,
        bool growOverTime)
    {
        if (NetworkClient.active)
            ShowBossCircleIndicatorLocal(position, radius, duration, growOverTime);
        RpcShowBossCircleIndicator(position, radius, duration, growOverTime);
    }

    [Server]
    public void ServerShowBossRectIndicator(
        Vector3 origin,
        Vector3 forward,
        float width,
        float length,
        float duration)
    {
        if (NetworkClient.active)
            ShowBossRectIndicatorLocal(origin, forward, width, length, duration);
        RpcShowBossRectIndicator(origin, forward, width, length, duration);
    }

    [Server]
    public void ServerPlayBossSkill(int skillId)
    {
        string stateName = skillId switch
        {
            1 => "Dash",
            2 => "Shoot",
            3 => "Burrage",
            4 => "Missile",
            5 => "PhaseMissile",
            7 => "WaitDash",
            _ => string.Empty,
        };
        if (string.IsNullOrEmpty(stateName))
            return;

        PlayBossStateLocal(stateName);
        RpcPlayBossState(stateName);
    }

    [Server]
    public void ServerPlayBossJumpAnimation()
    {
        PlayBossStateLocal("JumpAttack");
        RpcPlayBossState("JumpAttack");
    }

    [Server]
    public bool ServerTryBeginAttack(PlayerContext target)
    {
        if (isDead || attackPending || originalAnimation == null || !movement.CanControl ||
            !IsAliveTarget(target) || NetworkTime.time < nextAttackAt)
            return false;

        Vector3 offset = target.transform.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > Mathf.Pow(status.AttackRange + 0.5f, 2f))
            return false;

        if (offset.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(offset.normalized);

        float attackSpeed = Mathf.Max(0.01f, status.AttackSpeed);
        pendingAttackTarget = target;
        attackDeadline = NetworkTime.time + attackAnimationTimeout / Mathf.Max(0.01f, animator != null ? animator.speed : 1f);
        nextAttackAt = NetworkTime.time + enemyInfo.attackCoolTime / attackSpeed;
        attackPending = true;
        attackHitResolved = false;
        attackAnimationEntered = false;
        uint version = ++attackVersion;
        attackStartCount++;
        stateChangeNumber++;
        // 무애니메이션 원거리는 이 호출 안에서 즉시 타격·종료한다. 근접은 공유 클립 이벤트를 따른다.
        originalAnimation.PlayAttack(() =>
        {
            if (!isServer || !attackPending || attackVersion != version || attackHitResolved) return;
            attackHitResolved = true;
            ResolvePendingAttack();
        }, () =>
        {
            if (isServer && attackPending && attackVersion == version) FinishServerAttack();
        });
        if (attackPending) TriggerNetworkAnimation("Attack");
        return true;
    }

    [Server]
    public bool ServerDamagePlayer(PlayerContext target)
    {
        if (isDead || !IsAliveTarget(target) || target.Controller == null)
            return false;

        float before = target.Health.CurrentHealth;
        float shieldBefore = target.GetComponent<PlayerArmorEffectProvider>()?.ShieldAmount ?? 0f;
        WBH_CombatManager.ProcessDamage(new WBH_DamageRequest(
            controller,
            target.Controller,
            WBH_AttackType.Normal,
            ElementType.None,
            1f));

        float shieldAfter = target.GetComponent<PlayerArmorEffectProvider>()?.ShieldAmount ?? 0f;
        if (target.Health.CurrentHealth >= before && shieldAfter >= shieldBefore)
            return false;

        hitCount++;
        stateChangeNumber++;
        return true;
    }

    /// <summary>
    /// 원본 상태 효과를 서버에서 적용한다. 시각 효과 풀이 없어도 게임 규칙과 만료 처리는 유지한다.
    /// </summary>
    [Server]
    public bool ServerTryApplyStatusEffect(WBH_StatusEffectData data)
    {
        if (isDead || !originalStatusEffectsReady || controller == null)
            return false;

        var effects = GetComponent<WBH_EnemyStatusEffectController>();
        if (effects == null)
            return false;
        bool canApply = effects.CanApplyStatusEffect(data);
        controller.AddStatusEffect(data);
        return canApply && effects.HasStatusEffect(data.Type);
    }

    /// <summary>서버가 확정한 반응만 관찰자에게 보냅니다. Host도 같은 RPC로 한 번 표시합니다.</summary>
    private void HandleBurnResponse(bool immune)
    {
        if (netId != 0 && NetworkServer.spawned.ContainsKey(netId))
            RpcShowBurnResponse(immune, transform.position);
    }

    /// <summary>화상 상태의 저항·면역을 표시하며 직접 화염 피해 숫자는 그대로 둡니다.</summary>
    [ClientRpc(channel = Channels.Reliable)]
    private void RpcShowBurnResponse(bool immune, Vector3 enemyPosition)
    {
        if (combatView == null)
            combatView = GetComponent<NetworkEnemyCombatView>();
        combatView?.ShowBurnResponse(immune, enemyPosition);
    }

    [Server]
    private void ResolvePendingAttack()
    {
        PlayerContext target = pendingAttackTarget;
        pendingAttackTarget = null;

        if (isDead || !IsAliveTarget(target))
            return;

        if (enemyInfo.enemyAttackType == EnemyAttackType.Ranged)
            SpawnProjectile(target);
        else
            ResolveMeleeAttack();
    }

    [Server]
    private void ResolveMeleeAttack()
    {
        meleeTargets.Clear();
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            status.AttackRange,
            playerLayer,
            QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            PlayerContext target = hit.GetComponentInParent<PlayerContext>();
            if (!IsAliveTarget(target) || !meleeTargets.Add(target))
                continue;

            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f ||
                Vector3.Angle(transform.forward, direction.normalized) > MeleeAngle * 0.5f)
            {
                continue;
            }

            ServerDamagePlayer(target);
        }
    }

    [Server]
    private void SpawnProjectile(PlayerContext target)
    {
        if (projectilePrefab == null || networkPattern == null)
        {
            Debug.LogError("[NetworkEnemyAuthority] 원거리 적 투사체 Prefab이 비어 있습니다.", this);
            return;
        }

        Transform firePoint = networkPattern.FirePoint;
        Vector3 targetPoint = target.transform.position + Vector3.up;
        Vector3 direction = (targetPoint - firePoint.position).normalized;
        if (SpawnStraightProjectile(direction, status.AttackRange))
            GetComponent<WBH_EnemyEffect>().PlayCue(WBH_EnemyEffectCue.Normal_Range_01_Attack, transform.localScale);
    }

    [Server]
    private bool SpawnStraightProjectile(Vector3 direction, float maxDistance)
    {
        if (projectilePrefab == null || networkPattern == null || direction.sqrMagnitude < 0.001f)
            return false;

        direction.Normalize();
        Transform firePoint = networkPattern.FirePoint;
        NetworkEnemyProjectile projectile = Instantiate(
            projectilePrefab,
            firePoint.position,
            Quaternion.LookRotation(direction));

        projectile.InitializeServer(this, direction, status.ProjectileSpeed, maxDistance);
        NetworkServer.Spawn(projectile.gameObject);
        return true;
    }

    [Server]
    private void HandleHealthChanged(float current, float maximum)
    {
        currentHealth = current;
        maxHealth = maximum;
        stateChangeNumber++;
    }

    [Server]
    private void HandleDamaged(WBH_DamageResult result)
    {
        lastDamageWasDot = result.DamageCause == DamageCause.DoT;
        lastDamage = result.FinalDamage;
        lastDamageCritical = result.IsCritical;
        receivedDamagePresentationCount++;
        Component attackerComponent = result.Attacker as Component;
        PlayerContext attacker = attackerComponent != null
            ? attackerComponent.GetComponentInParent<PlayerContext>()
            : null;

        // 실제 Spawn된 적만 전송한다. Editor의 미Spawn 로직 검사는 그대로 실행한다.
        if (netIdentity != null && netIdentity.netId != 0 &&
            NetworkServer.spawned.TryGetValue(netIdentity.netId, out NetworkIdentity spawnedIdentity) &&
            spawnedIdentity == netIdentity)
        {
            RpcShowDamage(result.FinalDamage, result.IsCritical,
                result.ElementType, transform.position, GetNetId(attacker), ReferenceEquals(result.Attacker, controller));
            if (attacker != null && result.EffectData != null && result.EffectData.hitEffectPrefab != null)
            {
                var effects = attacker.GetComponent<WBH_PlayerEffect>();
                foreach (WBH_PlayerEffectCue cue in PlayerEffectCues)
                {
                    if (effects == null || !effects.TryGetEffectData(cue, out WBH_EffectData data) || data != result.EffectData) continue;
                    RpcShowHitEffect(GetNetId(attacker), cue, result.HitPosition ?? transform.position,
                        result.HitEffectDirection ?? Vector3.zero);
                    break;
                }
            }
        }

        if (attacker != null)
        {
            lastAttackerContext = attacker;
            lastAttackerNetId = GetNetId(attacker);
            lastAttackDirection = (transform.position - attacker.transform.position).normalized;
            // 원본 스킬·일반 공격 모두 실제 피해 수신 뒤 공격자 자신의 장비 효과를 발동한다.
            if (!lastDamageWasDot)
            {
                attacker.ItemTriggers?.FireDamageDealt(result, controller);
                attacker.GetComponent<FighterSkillAuthority>()?.ServerRecordSkillHit(result);
            }
        }
        else
        {
            // 점화자가 사라진 화상 처치를 직전 공격자에게 잘못 넘기지 않습니다.
            if (lastDamageWasDot)
            {
                lastAttackerContext = null;
                lastAttackerNetId = 0;
            }
            lastAttackDirection = -transform.forward;
        }

        lastImpactPoint = result.HitPosition ?? transform.position;
        if (result.HitEffectDirection.HasValue && result.HitEffectDirection.Value.sqrMagnitude > 0.0001f)
            lastAttackDirection = -result.HitEffectDirection.Value.normalized;
        stateChangeNumber++;
        if (!status.IsDead)
            TriggerNetworkAnimation("Hit");
    }

    [ClientRpc(channel = Channels.Reliable)]
    private void RpcShowHitEffect(uint attackerId, WBH_PlayerEffectCue cue, Vector3 point, Vector3 direction)
    {
        // Host는 WBH_EnemyController.TakeDamage에서 같은 이펙트를 이미 재생한다.
        if (isServer || !NetworkClient.spawned.TryGetValue(attackerId, out NetworkIdentity attacker)) return;
        var effects = attacker.GetComponent<WBH_PlayerEffect>();
        if (effects == null || !effects.TryGetEffectData(cue, out WBH_EffectData data) || data == null) return;
        if (sharedEffectSpawner == null) ResolveSharedSpawners();
        Quaternion rotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
        sharedEffectSpawner?.SpawnHitEffect(data, point, rotation);
    }

    [ClientRpc(channel = Channels.Reliable)]
    private void RpcShowDamage(float damage, bool critical,
        ElementType element, Vector3 enemyPosition, uint attackerId, bool selfAttack)
    {
        if (combatView == null)
            combatView = GetComponent<NetworkEnemyCombatView>();

        if (combatView != null)
        {
            combatView.ShowDamage(damage, critical, element, enemyPosition, attackerId, selfAttack);
            return;
        }

        if (!missingCombatViewReported)
        {
            missingCombatViewReported = true;
            Debug.LogWarning("[NetworkEnemyAuthority] 데미지 표시 View가 없습니다.", this);
        }
    }

    [Server]
    private void HandleDead()
    {
        if (deathHandled)
            return;

        deathHandled = true;
        ServerDeathCount++;
        if (NetworkManager.singleton is MirrorNetworkManager session)
        {
            session.ServerReportQuestKill(enemyInfo?.enemyId);
            session.ServerRecordDefeatedEnemy(lastAttackerContext);
        }
        isDead = true;
        statusVisualMask = 0;
        if (enemyInfo?.enemyAttackType == EnemyAttackType.Boss)
            bossPhase = MirrorAct1BossPhase.Dead;
        currentHealth = 0f;
        targetNetId = 0;
        FinishServerAttack();
        stateChangeNumber++;

        networkPattern?.StopServer();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            movement.SetControlEnable(false);

        SetCombatCollidersEnabled(false);
        TriggerNetworkAnimation("Die");
        GrantKillRewardOnce();
        PlayDeathPresentationOnce();
        StartCoroutine(DestroyAfterPresentation());
    }

    private static ItemDropTableSO cachedUniversalDropTable;
    private static NetworkWorldItem cachedWorldItemPrefab;
    private static readonly ItemDropRollService universalDropRollService = new();

    private static ItemDefinitionSO ResolveDropItemDefinition(EnemyGrade grade, PlayerContext rarityOwner)
    {
        ItemSystemController itemSystem = ItemSystemController.Instance ?? Object.FindFirstObjectByType<ItemSystemController>();
        if (cachedUniversalDropTable == null)
        {
            cachedUniversalDropTable = Resources.Load<ItemDropTableSO>("DataFiles/ItemData/3. GeneratedAssets/DropTableConfig/ItemDropTable");
        }

        // 로비에서 시작한 세션에는 ItemManager가 없을 수 있다. Instance를 읽어 빈
        // 영속 매니저를 만들지 않고, 싱글에서도 사용하는 생성 데이터베이스를 읽는다.
        var itemManager = Object.FindFirstObjectByType<Core.ItemManager>(FindObjectsInactive.Include);
        ItemDatabaseSO itemDatabase = itemManager != null ? itemManager.ItemDatabase : null;
        if (itemDatabase == null)
            itemDatabase = Resources.Load<ItemDatabaseSO>("DataFiles/ItemData/3. GeneratedAssets/DropTableConfig/AllItems");

        ItemDropTableSO table = itemSystem != null && itemSystem.itemDropTable != null
            ? itemSystem.itemDropTable : cachedUniversalDropTable;
        // NoDrop도 정상 결과다. 원본 확률을 보존하도록 처치당 한 번만 추첨한다.
        ItemDropRollResultData result = universalDropRollService.Roll(table, itemDatabase, grade, rarityOwner: rarityOwner);
        if (!result.HasDrop && result.Result != ItemDropRollResult.NoDrop)
            Debug.LogWarning($"[NetworkEnemyAuthority] {ItemDropMessageMapper.GetMessage(result)}");
        return result.HasDrop ? result.ItemDefinition : null;
    }

    [Server]
    private void GrantKillRewardOnce()
    {
        if (enemyInfo == null || killRewardCount != 0)
            return;

        PlayerContext rewardRecipient = lastAttackerContext;
        if (rewardRecipient == null && !lastDamageWasDot)
        {
            if (NetworkManager.singleton is MirrorNetworkManager session && session.ServerPlayerContexts.Count > 0)
            {
                foreach (PlayerContext candidate in session.ServerPlayerContexts)
                {
                    if (candidate != null)
                    {
                        rewardRecipient = candidate;
                        break;
                    }
                }
            }
            else
            {
                rewardRecipient = Object.FindFirstObjectByType<PlayerContext>();
            }
        }

        if (rewardRecipient != null)
        {
            rewardRecipient.ItemTriggers?.Fire(TriggerCondition.OnKill);
            EnemyKillReward.GrantReward(enemyInfo, rewardRecipient.Stats,
                amount => rewardRecipient.GetComponent<NetworkShopPlayerState>()?.ServerAddGold(amount));
        }

        killRewardCount++;
        ServerRewardCount++;

        ItemDefinitionSO definition = ResolveDropItemDefinition(enemyInfo.enemyGrade, rewardRecipient);
        if (definition == null)
            return;

        PlayerInventorySync inventorySync = rewardRecipient != null
            ? rewardRecipient.GetComponent<PlayerInventorySync>()
            : Object.FindFirstObjectByType<PlayerInventorySync>();

        NetworkWorldItem worldItemPrefab = inventorySync != null ? inventorySync.WorldItemPrefab : null;
        if (worldItemPrefab != null)
        {
            cachedWorldItemPrefab = worldItemPrefab;
        }
        else if (cachedWorldItemPrefab != null)
        {
            worldItemPrefab = cachedWorldItemPrefab;
        }
        else
        {
            worldItemPrefab = Resources.Load<NetworkWorldItem>("Prefabs/WorldItemCube");
        }

        ItemInstance item = ItemDataCreator.CreateItemData(definition);
        string snapshot = PlayerInventorySync.CreateSnapshotJson(new InventoryItem(item));
        if (worldItemPrefab != null &&
            NetworkWorldItemSpawnService.TrySpawnNew(
                worldItemPrefab,
                snapshot,
                transform.position,
                transform.position + Vector3.up * 0.5f,
                out _))
        {
            dropSpawnCount++;
            ServerDropCount++;
        }
    }

    [Server]
    private void PlayDeathPresentationOnce()
    {
        // 우병헌 Act 1 보스는 자체 사망 애니메이션을 보존한다. 모델과 맞지 않는 Artificer
        // 파괴 프리팹을 대신 재생하지 않고 NetworkAnimator가 동기화한 Die 뒤 서버가 제거한다.
        if (useAnimatorOnlyDeathPresentation)
            return;

        destructionPresentationCount++;

        // Host는 서버와 로컬 Client를 함께 가지므로 여기에서 한 번 직접 재생합니다.
        // 화면이 없는 전용 서버는 파편 풀을 실행하지 않고 ClientRpc만 발행합니다.
        if (NetworkClient.active)
        {
            PresentDeath(
                transform.position,
                transform.rotation,
                transform.lossyScale,
                lastImpactPoint,
                lastAttackDirection,
                lastDamage,
                maxHealth);
        }

        RpcPlayDeath(
            transform.position,
            transform.rotation,
            transform.lossyScale,
            lastImpactPoint,
            lastAttackDirection,
            lastDamage,
            maxHealth);
    }

    [ClientRpc]
    private void RpcPlayDeath(
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Vector3 impactPoint,
        Vector3 attackDirection,
        float killingDamage,
        float enemyMaxHealth)
    {
        if (isServer)
            return;

        ApplyStatusVisualMask(0);
        PresentDeath(position, rotation, scale, impactPoint, attackDirection, killingDamage, enemyMaxHealth);
    }

    private void PresentDeath(
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Vector3 impactPoint,
        Vector3 attackDirection,
        float killingDamage,
        float enemyMaxHealth)
    {
        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = scale;

        bool presentationStarted = destructionLink != null &&
                                   destructionLink.TryPlayDeath(
                                       impactPoint,
                                       attackDirection,
                                       killingDamage,
                                       enemyMaxHealth);
        if (presentationStarted)
        {
            LocalDeathPresentationCount++;
        }
        else
        {
            Debug.LogWarning(
                "[NetworkEnemyAuthority] 파괴 연출 풀 재생을 시작하지 못했습니다.",
                this);
        }

        SetCombatCollidersEnabled(false);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in renderers)
            targetRenderer.enabled = false;
    }

    [Server]
    private IEnumerator DestroyAfterPresentation()
    {
        yield return new WaitForSeconds(destroyDelay);
        if (gameObject != null)
            NetworkServer.Destroy(gameObject);
    }

    private bool IsAliveTarget(PlayerContext target)
    {
        return target != null &&
               target.gameObject.activeInHierarchy &&
               target.GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent != true &&
               target.RuntimeState?.IsDead != true &&
               target.Health != null &&
               target.Health.CurrentHealth > 0f;
    }

    private void SetCombatCollidersEnabled(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider targetCollider in colliders)
            targetCollider.enabled = enabled;
    }

    /// <summary>SW 수정: 원본 AI·전투·패턴·애니메이션 드라이버는 끄고, 공통 View만 외부 표시 모드로 켜 둔다.</summary>
    private void ConfigureOriginalDriversForNetwork()
    {
        if (controller != null)
            controller.enabled = false;
        if (originalCombat != null)
            originalCombat.enabled = false;
        if (originalPattern != null)
            originalPattern.enabled = false;
        if (originalAnimation != null)
            originalAnimation.enabled = false;
        if (originalView != null)
        {
            originalView.BindExternalPresentation(() => isClient);
            originalView.enabled = true;
        }
        if (TryGetComponent<WBHEnemyDestructionAdapter>(out var originalDestruction))
            originalDestruction.enabled = false;

        // 네트워크 Authority가 처치 트리거와 드롭을 한 번만 확정하므로,
        // WBH 로컬 사망 이벤트를 구독하는 기존 테스트 어댑터는 함께 실행하지 않는다.
        EnemyKillReward originalKillReward = GetComponent<EnemyKillReward>();
        if (originalKillReward != null)
            originalKillReward.enabled = false;

        WBHEnemyItemDropAdapter originalItemDropAdapter =
            GetComponent<WBHEnemyItemDropAdapter>();
        if (originalItemDropAdapter != null)
            originalItemDropAdapter.enabled = false;
    }

    /// <summary>
    /// 원본 적 애니메이션 클립의 발사 이벤트를 받아서 종료한다.
    /// Mirror 테스트의 실제 공격과 투사체 생성은 서버 Authority가 예약하므로 여기서는 아무 동작도 하지 않는다.
    /// </summary>
    public void AnimEventShoot()
    {
    }

    /// <summary>
    /// 원본 적 애니메이션 클립의 장전 시작 이벤트를 받아서 종료한다.
    /// </summary>
    public void AnimEventReloadStarted()
    {
    }

    /// <summary>
    /// 원본 적 애니메이션 클립의 장전 완료 이벤트를 받아서 종료한다.
    /// </summary>
    public void AnimEventReloadFinished()
    {
    }

    [ClientRpc]
    private void RpcShowBossCircleIndicator(
        Vector3 position,
        float radius,
        float duration,
        bool growOverTime)
    {
        if (isServer)
            return;

        ShowBossCircleIndicatorLocal(position, radius, duration, growOverTime);
    }

    [ClientRpc]
    private void RpcShowBossRectIndicator(
        Vector3 origin,
        Vector3 forward,
        float width,
        float length,
        float duration)
    {
        if (isServer)
            return;

        ShowBossRectIndicatorLocal(origin, forward, width, length, duration);
    }

    [ClientRpc]
    private void RpcPlayBossState(string stateName)
    {
        if (isServer)
            return;

        PlayBossStateLocal(stateName);
    }

    private void ShowBossCircleIndicatorLocal(
        Vector3 position,
        float radius,
        float duration,
        bool growOverTime)
    {
        InitializeLocalEffectSpawner();
        indicatorSpawner?.ShowCircle(position, radius, duration, growOverTime);
    }

    private void ShowBossRectIndicatorLocal(
        Vector3 origin,
        Vector3 forward,
        float width,
        float length,
        float duration)
    {
        InitializeLocalEffectSpawner();
        indicatorSpawner?.ShowRect(origin, forward, width, length, duration);
    }

    private void PlayBossStateLocal(string stateName)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.CrossFadeInFixedTime(stateName, 0.05f);
    }

    private void TriggerNetworkAnimation(string trigger)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            networkAnimator?.SetTrigger(trigger);
    }

    private void InitializeLocalEffectSpawner() // @!@ effectSpawner 가 인스펙터에서 effectPool 을 참조하게끔 변경했기에 불필요할 것으로 생각됩니다. 삭제 검토 요청드립니다.
    {
        if (localEffectSpawnerInitialized || originalEffectSpawner == null)
            return;

        WBH_EffectPoolManager effectPool =
            FindFirstObjectByType<WBH_EffectPoolManager>(FindObjectsInactive.Exclude);
        if (effectPool == null)
            return;

        //originalEffectSpawner.Initialize(effectPool); @!@
        localEffectSpawnerInitialized = true;
    }

    private void ResolveReferences()
    {
        controller ??= GetComponent<WBH_EnemyController>();
        status ??= GetComponent<WBH_EnemyStatus>();
        movement ??= GetComponent<WBH_EnemyMovement>();
        originalCombat ??= GetComponent<WBH_EnemyCombat>();
        originalPattern ??= GetComponent<WBH_EnemyPattern>();
        originalAnimation ??= GetComponent<WBH_EnemyAnimation>();
        originalView ??= GetComponent<WBH_EnemyView>();
        originalEffectSpawner ??= GetComponent<WBH_EffectSpawner>();
        indicatorSpawner ??= GetComponent<WBH_IndicatorSpawner>();
        networkPattern ??= GetComponent<NetworkEnemyPattern>();
        agent ??= GetComponent<NavMeshAgent>();
        networkAnimator ??= GetComponent<NetworkAnimator>();
        animator ??= GetComponent<Animator>();
        if (networkAnimator != null && (animator == null || animator.runtimeAnimatorController == null))
            networkAnimator.enabled = false;
        destructionLink ??= GetComponent<EnemyDestructionLink>();
    }

    private static uint GetNetId(PlayerContext context)
    {
        return context != null && context.TryGetComponent(out NetworkIdentity identity)
            ? identity.netId
            : 0;
    }


}

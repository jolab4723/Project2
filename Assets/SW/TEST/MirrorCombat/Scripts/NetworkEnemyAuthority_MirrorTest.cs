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
[RequireComponent(typeof(NetworkIdentity), typeof(NetworkAnimator), typeof(WBH_EnemyController))]
[RequireComponent(typeof(WBH_EnemyEffect))]
public sealed class NetworkEnemyAuthority_MirrorTest : NetworkBehaviour
{
    private const float MeleeAngle = 120f;

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
    [SerializeField, Min(0.01f)] private float attackImpactDelay = 0.45f;
    [SerializeField, Min(0.05f)] private float destroyDelay = 0.35f;
    [SerializeField] private bool useAnimatorOnlyDeathPresentation;
    [SerializeField] private LayerMask playerLayer = 1 << 15;

    [Header("서버 투사체")]
    [SerializeField] private NetworkEnemyProjectile_MirrorTest projectilePrefab;
    [SerializeField] private NetworkEnemyProjectile_MirrorTest bossMissilePrefab;

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
    [SerializeField] private WBH_EnemyPattern_MirrorTest networkPattern;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private NetworkAnimator networkAnimator;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyDestructionLink destructionLink;

    [SyncVar] private float currentHealth;
    [SyncVar] private float maxHealth;
    [SyncVar] private bool isDead;
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
    private double attackImpactAt;
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
    private NetworkEnemyCombatView_MirrorTest combatView;
    private bool missingCombatViewReported;

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
    public NetworkEnemyProjectile_MirrorTest BossMissilePrefab => bossMissilePrefab;
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
            Debug.LogError("[NetworkEnemyAuthority_MirrorTest] 적 데이터는 NetworkServer.Spawn 전에 설정해야 합니다.", this);
            return;
        }

        enemyInfo = info?.Clone();
    }

    private void Awake()
    {
        ResolveReferences();
        DisableOriginalRuntimeDrivers();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
        attackImpactDelay = Mathf.Max(0.01f, attackImpactDelay);
        destroyDelay = Mathf.Max(0.05f, destroyDelay);
    }
#endif

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResolveReferences();
        DisableOriginalRuntimeDrivers();

        if (enemyInfo == null || controller == null || status == null || movement == null || networkPattern == null)
        {
            Debug.LogError("[NetworkEnemyAuthority_MirrorTest] 적 데이터 또는 필수 컴포넌트가 비어 있습니다.", this);
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
        isDead = false;
        deathHandled = false;
        stateChangeNumber++;

        networkPattern.InitializeServer(this);
    }

    public bool IsServerDamageHandlingActive => netIdentity != null && netIdentity.isServer && serverDamageSubscribed;
    private bool serverDamageSubscribed;

    public override void OnStartClient()
    {
        base.OnStartClient();
        ResolveReferences();
        //InitializeLocalEffectSpawner(); @!@
        ResolveSharedSpawners(); // @!@
        if (isServer)
            return;

        DisableOriginalRuntimeDrivers();
        if (agent != null)
            agent.enabled = false;
        if (networkPattern != null)
            networkPattern.enabled = false;
    }

    public override void OnStopServer()
    {
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

    private void Update()
    {
        if (!isServer || !attackPending || NetworkTime.time < attackImpactAt)
            return;

        attackPending = false;
        ResolvePendingAttack();
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
        NetworkEnemyProjectile_MirrorTest projectile = Instantiate(
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
        if (isDead || attackPending || !IsAliveTarget(target) || NetworkTime.time < nextAttackAt)
            return false;

        Vector3 offset = target.transform.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > Mathf.Pow(status.AttackRange + 0.5f, 2f))
            return false;

        if (offset.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(offset.normalized);

        float attackSpeed = Mathf.Max(0.01f, status.AttackSpeed);
        pendingAttackTarget = target;
        attackImpactAt = NetworkTime.time + attackImpactDelay / attackSpeed;
        nextAttackAt = NetworkTime.time + Mathf.Max(attackImpactDelay, enemyInfo.attackCoolTime) / attackSpeed;
        attackPending = true;
        attackStartCount++;
        stateChangeNumber++;
        networkAnimator?.SetTrigger("Attack");
        return true;
    }

    [Server]
    public bool ServerDamagePlayer(PlayerContext target)
    {
        if (isDead || !IsAliveTarget(target) || target.Controller == null)
            return false;

        float before = target.Health.CurrentHealth;
        float shieldBefore = target.GetComponent<PlayerArmorEffectProvider_MirrorTest>()?.ShieldAmount ?? 0f;
        WBH_CombatManager.ProcessDamage(new WBH_DamageRequest(
            controller,
            target.Controller,
            WBH_AttackType.Normal,
            ElementType.None,
            1f));

        float shieldAfter = target.GetComponent<PlayerArmorEffectProvider_MirrorTest>()?.ShieldAmount ?? 0f;
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
            combatView = GetComponent<NetworkEnemyCombatView_MirrorTest>();
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
            Debug.LogError("[NetworkEnemyAuthority_MirrorTest] 원거리 적 투사체 Prefab이 비어 있습니다.", this);
            return;
        }

        Transform firePoint = networkPattern.FirePoint;
        Vector3 targetPoint = target.transform.position + Vector3.up;
        Vector3 direction = (targetPoint - firePoint.position).normalized;
        SpawnStraightProjectile(direction, status.AttackRange);
    }

    [Server]
    private bool SpawnStraightProjectile(Vector3 direction, float maxDistance)
    {
        if (projectilePrefab == null || networkPattern == null || direction.sqrMagnitude < 0.001f)
            return false;

        direction.Normalize();
        Transform firePoint = networkPattern.FirePoint;
        NetworkEnemyProjectile_MirrorTest projectile = Instantiate(
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

        // 실제 Spawn된 적만 전송한다. Editor의 미Spawn 로직 검사는 그대로 실행한다.
        if (netIdentity != null && netIdentity.netId != 0 &&
            NetworkServer.spawned.TryGetValue(netIdentity.netId, out NetworkIdentity spawnedIdentity) &&
            spawnedIdentity == netIdentity)
        {
            RpcShowDamage(result.FinalDamage, result.IsCritical,
                result.ElementType, transform.position);
        }

        Component attackerComponent = result.Attacker as Component;
        PlayerContext attacker = attackerComponent != null
            ? attackerComponent.GetComponentInParent<PlayerContext>()
            : null;

        if (attacker != null)
        {
            lastAttackerContext = attacker;
            lastAttackerNetId = GetNetId(attacker);
            lastAttackDirection = (transform.position - attacker.transform.position).normalized;
            // 원본 스킬·일반 공격 모두 실제 피해 수신 뒤 공격자 자신의 장비 효과를 발동한다.
            if (!lastDamageWasDot)
            {
                attacker.ItemTriggers?.FireDamageDealt(result, controller);
                attacker.GetComponent<FighterSkillAuthority_MirrorTest>()?.ServerRecordSkillHit(result);
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

        lastImpactPoint = transform.position;
        stateChangeNumber++;
        if (!status.IsDead)
            networkAnimator?.SetTrigger("Hit");
    }

    [ClientRpc(channel = Channels.Reliable)]
    private void RpcShowDamage(float damage, bool critical,
        ElementType element, Vector3 enemyPosition)
    {
        if (combatView == null)
            combatView = GetComponent<NetworkEnemyCombatView_MirrorTest>();

        if (combatView != null)
        {
            combatView.ShowDamage(damage, critical, element, enemyPosition);
            return;
        }

        if (!missingCombatViewReported)
        {
            missingCombatViewReported = true;
            Debug.LogWarning("[NetworkEnemyAuthority_MirrorTest] 데미지 표시 View가 없습니다.", this);
        }
    }

    [Server]
    private void HandleDead()
    {
        if (deathHandled)
            return;

        deathHandled = true;
        ServerDeathCount++;
        if (NetworkManager.singleton is MirrorTestNetworkManager session)
            session.ServerReportQuestKill(enemyInfo?.enemyId);
        isDead = true;
        if (enemyInfo?.enemyAttackType == EnemyAttackType.Boss)
            bossPhase = MirrorAct1BossPhase.Dead;
        currentHealth = 0f;
        targetNetId = 0;
        attackPending = false;
        pendingAttackTarget = null;
        stateChangeNumber++;

        networkPattern?.StopServer();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            movement.SetControlEnable(false);

        SetCombatCollidersEnabled(false);
        networkAnimator?.SetTrigger("Die");
        GrantKillRewardOnce();
        PlayDeathPresentationOnce();
        StartCoroutine(DestroyAfterPresentation());
    }

    private static ItemDropTableSO cachedUniversalDropTable;
    private static NetworkWorldItem_MirrorTest cachedWorldItemPrefab;
    private static readonly ItemDropRollService universalDropRollService = new();

    private static ItemDefinitionSO ResolveDropItemDefinition(EnemyGrade grade)
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
        ItemDropRollResultData result = universalDropRollService.Roll(table, itemDatabase, grade);
        if (!result.HasDrop && result.Result != ItemDropRollResult.NoDrop)
            Debug.LogWarning($"[NetworkEnemyAuthority_MirrorTest] {ItemDropMessageMapper.GetMessage(result)}");
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
            if (NetworkManager.singleton is MirrorTestNetworkManager session && session.ServerPlayerContexts.Count > 0)
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
            rewardRecipient.Stats?.GainExp(enemyInfo.exp);
            rewardRecipient.GetComponent<NetworkShopPlayerState_MirrorTest>()?.ServerAddGold(enemyInfo.credit);
        }

        killRewardCount++;
        ServerRewardCount++;

        ItemDefinitionSO definition = ResolveDropItemDefinition(enemyInfo.enemyGrade);
        if (definition == null)
            return;

        PlayerInventorySync_MirrorTest inventorySync = rewardRecipient != null
            ? rewardRecipient.GetComponent<PlayerInventorySync_MirrorTest>()
            : Object.FindFirstObjectByType<PlayerInventorySync_MirrorTest>();

        NetworkWorldItem_MirrorTest worldItemPrefab = inventorySync != null ? inventorySync.WorldItemPrefab : null;
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
            worldItemPrefab = Resources.Load<NetworkWorldItem_MirrorTest>("Prefabs/WorldItemCube_MirrorTest");
        }

        ItemInstance item = ItemDataCreator.CreateItemData(definition);
        string snapshot = PlayerInventorySync_MirrorTest.CreateSnapshotJson(new InventoryItem(item));
        if (worldItemPrefab != null &&
            NetworkWorldItemSpawnService_MirrorTest.TrySpawnNew(
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
                "[NetworkEnemyAuthority_MirrorTest] 파괴 연출 풀 재생을 시작하지 못했습니다.",
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

    private void DisableOriginalRuntimeDrivers()
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
            originalView.enabled = false;

        // 네트워크 Authority가 처치 트리거와 드롭을 한 번만 확정하므로,
        // WBH 로컬 사망 이벤트를 구독하는 기존 테스트 어댑터는 함께 실행하지 않는다.
        EnemyKillReward originalKillReward = GetComponent<EnemyKillReward>();
        if (originalKillReward != null)
            originalKillReward.enabled = false;

        WBHEnemyItemDropAdapter originalItemDropAdapter =
            GetComponent<WBHEnemyItemDropAdapter>();
        if (originalItemDropAdapter != null)
            originalItemDropAdapter.enabled = false;

        WBHEnemyItemDropAdapter_MirrorTest mirrorTestItemDropAdapter =
            GetComponent<WBHEnemyItemDropAdapter_MirrorTest>();
        if (mirrorTestItemDropAdapter != null)
            mirrorTestItemDropAdapter.enabled = false;
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
        if (animator != null)
            animator.CrossFadeInFixedTime(stateName, 0.05f);
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
        networkPattern ??= GetComponent<WBH_EnemyPattern_MirrorTest>();
        agent ??= GetComponent<NavMeshAgent>();
        networkAnimator ??= GetComponent<NetworkAnimator>();
        animator ??= GetComponent<Animator>();
        destructionLink ??= GetComponent<EnemyDestructionLink>();
    }

    private static uint GetNetId(PlayerContext context)
    {
        return context != null && context.TryGetComponent(out NetworkIdentity identity)
            ? identity.netId
            : 0;
    }


}

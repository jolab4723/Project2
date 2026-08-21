using System.Collections;
using System.Collections.Generic;
using ItemSystem;
using Mirror;
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
    [SerializeField] private WBH_EnemyInfo enemyInfo;
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
    private bool localEffectSpawnerInitialized;

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
    public uint BossDashCount => bossDashCount;
    public uint BossJumpCount => bossJumpCount;
    public bool IsAttackPending => attackPending;

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

        controller.Initialize(enemyInfo, null);
        InitializeLocalEffectSpawner();
        originalStatusEffectsReady = localEffectSpawnerInitialized;

        status.OnHpChanged += HandleHealthChanged;
        status.OnDamaged += HandleDamaged;
        status.OnDead += HandleDead;

        currentHealth = status.CurrentHp;
        maxHealth = status.MaxHealth;
        isDead = false;
        deathHandled = false;
        stateChangeNumber++;

        if (agent != null && agent.enabled && !agent.isOnNavMesh &&
            NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, agent.areaMask))
        {
            agent.Warp(hit.position);
        }

        networkPattern.InitializeServer(this);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ResolveReferences();
        InitializeLocalEffectSpawner();
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
        WBH_CombatManager.ProcessDamage(new WBH_DamageRequest(
            controller,
            target.Controller,
            WBH_AttackType.Normal,
            ElementType.None,
            1f));

        if (target.Health.CurrentHealth >= before)
            return false;

        hitCount++;
        stateChangeNumber++;
        return true;
    }

    /// <summary>
    /// 원본 상태 효과의 게임 규칙은 유지하되, Act1 효과 풀이 준비된 서버에서만 적용한다.
    /// 테스트 씬의 효과 풀이 없을 때는 피해 판정을 취소하거나 예외를 내지 않고 상태 효과만 건너뛴다.
    /// </summary>
    [Server]
    public bool ServerTryApplyStatusEffect(WBH_StatusEffectData data)
    {
        if (isDead || !originalStatusEffectsReady || controller == null)
            return false;

        controller.AddStatusEffect(data);
        return true;
    }

    [Server]
    private void ResolvePendingAttack()
    {
        PlayerContext target = pendingAttackTarget;
        pendingAttackTarget = null;

        if (isDead || !IsAliveTarget(target))
            return;

        if (enemyInfo.enemyType == EnemyType.Ranged)
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
        NetworkEnemyProjectile_MirrorTest projectile = Instantiate(
            projectilePrefab,
            firePoint.position,
            Quaternion.LookRotation(direction));

        projectile.InitializeServer(this, direction, status.ProjectileSpeed, status.AttackRange);
        NetworkServer.Spawn(projectile.gameObject);
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
        lastDamage = result.FinalDamage;
        lastDamageCritical = result.IsCritical;
        receivedDamagePresentationCount++;
        Component attackerComponent = result.Attacker as Component;
        PlayerContext attacker = attackerComponent != null
            ? attackerComponent.GetComponentInParent<PlayerContext>()
            : null;

        if (attacker != null)
        {
            lastAttackerContext = attacker;
            lastAttackerNetId = GetNetId(attacker);
            lastAttackDirection = (transform.position - attacker.transform.position).normalized;
        }
        else
        {
            lastAttackDirection = -transform.forward;
        }

        lastImpactPoint = transform.position;
        stateChangeNumber++;
        if (!status.IsDead)
            networkAnimator?.SetTrigger("Hit");
    }

    [Server]
    private void HandleDead()
    {
        if (deathHandled)
            return;

        deathHandled = true;
        ServerDeathCount++;
        isDead = true;
        if (enemyInfo?.enemyType == EnemyType.Boss)
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

    [Server]
    private void GrantKillRewardOnce()
    {
        if (lastAttackerContext == null || enemyInfo == null)
            return;

        lastAttackerContext.ItemTriggers?.Fire(TriggerCondition.OnKill);
        lastAttackerContext.Stats?.GainExp(enemyInfo.exp);
        killRewardCount++;
        ServerRewardCount++;

        ItemSystemController itemSystem = ItemSystemController.Instance;
        ItemDefinitionSO definition = itemSystem != null
            ? itemSystem.GetRandomItemSO(enemyInfo.enemyGrade)
            : null;
        if (definition == null)
            return;

        PlayerInventorySync_MirrorTest inventorySync =
            lastAttackerContext.GetComponent<PlayerInventorySync_MirrorTest>();
        ItemInstance item = ItemDataCreator.CreateItemData(definition);
        string snapshot = PlayerInventorySync_MirrorTest.CreateSnapshotJson(new InventoryItem(item));
        if (inventorySync != null &&
            NetworkWorldItemSpawnService_MirrorTest.TrySpawnNew(
                inventorySync.WorldItemPrefab,
                snapshot,
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

    private void InitializeLocalEffectSpawner()
    {
        if (localEffectSpawnerInitialized || originalEffectSpawner == null)
            return;

        WBH_EffectPoolManager effectPool =
            FindFirstObjectByType<WBH_EffectPoolManager>(FindObjectsInactive.Exclude);
        if (effectPool == null)
            return;

        originalEffectSpawner.Initialize(effectPool);
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

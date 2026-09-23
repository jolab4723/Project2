using EnemySystem;
using UnityEngine;

[RequireComponent(typeof(WBH_EnemyMovement))]
[RequireComponent(typeof(WBH_EnemyCombat))]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyAnimation))]
public class WBH_EnemyPattern : MonoBehaviour
{
    [System.Serializable]
    [Tooltip("자폭 적 세팅")]
    public class SelfDestructSettings
    {
        [Min(0f)] public float startDelay = 1f; // 딜레이 이후 추격모드(반짝임 및 이동속도 증가) 돌입
        [Tooltip("자폭 시퀀스 시작 거리")]
        [Min(0.1f)] public float triggerDistance = 1.25f;
        
        [Min(0.1f)] public float fuseDuration = 2f;
        [Min(0.1f)] public float explosionRadius = 3f;
        [Min(0f)] public float damageMultiplier = 2f;

        [Tooltip("가속 시퀀스 시작 거리")]
        [Min(0.1f)] public float accelerationStartDistance = 8f;

        [Min(1f)] public float maxSpeedMultiplier = 2.2f;
        [Min(0.1f)] public float farBlinkInterval = 0.5f;
        [Min(0.1f)] public float nearBlinkInterval = 0.08f;
    }

    [System.Serializable]
    [Tooltip("히든 적 세팅")]
    public class HiddenSettings
    {
        [Min(1f)] public float lifeTime = 60f; // 생존시간
        [Min(1f)] public float damagedSpeedMultiplier = 1.5f; // 피격 시, 이속증가 배율
        [Min(0.1f)] public float damagedSpeedDuration = 3f; // 피격 시, 이속증가 시간
        [Min(0.05f)] public float repathInterval = 0.35f; // 경로 재탐색 간격
        [Min(1f)] public float fleeDistance = 8f; // 도망 시작 거리
        [Min(0.1f)] public float navMeshSampleRadius = 2f; 
        [Range(1,9)] public int candidateCount = 5; // 도망 경로 후보
        [Range(0f, 180f)] public float maxFleeAngle = 70f; // 도망 경로 탐색 각도
        [Min(0f)] public float initialDirectionWeight = 5f; // 방향 가중치
        public WBH_EffectData despawnEffect; // 역소환 이펙트
    }

    [SerializeField] private SelfDestructSettings explodeSettings = new SelfDestructSettings();
    [SerializeField] private HiddenSettings hiddenSettings = new HiddenSettings();

    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform grenadePoint; // 미사일, 유탄 등 판정 범위가 넓어 별도의 투사체 생성포인트가 필요할 때 사용. ex) act 01 보스

    public WBH_EnemyAnimation enemyAnimation; // pattern 에서의 참조를 위해 public
    private WBH_EnemyController controller;
    private WBH_EnemyMovement movement;
    private WBH_EnemyCombat combat;
    private WBH_EnemyStatus status;
    private WBH_IndicatorSpawner indicatorSpawner;
    private WBH_EnemyView view;
    private WBH_EnemyEffect enemyEffect;

    private WBH_EffectSpawner effectSpawner;

    private WBH_ProjectileSpawner projectileSpawner;

    private Transform target;

    private WBH_IEnemyPattern currentPattern;

    public float Distance { get; private set; } = 0;
    public bool isShowSkillRange;
    private float basicAttackMult = 1f;
    private float basicMeleeAttackAngle = 120; // % int 로 변경하면 최적화?
    protected float dashHitRadius = 3f;
    private float rangedTurnSpeed = 360f;
    private float facingDeadZone = 1f;
    private bool waitingForTarget;

    public WBH_EnemyMovement Movement => movement;
    public WBH_EnemyCombat Combat => combat;
    public WBH_EnemyStatus Status => status;
    public WBH_EnemyEffect EnemyEffect => enemyEffect;
    public WBH_EffectSpawner EffectSpawner => effectSpawner;
    public WBH_IndicatorSpawner IndicatorSpawner => indicatorSpawner;
    public float AttackRange => status.AttackRange;
    public Transform Target => target;
    public Transform FirePoint => firePoint;
    public Transform GrenadePoint => grenadePoint;
    public LayerMask PlayerLayer => playerLayer;

    public float DashHitRadius => dashHitRadius;

    public float HealthRatio => status.MaxHealth > 0f ? status.CurrentHp / status.MaxHealth : 1f;
    public SelfDestructSettings SelfDestructConfig => explodeSettings;
    public HiddenSettings HiddenConfig => hiddenSettings;
    public WBH_EnemyView EnemyView => view;
    public float CurrentMoveSpeed => status.MoveSpeed;

    private void Awake()
    {
        movement = GetComponent<WBH_EnemyMovement>();
        combat = GetComponent<WBH_EnemyCombat>();
        status = GetComponent<WBH_EnemyStatus>();
        enemyAnimation = GetComponent<WBH_EnemyAnimation>();
        enemyEffect = GetComponent<WBH_EnemyEffect>();
        effectSpawner = GetComponent<WBH_EffectSpawner>();
        projectileSpawner = GetComponent<WBH_ProjectileSpawner>();
        indicatorSpawner = GetComponent <WBH_IndicatorSpawner>();
        view = GetComponent<WBH_EnemyView>();
    }

    public virtual void Initialize(WBH_EnemyController controller, WBH_EffectSpawner effectSpawner, WBH_ProjectileSpawner projectileSpawner)
    {
        CleanCurrentPattern();

        this.controller = controller;
        this.effectSpawner = effectSpawner;
        this.projectileSpawner= projectileSpawner;

        CreatePattern();
    }

    protected virtual void Update()
    {
        if (status == null || controller == null)
            return;

        if(status.IsDead)
        {
            Die();
            return;
        }

        if (!EnsureTarget()) // 현재 타겟이 null 인지 체크 및 null 일 경우 가까운 다른 player 체크하여 타겟 재설정
            return;

        if (!movement.CanControl)
            return;

        TickPattern(Time.deltaTime);
    }

    /// <summary>SW 수정: 외부 권한 소유자가 지정한 대상만 사용하며 로컬 플레이어 검색을 실행하지 않습니다.</summary>
    public void TickWithTarget(Transform authoritativeTarget, float deltaTime)
    {
        SetTarget(authoritativeTarget);
        if (status == null || controller == null || status.IsDead || target == null || !movement.CanControl) return;
        TickPattern(deltaTime);
    }

    private void TickPattern(float deltaTime)
    {
        Distance = Vector3.Distance(transform.position, target.position);

        if(currentPattern != null)
        {
            currentPattern.Tick(deltaTime);
        }
        else
        {
            UpdateMove(Distance);
            UpdateFacing(Distance, deltaTime);
            UpdateAttack(Distance);
        }
    }

    private void OnDisable()
    {
        CleanCurrentPattern();
    }

    // patternID 혹은 EnemyType에 따라 고유패턴 실행 (normal, advanced 는 영향 X)
    private void CreatePattern()
    {
        if (controller.Info.enemyAttackType == EnemyAttackType.SelfDestruct)
        {
            currentPattern = new WBH_EnemySelfDestructPattern();
        }
        else if(controller.Info.enemyAttackType == EnemyAttackType.Hidden)
        {
            currentPattern = new WBH_EnemyHiddenPattern();
        }
        else
        {
            switch (controller.Info.patternID)
            {
                case 11: // 엘리트 근접
                    currentPattern = new WBH_EnemyElitePattern();
                    break;
                case 101: // 액트1 보스
                    currentPattern = new WBH_EnemyBossPattern_Act1();
                    break;
                case 102: // 액트2 보스
                    currentPattern = new WBH_EnemyBossPattern_Act2();
                    break;
            }
        }
        currentPattern?.Initialize(this);
    }

    protected virtual void UpdateMove(float distance)
    {
        if(distance > status.AttackRange)
        {
            movement.Move(target.position);
        }
        else
        {
            movement.Stop();
        }
    }

    protected virtual void UpdateAttack(float distance)
    {
        if (distance > status.AttackRange)
            return;

        combat.TryAttack();
    }

    protected virtual void UpdateFacing(float distance, float deltaTime)
    {
        if (combat.IsActionInProgress)
            return;
        if (distance > status.AttackRange)
            return;
        if (target == null)
            return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(dir.normalized);

        float angle = Quaternion.Angle(transform.rotation, targetRotation);

        if (angle <= facingDeadZone)
            return;

        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rangedTurnSpeed * deltaTime);
    }

    public virtual void Hit()
    {
        enemyAnimation.PlayHit();
    }

    public virtual void Die()
    {
        CleanCurrentPattern();

        movement.Stop();
        enemyAnimation.PlayDie();
        enabled = false;
    }

    public virtual void ExecuteAttack()
    {
        FaceTarget();

        switch (controller.Info.enemyAttackType)
        {
            case EnemyAttackType.Melee:
                MeleeAttack();
                break;
            case EnemyAttackType.Ranged:
                RangedAttack();
                break;
            case EnemyAttackType.Boss:
                MeleeAttack();
                break;
        }
    }

    private void FaceTarget()
    {
        if (target == null)
            return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    protected virtual void MeleeAttack()
    {
        SectorAttack(status.AttackRange, basicMeleeAttackAngle);
    }

    protected virtual void RangedAttack()
    {
        Vector3 targetPos = target.position + Vector3.up;

        Vector3 direction = (targetPos - firePoint.position).normalized;

        WBH_DamageRequest request = combat.CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, basicAttackMult);

        projectileSpawner.FireProjectile(ProjectileType.NormalEnemy, firePoint.position, direction, request, status.ProjectileSpeed, status.AttackRange, playerLayer);

        enemyEffect.PlayCue(WBH_EnemyEffectCue.Normal_Range_01_Attack, transform.localScale);
    }

    // player 공격 코드 재활용
    // 전방 부채꼴 범위 공격
    private void SectorAttack(float range, float angle)
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, range, playerLayer);

        foreach (Collider target in targets)
        {
            Vector3 dirToTarget = (target.transform.position - transform.position).normalized;

            dirToTarget.y = 0;
            float targetAngle = Vector3.Angle(transform.forward, dirToTarget);

            if (targetAngle > angle * 0.5f)
                continue;

            if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
                continue;

            WBH_CombatManager.ProcessDamage(combat.CreateDamageRequest(combatTarget, WBH_AttackType.Normal, ItemSystem.ElementType.None, basicAttackMult));

            //if (targetAngle <= angle * 0.5f)
            //{
            //    if (target.TryGetComponent<T_IDamageable>(out var damageable))
            //    {
            //        damageable.TakeDamage(damage);
            //    }
            //}
        }
    }

    // 멀티플레이 감안. 타겟 설정.
    // 초기 타겟은 Spawner 에서 호출 예정.
    public void SetTarget(Transform target)
    {
        this.target = target;
        waitingForTarget = false;
    }

    private System.Func<System.Collections.Generic.IEnumerable<Transform>> externalTargets;

    /// <summary>SW 수정: 서버가 확정한 생존 참가자 목록을 기존 보스 대상 선택 규칙에 공급합니다.</summary>
    public void BindExternalTargets(System.Func<System.Collections.Generic.IEnumerable<Transform>> targets)
        => externalTargets = targets;

    public System.Collections.Generic.IEnumerable<Transform> GetActiveTargets()
    {
        if (externalTargets != null) return externalTargets();
        var result = new System.Collections.Generic.List<Transform>();
        foreach (var player in FindObjectsByType<T_PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (player.isActiveAndEnabled) result.Add(player.transform);
        return result;
    }

    // 타겟 유효성 검사
    private bool IsTargetValid()
    {
        if(target == null)
            return false;
        if(!target.gameObject.activeInHierarchy)
            return false;

        if (externalTargets != null)
        {
            foreach (var candidate in externalTargets()) if (candidate == target) return true;
            return false;
        }
        return target.TryGetComponent<T_PlayerController>(out T_PlayerController player) && player.isActiveAndEnabled;
    }

    // 타겟 비유효 시, 근접한 다른 플레이어로 재설정.
    private bool EnsureTarget()
    {
        if(IsTargetValid())
        {
            waitingForTarget = false;
            return true;
        }
        var players = GetActiveTargets();

        Transform closetPlayer = null;
        float closestSqrDistance = float.MaxValue;

        foreach(Transform player in players)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                continue;

            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;

            if (sqrDistance >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistance;
            closetPlayer = player.transform;
        }

        target = closetPlayer;

        if(target != null)
        {
            waitingForTarget = false;
            return true;
        }

        if(!waitingForTarget)
        {
            movement.Stop();
            waitingForTarget = true;
        }
        return false;
    }

    

    // 랜덤 타겟 선택
    public bool TrySelectAnotherActivePlayer()
    {
        var players = GetActiveTargets();

        Transform selected = null;
        int candidateCount = 0;

        foreach(Transform player in players)
        {
            if (player == null || !player.gameObject.activeInHierarchy || player.transform == target)
                continue;

            candidateCount++;

            // 현재 타겟 제외 플레이어 중 하나 랜덤 선택
            if(Random.Range(0, candidateCount) == 0)
            {
                selected = player.transform;
            }
        }
        if(selected == null)
            return false;

        SetTarget(selected);
        return true;
    }

    // 사거리 이내 가장 먼 거리의 타겟 선택
    public bool TrySelectFarTarget(float maxRange)
    {
        var players = GetActiveTargets();

        float maxRangeSqr = maxRange * maxRange;
        float farSqr = -1f;
        Transform selected = null;

        foreach (Transform player in players)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                continue;

            float distanceSqr = (player.transform.position - transform.position).sqrMagnitude;

            if (distanceSqr > maxRangeSqr || distanceSqr <= farSqr)
                continue;

            farSqr = distanceSqr;
            selected = player.transform;
        }

        if (selected == null)
            return false;

        SetTarget(selected);
        return true;
    }

    // 가장 가까운 타겟 선택. 가까운 타겟이기에 TrySelectFarTarget 과 달리 유효거리를 매개변수로 받지 않음
    public bool TrySelectNearTarget()
    {
        var players = GetActiveTargets();

        Transform near = null;
        float nearSqrDistance = float.MaxValue;

        foreach (Transform player in players)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                continue;

            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;

            if (sqrDistance >= nearSqrDistance)
                continue;

            nearSqrDistance = sqrDistance;
            near = player.transform;
        }
        if (near == null)
            return false;

        SetTarget(near);
        return true;
    }

    public void KillSelf()
    {
        controller.KillSelf();
    }

    private void CleanCurrentPattern()
    {
        WBH_IEnemyPattern pattern = currentPattern;
        currentPattern = null;

        if(pattern is WBH_EnemySelfDestructPattern selfDestruct)
        {
            selfDestruct.Cancel();
        }
        if(pattern is WBH_EnemyHiddenPattern hidden)
        {
            hidden.Cleanup();
        }
        if(pattern is WBH_EnemyBossPattern_Act2 act2)
        {
            act2.Cleanup();
        }

    }

    // WBH_EnemyController.cs 의 DeSpawn 메서드를 WBH_IEnemyPattern 상속자들에게 전달
    public void Despawn()
    {
        controller.Despawn();
    }

    public void SetPatternDamageBlock(bool blocked)
    {
        controller.SetPatternDamageBlock(blocked);
    }
}

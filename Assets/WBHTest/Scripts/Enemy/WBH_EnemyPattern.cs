using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

[RequireComponent(typeof(WBH_EnemyMovement))]
[RequireComponent(typeof(WBH_EnemyCombat))]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyAnimation))]
[RequireComponent(typeof(WBH_EffectSpawner))]
[RequireComponent(typeof(WBH_ProjectileSpawner))]
public class WBH_EnemyPattern : MonoBehaviour
{
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform meleeEffectPoint;
    [SerializeField] private WBH_EffectData normalMeleeEffect;

    private WBH_EnemyController controller;
    private WBH_EnemyMovement movement;
    private WBH_EnemyCombat combat;
    private WBH_EnemyStatus status;
    private WBH_EnemyAnimation enemyAnimation;
    private WBH_Indicator indicator;
    private WBH_IndicatorSpawner indicatorSpawner;

    private WBH_EffectSpawner effectSpawner;

    private WBH_ProjectileSpawner projectileSpawner;

    private Transform target;

    private WBH_IEnemyPattern currentPattern;

    public float Distance { get; private set; } = 0;
    private float basicAttackMult = 1f;
    private float basicMeleeAttackAngle = 120; // % int 로 변경하면 최적화?
    protected float dashHitRadius = 3f;
    private bool waitingForTarget;

    public WBH_EnemyMovement Movement => movement;
    public WBH_EnemyCombat Combat => combat;
    public WBH_Indicator Indicator => indicator;
    public WBH_IndicatorSpawner IndicatorSpawner => indicatorSpawner;
    public float AttackRange => status.AttackRange;
    public Transform Target => target;
    public Transform FirePoint => firePoint;
    public LayerMask PlayerLayer => playerLayer;

    public float DashHitRadius => dashHitRadius;

    public float HealthRatio => status.MaxHealth > 0f ? status.CurrentHp / status.MaxHealth : 1f;

    private void Awake()
    {
        movement = GetComponent<WBH_EnemyMovement>();
        combat = GetComponent<WBH_EnemyCombat>();
        status = GetComponent<WBH_EnemyStatus>();
        enemyAnimation = GetComponent<WBH_EnemyAnimation>();
        effectSpawner = GetComponent<WBH_EffectSpawner>();
        projectileSpawner = GetComponent<WBH_ProjectileSpawner>();
        indicator = GetComponent<WBH_Indicator>();
        indicatorSpawner = GetComponent <WBH_IndicatorSpawner>();
    }

    public virtual void Initialize(WBH_EnemyController controller)
    {
        this.controller = controller;

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

        Distance = Vector3.Distance(transform.position, target.position);

        if(currentPattern != null)
        {
            currentPattern?.Tick(Time.deltaTime);
        }
        else
        {
            UpdateMove(Distance);
            UpdateAttack(Distance);
        }
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

    public virtual void Hit()
    {
        enemyAnimation.PlayHit();
    }

    public virtual void Die()
    {
        movement.Stop();
        enemyAnimation.PlayDie();
        enabled = false;
    }

    public virtual void ExecuteAttack()
    {
        transform.LookAt(Target);

        switch (controller.Info.enemyType)
        {
            case EnemyType.Melee:
                MeleeAttack();
                break;
            case EnemyType.Ranged:
                RangedAttack();
                break;
        }
    }

    protected virtual void MeleeAttack()
    {
        SectorAttack(status.AttackRange, basicMeleeAttackAngle);
        effectSpawner.SpawnEffect(normalMeleeEffect, meleeEffectPoint);
    }

    protected virtual void RangedAttack()
    {
        Vector3 targetPos = target.position + Vector3.up;

        Vector3 direction = (targetPos - firePoint.position).normalized;

        WBH_DamageRequest request = combat.CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, basicAttackMult);

        projectileSpawner.FireProjectile(ProjectileType.NormalEnemy, firePoint.position, direction, request, status.ProjectileSpeed, status.AttackRange, playerLayer);
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

    private bool IsTargetValid()
    {
        if(target == null)
            return false;
        if(!target.gameObject.activeInHierarchy)
            return false;

        return target.TryGetComponent<T_PlayerController>(out T_PlayerController player) && player.isActiveAndEnabled;
    }

    private bool EnsureTarget()
    {
        if(IsTargetValid())
        {
            waitingForTarget = false;
            return true;
        }
        T_PlayerController[] players = FindObjectsByType<T_PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        Transform closetPlayer = null;
        float closestSqrDistance = float.MaxValue;

        foreach(T_PlayerController player in players)
        {
            if (!player.isActiveAndEnabled)
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

    private void CreatePattern()
    {
        switch(controller.Info.patternID)
        {
            case 1:
                currentPattern = new WBH_EnemyElitePattern();
                break;
            case 10:
                currentPattern = new WBH_EnemyBossPattern_Act1();
                break;
        }
        currentPattern?.Initialize(this);
    }

    // 랜덤 타겟 선택
    public bool TrySelectAnotherActivePlayer()
    {
        T_PlayerController[] players = FindObjectsByType<T_PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        Transform selected = null;
        int candidateCount = 0;

        foreach(T_PlayerController player in players)
        {
            if (!player.isActiveAndEnabled || player.transform == target)
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
        T_PlayerController[] players = FindObjectsByType<T_PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        float maxRangeSqr = maxRange * maxRange;
        float farSqr = -1f;
        Transform selected = null;

        foreach (T_PlayerController player in players)
        {
            if (!player.isActiveAndEnabled)
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
}

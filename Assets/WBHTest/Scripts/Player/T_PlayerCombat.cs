using UnityEngine;


public enum PlayerClass { Fighter, gunner }
public enum GunnerWeaponType {Rifle, Shotgun, GrenadeLauncher}

public class T_PlayerCombat : MonoBehaviour
{
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private WBH_ProjectileSpawner projectileSpawner;
    [SerializeField] private Transform firePoint;
    [SerializeField] public GunnerWeaponType currentWeapon;
    // 차후 무기 데이터에 폭발반경 포함되면 변수 삭제 및 GunnerAttack 메서드에서 해당 변수 내용 수정 필요
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;
    [SerializeField] private PlayerClass playerClass;

    private Animator animator;
    private WBH_PlayerStatus status;
    private T_PlayerController controller;
    private WBH_PlayerEffect effect;
    private WBH_EnemyController chaseTarget;
    private Collider chaseTargetCollider;
    private Vector3 lastChaseDestination;
    private Vector3 grenadePoint;

    private bool hasChaseDestination;
    private float basicAttackMult = 1f;

    private const float ChaseRefreshDistance = 0.25f;

    public bool IsDead => stateMachine.CurrentState == PlayerState.Dead;
    public float AttackRange => playerClass == PlayerClass.Fighter ? status.FighterAttackRange : status.GunnerAttackRange;
    private bool CanAttack => !stateMachine.IsAnyState(PlayerState.Hit,
                                                       PlayerState.Attack,
                                                       PlayerState.Skill,
                                                       PlayerState.Dodge,
                                                       PlayerState.Dead);

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<T_PlayerController>();
        stateMachine = GetComponent<WBH_PlayerStateMachine>();
        effect = GetComponent<WBH_PlayerEffect>();
        status = GetComponent<WBH_PlayerStatus>();
    }

    private void Update()
    {
        if (status.IsDead)
        {
            CancelChase();
            return;
        }

        UpdateChase();
        TestMultiple();
    }

    public void NormalAttack()
    {
        if (status.IsDead || !CanAttack) // 피격 상태에서 공격을 못하게 할 경우 조건 추가 필요
            return;

        stateMachine.ChangeState(PlayerState.Attack);
    }

    private void FighterAttack()
    {
        SectorAttack(status.FighterAttackRange, 230f);
    }
    private void GunnerAttack()
    {
        Vector3 direction = transform.forward;

        // 투사체용 데미지 요청 생성. ElementType은 현재 장착 무기에 인챈트된 속성을 그대로 사용한다.
        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, status.CurrentElement, basicAttackMult, WBH_StatusEffectPresets.Burn1); // Burn1은 아직 테스트값

        switch(currentWeapon)
        {
            case GunnerWeaponType.Rifle:
                projectileSpawner.FireProjectile(ProjectileType.Normal, firePoint.position, direction, request, status.GunnerBulletSpeed, status.GunnerAttackRange, enemyLayer);
                break;
            case GunnerWeaponType.Shotgun:
                {
                    SectorAttack(status.GunnerAttackRange, 90f);
                    //effect.ShotGunEffect();
                }
                break;
            case GunnerWeaponType.GrenadeLauncher:
                {
                    projectileSpawner.FireGrenade(ProjectileType.Grenade, firePoint.position, grenadePoint, request, status.GunnerBulletSpeed, status.GunnerAttackRange, explosionRadius, enemyLayer);
                }
                break;
        }

        // -- 즉발 공격 시 사용할 예비 코드
        //Ray ray = new Ray(transform.position + Vector3.up, transform.forward);

        //if (Physics.Raycast(ray, out RaycastHit hit, gunnerAttackRange, enemyLayer))
        //{
        //    if (hit.collider.TryGetComponent<T_IDamageable>(out var damageable))
        //    {
        //        damageable.TakeDamage(gunnerAttackDamage);
        //    }
        //}
    }


    private void SectorAttack(float range, float angle)
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, range, enemyLayer);

        foreach (Collider target in targets)
        {
            Vector3 dirToTarget = (target.transform.position - transform.position).normalized;

            dirToTarget.y = 0;
            float targetAngle = Vector3.Angle(transform.forward, dirToTarget);

            if (targetAngle > angle * 0.5f)
                continue;

            if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
                continue;

            WBH_CombatManager.ProcessDamage(CreateDamageRequest(combatTarget, WBH_AttackType.Normal, status.CurrentElement, basicAttackMult, WBH_StatusEffectPresets.Slow1)); // Slow1은 아직 테스트값
        }
    }

    // 투사체 외
    public WBH_DamageRequest CreateDamageRequest(WBH_ICombat target, WBH_AttackType atkType, ItemSystem.ElementType elementType, float damageMult, WBH_StatusEffectData? statusEffect = null)
    {
        return new WBH_DamageRequest(controller, target, atkType, elementType, damageMult, statusEffect);
    }

    // 투사체는 타겟이 충돌 시 결정되기에 null 로 비워둠.
    public WBH_DamageRequest CreateDamageRequest(WBH_AttackType atkType, ItemSystem.ElementType elementType, float damageMult, WBH_StatusEffectData? statusEffect = null)
    {
        return new WBH_DamageRequest(controller, null, atkType, elementType, damageMult, statusEffect);
    }

    private void UpdateChase()
    {
        if (chaseTarget == null)
            return;

        if(!chaseTarget.gameObject.activeInHierarchy || chaseTarget.Status == null || chaseTarget.Status.IsDead)
        {
            CancelChase();
            return;
        }

        if(stateMachine.IsAnyState(PlayerState.Skill,PlayerState.Dodge, PlayerState.Dead, PlayerState.Hit))
        {
            CancelChase();
            return;
        }

        // 컬라이더 외곽부분부터 플레이어까지의 거리를 계산하여 공격사거리 판정
        Vector3 targetPoint = chaseTargetCollider != null ? chaseTargetCollider.ClosestPoint(transform.position) : chaseTarget.transform.position;

        Vector3 distanceVector = targetPoint - transform.position;

        distanceVector.y = 0;
        float attackRange = AttackRange;

        if(distanceVector.sqrMagnitude <= attackRange * attackRange)
        {
            Vector3 attackPos = chaseTarget.transform.position;

            CancelChase();
            TryAttack(attackPos);
            return;
        }

        Vector3 destination = chaseTarget.transform.position;

        float refreshDistanceSqr = ChaseRefreshDistance * ChaseRefreshDistance;

        bool shouldRefresh = !hasChaseDestination || (destination - lastChaseDestination).sqrMagnitude >= refreshDistanceSqr;

        if (!shouldRefresh)
            return;

        float stoppingDistance = attackRange * 0.85f; // 공격 사거리의 85% 까지 접근

        if(!controller.ChaseCommand(destination,stoppingDistance))
        {
            CancelChase();
            return;
        }

        lastChaseDestination = destination; ;
        hasChaseDestination = true;
    }

    // 적 추격 취소
    public void CancelChase()
    {
        chaseTarget = null;
        chaseTargetCollider = null;
        hasChaseDestination = false;

        controller.StopMovement();
    }

    public bool TryAtkTarget(WBH_EnemyController target)
    {
        if (!CanAttack || target == null || target.Status == null || target.Status.IsDead || !target.gameObject.activeInHierarchy)
            return false;

        CancelChase();

        chaseTarget = target;
        chaseTargetCollider = target.GetComponent<Collider>();

        hasChaseDestination = false;

        UpdateChase();

        return true;
    }

    // -- 입력 시스템 호출용 메서드
    public void TryAttack(Vector3 targetPos)
    {
        if (!CanAttack) 
            return;

        grenadePoint = targetPos;

        CancelChase();

        Vector3 lookDir = targetPos - transform.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude < 0.001f)
            return;

        transform.forward = lookDir.normalized;

        NormalAttack();
    }

    // -- 애니메이터 호출용 메서드
    public void ExecuteAttack()
    {
        if (!stateMachine.Is(PlayerState.Attack))
            return;

        switch (playerClass)
        {
            case PlayerClass.Fighter:
                FighterAttack();
                break;
            case PlayerClass.gunner:
                GunnerAttack();
                break;
        }
    }


    // ------ 테스트용 메서드

   



    // -- 작동 테스트용 메서드
    public void TestMultiple()
    {
        //WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, basicAttackMult);

        //if (Input.GetKeyDown(KeyCode.Alpha1))
        //{
        //    Vector3 targetPos = transform.position + transform.forward * 8f;

        //    projectileSpawner.FireMultipleProjectile(ProjectileType.Normal, firePoint.position, transform.forward, request, gunnerBulletSpeed, gunnerAttackRange, enemyLayer, 5, 30);
        //}

        //if (Input.GetKeyDown(KeyCode.Alpha2))
        //{
        //    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        //    if (Physics.Raycast(ray, out RaycastHit hit))
        //        projectileSpawner.FireMultipleGrenade(ProjectileType.Grenade, firePoint.position, hit.point, 5, 30f, request, gunnerBulletSpeed, gunnerAttackRange, explosionRadius, enemyLayer);
        //}
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        //Gizmos.DrawWireSphere(transform.position, fighterAttackRange);
    }
#endif
}

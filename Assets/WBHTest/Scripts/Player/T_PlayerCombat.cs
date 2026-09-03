using UnityEngine;


public enum PlayerClass { Fighter, gunner }
public enum GunnerWeaponType {Rifle, Shotgun, GrenadeLauncher}

public class T_PlayerCombat : MonoBehaviour
{
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform firePoint;
    [SerializeField] public GunnerWeaponType currentWeapon;
    // 차후 무기 데이터에 폭발반경 포함되면 변수 삭제 및 GunnerAttack 메서드에서 해당 변수 내용 수정 필요
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;
    [SerializeField] private PlayerClass playerClass;
    [SerializeField] private WBH_EffectData basicGrenadeEffect;

    [Header("Fighter Basic Atk Range Visual")]
    [SerializeField] private bool showAtkRange = true;
    [SerializeField] private Color atkAreaColor = new Color(1f, 0.2f, 0.1f, 0.25f);
    [SerializeField] private float showDuration = 0.25f;


    private Animator animator;
    private WBH_PlayerStatus status;
    private T_PlayerController controller;
    private WBH_PlayerEffect effect;
    private WBH_EnemyController chaseTarget;
    private WBH_ProjectileSpawner projectileSpawner;
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
                                                       PlayerState.Dead,
                                                       PlayerState.Revive);

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

    public void Initialize(WBH_ProjectileSpawner projectileSpawner)
    {
        this.projectileSpawner = projectileSpawner;
    }

    public void NormalAttack()
    {
        if (status.IsDead || !CanAttack) // 피격 상태에서 공격을 못하게 할 경우 조건 추가 필요
            return;

        stateMachine.ChangeState(PlayerState.Attack);
    }

    private void FighterAttack()
    {
        if(showAtkRange)
        {
            SkillRangeVisual.ShowSector(transform.position,
                                        transform.forward,
                                        status.FighterAttackRange,
                                        status.FighterAttackAngle,
                                        atkAreaColor,
                                        showDuration);
        }

        WBH_EffectData effectData = null;

        if (effect != null)
            effect.TryGetEffectData(WBH_PlayerEffectCue.F_normal0_evo0_etc0, out effectData);

        SectorAttack(status.FighterAttackRange, status.FighterAttackAngle, effectData);
    }
    private void GunnerAttack()
    {
        // SW 추가:
        // 장비 표시 코드는 현재 손에 든 무기 외형 하나만 켜 둡니다.
        // 따라서 아래 검색은 보관 중인 꺼진 무기가 아니라 지금 손에 보이는 무기의 VFX 연결 정보만 찾습니다.
        GunnerWeaponVfxBinding vfxBinding =
            GetComponentInChildren<GunnerWeaponVfxBinding>();

        // SW 추가:
        // 모든 신규 거너 WeaponVisual은 root 직속 Muzzle을 가지고 있으며 그 Transform의 +Z(forward)가 발사 방향입니다.
        // 아직 바인딩되지 않은 테스트 프리팹도 공격이 멈추지 않도록 기존 firePoint를 두 번째 선택지로 남깁니다.
        Transform resolvedMuzzle = vfxBinding != null && vfxBinding.Muzzle != null
            ? vfxBinding.Muzzle
            : firePoint;

        // SW 추가:
        // 실제 장착 무기의 총열 끝 위치와 +Z를 우선 사용합니다. Muzzle과 기존 firePoint가 모두 없는 비정상 테스트 상태에서는
        // 플레이어 위치/정면을 마지막 대신 값으로 사용하여 빈 참조 오류 없이 기존 공격 흐름을 계속 진행합니다.
        Vector3 spawnPosition = resolvedMuzzle != null
            ? resolvedMuzzle.position
            : transform.position;
        Vector3 direction = resolvedMuzzle != null
            ? resolvedMuzzle.forward
            : transform.forward;
        // SW 추가:
        // 장착 외형마다 저장된 Rifle/Shotgun/GrenadeLauncher 값을 사용하면 Inspector의 currentWeapon을 매 장비 교체마다
        // 별도로 맞춰 주지 않아도 됩니다. 예전 테스트 프리팹에는 이 정보가 없을 수 있으므로 currentWeapon을 대신 사용합니다.
        GunnerWeaponType resolvedWeapon = vfxBinding != null
            ? vfxBinding.WeaponType
            : currentWeapon;

        // SW 추가:
        // 무기 VFX 연결 정보에는 같은 아이템의 비행/명중 프리팹이 들어 있습니다. 값이 비어 있으면 예전 무기라는 뜻이며,
        // WBH_Projectile이 기존 렌더러를 그대로 보여 주므로 기존 공격도 깨지지 않습니다.
        GameObject projectileVisual = vfxBinding != null
            ? vfxBinding.ProjectileVisualPrefab
            : null;
        GameObject impactVisual = vfxBinding != null
            ? vfxBinding.ImpactVisualPrefab
            : null;

        // SW 추가:
        // 총구 연출은 장착 외형의 root 직속 Muzzle 아래에 한 번만 생성되고, 이후 공격에서는 Particle/Trail만 초기화해 재생합니다.
        // VFX 연결 정보가 없는 예전 테스트 프리팹에서는 총구 연출만 건너뛰고 공격은 계속됩니다.
        vfxBinding?.PlayMuzzle();

        // 투사체용 데미지 요청 생성. ElementType은 현재 장착 무기에 인챈트된 속성을 그대로 사용한다.
        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, status.CurrentElement, basicAttackMult, WBH_StatusEffectPresets.Burn1); // Burn1은 아직 테스트값

        // SW 추가:
        // 아래 switch의 피해 방식은 팀원 기존 구현을 그대로 사용합니다. 달라지는 것은 발사 위치와 전달되는 시각 프리팹뿐입니다.
        switch(resolvedWeapon)
        {
            case GunnerWeaponType.Rifle:
                // SW 추가:
                // 라이플은 기존 Normal 풀 투사체의 이동, Trigger 충돌, 단일 대상 피해를 그대로 사용합니다.
                // 마지막 두 인수는 해당 장착 무기의 비행/명중 외형이며 실제 전투 수치에는 관여하지 않습니다.
                projectileSpawner.FireProjectile(ProjectileType.Normal, spawnPosition, direction, request, status.GunnerBulletSpeed, status.GunnerAttackRange, enemyLayer, projectileVisual, impactVisual);
                break;
            case GunnerWeaponType.Shotgun:
                {
                    // SW 추가:
                    // WBH 샷건은 여러 물리 탄환이 아니라 90도 부채꼴 안의 대상에게 즉시 피해를 줍니다.
                    // 따라서 명중 VFX도 중앙 투사체가 나중에 충돌할 때가 아니라 실제 피해를 받은 각 대상 위치에서 바로 재생합니다.
                    SectorAttack(
                        status.GunnerAttackRange,
                        90f,
                        impactVisualPrefab: impactVisual);
                    // SW 추가:
                    // 산탄총은 총구에서 10m·90도 부채꼴 VFX가 바로 펼쳐지고, 실제 피해 대상 위치에서 명중 VFX가 재생됩니다.
                    // 중앙으로 탄환 한 발을 추가로 날리면 부채꼴 공격인데도 라이플처럼 보여 어색하므로 투사체 풀은 호출하지 않습니다.
                    // 데미지는 바로 위 SectorAttack이 이미 처리했기 때문에 이 변경은 공격 범위와 피해량에 영향을 주지 않습니다.
                    //effect.ShotGunEffect();
                }
                break;
            case GunnerWeaponType.GrenadeLauncher:
                {
                    // SW 추가:
                    // 유탄은 기존 FireGrenade의 포물선, 도착 지점, 폭발 반경, 광역 피해를 그대로 사용합니다.
                    // 팀원이 만든 basicGrenadeEffect도 그대로 두었으므로 기존 폭발 효과와 새 무기별 명중 효과가 함께 재생됩니다.
                    projectileSpawner.FireGrenade(ProjectileType.Grenade, spawnPosition, grenadePoint, request, status.GunnerBulletSpeed, status.GunnerAttackRange, explosionRadius, enemyLayer, basicGrenadeEffect, projectileVisual, impactVisual);
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


    private void SectorAttack(
        float range,
        float angle,
        WBH_EffectData effectData = null,
        // SW 추가:
        // 거너 샷건만 사용하는 선택 값입니다. Fighter 호출은 값을 넘기지 않으므로 기존 동작이 그대로 유지됩니다.
        GameObject impactVisualPrefab = null)
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

            WBH_DamageRequest request = CreateDamageRequest(combatTarget,
                                                            WBH_AttackType.Normal,
                                                            status.CurrentElement,
                                                            basicAttackMult,
                                                            statusEffect: WBH_StatusEffectPresets.Slow1, // Slow1은 아직 테스트값
                                                            effectData: effectData);

            WBH_CombatManager.ProcessDamage(request);

            // SW 추가:
            // SectorAttack은 투사체 충돌보다 먼저 실제 피해를 처리합니다. 같은 프레임에 실제 피해 대상 위치에서 명중 VFX를
            // 재생해야 화면과 판정이 어긋나지 않습니다. -dirToTarget은 탄환이 날아온 반대쪽인 피격면 바깥 방향입니다.
            GunnerVfxPlayback.SpawnTransient(
                impactVisualPrefab,
                target.transform.position,
                -dirToTarget);
        }
    }

    // 투사체 외
    public WBH_DamageRequest CreateDamageRequest(WBH_ICombat target,
                                                 WBH_AttackType atkType,
                                                 ItemSystem.ElementType elementType,
                                                 float damageMult,
                                                 WBH_StatusEffectData? statusEffect = null,
                                                 WBH_EffectData effectData = null)
    {
        return new WBH_DamageRequest(controller, target, atkType, elementType, damageMult, statusEffect, effectData);
    }

    // 투사체는 타겟이 충돌 시 결정되기에 null 로 비워둠.
    public WBH_DamageRequest CreateDamageRequest(WBH_AttackType atkType,
                                                 ItemSystem.ElementType elementType,
                                                 float damageMult,
                                                 WBH_StatusEffectData? statusEffect = null,
                                                 WBH_EffectData effectData = null)
    {
        return new WBH_DamageRequest(controller, null, atkType, elementType, damageMult, statusEffect, effectData);
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
    public void CancelChase(bool stopMovement = true)
    {
        chaseTarget = null;
        chaseTargetCollider = null;
        hasChaseDestination = false;

        if(stopMovement)
        {
            controller.StopMovement();
        }
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

using System.Data;
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

    private float fighterAttackRange = 2f;
    private float gunnerAttackRange = 10f;
    private float gunnerBulletSpeed = 10f;
    private float basicAttackMult = 1f;


    private bool CanAttack => !stateMachine.IsAnyState(PlayerState.Hit,
                                                       PlayerState.Skill,
                                                       PlayerState.Dodge,
                                                       PlayerState.Dead);

    private Animator animator;
    private WBH_PlayerStatus status;

    public bool IsDead => stateMachine.CurrentState == PlayerState.Dead;

    [SerializeField] private PlayerClass playerClass;
    private T_PlayerController controller;
    private WBH_PlayerEffect effect;
    private Vector3 grenadePoint;


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
            return;

        UpdateStats();

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
        SectorAttack(fighterAttackRange, 230f);
    }
    private void GunnerAttack()
    {
        Vector3 direction = transform.forward;

        // 투사체용 데미지 요청 생성.
        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, basicAttackMult);

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

            WBH_CombatManager.ProcessDamage(CreateDamageRequest(combatTarget, WBH_AttackType.Normal, ItemSystem.ElementType.None, basicAttackMult));
        }
    }

    // 투사체 외
    public WBH_DamageRequest CreateDamageRequest(WBH_ICombat target, WBH_AttackType atkType, ItemSystem.ElementType elementType, float damageMult)
    {
        return new WBH_DamageRequest(controller, target, atkType, elementType, damageMult);
    }

    // 투사체는 타겟이 충돌 시 결정되기에 null 로 비워둠.
    public WBH_DamageRequest CreateDamageRequest(WBH_AttackType atkType, ItemSystem.ElementType elementType, float damageMult)
    {
        return new WBH_DamageRequest(controller, null, atkType, elementType, damageMult);
    }

    public void CancelChase()
    {
        stateMachine.ChangeState(PlayerState.Idle);
        controller.ResetStoppingDistance();
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

    private void UpdateStats()
    {
        switch (currentWeapon)
        {
            case GunnerWeaponType.Rifle:
                gunnerAttackRange = 10f;
                break;
            case GunnerWeaponType.Shotgun:
                gunnerAttackRange = 4f;
                break;
            case GunnerWeaponType.GrenadeLauncher:
                gunnerAttackRange = 7f;
                break;
        }
    }

    // -- 작동 테스트용 메서드
    public void TestMultiple()
    {
        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal, ItemSystem.ElementType.None, basicAttackMult);

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Vector3 targetPos = transform.position + transform.forward * 8f;

            projectileSpawner.FireMultipleProjectile(ProjectileType.Normal, firePoint.position, transform.forward, request, gunnerBulletSpeed, gunnerAttackRange, enemyLayer, 5, 30);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
                projectileSpawner.FireMultipleGrenade(ProjectileType.Grenade, firePoint.position, hit.point, 5, 30f, request, gunnerBulletSpeed, gunnerAttackRange, explosionRadius, enemyLayer);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(transform.position, fighterAttackRange);
    }
#endif
}

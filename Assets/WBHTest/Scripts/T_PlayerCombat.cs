using UnityEngine;
using UnityEngine.UIElements;


public enum PlayerClass { Fighter, gunner }
public enum GunnerWeaponType {Rifle, Shotgun, GrenadeLauncher}

public class T_PlayerCombat : MonoBehaviour, T_IDamageable
{
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private WBH_ProjectileSpawner projectileSpawner;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GunnerWeaponType currentWeapon;
    // 차후 무기 데이터에 폭발반경 포함되면 변수 삭제 및 GunnerAttack 메서드에서 해당 변수 내용 수정 필요
    [SerializeField] private float explosionRadius = 3f;

    private float maxHp = 100f;

    private float fighterAttackRange = 2f;
    private float gunnerAttackRange = 10f;
    private float fighterAttackDamage = 15f;
    private float gunnerAttackDamage = 10f;
    private float gunnerBulletSpeed = 10f;

    private Animator animator;

    public float CurrentHp { get; private set; }
    public PlayerState CurrentState { get; private set; }
    public bool IsDead => CurrentState == PlayerState.Dead;

    [SerializeField] private PlayerClass playerClass;
    private Transform attackTarget;
    private bool isChasingTarget;
    private T_PlayerController controller;

    private float CurrentAttackRange
    {
        get
        {
            switch (playerClass)
            {
                case PlayerClass.Fighter:
                    return fighterAttackRange;
                case PlayerClass.gunner:
                    return gunnerAttackRange;
                default:
                    return fighterAttackRange;
            }
        }
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<T_PlayerController>();

        CurrentHp = maxHp;
        CurrentState = PlayerState.Idle;
    }

    private void Update()
    {
        if (CurrentState == PlayerState.Dead)
            return;

            CheckAttackDistance();

        TestMultiple();
    }

    private void UpdateState() // 상태 변경 추후 매개변수에 State 들어가도록 수정 필요
    {
        if (CurrentState == PlayerState.Attack || CurrentState == PlayerState.Hit)
            return;

        CurrentState = PlayerState.Idle;
    }



    public void NormalAttack()
    {
        if (CurrentState == PlayerState.Dead) // 피격 상태에서 공격을 못하게 할 경우 조건 추가 필요
            return;

        if(attackTarget != null)
        {
            Vector3 lookDir = attackTarget.position - transform.position;
            lookDir.y = 0f;
            transform.forward = lookDir.normalized;
        }

        CurrentState = PlayerState.Attack;

        animator.SetTrigger("Attack"); // 애니메이션 추가 시 코드 확정
        print("일반공격 실행");

        
    }

    private void FighterAttack()
    {
        SectorAttack(fighterAttackRange, 230f, fighterAttackDamage);
    }
    private void GunnerAttack()
    {
        if (attackTarget == null)
            return;

        Vector3 targetPos = attackTarget.position + Vector3.up;

        Vector3 direction = (targetPos - firePoint.position).normalized;

        switch(currentWeapon)
        {
            case GunnerWeaponType.Rifle:
                projectileSpawner.FireProjectile(ProjectileType.Normal, firePoint.position, direction, gunnerAttackDamage, gunnerBulletSpeed, gunnerAttackRange, enemyLayer);
                break;
            case GunnerWeaponType.Shotgun:
                {
                    SectorAttack(gunnerAttackRange, 90f, gunnerAttackDamage);
                }
                break;
            case GunnerWeaponType.GrenadeLauncher:
                {
                    projectileSpawner.FireGrenade(ProjectileType.Grenade, firePoint.position, attackTarget.position, gunnerAttackDamage, gunnerBulletSpeed, gunnerAttackRange, explosionRadius, enemyLayer);
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

    public void TakeDamage(float damage)
    {
        if (IsDead)
            return;

        CurrentHp -= damage;

        //animator.SetTrigger("Hit"); // 애니메이션 추가 시

        CurrentState = PlayerState.Hit;

        if (CurrentHp <= 0)
            Die();
    }

    private void Die()
    {
        CurrentHp = 0;

        CurrentState = PlayerState.Dead;

        //animator.SetBool("Dead", true); // 애니메이션 추가
    }

    private void CheckAttackDistance()
    {
        //if (!isChasingTarget)
        //    return;

        if (attackTarget == null)
        {
            isChasingTarget = false;
            return;
        }

        float distance = Vector3.Distance(transform.position, attackTarget.position);

        if (distance <= CurrentAttackRange)
        {
            isChasingTarget = false;

            controller.ResetStoppingDistance();

            NormalAttack();
        }
        else
        {
            controller.MoveToTarget(attackTarget.position, CurrentAttackRange);
            return;
        }
    }

    private void SectorAttack( float range, float angle, float damage)
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, range, enemyLayer);

        foreach (Collider target in targets)
        {
            Vector3 dirToTarget = (target.transform.position - transform.position).normalized;

            dirToTarget.y = 0;
            float targetAngle = Vector3.Angle(transform.forward, dirToTarget);
            if(targetAngle <= angle * 0.5f)
            {
                if(target.TryGetComponent<T_IDamageable> (out var damageable))
                    {
                    damageable.TakeDamage(damage);
                    }
            }
        }
    }

    public void CancelChase()
    {
        isChasingTarget = false;
        attackTarget = null;
        controller.ResetStoppingDistance();
    }

    // -- 작동 테스트용 메서드
    public void TestMultiple()
    {
        if(Input.GetKeyDown(KeyCode.Alpha1))
        {
            Vector3 targetPos = transform.position + transform.forward * 8f;

            projectileSpawner.FireMultipleProjectile(ProjectileType.Normal, firePoint.position, transform.forward,gunnerAttackDamage,gunnerBulletSpeed,gunnerAttackRange,enemyLayer,5,30);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if(Physics.Raycast(ray, out RaycastHit hit))
            projectileSpawner.FireMultipleGrenade(ProjectileType.Grenade, firePoint.position, hit.point, 5, 30f, gunnerAttackDamage, gunnerBulletSpeed, gunnerAttackRange, explosionRadius, enemyLayer);
        }
    }

    // -- 입력 시스템 호출용 메서드
    // 적 클릭 시, 공격 사거리 안이면 공격, 밖이면 사거리까지 이동 후 공격
    public void TryAttackTarget(Transform target)
    {
        if (CurrentState == PlayerState.Dead)
            return;

        attackTarget = target;

        float distance = Vector3.Distance(transform.position, attackTarget.position);

        if(distance <= CurrentAttackRange)
        {
            transform.forward = (target.position - transform.position).normalized;
            NormalAttack();
        }

        else
        {
            isChasingTarget = true;

            controller.MoveToTarget(target.position, CurrentAttackRange);
        }
    }

    // -- 애니메이터 호출용 메서드
    public void ExecuteAttack()
    {
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

    public void EndAttack()
    {
        if (CurrentState == PlayerState.Attack)
            CurrentState = PlayerState.Idle;
        attackTarget = null;
    }
    public void EndHit()
    {
        if (CurrentState == PlayerState.Hit)
            CurrentState = PlayerState.Idle;
    }
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(transform.position, fighterAttackRange);
    }
#endif
}

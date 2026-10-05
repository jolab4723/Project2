using UnityEngine;


public enum PlayerClass { Fighter, gunner }
public enum GunnerWeaponType {Rifle, Shotgun, GrenadeLauncher}

public class T_PlayerCombat : MonoBehaviour
{
    // SW 수정 : 같은 조건에서 세 총기의 발사 누락이 없음을 확인한 뒤 라이플만 낮은 발당 피해·고연사로 조정한다.
    public const float RifleAttackSpeedMultiplier = 1.5f;
    public static float GunnerBasicDamageMultiplier(GunnerWeaponType weapon)
        => weapon == GunnerWeaponType.Rifle ? 0.8f : 1f;

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
    private uint nextAttackId;
    private System.Func<Vector3, bool> externalAttackRequest;

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
        // SW 수정: TestMultiple 본문은 모두 주석이라 매 프레임 호출하지 않는다. 테스트 코드는 메서드에 보존한다.
        //TestMultiple();
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
        // 실제 투사체와 공격 판정은 무기마다 움직이는 Muzzle이 아니라 Gunner 루트 직속 FirePoint에서 시작합니다.
        // FirePoint는 모든 총이 공유하는 중앙 기준점이므로 총의 길이, 손목 회전, 공격 모션이 달라도
        // 라이플·산탄총·유탄발사기의 실제 공격 시작 위치가 바뀌지 않습니다.
        Transform resolvedFirePoint = firePoint != null
            ? firePoint
            : transform;
        Vector3 spawnPosition = resolvedFirePoint.position;

        // SW 추가:
        // 캐릭터가 보고 있는 수평 정면만 발사 방향으로 사용합니다. 애니메이션이 Muzzle을 위·아래·옆으로 돌려도
        // 실제 탄환과 산탄 판정은 플레이어 정면에서 벗어나지 않습니다.
        Vector3 direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        direction = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.forward;
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
        // 총구 섬광은 실제 무기의 Muzzle 위치와 회전을 따라갑니다. 이 섬광은 총열에서만 보이는 장식이므로
        // 애니메이션을 그대로 따르되, 실제 투사체와 공격 판정은 위에서 계산한 공통 FirePoint·플레이어 정면을 사용합니다.
        // VFX 연결 정보가 없는 예전 테스트 프리팹에서는 총구 연출만 건너뛰고 공격은 계속됩니다.
        vfxBinding?.PlayMuzzle(spawnPosition, direction, status.GunnerAttackRange);

        WBH_PlayerEffectCue hitCue = resolvedWeapon switch
        {
            GunnerWeaponType.Rifle
                => WBH_PlayerEffectCue.G_normal0_evo0_etc0,

            GunnerWeaponType.Shotgun
                => WBH_PlayerEffectCue.G_normal0_evo0_etc1,

            GunnerWeaponType.GrenadeLauncher
                => WBH_PlayerEffectCue.G_normal0_evo0_etc2,

            _ => WBH_PlayerEffectCue.None
        };

        WBH_EffectData effectData = null;

        if (effect != null && hitCue != WBH_PlayerEffectCue.None)
            effect.TryGetEffectData(hitCue, out effectData);

        // 투사체용 데미지 요청 생성. ElementType은 현재 장착 무기에 인챈트된 속성을 그대로 사용한다.
        // 부여할 상태이상도 같은 속성에서 뽑아 쓰므로, 무속성 무기는 상태이상 없이 순수 피해만 준다.
        ItemSystem.ElementType element = status.CurrentElement;

        WBH_DamageRequest request = CreateDamageRequest(WBH_AttackType.Normal,
                                                        element,
                                                        basicAttackMult * GunnerBasicDamageMultiplier(resolvedWeapon),
                                                        GetElementStatusEffect(element),
                                                        effectData: effectData);

        // SW 추가:
        // 기존 무기별 발사 경로에 실제 총구 위치와 장착 외형을 전달합니다.
        // SW 수정 : 라이플 피해 배율은 위 요청에, 반물질 랜스의 관통은 투사체 적중 경로에 적용합니다.
        switch(resolvedWeapon)
        {
            case GunnerWeaponType.Rifle:
                // SW 추가:
                // 일반 라이플은 기존 Normal 풀 투사체를 사용하며, 반물질 랜스는 이동 구간의 관통 대상을 별도로 판정합니다.
                // 마지막 두 인수는 해당 장착 무기의 비행/명중 외형이며 실제 전투 수치에는 관여하지 않습니다.
                projectileSpawner.FireProjectile(ProjectileType.Normal,
                                                 spawnPosition,
                                                 direction,
                                                 request,
                                                 status.GunnerBulletSpeed,
                                                 status.GunnerAttackRange,
                                                 enemyLayer,
                                                 projectileVisual,
                                                 impactVisual);
                break;
            case GunnerWeaponType.Shotgun:
                {
                    // SW 수정: 실제 산탄 실행 공간을 피해 대상 검색 전에 기록해 빗나간 사격도 잔향으로 재생한다.
                    GetComponent<PlayerContext>()?.Effects.ReserveEchoReplay(request.AttackId, spawnPosition,
                        direction, status.GunnerAttackRange, 90f);
                    // SW 추가:
                    // WBH 샷건은 여러 물리 탄환이 아니라 90도 부채꼴 안의 대상에게 즉시 피해를 줍니다.
                    // 따라서 명중 VFX도 중앙 투사체가 나중에 충돌할 때가 아니라 실제 피해를 받은 각 대상 위치에서 바로 재생합니다.
                    SectorAttack(status.GunnerAttackRange,
                                 90f,
                                 effectData: effectData,
                                 impactVisualPrefab: impactVisual,
                                 attackOrigin: spawnPosition,
                                 attackForward: direction,
                                 attackId: request.AttackId,
                                 shotgunAttack: true);
                    // SW 추가:
                    // 산탄총은 현재 사거리의 90도 부채꼴을 공격하고 실제 피해 대상 위치에서 명중 VFX를 재생합니다.
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
                    projectileSpawner.FireGrenade(ProjectileType.Grenade,
                                                  spawnPosition,
                                                  grenadePoint,
                                                  request,
                                                  status.GunnerBulletSpeed,
                                                  status.GunnerAttackRange, 
                                                  explosionRadius,
                                                  enemyLayer,
                                                  basicGrenadeEffect,
                                                  projectileVisual,
                                                  impactVisual);
                }
                break;
        }



        // -- 즉발 공격 시 사용할 예비 코드
        //Ray ray = new Ray(transform.position + Vector3.up, transform.forward)s;

        //if (Physics.Raycast(ray, out RaycastHit hit, gunnerAttackRange, enemyLayer))
        //{
        //    if (hit.collider.TryGetComponent<T_IDamageable>(out var damageable))
        //    {
        //        damageable.TakeDamage(gunnerAttackDamage);
        //    }
        //}
    }


    /// <summary>SW 수정: 싱글의 실제 Fighter/Shotgun 기본 공격 대상·원점·적중점을 확정하고 해당 무기 효과 출처를 피해 처리 범위 안에서 유지한다. Shotgun은 서버와 같은 벽 검사를 적용한다.</summary>
    // SW 추가 : 거너 샷건은 전용 명중 외형, 공통 FirePoint와 플레이어 정면을 전달합니다.
    // Fighter는 선택 인수를 넘기지 않으므로 기존 외형과 캐릭터 중심·정면 판정을 유지합니다.
    private void SectorAttack(float range,
                              float angle,
                              WBH_EffectData effectData = null,
                              GameObject impactVisualPrefab = null,
                              Vector3? attackOrigin = null,
                              Vector3? attackForward = null,
                              uint attackId = 0,
                              bool shotgunAttack = false)
    {
        if (attackId == 0)
            attackId = CreateAttackId();

        Vector3 origin = attackOrigin ?? transform.position;
        Vector3 forward = attackForward ?? transform.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        Collider[] targets = Physics.OverlapSphere(origin, range, enemyLayer);

        // 한 번의 공격 안에서는 인챈트 속성이 바뀌지 않으므로 루프 밖에서 한 번만 조회한다.
        ItemSystem.ElementType element = status.CurrentElement;
        WBH_StatusEffectData? elementStatusEffect = GetElementStatusEffect(element);

        // SW 수정: 피해 적용 전에 직접 대상과 실제 원점 기준 피격점을 확정해 연쇄 제외·중복 제거·근거리 조건에 공유합니다.
        var directTargets = new System.Collections.Generic.Dictionary<WBH_ICombat, Vector3>();
        foreach (Collider target in targets)
        {
            Vector3 hitPosition = target.ClosestPoint(origin);
            Vector3 dirToTarget = (shotgunAttack ? hitPosition : target.transform.position) - origin;
            dirToTarget.y = 0;
            float targetAngle = Vector3.Angle(forward, dirToTarget);

            if (targetAngle > angle * 0.5f)
                continue;

            WBH_ICombat combatTarget = PlayerCombatAuthority.FindCombatTarget(target);
            if (combatTarget == null || (shotgunAttack &&
                (combatTarget.Status == null || combatTarget.Status.IsDead ||
                 Physics.Linecast(origin, hitPosition, LayerMask.GetMask("Wall", "Prop", "Ground"), QueryTriggerInteraction.Ignore))))
                continue;
            if (directTargets.TryGetValue(combatTarget, out Vector3 previous) &&
                (!shotgunAttack || (previous - origin).sqrMagnitude <= (hitPosition - origin).sqrMagnitude))
                continue;
            directTargets[combatTarget] = hitPosition;
        }

        var orderedTargets = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<WBH_ICombat, Vector3>>(directTargets);
        if (shotgunAttack)
            orderedTargets.Sort((left, right) =>
            {
                int byDistance = (left.Value - origin).sqrMagnitude.CompareTo((right.Value - origin).sqrMagnitude);
                return byDistance != 0 ? byDistance : ((Component)left.Key).GetInstanceID().CompareTo(((Component)right.Key).GetInstanceID());
            });
        PlayerContext context = GetComponent<PlayerContext>();
        // SW 수정: 실제 클래스·발사 경로별 출처를 구분해 Gunner Shotgun이 Fighter 효과를 발동하지 않게 한다.
        context?.Effects.SetDirectTargets(attackId, directTargets.Keys, forward, playerClass == PlayerClass.Fighter,
            origin, shotgunAttack);
        try
        {
            foreach (var pair in orderedTargets)
            {
                WBH_ICombat combatTarget = pair.Key;
                Vector3 hitPosition = pair.Value;
                Vector3 dirToTarget = (hitPosition - origin).normalized;
                dirToTarget.y = 0;

                Vector3 lookDirection = origin - hitPosition;

                if (lookDirection.sqrMagnitude <= 0.0001f)
                    lookDirection = -forward;

                WBH_DamageRequest request = CreateDamageRequest(combatTarget,
                                                            WBH_AttackType.Normal,
                                                            element,
                                                            basicAttackMult,
                                                            statusEffect: elementStatusEffect,
                                                            effectData: effectData,
                                                            hitPosition: hitPosition,
                                                            hitEffectDirection: lookDirection,
                                                            attackId: attackId);

                WBH_CombatManager.ProcessDamage(request);

                // SW 추가:
                // SectorAttack은 투사체 충돌보다 먼저 실제 피해를 처리합니다. 같은 프레임에 실제 피해 대상 위치에서 명중 VFX를
                // 재생해야 화면과 판정이 어긋나지 않습니다. -dirToTarget은 탄환이 날아온 반대쪽인 피격면 바깥 방향입니다.
                GunnerVfxPlayback.SpawnTransient(
                    impactVisualPrefab,
                    hitPosition,
                    -dirToTarget);
            }
        }
        finally
        {
            // SW 수정: 예외가 나도 끝난 공격의 정면·무기 효과를 다음 피해 요청에 남기지 않는다.
            context?.Effects.SetDirectTargets(0, null);
        }
    }

    /// <summary>
    /// 인챈트 속성에 대응하는 기본 공격 상태이상을 돌려준다. 무속성(None)이면 null이라 상태이상이 붙지 않는다.
    /// 예전에는 호출부에 Burn1/Slow1이 고정으로 박혀 있어서, 인챈트가 없거나 무기를 끼지 않아도
    /// 거너 기본 공격은 항상 화상, 파이터 근접과 산탄은 항상 둔화를 걸었다.
    /// 상태이상 수치 자체는 WBH_StatusEffectPresets가 소유하므로 밸런스 조정은 그쪽에서 한다.
    /// </summary>
    private static WBH_StatusEffectData? GetElementStatusEffect(ItemSystem.ElementType elementType)
    {
        switch (elementType)
        {
            case ItemSystem.ElementType.Fire:
                return WBH_StatusEffectPresets.Burn1;      // 화상 : 5초동안 1초마다 최대 체력의 1% 감소

            case ItemSystem.ElementType.Ice:
                return WBH_StatusEffectPresets.Freeze1;    // 동상 : 5초동안 공격속도, 이동속도 50% 감소

            case ItemSystem.ElementType.Electric:
                return WBH_StatusEffectPresets.Electric1;  // 감전 : 5초동안 공격력 50% 감소

            default:
                return null;                               // 무속성 - 상태이상 없이 피해만
        }
    }

    // 투사체 외
    public WBH_DamageRequest CreateDamageRequest(WBH_ICombat target,
                                                 WBH_AttackType atkType,
                                                 ItemSystem.ElementType elementType,
                                                 float damageMult,
                                                 WBH_StatusEffectData? statusEffect = null,
                                                 WBH_EffectData effectData = null,
                                                 Vector3? hitPosition = null,
                                                 Vector3? hitEffectDirection = null,
                                                 DamageCause? damageCause = null,
                                                 uint attackId = 0)
    {
        if (attackId == 0)
            attackId = CreateAttackId();

        return new WBH_DamageRequest(controller,
                                     target,
                                     atkType, 
                                     elementType,
                                     damageMult, 
                                     statusEffect, 
                                     effectData, 
                                     hitPosition, 
                                     hitEffectDirection,
                                     damageCause,
                                     attackId);
    }

    // 투사체는 타겟이 충돌 시 결정되기에 null 로 비워둠.
    public WBH_DamageRequest CreateDamageRequest(WBH_AttackType atkType,
                                                 ItemSystem.ElementType elementType,
                                                 float damageMult,
                                                 WBH_StatusEffectData? statusEffect = null,
                                                 WBH_EffectData effectData = null,
                                                 Vector3? hitPosition = null,
                                                 Vector3? hitEffectDirection = null,
                                                 DamageCause? damageCause = null,
                                                 uint attackId = 0)
    {
        if (attackId == 0)
            attackId = CreateAttackId();

        return new WBH_DamageRequest(controller,
                                     null, 
                                     atkType,
                                     elementType, 
                                     damageMult, 
                                     statusEffect, 
                                     effectData, 
                                     hitPosition, 
                                     hitEffectDirection,
                                     damageCause,
                                     attackId);
    }

    /// <summary>SW 수정: 0을 예약값으로 남기고 순차 공격 ID를 생성합니다.</summary>
    public uint CreateAttackId()
    {
        nextAttackId++;
        if (nextAttackId == 0)
            nextAttackId++;
        return nextAttackId;
    }

    /// <summary>SW 수정: 추적 대상이 사거리에 들어오면 추적을 끝내고, 직접 조준과 같은 공격 요청 경로를 사용합니다.</summary>
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
            RequestAttack(attackPos);
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

        lastChaseDestination = destination;
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
    /// <summary>SW 수정: 추적 완료와 직접 조준의 공격 의도를 같은 서버 요청으로 연결한다.</summary>
    public void BindAttackRequest(System.Func<Vector3, bool> attackRequest) => externalAttackRequest = attackRequest;

    /// <summary>SW 수정: 연결된 권한에 공격을 요청한다. 싱글에서는 기존 공격 실행을 사용한다.</summary>
    public void RequestAttack(Vector3 targetPosition)
    {
        if (externalAttackRequest != null)
            externalAttackRequest(targetPosition);
        else
            TryAttack(targetPosition);
    }

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

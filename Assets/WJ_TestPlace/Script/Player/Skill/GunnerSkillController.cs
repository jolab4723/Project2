using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 거너 액티브 스킬(Skill1~3, 키는 A/S/D) 실행을 담당한다. FighterSkillController와 같은 입력 이벤트
/// (PlayerActionInputHandler.OnSkillKeyPressed)를 구독하고, ISkillController를 구현해서 같은 진화/강화
/// 선택 UI(SkillEvolutionSelectUI)와 쿨타임 HUD(KY_SkillView)를 그대로 재사용한다.
///
/// 파이터의 부채꼴/직선 판정 셰이프(SkillShapeType.SectorSlash/LineSlam)를 그대로 재사용한다 - 셰이프
/// 자체는 근접 전용이 아니라 "부채꼴/직선 범위 안의 대상 찾기"라는 순수 판정 로직이라, 사거리를 늘리고
/// 투사체 없이 판정 순간 즉시 명중(히트스캔)시키면 원거리 스킬로도 그대로 쓸 수 있다.
///
/// !! 프로젝트 전체에서 WBH_ProjectileSpawner.poolManager가 어디에도 연결돼 있지 않다(적 프리팹도
///    마찬가지) - 투사체 풀링 시스템 자체가 아직 미완성이라, 실제 투사체를 날리는 대신 즉시 명중하는
///    히트스캔 방식으로 만들었다. 나중에 투사체 시스템이 완성되면 시각 효과만 투사체로 바꿀 수 있다
///    (판정 타이밍/데미지 로직은 그대로 두고 SkillRangeVisual 대신 실제 발사체를 스폰하면 됨).
///
/// 진화(evolution)는 슬롯마다 선택 가능(UI에 뜸)하고, 아크 버스터(Skill1)는 진화1(아크 레이저)/진화2
/// (트리플 슈팅)까지 구현됐다(115번) - 진화3은 아직 미정. 폭탄 투척/백스탭 샷(Skill2~3)은 아직 진화별
/// 효과가 없다 - 파이터도 처음엔 진화 없이 스킬 3개만 만들고(63번) 나중에 하나씩 만들었던 것과 같은
/// 순서(66번 이후). 강화(enhancement)는 파이터와 완전히 동일한 방식(위력/쿨타임감소/범위)으로 스킬
/// 3개 전부 이미 실제 효과가 있다.
/// </summary>
public class GunnerSkillController : MonoBehaviour, ISkillController
{
    [SerializeField] private PlayerActionInputHandler inputHandler;
    [SerializeField] private T_PlayerCombat combat;
    [SerializeField] private T_PlayerController controller;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;
    [SerializeField] private WBH_PlayerStatus status;
    [SerializeField] private PlayerBuffManager buffManager;
    [SerializeField] private LayerMask enemyLayer;

    [Tooltip("ArcProjectile(아크 버스터) 발사 시작 위치. 비워두면 transform.position을 쓴다.")]
    [SerializeField] private Transform firePoint;

    [Tooltip("인덱스 0~2 = Skill1~3(A/S/D)")]
    [SerializeField] private SkillDefinitionSO[] skills = new SkillDefinitionSO[3];

    [Tooltip("스킬 슬롯별 진화 선택(None = 미진화). 아직 진화별 효과는 없고 선택 상태만 저장/표시된다.")]
    [SerializeField] private SkillEvolutionId[] activeEvolutions = new SkillEvolutionId[3];

    [Tooltip("스킬 슬롯별 강화 선택(None = 미강화). 파이터와 동일하게 실제 효과가 있다.")]
    [SerializeField] private SkillEnhancementId[] activeEnhancements = new SkillEnhancementId[3];

    [Header("범위 표시(피드백용, 판정과 무관)")]
    [SerializeField] private Color sectorVisualColor = new Color(0.2f, 0.6f, 1f, 0.35f);
    [SerializeField] private Color lineVisualColor = new Color(0.2f, 0.9f, 1f, 0.35f);
    [SerializeField] private Color dashVisualColor = new Color(0.2f, 0.7f, 1f, 0.35f);

    private readonly float[] cooldownRemaining = new float[3];

    // 아크 버스터(ArcProjectile) 전용 스택 상태. -1 = 아직 초기화 안 됨(Start에서 maxStacks로 채움).
    // FighterSkillController의 대시 2스택(진화 전용)과 달리, 이건 기본 스킬 자체가 스택형이라
    // 슬롯 인덱스에 매이지 않고 "지금 스킬 중 ArcProjectile인 것"을 찾아서 적용한다.
    private int arcBusterStacks = -1;
    private float arcBusterStackTimer;

    private bool CanUseSkill => !stateMachine.IsAnyState(PlayerState.Hit, PlayerState.Attack,
        PlayerState.Skill, PlayerState.Dodge, PlayerState.Dead);

    private void OnEnable()
    {
        if (inputHandler != null)
            inputHandler.OnSkillKeyPressed += HandleSkillKeyPressed;
    }

    private void OnDisable()
    {
        if (inputHandler != null)
            inputHandler.OnSkillKeyPressed -= HandleSkillKeyPressed;
    }

    private void Start()
    {
        for (int i = 0; i < skills.Length; i++)
        {
            if (IsArcBusterSlot(i))
                arcBusterStacks = skills[i].maxStacks;
        }
    }

    private void Update()
    {
        for (int i = 0; i < cooldownRemaining.Length; i++)
        {
            if (cooldownRemaining[i] > 0f)
                cooldownRemaining[i] -= Time.deltaTime;
        }

        UpdateArcBusterRecharge();
    }

    private bool IsArcBusterSlot(int index)
    {
        SkillDefinitionSO def = GetSkillDefinition(index);
        return def != null && def.shapeType == SkillShapeType.ArcProjectile;
    }

    private int FindArcBusterSlotIndex()
    {
        for (int i = 0; i < skills.Length; i++)
        {
            if (IsArcBusterSlot(i))
                return i;
        }
        return -1;
    }

    /// <summary>FighterSkillController.UpdateDashStackRecharge와 같은 패턴 - 스택이 최대치 미만이면 계속 충전한다.</summary>
    private void UpdateArcBusterRecharge()
    {
        if (arcBusterStacks < 0 || arcBusterStackTimer <= 0f)
            return;

        int index = FindArcBusterSlotIndex();
        SkillDefinitionSO def = index >= 0 ? skills[index] : null;
        if (def == null)
            return;

        arcBusterStackTimer -= Time.deltaTime;
        if (arcBusterStackTimer > 0f)
            return;

        arcBusterStacks = Mathf.Min(arcBusterStacks + 1, def.maxStacks);
        if (arcBusterStacks < def.maxStacks)
            arcBusterStackTimer = ApplyCooldownEnhancement(def, index, def.stackRechargeSeconds);
    }

    private void HandleSkillKeyPressed(int index) => TryUseSkill(index);

    /// <summary>슬롯(0~2)의 스킬 데이터. UI가 이름/모양 등을 표시할 때 사용.</summary>
    public SkillDefinitionSO GetSkillDefinition(int index) =>
        index >= 0 && index < skills.Length ? skills[index] : null;

    public int SkillCount => skills.Length;

    /// <summary>인덱스(0~2 = Skill1~3)에 해당하는 스킬을 사용한다. 쿨타임 중이거나 행동 불가 상태면 조용히 실패.</summary>
    public bool TryUseSkill(int index)
    {
        if (index < 0 || index >= skills.Length)
            return false;

        SkillDefinitionSO def = skills[index];
        if (def == null || !CanUseSkill || !IsSkillReady(index))
            return false;

        // 아크 레이저(진화1)가 "현재 스택을 모두" 소모하므로, ConsumeSkillUse가 스택을 지우기 전에
        // 몇 스택을 들고 있었는지 먼저 캡처해서 데미지 계산(소모 스택당 보너스)에 넘겨준다.
        int arcBusterStacksBeforeConsume = arcBusterStacks;

        ConsumeSkillUse(index, def);
        FaceCursor();
        combat.CancelChase();
        stateMachine.ChangeState(PlayerState.Skill);

        SkillEvolutionId evo = GetEvolution(index);

        switch (def.shapeType)
        {
            case SkillShapeType.SectorSlash:
                ExecuteSectorShot(def, index);
                StartCoroutine(ReturnToIdleAfter(0.3f));
                break;

            case SkillShapeType.LineSlam:
                ExecuteLineShot(def, index);
                StartCoroutine(ReturnToIdleAfter(0.3f));
                break;

            case SkillShapeType.Dash:
                StartCoroutine(ExecuteDash(def, index));
                break;

            case SkillShapeType.ArcProjectile:
                if (evo == SkillEvolutionId.Evolution1)
                    ExecuteArcLaser(def, index, arcBusterStacksBeforeConsume);
                else if (evo == SkillEvolutionId.Evolution2)
                    StartCoroutine(ExecuteTripleShot(def, index));
                else
                    ExecuteArcBuster(def, index);
                StartCoroutine(ReturnToIdleAfter(0.2f));
                break;

            case SkillShapeType.BombThrow:
                ExecuteBombThrow(def, index);
                StartCoroutine(ReturnToIdleAfter(0.3f));
                break;

            case SkillShapeType.BackstepShot:
                StartCoroutine(ExecuteBackstepShot(def, index));
                break;
        }

        return true;
    }

    public SkillEvolutionId GetEvolution(int index) =>
        index >= 0 && index < activeEvolutions.Length ? activeEvolutions[index] : SkillEvolutionId.None;

    public void SetEvolution(int index, SkillEvolutionId evolution)
    {
        if (index < 0 || index >= activeEvolutions.Length)
            return;

        activeEvolutions[index] = evolution;
    }

    public SkillEnhancementId GetEnhancement(int index) =>
        index >= 0 && index < activeEnhancements.Length ? activeEnhancements[index] : SkillEnhancementId.None;

    public void SetEnhancement(int index, SkillEnhancementId enhancement)
    {
        if (index < 0 || index >= activeEnhancements.Length)
            return;

        activeEnhancements[index] = enhancement;
    }

    /// <summary>남은 쿨타임(초). 준비됐으면 0. 아크 버스터(스택형)는 연사 제한(1초, cooldownRemaining)은
    /// UI에 표시하지 않고 스택 충전 시간(4초)만 보여준다 - 연사 제한은 너무 짧아서 UI로는 의미가 없고,
    /// 사용자가 "다음 탄까지의 1초가 아니라 스택 충전 4초를 보여달라"고 명시적으로 요청함(114번).
    /// FighterSkillController.GetRemainingCooldown(대시 2스택)과 동일한 공식 - 스택이 1개 이상 있어도
    /// 최대치 미만이면 계속 충전 중이므로 dashStacks처럼 ">= max"로 판정해야 한다(105번에서 파이터 쪽에
    /// 이미 한 번 고친 것과 같은 종류의 버그라 여기도 같은 공식으로 맞춤).</summary>
    public float GetRemainingCooldown(int index)
    {
        if (index < 0 || index >= cooldownRemaining.Length)
            return 0f;

        if (IsArcBusterSlot(index))
        {
            SkillDefinitionSO def = GetSkillDefinition(index);
            int max = def != null ? def.maxStacks : 0;
            return arcBusterStacks >= max ? 0f : Mathf.Max(0f, arcBusterStackTimer);
        }

        return Mathf.Max(0f, cooldownRemaining[index]);
    }

    /// <summary>지금 적용 중인 최대 쿨타임(초, 강화로 감소됐으면 그 값). 아크 버스터는 스택 충전 시간 기준.</summary>
    public float GetEffectiveCooldown(int index)
    {
        SkillDefinitionSO def = GetSkillDefinition(index);
        if (def == null)
            return 0f;

        float baseCooldown = IsArcBusterSlot(index) ? def.stackRechargeSeconds : def.cooldownSeconds;
        return ApplyCooldownEnhancement(def, index, baseCooldown);
    }

    /// <summary>아크 버스터(ArcProjectile)면 현재/최대 스택을 낸다. 그 외 스킬은 스택 개념이 없어서 false.</summary>
    public bool TryGetStackInfo(int index, out int current, out int max)
    {
        current = 0;
        max = 0;

        if (!IsArcBusterSlot(index))
            return false;

        SkillDefinitionSO def = GetSkillDefinition(index);
        current = arcBusterStacks;
        max = def != null ? def.maxStacks : 0;
        return true;
    }

    private bool IsSkillReady(int index)
    {
        if (IsArcBusterSlot(index))
            return arcBusterStacks > 0 && cooldownRemaining[index] <= 0f;

        return cooldownRemaining[index] <= 0f;
    }

    private void ConsumeSkillUse(int index, SkillDefinitionSO def)
    {
        if (IsArcBusterSlot(index))
        {
            if (GetEvolution(index) == SkillEvolutionId.Evolution1)
                arcBusterStacks = 0; // 아크 레이저 - "현재의 모든 스택을 소모"
            else
                arcBusterStacks--;

            cooldownRemaining[index] = def.cooldownSeconds; // 연사 제한(1초) - 강화(쿨타임감소)와는 별개 개념이라 안 줄임
            if (arcBusterStackTimer <= 0f)
                arcBusterStackTimer = ApplyCooldownEnhancement(def, index, def.stackRechargeSeconds);
            return;
        }

        cooldownRemaining[index] = ApplyCooldownEnhancement(def, index, def.cooldownSeconds);
    }

    /// <summary>강화(Enhance2: 쿨타임 감소)가 선택돼 있으면 쿨타임을 줄인다. FighterSkillController와 동일한 공식.</summary>
    private float ApplyCooldownEnhancement(SkillDefinitionSO def, int index, float baseCooldown)
    {
        if (GetEnhancement(index) != SkillEnhancementId.Enhance2)
            return baseCooldown;

        return baseCooldown * (1f - def.enhanceCooldownReductionPercent / 100f);
    }

    /// <summary>PlayerStatManager의 "스킬 범위" 스탯 + 강화(Enhance3: 범위 강화)만큼 기본 판정 거리를 늘린다.
    /// FighterSkillController.ApplySkillRangeBonus와 완전히 동일한 공식(95~98번에서 검증됨).</summary>
    private float ApplySkillRangeBonus(SkillDefinitionSO def, int index, float baseRange)
    {
        float flatBonus = 0f;
        float percentBonus = 0f;
        if (PlayerStatManager.Instance != null)
            PlayerStatManager.Instance.GetSkillRangeBonus(out flatBonus, out percentBonus);

        if (GetEnhancement(index) == SkillEnhancementId.Enhance3)
            percentBonus += def.enhanceRangeBonusPercent;

        return (baseRange + flatBonus) * (1f + percentBonus / 100f);
    }

    /// <summary>부채꼴 범위 안의 적 콜라이더 목록 - FighterSkillController.GetSectorTargets와 동일한 방식.</summary>
    private List<Collider> GetSectorTargets(float range, float angle)
    {
        var result = new List<Collider>();
        Collider[] candidates = Physics.OverlapSphere(transform.position, range, enemyLayer);

        foreach (Collider c in candidates)
        {
            Vector3 dirToTarget = (c.transform.position - transform.position).normalized;
            dirToTarget.y = 0f;
            if (Vector3.Angle(transform.forward, dirToTarget) <= angle * 0.5f)
                result.Add(c);
        }

        return result;
    }

    /// <summary>정면 직선(사각형) 범위 안의 적 콜라이더 목록.</summary>
    private List<Collider> GetLineTargets(float length, float width)
    {
        Vector3 center = transform.position + transform.forward * (length * 0.5f);
        Vector3 halfExtents = new Vector3(width * 0.5f, 1.5f, length * 0.5f);
        Collider[] candidates = Physics.OverlapBox(center, halfExtents, transform.rotation, enemyLayer);
        return new List<Collider>(candidates);
    }

    /// <summary>확산 사격 - 정면 부채꼴 범위를 즉시 명중시킨다(투사체 없음, 위 클래스 주석 참고).</summary>
    private void ExecuteSectorShot(SkillDefinitionSO def, int index)
    {
        float range = ApplySkillRangeBonus(def, index, def.sectorRange);
        SkillRangeVisual.ShowSector(transform.position, transform.forward, range, def.sectorAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(range, def.sectorAngle))
            ApplyHit(target, def, def.damageMultiplier, index);
    }

    /// <summary>관통 사격 - 정면 직선 범위를 즉시 명중시킨다(투사체 없음, 위 클래스 주석 참고).</summary>
    private void ExecuteLineShot(SkillDefinitionSO def, int index)
    {
        float length = ApplySkillRangeBonus(def, index, def.lineLength);
        SkillRangeVisual.ShowLine(transform.position, transform.forward, length, def.lineWidth, lineVisualColor);

        foreach (Collider target in GetLineTargets(length, def.lineWidth))
            ApplyHit(target, def, def.damageMultiplier, index);
    }

    /// <summary>
    /// 아크 버스터 - 정면으로 GunnerArcProjectile을 발사한다(직선 이동, 적 접촉 시 관통 없이 그 자리에서
    /// explosionRadius 범위 폭발). 강화(Enhance1: 위력 강화)는 데미지에, Enhance3(범위 강화)는
    /// projectileMaxDistance에 다른 스킬과 동일하게 적용된다.
    /// </summary>
    private void ExecuteArcBuster(SkillDefinitionSO def, int index)
    {
        if (def.arcProjectilePrefab == null)
        {
            Debug.LogWarning("[GunnerSkillController] arcProjectilePrefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Vector3 dir = transform.forward;
        float maxDistance = ApplySkillRangeBonus(def, index, def.projectileMaxDistance);

        float damageMultiplier = def.damageMultiplier;
        if (GetEnhancement(index) == SkillEnhancementId.Enhance1)
            damageMultiplier *= 1f + def.enhanceDamageMultiplierBonusPercent / 100f;

        WBH_DamageRequest request = combat.CreateDamageRequest(WBH_AttackType.Skill, status.CurrentElement, damageMultiplier);

        GameObject projectileGO = Instantiate(def.arcProjectilePrefab, spawnPos, Quaternion.LookRotation(dir));
        GunnerArcProjectile projectile = projectileGO.GetComponent<GunnerArcProjectile>();
        if (projectile == null)
        {
            Debug.LogWarning("[GunnerSkillController] arcProjectilePrefab에 GunnerArcProjectile 컴포넌트가 없습니다.");
            Destroy(projectileGO);
            return;
        }

        projectile.Initialize(dir, def.projectileSpeed, maxDistance, def.explosionRadius, enemyLayer, request);
    }

    /// <summary>
    /// 진화1: 아크 레이저 - 현재 스택을 전부 소모해서(ConsumeSkillUse에서 이미 처리) 직선(evoLaserLength x
    /// evoLaserWidth) 범위를 즉시 명중시킨다. 투사체 없이 판정 즉시 명중(다른 거너 스킬의 히트스캔 판정과
    /// 동일한 방식 - GetLineTargets 재사용). 소모한 스택마다(최대 evoLaserMaxBonusStacks) 피해 배율에
    /// evoLaserDamagePerStackPercent%씩 곱연산으로 증가한다.
    /// </summary>
    private void ExecuteArcLaser(SkillDefinitionSO def, int index, int consumedStacks)
    {
        float length = ApplySkillRangeBonus(def, index, def.evoLaserLength);
        SkillRangeVisual.ShowLine(transform.position, transform.forward, length, def.evoLaserWidth, lineVisualColor);

        int bonusStacks = Mathf.Min(consumedStacks, def.evoLaserMaxBonusStacks);
        float damageMultiplier = def.damageMultiplier * (1f + def.evoLaserDamagePerStackPercent / 100f * bonusStacks);

        foreach (Collider target in GetLineTargets(length, def.evoLaserWidth))
            ApplyHit(target, def, damageMultiplier, index);
    }

    /// <summary>
    /// 진화2: 트리플 슈팅 - 같은 스택 1개(ConsumeSkillUse에서 이미 1 차감)로 GunnerArcProjectile을
    /// evoTripleShotInterval 간격으로 3번 연달아 발사한다. 발당 피해 배율은 기본 damageMultiplier 대신
    /// evoTripleShotDamageMultiplier를 그대로 쓴다. 기본 아크 버스터(ExecuteArcBuster)와 발사 로직은
    /// 동일하지만, 폭발 속성은 뺐다(사용자 요청 - 118번) - explodeOnHit=false로 넘겨서 맞은 대상
    /// 하나에게만 데미지가 들어가고 explosionRadius 범위 판정은 하지 않는다.
    /// </summary>
    private IEnumerator ExecuteTripleShot(SkillDefinitionSO def, int index)
    {
        if (def.arcProjectilePrefab == null)
        {
            Debug.LogWarning("[GunnerSkillController] arcProjectilePrefab이 연결되지 않았습니다.");
            yield break;
        }

        float maxDistance = ApplySkillRangeBonus(def, index, def.projectileMaxDistance);

        float damageMultiplier = def.evoTripleShotDamageMultiplier;
        if (GetEnhancement(index) == SkillEnhancementId.Enhance1)
            damageMultiplier *= 1f + def.enhanceDamageMultiplierBonusPercent / 100f;

        for (int shot = 0; shot < 3; shot++)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Vector3 dir = transform.forward;

            WBH_DamageRequest request = combat.CreateDamageRequest(WBH_AttackType.Skill, status.CurrentElement, damageMultiplier);

            GameObject projectileGO = Instantiate(def.arcProjectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            GunnerArcProjectile projectile = projectileGO.GetComponent<GunnerArcProjectile>();
            if (projectile == null)
            {
                Debug.LogWarning("[GunnerSkillController] arcProjectilePrefab에 GunnerArcProjectile 컴포넌트가 없습니다.");
                Destroy(projectileGO);
                yield break;
            }

            projectile.Initialize(dir, def.projectileSpeed, maxDistance, 0f, enemyLayer, request, explodeOnHit: false);

            if (shot < 2)
                yield return new WaitForSeconds(def.evoTripleShotInterval);
        }
    }

    /// <summary>
    /// 폭탄 투척 - 플레이어 기준 bombThrowRange 이내로 clamp한 커서 위치로 GunnerBomb을 포물선 투척한다.
    /// 적 접촉 또는 착지 후 bombFuseSeconds 경과 시 폭발 반경만큼 폭발(GunnerBomb 내부 처리).
    /// 강화(Enhance1: 위력 강화)는 데미지에, Enhance3(범위 강화)는 bombThrowRange(던질 수 있는 거리)에
    /// 다른 스킬과 동일하게 적용된다.
    ///
    /// 진화1(집속 폭탄)은 첫 폭발 후 지연 2차 폭발을, 진화2(에너지 폭발)는 확대된 폭발 반경 + 기절 +
    /// 슬로우 영역을, 진화3(글리터 폭탄)은 마커(받는 데미지 증가) 부여를 GunnerBomb에 추가 파라미터로
    /// 넘겨서 처리한다(118번).
    /// </summary>
    private void ExecuteBombThrow(SkillDefinitionSO def, int index)
    {
        if (def.bombPrefab == null)
        {
            Debug.LogWarning("[GunnerSkillController] bombPrefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

        Vector3 toCursor = GetCursorGroundPosition() - transform.position;
        toCursor.y = 0f;

        float throwRange = ApplySkillRangeBonus(def, index, def.bombThrowRange);
        Vector3 targetPos = toCursor.sqrMagnitude > throwRange * throwRange
            ? transform.position + toCursor.normalized * throwRange
            : transform.position + toCursor;
        targetPos.y = transform.position.y;

        SkillEvolutionId evo = GetEvolution(index);
        float explosionRadius = evo == SkillEvolutionId.Evolution2 ? def.evoEnergyBurstRadius : def.bombExplosionRadius;

        // 폭발 반경 표시 - 원형이라 angle=360으로 ShowSector 재사용, forward는 원이라 무의미.
        // 표시 시간은 폭탄이 실제로 위협인 구간(비행 시간 + 퓨즈)만큼 - GunnerBomb.Initialize의 travelTime 계산과 동일.
        float showDuration = Mathf.Max(0.2f, Vector3.Distance(spawnPos, targetPos) / def.bombThrowSpeed) + def.bombFuseSeconds;
        SkillRangeVisual.ShowSector(targetPos, Vector3.forward, explosionRadius, 360f, sectorVisualColor, showDuration);

        float damageMultiplier = def.damageMultiplier;
        if (GetEnhancement(index) == SkillEnhancementId.Enhance1)
            damageMultiplier *= 1f + def.enhanceDamageMultiplierBonusPercent / 100f;

        WBH_DamageRequest request = combat.CreateDamageRequest(WBH_AttackType.Skill, status.CurrentElement, damageMultiplier);

        GameObject bombGO = Instantiate(def.bombPrefab, spawnPos, Quaternion.identity);
        GunnerBomb bomb = bombGO.GetComponent<GunnerBomb>();
        if (bomb == null)
        {
            Debug.LogWarning("[GunnerSkillController] bombPrefab에 GunnerBomb 컴포넌트가 없습니다.");
            Destroy(bombGO);
            return;
        }

        if (evo == SkillEvolutionId.Evolution1) // 집속 폭탄
        {
            bomb.Initialize(targetPos, def.bombThrowSpeed, def.bombArcHeight, def.bombFuseSeconds,
                explosionRadius, enemyLayer, request,
                secondExplosionDelay: def.evoClusterDelaySeconds,
                secondExplosionRadius: def.evoClusterRadius,
                secondExplosionDamageMultiplier: def.evoClusterDamageMultiplier);
        }
        else if (evo == SkillEvolutionId.Evolution2) // 에너지 폭발
        {
            bomb.Initialize(targetPos, def.bombThrowSpeed, def.bombArcHeight, def.bombFuseSeconds,
                explosionRadius, enemyLayer, request,
                stunDurationOnHit: def.evoEnergyBurstStunDuration,
                slowZoneRadius: explosionRadius,
                slowZoneDuration: def.evoEnergyBurstSlowZoneDuration,
                slowSpeedMultiplier: def.evoEnergyBurstSlowMultiplier);
        }
        else if (evo == SkillEvolutionId.Evolution3) // 글리터 폭탄
        {
            bomb.Initialize(targetPos, def.bombThrowSpeed, def.bombArcHeight, def.bombFuseSeconds,
                explosionRadius, enemyLayer, request,
                markDuration: def.evoGlitterMarkDuration,
                markDamageMultiplier: def.evoGlitterMarkedDamageMultiplier);
        }
        else
        {
            bomb.Initialize(targetPos, def.bombThrowSpeed, def.bombArcHeight, def.bombFuseSeconds,
                explosionRadius, enemyLayer, request);
        }
    }

    /// <summary>강화(Enhance1: 위력 강화)가 선택돼 있으면 데미지 계수에 곱해지는 보너스를 곱한다.</summary>
    private void ApplyHit(Collider target, SkillDefinitionSO def, float damageMultiplier, int index)
    {
        if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
            return;

        if (GetEnhancement(index) == SkillEnhancementId.Enhance1)
            damageMultiplier *= 1f + def.enhanceDamageMultiplierBonusPercent / 100f;

        WBH_DamageRequest request = combat.CreateDamageRequest(combatTarget, WBH_AttackType.Skill, status.CurrentElement, damageMultiplier);
        WBH_CombatManager.ProcessDamage(request);
    }

    /// <summary>
    /// 후방 회피 - 커서 방향으로 짧게 대시한다. FighterSkillController.ExecuteDash와 이동 로직은 동일하고,
    /// 무적/스택/피해버프 같은 진화 전용 효과가 아직 없다는 점만 다르다.
    /// 강화(Enhance1: 위력 강화)는 거너도 자체 피해가 없는 이동기라 대신 이동 시간을 줄여 더 빠르게 만든다.
    /// </summary>
    private IEnumerator ExecuteDash(SkillDefinitionSO def, int index)
    {
        Vector3 dir = GetCursorDirection();
        float distance = ApplySkillRangeBonus(def, index, def.dashDistance);
        SkillRangeVisual.ShowLine(transform.position, dir, distance, 0.6f, dashVisualColor);

        NavMeshAgent agent = controller.agent;

        Vector3 targetPos = transform.position + dir * distance;
        if (NavMesh.Raycast(transform.position, targetPos, out NavMeshHit hit, NavMesh.AllAreas))
            targetPos = hit.position;

        float duration = GetEnhancement(index) == SkillEnhancementId.Enhance1
            ? def.dashDuration * (1f - def.enhanceDashSpeedBonusPercent / 100f)
            : def.dashDuration;

        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 next = Vector3.Lerp(start, targetPos, t);
            agent.Move(next - transform.position);
            yield return null;
        }

        agent.Warp(targetPos);

        if (stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    /// <summary>
    /// 백스탭 샷 - 전방(커서 방향) 원뿔 범위를 즉시 명중시킨 뒤, 커서 반대 방향(후방)으로 백스탭 이동한다.
    /// 공격 판정은 SectorSlash용 GetSectorTargets/ApplyHit을 그대로 재사용하고, 이동은 ExecuteDash와 같은
    /// NavMeshAgent 보간 방식을 커서 반대 방향(FaceCursor 적용 후의 -transform.forward)에 적용한다.
    /// 강화(Enhance1: 위력 강화)는 ApplyHit을 통해 데미지에만 적용된다(이동 속도는 안 바뀜 - SectorSlash와
    /// 동일한 해석). 강화(Enhance3: 범위 강화)는 공격 사거리와 이동 거리 양쪽에 각각 적용된다.
    /// </summary>
    private IEnumerator ExecuteBackstepShot(SkillDefinitionSO def, int index)
    {
        float coneRange = ApplySkillRangeBonus(def, index, def.backstepConeRange);
        SkillRangeVisual.ShowSector(transform.position, transform.forward, coneRange, def.backstepConeAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(coneRange, def.backstepConeAngle))
            ApplyHit(target, def, def.damageMultiplier, index);

        Vector3 dir = -transform.forward; // FaceCursor 적용 후라 -forward = 커서 반대 방향(후방)
        float distance = ApplySkillRangeBonus(def, index, def.backstepDistance);
        SkillRangeVisual.ShowLine(transform.position, dir, distance, 0.6f, dashVisualColor);

        NavMeshAgent agent = controller.agent;

        Vector3 targetPos = transform.position + dir * distance;
        if (NavMesh.Raycast(transform.position, targetPos, out NavMeshHit hit, NavMesh.AllAreas))
            targetPos = hit.position;

        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < def.backstepDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / def.backstepDuration);
            Vector3 next = Vector3.Lerp(start, targetPos, t);
            agent.Move(next - transform.position);
            yield return null;
        }

        agent.Warp(targetPos);

        if (stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    private IEnumerator ReturnToIdleAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    private void FaceCursor()
    {
        Vector3 dir = GetCursorDirection();
        if (dir.sqrMagnitude > 0.001f)
            transform.forward = dir;
    }

    /// <summary>마우스 커서가 가리키는 바닥 방향(XZ 평면, 정규화). FighterSkillController.GetCursorDirection과 동일.</summary>
    private Vector3 GetCursorDirection()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);

        if (plane.Raycast(ray, out float distance))
        {
            Vector3 point = ray.GetPoint(distance);
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            return dir.normalized;
        }

        return transform.forward;
    }

    /// <summary>마우스 커서가 가리키는 바닥 위치(월드 좌표, clamp 없음 - 호출부에서 필요한 만큼 clamp).</summary>
    private Vector3 GetCursorGroundPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);

        if (plane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);

        return transform.position + transform.forward;
    }
}

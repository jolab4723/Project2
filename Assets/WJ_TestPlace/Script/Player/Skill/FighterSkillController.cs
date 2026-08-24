using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 파이터 액티브 스킬(Skill1~3, 키는 A/S/D) 실행을 담당한다.
/// PlayerActionInputHandler.OnSkillKeyPressed(이미 있었지만 아무도 구독하지 않던 이벤트)를
/// 구독해서 쿨타임을 확인하고, 스킬 형태(부채꼴/직선/대시)에 맞는 판정과 효과를 적용한다.
///
/// 전용 스킬 애니메이션이 아직 없어서, 기존 공격처럼 애니메이션 이벤트로 타이밍을 맞추는 대신
/// 코루틴으로 "스킬 진행 시간"만큼 PlayerState.Skill을 유지했다가 Idle로 돌아간다 - 기능은
/// 정상 동작하지만 시각적으로는 애니메이션 없이 즉시 판정된다.
///
/// 피해 적용은 T_PlayerCombat.CreateDamageRequest + WBH_CombatManager.ProcessDamage를 그대로
/// 재사용한다 - Attacker가 T_PlayerController여야 OnDamageDealt/OnCrit 아이템 트리거가 제대로
/// 발동하기 때문에(이번 세션에서 이미 확인한 부분) 직접 WBH_DamageRequest를 새로 만들지 않는다.
///
/// 진화(evolutions)는 스킬 슬롯별로 3가지 형태 중 하나를 선택한다(인스펙터 기본값 + K키
/// SkillEvolutionSelectUI로 런타임 변경 가능). 9개 전부 구현 완료됐다 - 다른 팀원 파일(BH)
/// 쪽 공개 API가 필요했던 것들도 각각 작은 public 메서드를 추가해서 해결함: LineSlam 진화1
/// (방어감소+기절)은 MultiplyDefense/DefenseDown 상태이상을, SectorSlash 진화2(투사체 제거)는
/// WBH_Projectile.ForceRemove()를, Dash 진화1(무적)은 T_PlayerController.ApplyInvincibility()를
/// 각각 BH님 파일에 직접 추가했다.
///
/// ISkillController를 구현해서 진화/강화/쿨타임/스택 조회 API를 캐릭터 클래스 무관 인터페이스로
/// 노출한다 - SkillEvolutionSelectUI/KY_SkillView가 이 인터페이스만 보고 동작하므로, 나중에
/// 거너용 스킬 컨트롤러가 생겨도 같은 UI를 그대로 재사용할 수 있다.
/// </summary>
public class FighterSkillController : MonoBehaviour, ISkillController
{
    [SerializeField] private PlayerActionInputHandler inputHandler;
    [SerializeField] private T_PlayerCombat combat;
    [SerializeField] private T_PlayerController controller;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;
    [SerializeField] private WBH_PlayerStatus status;
    [SerializeField] private PlayerBuffManager buffManager;
    [SerializeField] private LayerMask enemyLayer;
    [Tooltip("SectorSlash 진화2(투사체 제거)가 찾을 투사체 레이어. 보통 \"Projectile\" 레이어.")]
    [SerializeField] private LayerMask projectileLayer;

    [Tooltip("SectorSlash 진화3(원형+차징) 차징 중 재생할 지속형 이펙트를 생성하는 스포너.")]
    [SerializeField] private WBH_EffectSpawner effectSpawner;
    [Tooltip("차징 중 플레이어에게 붙는 지속형 이펙트 데이터.")]
    [SerializeField] private WBH_EffectData chargeEffectData;
    private WBH_Effect activeChargeEffect;
    private GameObject activeChargeRangeVisual;

    [Tooltip("인덱스 0~2 = Skill1~3(A/S/D)")]
    [SerializeField] private SkillDefinitionSO[] skills = new SkillDefinitionSO[3];

    [Tooltip("스킬 슬롯별 진화 선택(None = 미진화). 진화 선택 UI가 없어서 지금은 여기서 직접 지정.")]
    [SerializeField] private SkillEvolutionId[] activeEvolutions = new SkillEvolutionId[3];

    [Tooltip("스킬 슬롯별 강화 선택(None = 미강화). 진화와 별개의 축 - SkillEvolutionSelectUI(K키 패널)의 강화 버튼으로 런타임 변경 가능.")]
    [SerializeField] private SkillEnhancementId[] activeEnhancements = new SkillEnhancementId[3];

    [Header("범위 표시(피드백용, 판정과 무관)")]
    [SerializeField] private Color sectorVisualColor = new Color(1f, 0.5f, 0.1f, 0.35f);
    [SerializeField] private Color lineVisualColor = new Color(1f, 0.15f, 0.1f, 0.35f);
    [SerializeField] private Color dashVisualColor = new Color(0.2f, 0.7f, 1f, 0.35f);

    private readonly float[] cooldownRemaining = new float[3];

    // Dash 진화2(2스택화) 전용 상태. -1 = 아직 초기화 안 됨(Start에서 evoDashMaxStacks로 채움).
    private int dashStacks = -1;
    private float dashStackRechargeTimer;

    // SectorSlash 진화3(차징) 전용 상태. -1 = 차징 중 아님.
    private int chargingSkillIndex = -1;
    private float chargeElapsed;

    // T_PlayerCombat.CanAttack과 같은 조건 - 그 필드는 private라 직접 재사용할 수 없어 그대로 옮겨왔다.
    private bool CanUseSkill => !stateMachine.IsAnyState(PlayerState.Hit, PlayerState.Attack,
        PlayerState.Skill, PlayerState.Dodge, PlayerState.Dead);

    private void OnEnable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnSkillKeyPressed += HandleSkillKeyPressed;
            inputHandler.OnSkillKeyReleased += HandleSkillKeyReleased;
        }
    }

    private void OnDisable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnSkillKeyPressed -= HandleSkillKeyPressed;
            inputHandler.OnSkillKeyReleased -= HandleSkillKeyReleased;
        }
    }

    private void Start()
    {
        for (int i = 0; i < skills.Length; i++)
        {
            if (IsDashStackSlot(i))
                dashStacks = skills[i].evoDashMaxStacks;
        }
    }

    private void Update()
    {
        for (int i = 0; i < cooldownRemaining.Length; i++)
        {
            if (cooldownRemaining[i] > 0f)
                cooldownRemaining[i] -= Time.deltaTime;
        }

        UpdateDashStackRecharge();
        UpdateCharge();
    }

    private void UpdateDashStackRecharge()
    {
        if (dashStacks < 0 || dashStackRechargeTimer <= 0f)
            return;

        SkillDefinitionSO def = FindDashStackDef();
        if (def == null)
            return;

        dashStackRechargeTimer -= Time.deltaTime;
        if (dashStackRechargeTimer > 0f)
            return;

        dashStacks = Mathf.Min(dashStacks + 1, def.evoDashMaxStacks);
        if (dashStacks < def.evoDashMaxStacks)
            dashStackRechargeTimer = def.evoDashStackRechargeSeconds; // 아직 최대치 미만이면 다음 스택도 이어서 충전
    }

    private void UpdateCharge()
    {
        if (chargingSkillIndex < 0)
            return;

        SkillDefinitionSO def = skills[chargingSkillIndex];
        if (def == null || !stateMachine.Is(PlayerState.Skill))
        {
            // 피격 등으로 스킬 상태가 풀리면 차징도 취소(발동하지 않음).
            chargingSkillIndex = -1;
            StopChargeEffect();
            return;
        }

        chargeElapsed += Time.deltaTime;
        if (chargeElapsed >= def.evoChargeMaxSeconds)
            ReleaseCharge(); // 최대 차징 도달 - 자동 발동
    }

    /// <summary>남은 쿨타임(초). Dash 진화2(스택)면 다음 스택까지 남은 시간. 스택이 최대치일 때만 0.
    /// !! 스택이 1개 이상 있어도(사용은 가능해도) 최대치 미만이면 다음 스택 충전이 계속 진행 중이므로
    /// 여기서 0을 반환하면 안 된다 - "스택 1개 남았을 때 회복 쿨타임이 안 도는 것처럼 보인다"는
    /// 버그 리포트로 발견함(예전엔 dashStacks > 0이면 무조건 0을 반환해서 최대치 미만이어도
    /// UI에 항상 "준비 완료"로만 보였음).</summary>
    public float GetRemainingCooldown(int index)
    {
        if (index < 0 || index >= cooldownRemaining.Length)
            return 0f;

        if (IsDashStackSlot(index))
        {
            SkillDefinitionSO def = GetSkillDefinition(index);
            int max = def != null ? def.evoDashMaxStacks : 0;
            return dashStacks >= max ? 0f : Mathf.Max(0f, dashStackRechargeTimer);
        }

        return Mathf.Max(0f, cooldownRemaining[index]);
    }

    /// <summary>슬롯(0~2)의 "지금" 최대 쿨타임(강화로 감소돼 있으면 그 값). 쿨타임 UI가 GetRemainingCooldown과
    /// 나눠서 남은 비율(라디얼 필 등)을 계산할 때 분모로 쓴다. Dash 진화2(스택)면 스택 충전 시간 기준.</summary>
    public float GetEffectiveCooldown(int index)
    {
        SkillDefinitionSO def = GetSkillDefinition(index);
        if (def == null)
            return 0f;

        float baseCooldown = IsDashStackSlot(index) ? def.evoDashStackRechargeSeconds : def.cooldownSeconds;
        return ApplyCooldownEnhancement(def, index, baseCooldown);
    }

    /// <summary>슬롯(0~2)이 지금 스택 모드(Dash 진화2)인지, 맞다면 현재/최대 스택 수를 낸다.
    /// 스택 모드가 아니면 false(쿨타임 UI가 스택 배지를 숨기는 신호로 씀).</summary>
    public bool TryGetStackInfo(int index, out int current, out int max)
    {
        current = 0;
        max = 0;

        if (!IsDashStackSlot(index))
            return false;

        SkillDefinitionSO def = GetSkillDefinition(index);
        current = dashStacks;
        max = def != null ? def.evoDashMaxStacks : 0;
        return true;
    }

    private void HandleSkillKeyPressed(int index)
    {
        if (index >= 0 && index < skills.Length && skills[index] != null &&
            skills[index].shapeType == SkillShapeType.SectorSlash &&
            GetEvolution(index) == SkillEvolutionId.Evolution3)
        {
            StartCharge(index);
            return;
        }

        TryUseSkill(index);
    }

    private void HandleSkillKeyReleased(int index)
    {
        if (chargingSkillIndex == index)
            ReleaseCharge();
    }

    /// <summary>인덱스(0~2 = Skill1~3)에 해당하는 스킬을 사용한다. 쿨타임 중이거나 행동 불가 상태면 조용히 실패.</summary>
    public bool TryUseSkill(int index)
    {
        if (index < 0 || index >= skills.Length)
            return false;

        SkillDefinitionSO def = skills[index];
        if (def == null || !CanUseSkill || !IsSkillReady(index))
            return false;

        ConsumeSkillUse(index, def);
        FaceCursor();
        combat.CancelChase();
        stateMachine.ChangeState(PlayerState.Skill);

        SkillEvolutionId evo = GetEvolution(index);

        switch (def.shapeType)
        {
            case SkillShapeType.SectorSlash:
                if (evo == SkillEvolutionId.Evolution1)
                    ExecuteSectorSlashEvo1(def, index);
                else if (evo == SkillEvolutionId.Evolution2)
                    ExecuteSectorSlashEvo2(def, index);
                else
                    ExecuteSectorSlash(def, index);
                StartCoroutine(ReturnToIdleAfter(0.3f));
                break;

            case SkillShapeType.LineSlam:
                if (evo == SkillEvolutionId.Evolution1)
                    ExecuteLineSlamEvo1(def, index);
                else if (evo == SkillEvolutionId.Evolution2)
                    ExecuteLineSlamEvo2(def, index);
                else if (evo == SkillEvolutionId.Evolution3)
                    ExecuteLineSlamEvo3(def, index);
                else
                    ExecuteLineSlam(def, index);
                StartCoroutine(ReturnToIdleAfter(0.35f));
                break;

            case SkillShapeType.Dash:
                StartCoroutine(ExecuteDash(def, evo, index));
                break;
        }

        return true;
    }

    /// <summary>스킬 슬롯(0~2)의 현재 진화. 범위 밖이면 None.</summary>
    public SkillEvolutionId GetEvolution(int index) =>
        index >= 0 && index < activeEvolutions.Length ? activeEvolutions[index] : SkillEvolutionId.None;

    /// <summary>스킬 슬롯(0~2)의 진화를 외부(진화 선택 UI 등)에서 변경한다.</summary>
    public void SetEvolution(int index, SkillEvolutionId evolution)
    {
        if (index < 0 || index >= activeEvolutions.Length)
            return;

        if (chargingSkillIndex == index)
            chargingSkillIndex = -1; // 진화 변경 시 진행 중이던 차징은 취소

        activeEvolutions[index] = evolution;

        SkillDefinitionSO def = skills[index];
        if (def != null && def.shapeType == SkillShapeType.Dash && evolution == SkillEvolutionId.Evolution2 && dashStacks < 0)
            dashStacks = def.evoDashMaxStacks; // 대시 2스택 진화로 처음 전환할 때 스택을 초기화
    }

    /// <summary>스킬 슬롯(0~2)의 현재 강화. 범위 밖이면 None.</summary>
    public SkillEnhancementId GetEnhancement(int index) =>
        index >= 0 && index < activeEnhancements.Length ? activeEnhancements[index] : SkillEnhancementId.None;

    /// <summary>스킬 슬롯(0~2)의 강화를 외부(강화 선택 UI 등)에서 변경한다.</summary>
    public void SetEnhancement(int index, SkillEnhancementId enhancement)
    {
        if (index < 0 || index >= activeEnhancements.Length)
            return;

        activeEnhancements[index] = enhancement;
    }

    /// <summary>스킬 슬롯 개수(0~2). UI가 슬롯 수만큼 반복해서 그릴 때 사용.</summary>
    public int SkillCount => skills.Length;

    /// <summary>슬롯(0~2)의 스킬 데이터. UI가 이름/모양 등을 표시할 때 사용.</summary>
    public SkillDefinitionSO GetSkillDefinition(int index) =>
        index >= 0 && index < skills.Length ? skills[index] : null;

    private bool IsDashStackSlot(int index)
    {
        SkillDefinitionSO def = index >= 0 && index < skills.Length ? skills[index] : null;
        return def != null && def.shapeType == SkillShapeType.Dash && GetEvolution(index) == SkillEvolutionId.Evolution2;
    }

    private SkillDefinitionSO FindDashStackDef()
    {
        for (int i = 0; i < skills.Length; i++)
        {
            if (IsDashStackSlot(i))
                return skills[i];
        }
        return null;
    }

    private bool IsSkillReady(int index) =>
        IsDashStackSlot(index) ? dashStacks > 0 : cooldownRemaining[index] <= 0f;

    private void ConsumeSkillUse(int index, SkillDefinitionSO def)
    {
        if (IsDashStackSlot(index))
        {
            dashStacks--;
            if (dashStackRechargeTimer <= 0f)
                dashStackRechargeTimer = ApplyCooldownEnhancement(def, index, def.evoDashStackRechargeSeconds);
            return;
        }

        cooldownRemaining[index] = ApplyCooldownEnhancement(def, index, def.cooldownSeconds);
    }

    /// <summary>강화(Enhance2: 쿨타임 감소)가 선택돼 있으면 쿨타임/스택 충전 시간을 줄인다.</summary>
    private float ApplyCooldownEnhancement(SkillDefinitionSO def, int index, float baseCooldown)
    {
        if (GetEnhancement(index) != SkillEnhancementId.Enhance2)
            return baseCooldown;

        return baseCooldown * (1f - def.enhanceCooldownReductionPercent / 100f);
    }

    private void StartCharge(int index)
    {
        SkillDefinitionSO def = skills[index];
        if (def == null || !CanUseSkill || !IsSkillReady(index))
            return;

        chargingSkillIndex = index;
        chargeElapsed = 0f;
        FaceCursor();
        combat.CancelChase();
        stateMachine.ChangeState(PlayerState.Skill);

        if (effectSpawner != null && chargeEffectData != null)
            activeChargeEffect = effectSpawner.SpawnPersistentEffect(chargeEffectData, transform);

        // 얇은 선이라 sectorVisualColor의 낮은 알파(플래시 채우기용, 0.35)로는 잘 안 보여서 불투명하게 조정해서 쓴다.
        Color outlineColor = sectorVisualColor;
        outlineColor.a = 1f;
        activeChargeRangeVisual = SkillRangeVisual.ShowPersistentSectorOutline(transform, ApplySkillRangeBonus(def, index, def.sectorRange), 360f, outlineColor, lineWidth: 0.15f);
    }

    /// <summary>PlayerStatManager의 "스킬 범위" 스탯 + 강화(Enhance3: 범위 강화)만큼 기본 판정 거리를 늘린다.
    /// (기본거리 + flat 보너스) * (1 + % 보너스) 순서. flat/%는 PlayerStatManager.GetSkillRangeBonus()로
    /// 4단 공식(CalcFinal)을 우회한 원시 합을 받는다 - Stat.skillRange(CalcFinal 결과)를 그대로 쓰면
    /// buff/equip의 %가 이미 캐릭터의 작은 flat 값에 한 번 곱해져 들어가 있어서, 여기서 %를 또 곱하면
    /// 같은 보너스가 두 번 적용되기 때문이다. 각도(sectorAngle)나 폭(lineWidth)은 이 스탯의 대상이 아니라서
    /// 건드리지 않는다.</summary>
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

    private void StopChargeEffect()
    {
        if (activeChargeRangeVisual != null)
        {
            Destroy(activeChargeRangeVisual);
            activeChargeRangeVisual = null;
        }

        if (activeChargeEffect == null)
            return;

        activeChargeEffect.StopEffect();
        activeChargeEffect = null;
    }

    private void ReleaseCharge()
    {
        int index = chargingSkillIndex;
        chargingSkillIndex = -1;
        StopChargeEffect();

        if (index < 0)
            return;

        SkillDefinitionSO def = skills[index];
        if (def == null)
            return;

        ConsumeSkillUse(index, def);

        float ratio = def.evoChargeMaxSeconds > 0f ? Mathf.Clamp01(chargeElapsed / def.evoChargeMaxSeconds) : 0f;
        ExecuteSectorSlashEvo3(def, ratio, index);
        StartCoroutine(ReturnToIdleAfter(0.3f));
    }

    private IEnumerator ReturnToIdleAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    /// <summary>부채꼴 범위 안의 적 콜라이더 목록 - T_PlayerCombat.SectorAttack과 같은 방식(구체 오버랩 + 각도 필터).</summary>
    private List<Collider> GetSectorTargets(float range, float angle) => GetSectorTargets(range, angle, enemyLayer);

    /// <summary>부채꼴 범위 안의 콜라이더 목록 - 레이어를 직접 지정(투사체 제거 진화 등 적이 아닌 대상용).</summary>
    private List<Collider> GetSectorTargets(float range, float angle, LayerMask layer)
    {
        var result = new List<Collider>();
        Collider[] candidates = Physics.OverlapSphere(transform.position, range, layer);

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

    /// <summary>기본 반원 베기 - 진화 미선택일 때.</summary>
    private void ExecuteSectorSlash(SkillDefinitionSO def, int index)
    {
        float range = ApplySkillRangeBonus(def, index, def.sectorRange);
        SkillRangeVisual.ShowSector(transform.position, transform.forward, range, def.sectorAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(range, def.sectorAngle))
            ApplyHit(target, def, def.damageMultiplier, index);
    }

    /// <summary>진화1: 밀치기 + 기절 - 기본 판정에 넉백/기절 상태이상을 추가로 건다.</summary>
    private void ExecuteSectorSlashEvo1(SkillDefinitionSO def, int index)
    {
        float range = ApplySkillRangeBonus(def, index, def.sectorRange);
        SkillRangeVisual.ShowSector(transform.position, transform.forward, range, def.sectorAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(range, def.sectorAngle))
        {
            ApplyHit(target, def, def.damageMultiplier, index);
            ApplyKnockbackAndStun(target, def);
        }
    }

    /// <summary>진화2: 투사체 제거 - 기본 판정에 더해 부채꼴 범위 안의 적 투사체를 전부 제거한다.</summary>
    private void ExecuteSectorSlashEvo2(SkillDefinitionSO def, int index)
    {
        float range = ApplySkillRangeBonus(def, index, def.sectorRange);
        SkillRangeVisual.ShowSector(transform.position, transform.forward, range, def.sectorAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(range, def.sectorAngle))
            ApplyHit(target, def, def.damageMultiplier, index);

        foreach (Collider projectile in GetSectorTargets(range, def.sectorAngle, projectileLayer))
        {
            if (projectile.TryGetComponent<WBH_Projectile>(out var wbhProjectile))
                wbhProjectile.ForceRemove();
        }
    }

    /// <summary>진화3: 원형(360도) + 차징 - 차징 비율(0~1)에 따라 피해 배율이 evoChargeMinDamageMultiplier~evoChargeMaxDamageMultiplier로 선형 증가.</summary>
    private void ExecuteSectorSlashEvo3(SkillDefinitionSO def, float chargeRatio, int index)
    {
        float multiplier = Mathf.Lerp(def.evoChargeMinDamageMultiplier, def.evoChargeMaxDamageMultiplier, chargeRatio);
        float range = ApplySkillRangeBonus(def, index, def.sectorRange);
        SkillRangeVisual.ShowSector(transform.position, transform.forward, range, 360f, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(range, 360f))
            ApplyHit(target, def, multiplier, index);
    }

    private void ApplyKnockbackAndStun(Collider target, SkillDefinitionSO def)
    {
        if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
            return;

        Vector3 dir = (target.transform.position - transform.position).normalized;
        dir.y = 0f;

        // WBH_StatusEffectPresets.Knockback1은 거리(10)가 스킬용으로 너무 멀어서 안 쓰고,
        // 스킬 전용 수치(evoKnockbackForce/Duration)로 직접 데이터를 만든다.
        var knockback = new WBH_StatusEffectData(WBH_StatusEffectType.KnockBack,
            duration: def.evoKnockbackDuration, direction: dir, force: def.evoKnockbackForce);

        combatTarget.AddStatusEffect(knockback);
        combatTarget.AddStatusEffect(WBH_StatusEffectPresets.Stun1);
    }

    private void ApplyDefenseDownAndStun(Collider target, SkillDefinitionSO def)
    {
        if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
            return;

        var defenseDown = new WBH_StatusEffectData(WBH_StatusEffectType.DefenseDown,
            duration: def.evoDefenseDownDuration, value: def.evoDefenseDownMultiplier);

        combatTarget.AddStatusEffect(defenseDown);
        combatTarget.AddStatusEffect(WBH_StatusEffectPresets.Stun1);
    }

    /// <summary>기본 정면 직선 내려찍기 - 진화 미선택일 때.</summary>
    private void ExecuteLineSlam(SkillDefinitionSO def, int index)
    {
        float length = ApplySkillRangeBonus(def, index, def.lineLength);
        SkillRangeVisual.ShowLine(transform.position, transform.forward, length, def.lineWidth, lineVisualColor);

        foreach (Collider target in GetLineTargets(length, def.lineWidth))
            ApplyHit(target, def, def.damageMultiplier, index);
    }

    /// <summary>진화1: 방어 감소 + 기절 - 기본 판정에 방어력 감소/기절 상태이상을 추가로 건다.</summary>
    private void ExecuteLineSlamEvo1(SkillDefinitionSO def, int index)
    {
        float length = ApplySkillRangeBonus(def, index, def.lineLength);
        SkillRangeVisual.ShowLine(transform.position, transform.forward, length, def.lineWidth, lineVisualColor);

        foreach (Collider target in GetLineTargets(length, def.lineWidth))
        {
            ApplyHit(target, def, def.damageMultiplier, index);
            ApplyDefenseDownAndStun(target, def);
        }
    }

    /// <summary>진화2: 범위 증가 + 에어본.</summary>
    private void ExecuteLineSlamEvo2(SkillDefinitionSO def, int index)
    {
        float length = ApplySkillRangeBonus(def, index, def.evoWideLineLength);
        SkillRangeVisual.ShowLine(transform.position, transform.forward, length, def.evoWideLineWidth, lineVisualColor);

        var airborne = new WBH_StatusEffectData(WBH_StatusEffectType.Airborne, duration: def.evoAirborneDuration, height: def.evoAirborneHeight);

        foreach (Collider target in GetLineTargets(length, def.evoWideLineWidth))
        {
            ApplyHit(target, def, def.damageMultiplier, index);
            if (target.TryGetComponent<WBH_ICombat>(out var combatTarget))
                combatTarget.AddStatusEffect(airborne);
        }
    }

    /// <summary>진화3: 범위 감소 + 강한 데미지.</summary>
    private void ExecuteLineSlamEvo3(SkillDefinitionSO def, int index)
    {
        float length = ApplySkillRangeBonus(def, index, def.evoNarrowLineLength);
        SkillRangeVisual.ShowLine(transform.position, transform.forward, length, def.evoNarrowLineWidth, lineVisualColor);

        foreach (Collider target in GetLineTargets(length, def.evoNarrowLineWidth))
            ApplyHit(target, def, def.evoNarrowDamageMultiplier, index);
    }

    /// <summary>강화(Enhance1: 위력 강화)가 선택돼 있으면 데미지 계수에 보너스를 곱한다.</summary>
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
    /// 커서 방향으로 짧게 대시 - 피해 없이 이동만 한다.
    /// T_PlayerController.TryDodge의 NavMeshAgent 이동 방식을 참고했다(그 메서드 자체는 회피 전용
    /// 상태/무적 처리가 섞여 있어 그대로 재사용하지 않고, 이동 부분만 같은 방식으로 새로 짰다).
    ///
    /// 진화1(무적 부여)은 대시 시작과 동시에 T_PlayerController.ApplyInvincibility를 호출한다.
    /// 진화2(2스택화)는 입력/쿨타임 쪽(IsSkillReady/ConsumeSkillUse)에서 이미 처리되고, 이동 자체는
    /// 기본 대시와 동일하다. 진화3(피해 증가 버프)는 대시가 끝난 직후 버프를 건다.
    ///
    /// 강화(Enhance1: 위력 강화)는 대시가 자체 피해를 안 입혀서 대신 이동 시간(dashDuration)을 줄여
    /// 더 빠르게 대시하도록 한다. Enhance3(범위 강화)는 이동 거리에 적용된다(ApplySkillRangeBonus).
    /// </summary>
    private IEnumerator ExecuteDash(SkillDefinitionSO def, SkillEvolutionId evo, int index)
    {
        if (evo == SkillEvolutionId.Evolution1)
            controller.ApplyInvincibility(def.evoInvincibleDuration);

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

        if (evo == SkillEvolutionId.Evolution3 && def.evoDashDamageBuff != null && buffManager != null)
            buffManager.ApplyBuff(def.evoDashDamageBuff);

        if (stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    private void FaceCursor()
    {
        Vector3 dir = GetCursorDirection();
        if (dir.sqrMagnitude > 0.001f)
            transform.forward = dir;
    }

    /// <summary>마우스 커서가 가리키는 바닥 방향(XZ 평면, 정규화). T_PlayerController.GetMouseDirection과 같은 방식.</summary>
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
}

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
/// </summary>
public class FighterSkillController : MonoBehaviour
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

    /// <summary>남은 쿨타임(초). Dash 진화2(스택)면 다음 스택까지 남은 시간. 준비됐으면 0.</summary>
    public float GetRemainingCooldown(int index)
    {
        if (index < 0 || index >= cooldownRemaining.Length)
            return 0f;

        if (IsDashStackSlot(index))
            return dashStacks > 0 ? 0f : Mathf.Max(0f, dashStackRechargeTimer);

        return Mathf.Max(0f, cooldownRemaining[index]);
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
                    ExecuteSectorSlashEvo1(def);
                else if (evo == SkillEvolutionId.Evolution2)
                    ExecuteSectorSlashEvo2(def);
                else
                    ExecuteSectorSlash(def);
                StartCoroutine(ReturnToIdleAfter(0.3f));
                break;

            case SkillShapeType.LineSlam:
                if (evo == SkillEvolutionId.Evolution1)
                    ExecuteLineSlamEvo1(def);
                else if (evo == SkillEvolutionId.Evolution2)
                    ExecuteLineSlamEvo2(def);
                else if (evo == SkillEvolutionId.Evolution3)
                    ExecuteLineSlamEvo3(def);
                else
                    ExecuteLineSlam(def);
                StartCoroutine(ReturnToIdleAfter(0.35f));
                break;

            case SkillShapeType.Dash:
                StartCoroutine(ExecuteDash(def, evo));
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
                dashStackRechargeTimer = def.evoDashStackRechargeSeconds;
            return;
        }

        cooldownRemaining[index] = def.cooldownSeconds;
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
        activeChargeRangeVisual = SkillRangeVisual.ShowPersistentSectorOutline(transform, def.sectorRange, 360f, outlineColor, lineWidth: 0.15f);
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
        ExecuteSectorSlashEvo3(def, ratio);
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
    private void ExecuteSectorSlash(SkillDefinitionSO def)
    {
        SkillRangeVisual.ShowSector(transform.position, transform.forward, def.sectorRange, def.sectorAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(def.sectorRange, def.sectorAngle))
            ApplyHit(target, def, def.damageMultiplier);
    }

    /// <summary>진화1: 밀치기 + 기절 - 기본 판정에 넉백/기절 상태이상을 추가로 건다.</summary>
    private void ExecuteSectorSlashEvo1(SkillDefinitionSO def)
    {
        SkillRangeVisual.ShowSector(transform.position, transform.forward, def.sectorRange, def.sectorAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(def.sectorRange, def.sectorAngle))
        {
            ApplyHit(target, def, def.damageMultiplier);
            ApplyKnockbackAndStun(target, def);
        }
    }

    /// <summary>진화2: 투사체 제거 - 기본 판정에 더해 부채꼴 범위 안의 적 투사체를 전부 제거한다.</summary>
    private void ExecuteSectorSlashEvo2(SkillDefinitionSO def)
    {
        SkillRangeVisual.ShowSector(transform.position, transform.forward, def.sectorRange, def.sectorAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(def.sectorRange, def.sectorAngle))
            ApplyHit(target, def, def.damageMultiplier);

        foreach (Collider projectile in GetSectorTargets(def.sectorRange, def.sectorAngle, projectileLayer))
        {
            if (projectile.TryGetComponent<WBH_Projectile>(out var wbhProjectile))
                wbhProjectile.ForceRemove();
        }
    }

    /// <summary>진화3: 원형(360도) + 차징 - 차징 비율(0~1)에 따라 피해 배율이 evoChargeMinDamageMultiplier~evoChargeMaxDamageMultiplier로 선형 증가.</summary>
    private void ExecuteSectorSlashEvo3(SkillDefinitionSO def, float chargeRatio)
    {
        float multiplier = Mathf.Lerp(def.evoChargeMinDamageMultiplier, def.evoChargeMaxDamageMultiplier, chargeRatio);
        SkillRangeVisual.ShowSector(transform.position, transform.forward, def.sectorRange, 360f, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(def.sectorRange, 360f))
            ApplyHit(target, def, multiplier);
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
    private void ExecuteLineSlam(SkillDefinitionSO def)
    {
        SkillRangeVisual.ShowLine(transform.position, transform.forward, def.lineLength, def.lineWidth, lineVisualColor);

        foreach (Collider target in GetLineTargets(def.lineLength, def.lineWidth))
            ApplyHit(target, def, def.damageMultiplier);
    }

    /// <summary>진화1: 방어 감소 + 기절 - 기본 판정에 방어력 감소/기절 상태이상을 추가로 건다.</summary>
    private void ExecuteLineSlamEvo1(SkillDefinitionSO def)
    {
        SkillRangeVisual.ShowLine(transform.position, transform.forward, def.lineLength, def.lineWidth, lineVisualColor);

        foreach (Collider target in GetLineTargets(def.lineLength, def.lineWidth))
        {
            ApplyHit(target, def, def.damageMultiplier);
            ApplyDefenseDownAndStun(target, def);
        }
    }

    /// <summary>진화2: 범위 증가 + 에어본.</summary>
    private void ExecuteLineSlamEvo2(SkillDefinitionSO def)
    {
        SkillRangeVisual.ShowLine(transform.position, transform.forward, def.evoWideLineLength, def.evoWideLineWidth, lineVisualColor);

        var airborne = new WBH_StatusEffectData(WBH_StatusEffectType.Airborne, duration: def.evoAirborneDuration, height: def.evoAirborneHeight);

        foreach (Collider target in GetLineTargets(def.evoWideLineLength, def.evoWideLineWidth))
        {
            ApplyHit(target, def, def.damageMultiplier);
            if (target.TryGetComponent<WBH_ICombat>(out var combatTarget))
                combatTarget.AddStatusEffect(airborne);
        }
    }

    /// <summary>진화3: 범위 감소 + 강한 데미지.</summary>
    private void ExecuteLineSlamEvo3(SkillDefinitionSO def)
    {
        SkillRangeVisual.ShowLine(transform.position, transform.forward, def.evoNarrowLineLength, def.evoNarrowLineWidth, lineVisualColor);

        foreach (Collider target in GetLineTargets(def.evoNarrowLineLength, def.evoNarrowLineWidth))
            ApplyHit(target, def, def.evoNarrowDamageMultiplier);
    }

    private void ApplyHit(Collider target, SkillDefinitionSO def, float damageMultiplier)
    {
        if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
            return;

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
    /// </summary>
    private IEnumerator ExecuteDash(SkillDefinitionSO def, SkillEvolutionId evo)
    {
        if (evo == SkillEvolutionId.Evolution1)
            controller.ApplyInvincibility(def.evoInvincibleDuration);

        Vector3 dir = GetCursorDirection();
        SkillRangeVisual.ShowLine(transform.position, dir, def.dashDistance, 0.6f, dashVisualColor);

        NavMeshAgent agent = controller.agent;

        Vector3 targetPos = transform.position + dir * def.dashDistance;
        if (NavMesh.Raycast(transform.position, targetPos, out NavMeshHit hit, NavMesh.AllAreas))
            targetPos = hit.position;

        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < def.dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / def.dashDuration);
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

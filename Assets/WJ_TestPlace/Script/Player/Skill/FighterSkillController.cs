using System;
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
/// 정상 동작하지만 시각적으로는 애니메이션 없이 즉시 판정된다. > 8.25 WBH 애니메이션 연결 완료.
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

    [SerializeField] private bool visibleSkillArea = false; // 2026.08.07 조용준 추가 - 스킬 범위 볼 것인지 선택가능하게 bool 항목 처리 
    [SerializeField] private Color sectorVisualColor = new Color(1f, 0.5f, 0.1f, 0.35f);
    [SerializeField] private Color lineVisualColor = new Color(1f, 0.15f, 0.1f, 0.35f);
    [SerializeField] private Color dashVisualColor = new Color(0.2f, 0.7f, 1f, 0.35f);

    // 슬롯 수(skills.Length)에 맞춰 Awake에서 다시 잡는다 - 궁극기(Skill4)처럼 슬롯이 늘어나도
    // 쿨타임 배열만 3칸으로 남아 IndexOutOfRange가 나지 않도록 하기 위함.
    private float[] cooldownRemaining = new float[3];

    // Dash 진화2(2스택화) 전용 상태. -1 = 아직 초기화 안 됨(Start에서 evoDashMaxStacks로 채움).
    private int dashStacks = -1;
    private float dashStackRechargeTimer;

    // SectorSlash 진화3(차징) 전용 상태. -1 = 차징 중 아님.
    private int chargingSkillIndex = -1;
    private float chargeElapsed;

    // T_PlayerCombat.CanAttack과 같은 조건 - 그 필드는 private라 직접 재사용할 수 없어 그대로 옮겨왔다.
    private bool CanUseSkill => !stateMachine.IsAnyState(PlayerState.Hit, PlayerState.Attack,
        PlayerState.Skill, PlayerState.Dodge, PlayerState.Dead) && !SkillPopupController.IsOpen;

    // ------ 8.24 WBH 추가. 애니메이션 연결 및 타격시점 전환(코드 > 애니메이션 이벤트)을 위한 변수 + 이펙트 실행을 위한 변수
    public event Action<int, bool, float> OnSkillAniRequested;
    public event Action<bool> OnChargeAniChanged;

    private WBH_PlayerEffect playerEffect;

    private int pendingSkillIndex = -1;
    private SkillEvolutionId pendingEvo; // 스킬 사용 시 스킬 진화 상태를 임시로 저장하는 변수
    private SkillEnhancementId pendingEnhance; // 스킬 사용 시 스킬 강화 상태를 임시로 저장하는 변수
    private float pendingChargeRatio;
    private float pendingDashDuration;
    private Vector3 pendingAimDirection;
    private PlayerStatManager skillOwnerStats;

    // SW 수정
    /// <summary>호출자가 검증한 조준과 소유자 스탯으로 스킬을 준비한다. 권한 검증은 호출자가 담당한다.</summary>
    public bool TryUseSkill(int index, Vector3 aimDirection, PlayerStatManager ownerStats)
    {
        if (ownerStats == null || !TryNormalizeAim(ref aimDirection))
            return false;
        return TryUseSkillInternal(index, aimDirection, ownerStats, true);
    }

    /// <summary>외부 입력으로 차징을 시작한다. 마나와 쿨다운은 정상 해제 시점에 확정한다.</summary>
    public bool TryStartCharge(int index, Vector3 aimDirection, PlayerStatManager ownerStats)
    {
        if (ownerStats == null || !TryNormalizeAim(ref aimDirection) ||
            index < 0 || index >= skills.Length || skills[index] == null ||
            skills[index].shapeType != SkillShapeType.SectorSlash ||
            GetEvolution(index) != SkillEvolutionId.Evolution3)
            return false;
        return StartChargeInternal(index, aimDirection, ownerStats, true);
    }

    /// <summary>현재 차징 중인 슬롯을 한 번만 해제하고 실제 사용 성공 여부를 반환한다.</summary>
    public bool TryReleaseCharge(int index)
    {
        if (index < 0 || chargingSkillIndex != index || !stateMachine.Is(PlayerState.Skill))
            return false;
        return ReleaseCharge();
    }

    private bool CanUseSkillFrom(bool externalInput) => stateMachine != null &&
        !stateMachine.IsAnyState(PlayerState.Hit, PlayerState.Attack, PlayerState.Skill,
            PlayerState.Dodge, PlayerState.Dead) && (externalInput || !SkillPopupController.IsOpen);

    private static bool TryNormalizeAim(ref Vector3 direction)
    {
        if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z))
            return false;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || !float.IsFinite(direction.sqrMagnitude))
            return false;
        direction.Normalize();
        return true;
    }


    //-------

    private void Awake()
    {
        playerEffect = GetComponent<WBH_PlayerEffect>();

        if (cooldownRemaining.Length != skills.Length)
            cooldownRemaining = new float[skills.Length];
    }

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

        int index = FindDashStackSlotIndex();
        SkillDefinitionSO def = index >= 0 ? skills[index] : null;
        if (def == null)
            return;

        dashStackRechargeTimer -= Time.deltaTime;
        if (dashStackRechargeTimer > 0f)
            return;

        dashStacks = Mathf.Min(dashStacks + 1, def.evoDashMaxStacks);
        if (dashStacks < def.evoDashMaxStacks)
            dashStackRechargeTimer = ApplyCooldownEnhancement(def, index, def.evoDashStackRechargeSeconds); // 아직 최대치 미만이면 다음 스택도 이어서 충전(강화(쿨감) 반영)
    }

    private void UpdateCharge()
    {
        if (chargingSkillIndex < 0)
            return;

        SkillDefinitionSO def = skills[chargingSkillIndex];
        if (def == null || !stateMachine.Is(PlayerState.Skill))
        {
            // 피격 등으로 스킬 상태가 풀리면 차징도 취소(발동하지 않음).
            //chargingSkillIndex = -1; 
            //StopChargeEffect(); // 8.24 WBH 수정. CancelCharge() 로 메서드화 진행.
            CancelCharge();
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
    //public bool TryUseSkill(int index) // 8.24 WBH 주석화. 스킬이 시전되어 데미지가 적용되는 코드를 애니메이션 이벤트로 옮기기 위해 아래에 스킬 실행 가능 여부와 스킬 실행 기능 분리 처리.
    //{
    //    if (index < 0 || index >= skills.Length)
    //        return false;

    //    SkillDefinitionSO def = skills[index];
    //    if (def == null || !CanUseSkill || !IsSkillReady(index))
    //        return false;

    //    ConsumeSkillUse(index, def);
    //    FaceCursor();
    //    combat.CancelChase();
    //    stateMachine.ChangeState(PlayerState.Skill);

    //    SkillEvolutionId evo = GetEvolution(index);

    //    switch (def.shapeType)
    //    {
    //        case SkillShapeType.SectorSlash:
    //            if (evo == SkillEvolutionId.Evolution1)
    //                ExecuteSectorSlashEvo1(def, index);
    //            else if (evo == SkillEvolutionId.Evolution2)
    //                ExecuteSectorSlashEvo2(def, index);
    //            else
    //                ExecuteSectorSlash(def, index);
    //            StartCoroutine(ReturnToIdleAfter(0.3f));
    //            break;

    //        case SkillShapeType.LineSlam:
    //            if (evo == SkillEvolutionId.Evolution1)
    //                ExecuteLineSlamEvo1(def, index);
    //            else if (evo == SkillEvolutionId.Evolution2)
    //                ExecuteLineSlamEvo2(def, index);
    //            else if (evo == SkillEvolutionId.Evolution3)
    //                ExecuteLineSlamEvo3(def, index);
    //            else
    //                ExecuteLineSlam(def, index);
    //            StartCoroutine(ReturnToIdleAfter(0.35f));
    //            break;

    //        case SkillShapeType.Dash:
    //            StartCoroutine(ExecuteDash(def, evo, index));
    //            break;
    //    }

    //    return true;
    //}

    //------ 8.24 WBH 추가. 애니메이션 연결 및 타격시점 전환(코드 > 애니메이션 이벤트)을 위한 코드
    public bool TryUseSkill(int index)
    {
        if (Camera.main == null)
            return false;
        return TryUseSkillInternal(index, GetCursorDirection(),
            GetComponentInParent<PlayerStatManager>() ?? PlayerStatManager.Instance, false);
    }

    private bool TryUseSkillInternal(int index, Vector3 aimDirection, PlayerStatManager ownerStats, bool externalInput)
    {
        if (index < 0 || index >= skills.Length)
            return false;

        SkillDefinitionSO def = skills[index];
        if (def == null || !CanUseSkillFrom(externalInput) || !IsSkillReady(index) ||
            combat == null || (externalInput && (status == null || status.IsDead)) || !TryNormalizeAim(ref aimDirection))
            return false;

        SkillEvolutionId evolution = GetEvolution(index);

        // 쿨타임/스택을 깎기 전에 마나부터 확인한다 - 마나가 부족하면 여기서 조용히 실패하고
        // 쿨타임/스택/애니메이션 전부 건드리지 않는다(138번 후속 - manaCost 수치는 137/138번에서 이미 반영됨).
        // 진화별 마나 코스트가 설정돼 있으면 그 값을, 아니면 기본 manaCost를 쓴다(GetManaCost).
        if (status != null && !status.TryUseMana(def.GetManaCost(evolution)))
            return false;

        ConsumeSkillUse(index, def);
        skillOwnerStats = ownerStats;
        pendingAimDirection = aimDirection;
        transform.forward = aimDirection;
        combat.CancelChase();

        PreparePendingSkill(index, evolution);
        stateMachine.ChangeState(PlayerState.Skill);
        RequestSkillAni(index, false, pendingDashDuration);

        return true;
    }

    // 기존 WJ님의 TryUseSkill 메서드에서 즉시 데미지가 들어가는 부분 분리. WBH_PlayerAnimation 의 AniEvent_ExecuteSkill 에서 실행.
    public void ExecutePendingSkill()
    {
        //if (pendingSkillIndex < 0 || pendingSkillExecuted || !stateMachine.Is(PlayerState.Skill))
        //    return;

        if (pendingSkillIndex < 0 || !stateMachine.Is(PlayerState.Skill))
            return;

        int index = pendingSkillIndex;
        SkillDefinitionSO def = skills[index];

        transform.forward = pendingAimDirection;

        if(def == null)
        {
            ClearPendingSkill();
            return;
        }

        //pendingSkillExecuted = true;

        switch (def.shapeType)
        {
            case SkillShapeType.SectorSlash:
                if (pendingEvo == SkillEvolutionId.Evolution1)
                    ExecuteSectorSlashEvo1(def, index);
                else if (pendingEvo == SkillEvolutionId.Evolution2)
                    ExecuteSectorSlashEvo2(def, index);
                else if (pendingEvo == SkillEvolutionId.Evolution3)
                    ExecuteSectorSlashEvo3(def, pendingChargeRatio, index);
                else
                    ExecuteSectorSlash(def, index);
                break;

            case SkillShapeType.LineSlam:
                if (pendingEvo == SkillEvolutionId.Evolution1)
                    ExecuteLineSlamEvo1(def, index);
                else if (pendingEvo == SkillEvolutionId.Evolution2)
                    ExecuteLineSlamEvo2(def, index);
                else if (pendingEvo == SkillEvolutionId.Evolution3)
                    ExecuteLineSlamEvo3(def, index);
                else
                    ExecuteLineSlam(def, index);
                break;

            case SkillShapeType.Dash:
                StartCoroutine(ExecuteDash(def, pendingEvo, index, pendingDashDuration));
                break;

            case SkillShapeType.AwakeningBurst:
                ExecuteAwakeningBurst(def, index);
                break;
        }
    }

    /// <summary>
    /// 궁극기(각성). 시전 즉시 자기 주변 원형 범위를 한 번 때리고, 이어서 자신에게 강화 버프를 건다.
    ///
    /// 판정은 SectorSlash와 같은 부채꼴 질의를 각도 360으로 쓴다(= 원형). 새 도형을 만들지 않고
    /// 기존 GetSectorTargets/ApplyHit을 그대로 재사용하므로 피해 계산·이펙트 규칙이 다른 스킬과 같다.
    ///
    /// 범위 표시는 visibleSkillArea(디버그용 전역 토글)와 무관하게 항상 그린다 - 궁극기는 어디까지
    /// 맞는지가 플레이어에게 보여야 하는 연출의 일부라서 디버그 옵션에 묶어두지 않는다.
    /// </summary>
    private void ExecuteAwakeningBurst(SkillDefinitionSO def, int index)
    {
        float range = ApplySkillRangeBonus(def, index, def.sectorRange);

        SkillRangeVisual.ShowSector(transform.position, transform.forward, range, AwakeningBurstAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(range, AwakeningBurstAngle))
            ApplyHit(target, def, def.damageMultiplier, index);

        if (def.awakeningBuff != null && buffManager != null)
            buffManager.ApplyBuff(def.awakeningBuff);
    }

    // 스킬 종료 후 Idle 상태로 복귀.
    public void EndPendingSkillAni()
    {
        if (pendingSkillIndex < 0)
            return;

        SkillDefinitionSO def = skills[pendingSkillIndex];

        if (def != null && def.shapeType == SkillShapeType.Dash)
            return;

        ClearPendingSkill();

        if(stateMachine.Is(PlayerState.Skill))
        {
            stateMachine.ChangeState(PlayerState.Idle);
        }
    }

    // ExecutePendingSkill 메서드로 임시 값을 넘겨주기 위한 메서드
    private void PreparePendingSkill(int index, SkillEvolutionId evolution, float chargeRatio = 0f)
    {
        pendingSkillIndex = index;
        pendingEvo = evolution;
        pendingEnhance = GetEnhancement(index);
        pendingChargeRatio = chargeRatio;
        //pendingSkillExecuted = false;

        SkillDefinitionSO def = skills[index];

        pendingDashDuration = def != null && def.shapeType == SkillShapeType.Dash ? GetEffectiveDashDuration(def, index) : 0f;

    }

    // 대쉬시간을 계산하기 위한 메서드
    private float GetEffectiveDashDuration(SkillDefinitionSO def, int index)
    {
        if (GetEnhancement(index) != SkillEnhancementId.Enhance1)
            return def.dashDuration;

        return def.dashDuration * (1f - def.enhanceDashSpeedBonusPercent / 100f);
    }

    // 스킬 애니메이션 실행을 위한 이벤트 요청. 
    private void RequestSkillAni(int index, bool isCharging, float targetDuration = 0f)
    {
        OnSkillAniRequested?.Invoke((index + 1), isCharging, targetDuration); // animator 에서 실수방지를 위해 0 = none, 1 부터 스킬로 설정해둠.
    }

    /// <summary>각성 시전 타격은 자기 주변 전방위라 부채꼴 질의를 360도(=원형)로 쓴다.</summary>
    private const float AwakeningBurstAngle = 360f;

    /// <summary>궁극기 슬롯. 전용 애니메이션·이펙트가 준비되면 이 보정을 통째로 지운다.</summary>
    //private const int UltimateSlotIndex = 3; // 0922 WBH : 정식 이관을 위해 필요없는 코드이므로 주석처리

    /// <summary>궁극기가 임시로 빌려 쓰는 스킬 번호(= 데이터를 복사해 온 1번 스킬).</summary>
    //private const int UltimateBorrowedSkillNumber = 1;

    /// <summary>
    /// 애니메이터와 이펙트 큐에 보낼 스킬 번호(0=없음, 1부터 스킬).
    ///
    /// 궁극기(슬롯 4)는 아직 전용 애니메이션·이펙트가 없어서 1번 스킬 번호를 빌려 쓴다. 애니메이터에
    /// SkillID 4 전이가 없으면 스킬 클립이 아예 재생되지 않고, 실행 시점을 알리는 애니메이션 이벤트
    /// (AniEvent_ExecuteSkill)도 오지 않아 ExecutePendingSkill이 호출되지 않는다 - 그러면 피해도 안 들어가고
    /// 플레이어가 Skill 상태에서 빠져나오지 못해 조작이 멈춘다.
    ///
    /// 실제 스킬 로직은 계속 원래 슬롯 인덱스(pendingSkillIndex)로 돌아가므로 궁극기 데이터가 그대로 쓰인다.
    /// </summary>
    //private static int GetPresentationSkillNumber(int index) =>
    //    index == UltimateSlotIndex ? UltimateBorrowedSkillNumber : index + 1;

    // 초기화
    private void ClearPendingSkill()
    {
        playerEffect?.CancelPendingSfx();
        pendingSkillIndex = -1;
        pendingEvo = SkillEvolutionId.None;
        pendingEnhance = SkillEnhancementId.None;
        pendingChargeRatio = 0f;
        //pendingSkillExecuted = false;
        pendingDashDuration = 0f;
    }

    public void PlayPendingSkillEffect(int partValue)
    {
        if (pendingSkillIndex < 0 || pendingSkillIndex >= skills.Length)
            return;

        if(!System.Enum.IsDefined(typeof(SkillEffectPart), partValue))
        {
            Log.Warning($"알수 없는 스킬 이펙트 부가정보 : {partValue}");
            return;
        }

        SkillDefinitionSO def = skills[pendingSkillIndex];

        if (def == null || playerEffect == null)
            return;

        SkillEffectPart part = (SkillEffectPart)partValue;

        WBH_PlayerEffectCue cue = PlayerEffectCueUtility.CreateFighterSkillCue((pendingSkillIndex + 1 ), pendingEvo, part);

        Vector3 scaleMultiplier = GetPendingSkillEffectScale(partValue);

        playerEffect.PlayEffect(cue, scaleMultiplier);
    }

    /// <summary>시전 중인 원본 이펙트 배율을 반환해 외부 표시에서도 같은 계산을 사용한다.</summary>
    public Vector3 GetPendingSkillEffectScale(int partValue)
    {
        if (!System.Enum.IsDefined(typeof(SkillEffectPart), partValue)) return Vector3.one;
        if (chargingSkillIndex >= 0 && chargingSkillIndex < skills.Length)
        {
            SkillDefinitionSO charging = skills[chargingSkillIndex];
            return charging != null && GetEnhancement(chargingSkillIndex) == SkillEnhancementId.Enhance3
                ? Vector3.one * (1f + charging.enhanceRangeBonusPercent / 100f) : Vector3.one;
        }
        return pendingSkillIndex >= 0 && pendingSkillIndex < skills.Length
            ? CalculatePendingEnhancementEffectScale(skills[pendingSkillIndex]) : Vector3.one;
    }

    /// <summary>
    /// 시전 중인 Enhance3 범위 보너스를 이펙트 크기 배율로 변환한다.
    /// GetPendingBaseRange에서 각 스킬과 진화의 기본 범위를 선택한다.
    /// 배율 적용 여부는 각 WBH_EffectData가 결정한다.
    /// </summary>
    private Vector3 CalculatePendingEnhancementEffectScale(SkillDefinitionSO def)
    {
        if(def == null || pendingSkillIndex < 0 || pendingSkillIndex >= skills.Length)
        {
            return Vector3.one;
        }

        if (pendingEnhance != SkillEnhancementId.Enhance3)
            return Vector3.one;

        float baseRange = GetPendingBaseRange(def);

        if (baseRange <= Mathf.Epsilon)
            return Vector3.one;

        float enhancedRange = baseRange * (1f + def.enhanceRangeBonusPercent / 100f);
        float rangeScale = enhancedRange / baseRange;

        // 스킬 타입에 따라 다른 방향 확대
        return def.shapeType switch
        {
            SkillShapeType.SectorSlash => new Vector3(rangeScale, rangeScale, rangeScale),
            SkillShapeType.LineSlam => new Vector3(rangeScale, rangeScale, rangeScale),
            SkillShapeType.Dash => new Vector3(rangeScale, rangeScale, rangeScale),

            _ => Vector3.one
        };
    }

    // 기초 스킬 범위
    private float GetPendingBaseRange(SkillDefinitionSO def)
    {
            return def.shapeType switch
            {
                SkillShapeType.SectorSlash =>
                    def.sectorRange,

                SkillShapeType.LineSlam =>
                    pendingEvo switch
                    {
                        SkillEvolutionId.Evolution2 =>
                            def.evoWideLineLength,

                        SkillEvolutionId.Evolution3 =>
                            def.evoNarrowLineLength,

                        _ =>
                            def.lineLength,
                    },

                SkillShapeType.Dash =>
                    def.dashDistance,

                _ => 0f,
            };
    }
    // ------

    /// <summary>스킬 슬롯(0~2)의 현재 진화. 범위 밖이면 None.</summary>
    public SkillEvolutionId GetEvolution(int index) =>
        index >= 0 && index < activeEvolutions.Length ? activeEvolutions[index] : SkillEvolutionId.None;

    /// <summary>스킬 슬롯(0~2)의 진화를 외부(진화 선택 UI 등)에서 변경한다.</summary>
    public void SetEvolution(int index, SkillEvolutionId evolution)
    {
        if (index < 0 || index >= activeEvolutions.Length)
            return;

        if (chargingSkillIndex == index)
            //chargingSkillIndex = -1; // 진화 변경 시 진행 중이던 차징은 취소
            CancelCharge(); // 8.24 WBH 추가. 차징 취소 + 이펙트 중단

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

    private int FindDashStackSlotIndex()
    {
        for (int i = 0; i < skills.Length; i++)
        {
            if (IsDashStackSlot(i))
                return i;
        }
        return -1;
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
        if (Camera.main == null)
            return;
        StartChargeInternal(index, GetCursorDirection(),
            GetComponentInParent<PlayerStatManager>() ?? PlayerStatManager.Instance, false);
    }

    private bool StartChargeInternal(int index, Vector3 aimDirection, PlayerStatManager ownerStats, bool externalInput)
    {
        SkillDefinitionSO def = skills[index];
        if (def == null || !CanUseSkillFrom(externalInput) || !IsSkillReady(index) ||
            combat == null || (externalInput && (status == null || status.IsDead)) || !TryNormalizeAim(ref aimDirection))
            return false;

        // 즉발 스킬(TryUseSkill)과 동일하게 마나가 부족하면 아예 차징을 시작할 수 없다. 여기선 소모는 안 하고
        // 확인만 한다(HasEnoughMana에 대응하는 WBH_PlayerStatus API가 없어서 CurrentMp를 직접 비교) -
        // 실제 소모는 쿨타임/스택과 마찬가지로 릴리즈 시점(ReleaseCharge)에 커밋해서, 차징 중 피격 등으로
        // 취소(CancelCharge)되면 마나를 그대로 돌려주는 셈이 된다.
        if (status != null && status.CurrentMp < def.GetManaCost(SkillEvolutionId.Evolution3))
            return false;

        chargingSkillIndex = index;
        chargeElapsed = 0f;
        skillOwnerStats = ownerStats;
        pendingAimDirection = aimDirection;
        transform.forward = aimDirection;
        combat.CancelChase();
        stateMachine.ChangeState(PlayerState.Skill);

        Vector3 chargeEffectScale = GetPendingSkillEffectScale((int)SkillEffectPart.Main);

        playerEffect?.SetChargeEnhancementScale(chargeEffectScale);

        RequestSkillAni(index, true); // 8.24 WBH 추가

        if (effectSpawner != null && chargeEffectData != null)
            activeChargeEffect = effectSpawner.SpawnPersistentEffect(chargeEffectData, transform);

        // 얇은 선이라 sectorVisualColor의 낮은 알파(플래시 채우기용, 0.35)로는 잘 안 보여서 불투명하게 조정해서 쓴다.
        Color outlineColor = sectorVisualColor;
        outlineColor.a = 1f;
        if (visibleSkillArea)
            activeChargeRangeVisual = SkillRangeVisual.ShowPersistentSectorOutline(transform, ApplySkillRangeBonus(def, index, def.sectorRange), 360f, outlineColor, lineWidth: 0.15f);
        return true;
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
        if (skillOwnerStats != null)
            skillOwnerStats.GetSkillRangeBonus(out flatBonus, out percentBonus);

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

    private bool ReleaseCharge()
    {
        int index = chargingSkillIndex;
        chargingSkillIndex = -1;
        StopChargeEffect();

        if (index < 0)
            return false;

        SkillDefinitionSO def = skills[index];
        if (def == null)
            return false;

        // StartCharge에서 확인만 하고 소모는 안 했으므로 실제 커밋은 여기서 한다(쿨타임/스택과 같은 시점).
        // 이 시점에 실패하는 건 이론상 거의 없지만(StartCharge 이후 마나가 줄어들 수단이 현재 없음), 혹시
        // 실패해도 이미 PlayerState.Skill로 들어와 있으므로 Idle로 되돌려서 멈추지 않게 한다.
        if (status != null && !status.TryUseMana(def.GetManaCost(SkillEvolutionId.Evolution3)))
        {
            if (stateMachine.Is(PlayerState.Skill))
                stateMachine.ChangeState(PlayerState.Idle);
            return false;
        }

        ConsumeSkillUse(index, def);

        float ratio = def.evoChargeMaxSeconds > 0f ? Mathf.Clamp01(chargeElapsed / def.evoChargeMaxSeconds) : 0f;

        PreparePendingSkill(index, SkillEvolutionId.Evolution3, ratio);

        // (140번 최초 수정에서 여기 RequestSkillAni(index, false, ...)를 추가했었는데, 애니메이터
        // FighterController를 직접 열어보니 원인이 달랐다 - Fighter_Skill_Charging -> Fighter_Skill_ChargeSlash
        // 전환은 IsCharging 값만 보고(트리거 불필요, hasExitTime=false) 이미 정상 동작하도록 구성돼 있었고,
        // ChargeSlash 클립에도 AniEvent_ExecuteSkill/AniEvent_EndSkill이 전부 붙어있었다. RequestSkillAni가
        // 내부적으로 SetTrigger(Skill)까지 다시 쏘는 게 문제였다 - 이 트리거가 Charging->ChargeSlash
        // 전환(트리거 조건 없음)에서는 소모되지 않고 계속 "켜진" 채로 남아있다가, ChargeSlash가 끝나고
        // Locomotion으로 돌아가는 순간 Locomotion의 진입 조건(Skill 트리거 + SkillID==1)과 우연히 맞아떨어져서
        // 반원 베기(Fighter_Skill_HalfSlash)가 한 번 더 재생되는 원인이었다("차징 공격 후 반원베기가 한 번 더
        // 나온다" 버그 리포트로 발견). 트리거 재발사 없이 이 이벤트(IsCharging=false 설정)만으로도 충분해서
        // RequestSkillAni 호출을 제거했다.
        OnChargeAniChanged?.Invoke(false);

        //ExecuteSectorSlashEvo3(def, ratio, index);
        //StartCoroutine(ReturnToIdleAfter(0.3f));
        return true;
    }

    // 8.24 WBH 수정 : seconds 뒤 전환이 애니메이션 이벤트로 이뤄짐.
    //private IEnumerator ReturnToIdleAfter(float seconds)
    //{
    //    yield return new WaitForSeconds(seconds);
    //    if (stateMachine.Is(PlayerState.Skill))
    //        stateMachine.ChangeState(PlayerState.Idle);
    //}

    /// <summary>진행 중인 차징과 이동을 취소하며 이미 소모한 마나·쿨타임·스택은 유지한다.</summary>
    public void CancelActiveSkill()
    {
        StopAllCoroutines();
        CancelCharge();
        ClearPendingSkill();
        if (stateMachine != null && stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    private void CancelCharge()
    {
        chargingSkillIndex = -1;
        chargeElapsed = 0f;

        StopChargeEffect();
        OnChargeAniChanged?.Invoke(false);
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

        if (visibleSkillArea)
            SkillRangeVisual.ShowSector(transform.position, transform.forward, range, def.sectorAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(range, def.sectorAngle))
            ApplyHit(target, def, def.damageMultiplier, index);
    }

    /// <summary>진화1: 밀치기 + 기절 - 기본 판정에 넉백/기절 상태이상을 추가로 건다.</summary>
    private void ExecuteSectorSlashEvo1(SkillDefinitionSO def, int index)
    {
        float range = ApplySkillRangeBonus(def, index, def.sectorRange);

        if (visibleSkillArea)
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

        if (visibleSkillArea)
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

        if (visibleSkillArea)
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

        if (visibleSkillArea)
            SkillRangeVisual.ShowLine(transform.position, transform.forward, length, def.lineWidth, lineVisualColor);

        foreach (Collider target in GetLineTargets(length, def.lineWidth))
            ApplyHit(target, def, def.damageMultiplier, index);
    }

    /// <summary>진화1: 방어 감소 + 기절 - 기본 판정에 방어력 감소/기절 상태이상을 추가로 건다.</summary>
    private void ExecuteLineSlamEvo1(SkillDefinitionSO def, int index)
    {
        float length = ApplySkillRangeBonus(def, index, def.lineLength);

        if (visibleSkillArea)
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

        if (visibleSkillArea)
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

        if (visibleSkillArea)
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

        WBH_PlayerEffectCue cue = PlayerEffectCueUtility.CreateFighterSkillCue((index + 1), pendingEvo, SkillEffectPart.Main);

        playerEffect.TryGetEffectData(cue, out WBH_EffectData effectData);

        Vector3 hitPosition = target.ClosestPoint(transform.position);
        Vector3 lookDirection = transform.position - hitPosition;

        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = -transform.forward;

        WBH_DamageRequest request = combat.CreateDamageRequest(combatTarget,
                                                               WBH_AttackType.Skill,
                                                               status.CurrentElement,
                                                               damageMultiplier,
                                                               effectData: effectData,
                                                               hitPosition: hitPosition,
                                                               hitEffectDirection: lookDirection);
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
    private IEnumerator ExecuteDash(SkillDefinitionSO def, SkillEvolutionId evo, int index, float duration)
    {
        if (evo == SkillEvolutionId.Evolution1)
            controller.ApplyInvincibility(def.evoInvincibleDuration);

        Vector3 dir = pendingAimDirection;
        float distance = ApplySkillRangeBonus(def, index, def.dashDistance);

        if (visibleSkillArea)
            SkillRangeVisual.ShowLine(transform.position, dir, distance, 0.6f, dashVisualColor);

        NavMeshAgent agent = controller.agent;

        Vector3 targetPos = transform.position + dir * distance;
        if (NavMesh.Raycast(transform.position, targetPos, out NavMeshHit hit, NavMesh.AllAreas))
            targetPos = hit.position;

        //float duration = GetEnhancement(index) == SkillEnhancementId.Enhance1 // 8.25 WBH 수정. GetEffectiveDashDuration 메서드에서 계산하여 임시 저장변수 pendingDashDuration 에 할당
        //    ? def.dashDuration * (1f - def.enhanceDashSpeedBonusPercent / 100f)
        //    : def.dashDuration;

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

        ClearPendingSkill(); // 8.24 WBH 추가. 대쉬 종료 시, 기존 스킬

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

    public void PlayPendingSkillSfx(AnimationEvent animationEvent, Animator animator)
    {
        int partValue = animationEvent.intParameter;

        if (pendingSkillIndex < 0 || pendingSkillIndex >= skills.Length)
            return;

        if (!System.Enum.IsDefined(typeof(SkillEffectPart), partValue))
            return;

        if (skills[pendingSkillIndex] == null || playerEffect == null)
            return;

        WBH_PlayerEffectCue cue = PlayerEffectCueUtility.CreateFighterSkillCue((pendingSkillIndex + 1),
                                                                              pendingEvo,
                                                                              (SkillEffectPart)partValue);

        playerEffect.ScheduleSfx(cue, animator, animationEvent);
    }
}

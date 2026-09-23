using System;
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
/// (아크 불릿)/진화3(아크 캐논)까지 구현됐다(115번, 133번). 폭탄 투척/백스탭 샷(Skill2~3)도 진화 3종
/// 전부 구현됐다(118~121번) - 파이터도 처음엔 진화 없이 스킬 3개만 만들고(63번) 나중에 하나씩 만들었던
/// 것과 같은 순서(66번 이후). 강화(enhancement)는 파이터와 완전히 동일한 방식(위력/쿨타임감소/범위)으로 스킬
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
    [SerializeField] private bool visibleSkillArea = false; 
    [SerializeField] private Color sectorVisualColor = new Color(0.2f, 0.6f, 1f, 0.35f);
    [SerializeField] private Color lineVisualColor = new Color(0.2f, 0.9f, 1f, 0.35f);
    [SerializeField] private Color dashVisualColor = new Color(0.2f, 0.7f, 1f, 0.35f);

    // 슬롯 수(skills.Length)에 맞춰 Awake에서 다시 잡는다 - 궁극기(Skill4)처럼 슬롯이 늘어나도
    // 쿨타임 배열만 3칸으로 남아 IndexOutOfRange가 나지 않도록 하기 위함.
    private float[] cooldownRemaining = new float[3];

    // 아크 버스터(ArcProjectile) 전용 스택 상태. -1 = 아직 초기화 안 됨(Start에서 maxStacks로 채움).
    // FighterSkillController의 대시 2스택(진화 전용)과 달리, 이건 기본 스킬 자체가 스택형이라
    // 슬롯 인덱스에 매이지 않고 "지금 스킬 중 ArcProjectile인 것"을 찾아서 적용한다.
    private int arcBusterStacks = -1;
    private float arcBusterStackTimer;

    private bool CanUseSkill => !stateMachine.IsAnyState(PlayerState.Hit, PlayerState.Attack,
        PlayerState.Skill, PlayerState.Dodge, PlayerState.Dead) && !SkillPopupController.IsOpen;

    // ------ 9.1 WBH 추가. 애니메이션 연결 및 타격과 투사체 생성시점 전환(코드 > 애니메이션 이벤트)을 위한 변수 + 이펙트 실행을 위한 변수
    public event Action<int, int, float> OnSkillAniRequested;

    /// <summary>초기화가 끝난 스킬 생성물과 원본 프리팹을 알려 외부에서 시각 표현을 연결할 수 있게 한다.</summary>
    public event Action<GameObject, GameObject> SkillObjectSpawned;

    private WBH_PlayerEffect playerEffect;

    private int pendingSkillIndex = -1;
    private SkillEvolutionId pendingEvo; // 스킬 사용 시 스킬 진화 상태를 임시로 저장하는 변수
    private SkillEnhancementId pendingEnhance; // 스킬 사용 시 스킬 강화 상태를 임시로 저장하는 변수

    private int pendingArcBusterStacks;
    private Vector3 pendingAimDirection;
    private PlayerStatManager skillOwnerStats;

    // SW 수정
    /// <summary>호출자가 검증한 조준·목표 위치·소유자 스탯으로 스킬을 준비한다. 권한과 사거리 검증은 호출자가 담당한다.</summary>
    public bool TryUseSkill(int index, Vector3 aimDirection, Vector3 targetPosition, PlayerStatManager ownerStats)
    {
        if (ownerStats == null)
            return false;
        return TryUseSkillInternal(index, aimDirection, targetPosition, ownerStats, true);
    }

    private bool CanUseSkillFrom(bool externalInput) => stateMachine != null &&
        !stateMachine.IsAnyState(PlayerState.Hit, PlayerState.Attack, PlayerState.Skill,
            PlayerState.Dodge, PlayerState.Dead) && (externalInput || !SkillPopupController.IsOpen);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    private Vector3 pendingCursorPos; // 중복 실행 방지 변수


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
            inputHandler.OnSkillKeyPressed += HandleSkillKeyPressed;
    }

    private void OnDisable()
    {
        if (inputHandler != null)
            inputHandler.OnSkillKeyPressed -= HandleSkillKeyPressed;

        ClearPendingSkill();
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
            arcBusterStackTimer = ApplyCooldownReduction(def, index, def.stackRechargeSeconds);
    }

    private void HandleSkillKeyPressed(int index) => TryUseSkill(index);

    /// <summary>슬롯(0~2)의 스킬 데이터. UI가 이름/모양 등을 표시할 때 사용.</summary>
    public SkillDefinitionSO GetSkillDefinition(int index) =>
        index >= 0 && index < skills.Length ? skills[index] : null;

    public int SkillCount => skills.Length;

    /// <summary>인덱스(0~2 = Skill1~3)에 해당하는 스킬을 사용한다. 쿨타임 중이거나 행동 불가 상태면 조용히 실패.</summary>
    //public bool TryUseSkill(int index)
    //{
    //    if (index < 0 || index >= skills.Length)
    //        return false;

    //    SkillDefinitionSO def = skills[index];
    //    if (def == null || !CanUseSkill || !IsSkillReady(index))
    //        return false;

    //    SkillEvolutionId evo = GetEvolution(index);

    //    // 쿨타임/스택을 깎기 전에 마나부터 확인한다 - 마나가 부족하면 여기서 조용히 실패하고
    //    // 쿨타임/스택은 전혀 건드리지 않는다(138번 후속 - manaCost 수치 자체는 137/138번에서 이미 반영됨).
    //    // 진화별 마나 코스트가 설정돼 있으면 그 값을, 아니면 기본 manaCost를 쓴다(GetManaCost).
    //    if (status != null && !status.TryUseMana(def.GetManaCost(evo)))
    //        return false;

    //    // 아크 레이저(진화1)가 "현재 스택을 모두" 소모하므로, ConsumeSkillUse가 스택을 지우기 전에
    //    // 몇 스택을 들고 있었는지 먼저 캡처해서 데미지 계산(소모 스택당 보너스)에 넘겨준다.
    //    int arcBusterStacksBeforeConsume = arcBusterStacks;

    //    ConsumeSkillUse(index, def);
    //    FaceCursor();
    //    combat.CancelChase();
    //    stateMachine.ChangeState(PlayerState.Skill);

    //    switch (def.shapeType)
    //    {
    //        case SkillShapeType.SectorSlash:
    //            ExecuteSectorShot(def, index);
    //            StartCoroutine(ReturnToIdleAfter(0.3f));
    //            break;

    //        case SkillShapeType.LineSlam:
    //            ExecuteLineShot(def, index);
    //            StartCoroutine(ReturnToIdleAfter(0.3f));
    //            break;

    //        case SkillShapeType.Dash:
    //            StartCoroutine(ExecuteDash(def, index));
    //            break;

    //        case SkillShapeType.ArcProjectile:
    //            if (evo == SkillEvolutionId.Evolution1)
    //                ExecuteArcLaser(def, index, arcBusterStacksBeforeConsume);
    //            else if (evo == SkillEvolutionId.Evolution2)
    //                StartCoroutine(ExecuteArcBullet(def, index));
    //            else if (evo == SkillEvolutionId.Evolution3)
    //                ExecuteArcCannon(def, index);
    //            else
    //                ExecuteArcBuster(def, index);
    //            StartCoroutine(ReturnToIdleAfter(0.2f));
    //            break;

    //        case SkillShapeType.BombThrow:
    //            ExecuteBombThrow(def, index);
    //            StartCoroutine(ReturnToIdleAfter(0.3f));
    //            break;

    //        case SkillShapeType.BackstepShot:
    //            StartCoroutine(ExecuteBackstepShot(def, index));
    //            break;
    //    }

    //    return true;
    //}

    // 9.1 WBH 추가. 기존 TryUseSkill 에서 투사체, 데미지 부분 제외하고 PreparePendingSkill 에 일시적인 스킬 정보 전달.
    public bool TryUseSkill(int index)
    {
        if (Camera.main == null)
            return false;
        return TryUseSkillInternal(index, GetCursorDirection(), GetCursorGroundPosition(),
            GetComponentInParent<PlayerStatManager>() ?? PlayerStatManager.Instance, false);
    }

    private bool TryUseSkillInternal(int index, Vector3 aimDir, Vector3 cursorPos,
        PlayerStatManager ownerStats, bool externalInput)
    {
        if (index < 0 || index >= skills.Length)
            return false;

        if (!IsFinite(aimDir) || !IsFinite(cursorPos) || !float.IsFinite((cursorPos - transform.position).sqrMagnitude))
            return false;
        aimDir.y = 0f;
        if (aimDir.sqrMagnitude < 0.0001f || !float.IsFinite(aimDir.sqrMagnitude))
            return false;
        aimDir.Normalize();

        SkillDefinitionSO def = skills[index];
        if (def == null || !CanUseSkillFrom(externalInput) || !IsSkillReady(index) || combat == null ||
            (externalInput && (status == null || status.IsDead)))
            return false;

        SkillEvolutionId evo = GetEvolution(index);

        // 쿨타임/스택을 깎기 전에 마나부터 확인한다 - 마나가 부족하면 여기서 조용히 실패하고
        // 쿨타임/스택은 전혀 건드리지 않는다(138번 후속 - manaCost 수치 자체는 137/138번에서 이미 반영됨).
        // 진화별 마나 코스트가 설정돼 있으면 그 값을, 아니면 기본 manaCost를 쓴다(GetManaCost).
        if (status != null && !status.TryUseMana(def.GetManaCost(evo)))
            return false;

        // 아크 레이저(진화1)가 "현재 스택을 모두" 소모하므로, ConsumeSkillUse가 스택을 지우기 전에
        // 몇 스택을 들고 있었는지 먼저 캡처해서 데미지 계산(소모 스택당 보너스)에 넘겨준다.
        int arcBusterStacksBeforeConsume = arcBusterStacks;

        // !! ConsumeSkillUse보다 먼저 대입한다. 쿨타임을 소모 시점에 확정하는데 그 계산이
        //    skillOwnerStats의 쿨감 스탯을 읽기 때문이다(순서가 반대면 직전 시전의 스탯을 쓴다).
        skillOwnerStats = ownerStats;
        ConsumeSkillUse(index, def);
        combat.CancelChase();

        PreparePendingSkill(index, evo, arcBusterStacksBeforeConsume, aimDir, cursorPos);

        if(pendingAimDirection.sqrMagnitude > 0.001f)
        {
            transform.forward = pendingAimDirection;
        }

        stateMachine.ChangeState(PlayerState.Skill);
        RequestSkillAni(index);

        return true;
    }

    // 스킬당 일시 정보 저장
    private void PreparePendingSkill(int index, SkillEvolutionId evo, int arcBursterStack, Vector3 aimDirection, Vector3 cursorPos)
    {
        pendingSkillIndex = index;
        pendingEvo = evo;
        pendingEnhance = GetEnhancement(index);

        pendingArcBusterStacks = arcBursterStack;
        pendingAimDirection = aimDirection;
        pendingCursorPos = cursorPos;
    }

    // 애니메이션 파라미터 변경
    private void RequestSkillAni(int index)
    {
        SkillDefinitionSO def = skills[index];

        float backstepDuration = def != null && def.shapeType == SkillShapeType.BackstepShot ? Mathf.Max(0.01f, def.backstepDuration) : 0f;

        OnSkillAniRequested?.Invoke(GetPresentationSkillNumber(index), (int)pendingEvo, backstepDuration);
    }

    /// <summary>궁극기 슬롯. 전용 애니메이션·이펙트가 준비되면 이 보정을 통째로 지운다.</summary>
    private const int UltimateSlotIndex = 3;

    /// <summary>궁극기가 임시로 빌려 쓰는 스킬 번호(= 데이터를 복사해 온 1번 스킬, 아크 버스터).</summary>
    private const int UltimateBorrowedSkillNumber = 1;

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
    private static int GetPresentationSkillNumber(int index) =>
        index == UltimateSlotIndex ? UltimateBorrowedSkillNumber : index + 1;

    // 애니메이션 이벤트에서 실제 스킬 실행
    public void ExecutePendingSkill()
    {
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

        switch(def.shapeType)
        {
            case SkillShapeType.SectorSlash:
                ExecuteSectorShot(def, index);
                break;
            case SkillShapeType.LineSlam:
                ExecuteLineShot(def, index);
                break;
            case SkillShapeType.Dash:
                StartCoroutine(ExecuteDash(def,index, pendingAimDirection));
                break;
            case SkillShapeType.ArcProjectile:
                ExecutePendingArcSkill(def, index);
                break;
            case SkillShapeType.BombThrow:
                ExecuteBombThrow(def, index, pendingEvo,pendingCursorPos);
                break;
            case SkillShapeType.BackstepShot:
                ExecuteBackstepAction(def, index, pendingEvo);
                break;
            case SkillShapeType.CarpetBombing:
                StartCoroutine(ExecuteCarpetBombing(def, index, pendingCursorPos));
                break;

        }
    }

    /// <summary>
    /// 궁극기(융단폭격). 커서로 지정한 지점을 중심으로 한 원형 영역 **전체**를 일정 간격으로 여러 번 타격한다.
    ///
    /// 피해와 연출을 분리했다.
    ///   - 피해: 타격마다 영역 안의 적을 한 번에 전부 때린다(Physics.OverlapSphere). 폭탄이 어디에
    ///     떨어지느냐와 무관하게 영역 안이면 맞으므로 "운 좋게 빗나가는" 일이 없다.
    ///   - 연출: 같은 타이밍에 폭탄을 하늘에서 떨어뜨린다. 폭발 반경 0으로 넘겨 판정을 갖지 않게 하고,
    ///     비행·폭발 이펙트만 GunnerBomb이 그대로 처리하게 둔다.
    ///
    /// 낙하 지점은 영역 안 무작위이고, 폭탄은 지면이 아니라 영역 위 하늘에서 생성해 수직으로 내려꽂힌다.
    /// 범위 표시는 전체 폭격 영역 하나만 폭격이 끝날 때까지 유지한다.
    /// </summary>
    private IEnumerator ExecuteCarpetBombing(SkillDefinitionSO def, int index, Vector3 cursorPos)
    {
        // 지정 지점은 폭탄 투척과 같은 방식으로 최대 사거리까지만 허용한다.
        Vector3 toCursor = cursorPos - transform.position;
        toCursor.y = 0f;
        float designateRange = ApplySkillRangeBonus(def, index, def.bombThrowRange);
        Vector3 center = toCursor.sqrMagnitude > designateRange * designateRange
            ? transform.position + toCursor.normalized * designateRange
            : transform.position + toCursor;
        center.y = transform.position.y;

        // 진화3(산탄 폭격)은 "영역 전체를 때린다"는 기본 구조 자체가 달라서 별도 코루틴으로 뺀다.
        if (pendingEvo == SkillEvolutionId.Evolution3)
        {
            yield return ExecuteBarrageBombing(def, index, center);
            yield break;
        }

        // 진화1(레이저 폭격) : 1회만, 지연이 더 길다.
        // 진화2(마커 폭격)   : 횟수를 늘리고 맞은 적에게 마커를 건다.
        int waveCount = def.carpetWaveCount;
        float damagePerWave = def.carpetDamagePerWave;
        float impactDelay = def.carpetImpactDelay;
        float waveInterval = def.carpetWaveInterval;

        if (pendingEvo == SkillEvolutionId.Evolution1)
        {
            waveCount = 1;
            damagePerWave = def.evoLaserStrikeDamage;
            impactDelay = def.evoLaserStrikeDelay;
        }
        else if (pendingEvo == SkillEvolutionId.Evolution2)
        {
            waveCount = def.evoMarkerWaveCount;
            damagePerWave = def.evoMarkerDamagePerWave;
            waveInterval = def.evoMarkerWaveInterval;
        }

        float areaRadius = def.carpetAreaRadius;
        float totalDuration = waveCount * waveInterval;

        SkillRangeVisual.ShowSector(center, Vector3.forward, areaRadius, 360f, sectorVisualColor,
                                    totalDuration + impactDelay + 0.3f);

        float damageMultiplier = damagePerWave;
        if (GetEnhancement(index) == SkillEnhancementId.Enhance1)
            damageMultiplier *= 1f + def.enhanceDamageMultiplierBonusPercent / 100f;

        // 진화2만 마커를 건다. 0이면 ApplyCarpetWaveDamage가 상태이상을 붙이지 않는다.
        float markDuration = pendingEvo == SkillEvolutionId.Evolution2 ? def.evoMarkerDuration : 0f;

        WBH_EffectData effectData = GetSkillEffectData(index, pendingEvo, SkillEffectPart.Main);
        WBH_PlayerEffectCue explosionCue =
            PlayerEffectCueUtility.CreateGunnerSkillCue(GetPresentationSkillNumber(index), pendingEvo, SkillEffectPart.ProjectileExplosion1);

        for (int wave = 0; wave < waveCount; wave++)
        {
            SpawnFallingBombs(def, center, areaRadius, explosionCue);

            // 폭탄이 떨어지는 시간만큼 기다렸다가 영역 전체에 피해를 준다.
            yield return new WaitForSeconds(impactDelay);
            ApplyCarpetWaveDamage(def, index, center, areaRadius, damageMultiplier, effectData, markDuration);

            float rest = waveInterval - impactDelay;
            if (rest > 0f)
                yield return new WaitForSeconds(rest);
        }
    }

    /// <summary>
    /// 진화3 - 산탄 폭격. 영역 전체를 때리는 기본 융단폭격과 달리, 폭격 범위 안의 <b>랜덤한 지점</b>에
    /// 작은 반경의 포탄을 연속으로 떨어뜨린다. 한 발의 피해는 크지만 범위가 좁아 명중이 운에 달린다.
    ///
    /// !! 한 대상이 맞을 확률은 (포탄 반경 / 산포 반경)^2 정도다. 기본값(반경 2 / 산포 8)이면 한 발당
    ///    약 6%고 9발이면 기대 명중이 1발도 되지 않는다. 좁은 곳에 몰린 적이나 큰 적에게 쓰는 용도이고,
    ///    체감이 너무 약하면 evoBarrageScatterRadius를 줄여 포탄을 모으는 쪽으로 조정한다.
    /// </summary>
    private IEnumerator ExecuteBarrageBombing(SkillDefinitionSO def, int index, Vector3 center)
    {
        float scatterRadius = def.evoBarrageScatterRadius;
        // 포탄 반경만 스킬 범위 증가의 영향을 받는다. 산포 반경까지 넓히면 오히려 명중률이 떨어져서
        // "범위 증가"가 하향으로 작동한다.
        float shellRadius = ApplySkillRangeBonus(def, index, def.evoBarrageShellRadius);
        float totalDuration = def.evoBarrageShellCount * def.evoBarrageInterval;

        SkillRangeVisual.ShowSector(center, Vector3.forward, scatterRadius, 360f, sectorVisualColor,
                                    totalDuration + def.carpetImpactDelay + 0.3f);

        float damageMultiplier = def.evoBarrageDamagePerShell;
        if (GetEnhancement(index) == SkillEnhancementId.Enhance1)
            damageMultiplier *= 1f + def.enhanceDamageMultiplierBonusPercent / 100f;

        WBH_EffectData effectData = GetSkillEffectData(index, pendingEvo, SkillEffectPart.Main);
        WBH_PlayerEffectCue explosionCue =
            PlayerEffectCueUtility.CreateGunnerSkillCue(GetPresentationSkillNumber(index), pendingEvo, SkillEffectPart.ProjectileExplosion1);

        for (int shell = 0; shell < def.evoBarrageShellCount; shell++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * scatterRadius;
            Vector3 impactPos = center + new Vector3(offset.x, 0f, offset.y);

            SpawnFallingBombAt(def, impactPos, explosionCue);

            yield return new WaitForSeconds(def.carpetImpactDelay);
            ApplyCarpetWaveDamage(def, index, impactPos, shellRadius, damageMultiplier, effectData, 0f);

            float rest = def.evoBarrageInterval - def.carpetImpactDelay;
            if (rest > 0f)
                yield return new WaitForSeconds(rest);
        }
    }

    /// <summary>폭격 연출용 폭탄을 하늘에서 떨어뜨린다. 폭발 반경 0이라 판정은 없고 낙하·폭발 이펙트만 남는다.</summary>
    private void SpawnFallingBombs(SkillDefinitionSO def, Vector3 center, float areaRadius, WBH_PlayerEffectCue explosionCue)
    {
        if (def.bombPrefab == null)
            return;

        for (int i = 0; i < def.carpetVisualBombsPerWave; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * areaRadius;
            SpawnFallingBombAt(def, center + new Vector3(offset.x, 0f, offset.y), explosionCue);
        }
    }

    /// <summary>연출용 폭탄 한 발을 지정한 착탄 지점에 떨어뜨린다(진화3처럼 착탄점이 정해진 경우).</summary>
    private void SpawnFallingBombAt(SkillDefinitionSO def, Vector3 impactPos, WBH_PlayerEffectCue explosionCue)
    {
        if (def.bombPrefab == null)
            return;

        Vector3 skyPos = impactPos + Vector3.up * def.carpetDropHeight;

        GameObject bombGO = Instantiate(def.bombPrefab, skyPos, Quaternion.identity);
        GunnerBomb bomb = bombGO.GetComponent<GunnerBomb>();
        if (bomb == null)
        {
            Destroy(bombGO);
            return;
        }

        // 낙하 시간이 carpetImpactDelay와 얼추 맞도록 속도를 높이에서 역산한다.
        float fallSpeed = def.carpetImpactDelay > 0f
            ? def.carpetDropHeight / def.carpetImpactDelay
            : def.bombThrowSpeed;

        bomb.Initialize(impactPos, fallSpeed, 0f, 0f, 0f, enemyLayer, default(WBH_DamageRequest));
        bomb.ConfigureExplosionEffect(playerEffect, explosionCue, Vector3.one);
        SkillObjectSpawned?.Invoke(bombGO, def.bombPrefab);
    }

    /// <summary>폭격 한 번 분량의 피해를 영역 안 모든 적에게 적용한다.</summary>
    /// <summary>
    /// 지정한 지점 주변 범위의 적을 때린다. markDuration이 0보다 크면 맞은 적에게 Marked를 건다
    /// (진화2 - 마커 폭격). 마커 자체는 글리터 폭탄(폭탄 투척 진화3)이 쓰는 것과 같은 상태이상이다.
    /// </summary>
    private void ApplyCarpetWaveDamage(SkillDefinitionSO def, int index, Vector3 center, float areaRadius,
                                       float damageMultiplier, WBH_EffectData effectData, float markDuration)
    {
        Collider[] targets = Physics.OverlapSphere(center, areaRadius, enemyLayer);
        foreach (Collider target in targets)
        {
            if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
                continue;

            Vector3 hitPosition = target.ClosestPoint(center);
            WBH_DamageRequest request = combat.CreateDamageRequest(combatTarget,
                                                                   WBH_AttackType.Skill,
                                                                   status.CurrentElement,
                                                                   damageMultiplier,
                                                                   effectData: effectData,
                                                                   hitPosition: hitPosition);
            WBH_CombatManager.ProcessDamage(request);

            if (markDuration > 0f && !combatTarget.Status.IsDead)
            {
                // 마커는 갱신이 아니라 누적이다. WBH_StatusEffectBase.Refresh가 남은 시간을 새 값으로
                // 덮어쓰므로, 남은 시간을 미리 읽어 더한 총량을 넘긴다(상태이상 시스템은 손대지 않는다).
                var effectController = target.GetComponentInParent<WBH_StatusEffectController>();
                float remaining = effectController != null
                    ? effectController.GetRemainingTime(WBH_StatusEffectType.Marked)
                    : 0f;

                combatTarget.AddStatusEffect(new WBH_StatusEffectData(WBH_StatusEffectType.Marked,
                                                                      duration: remaining + markDuration,
                                                                      value: def.evoMarkerDamageMultiplier));
            }
        }
    }

    public void EndPendingSkillAni()
    {
        if (pendingSkillIndex < 0)
            return;

        SkillDefinitionSO def = skills[pendingSkillIndex];

        if (def != null && (def.shapeType == SkillShapeType.Dash || def.shapeType == SkillShapeType.BackstepShot))
            return;

        ClearPendingSkill();

        if(stateMachine.Is(PlayerState.Skill))
        {
            stateMachine.ChangeState(PlayerState.Idle);
        }
    }

    /// <summary>진행 중인 연사와 이동을 취소하며 이미 소모한 마나·쿨타임·스택은 유지한다.</summary>
    public void CancelActiveSkill()
    {
        StopAllCoroutines();
        ClearPendingSkill();
        if (stateMachine != null && stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    private void ClearPendingSkill()
    {
        playerEffect?.CancelPendingSfx();
        pendingSkillIndex = -1;
        pendingEvo = SkillEvolutionId.None;
        pendingEnhance = SkillEnhancementId.None;

        pendingArcBusterStacks = 0;
        pendingAimDirection = Vector3.zero;
        pendingCursorPos = Vector3.zero;
    }

    // 아크버스터의 경우 분기가 많아 별도 메서드 생성.
    private void ExecutePendingArcSkill(SkillDefinitionSO def, int index)
    {
        switch(pendingEvo)
        {
            case SkillEvolutionId.Evolution1:
                ExecuteArcLaser(def, index, pendingArcBusterStacks);
                break;
            case SkillEvolutionId.Evolution2:
                StartCoroutine(ExecuteArcBullet(def,index));
                break;
            case SkillEvolutionId.Evolution3:
                ExecuteArcCannon(def, index);
                break;
            default: ExecuteArcBuster(def, index);
                break;
        }
    }

    // 이펙트 스케일 조정, 재생 메서드 호출
    public void PlayPendingSkillEffect(int partValue)
    {
        if (pendingSkillIndex < 0 || pendingSkillIndex >= skills.Length)
            return;

        if (!System.Enum.IsDefined(typeof(SkillEffectPart), partValue))
        {
            Log.Warning($"알수 없는 스킬 이펙트 부가정보 : {partValue}");
            return;
        }

        SkillDefinitionSO def = skills[pendingSkillIndex];

        if (def == null || playerEffect == null)
            return;

        SkillEffectPart part = (SkillEffectPart)partValue;

        WBH_PlayerEffectCue cue = PlayerEffectCueUtility.CreateGunnerSkillCue(GetPresentationSkillNumber(pendingSkillIndex), pendingEvo, part);

        Vector3 scaleMultiPlier = GetPendingSkillEffectScale(partValue);

        playerEffect.PlayEffect(cue, scaleMultiPlier);
    }

    /// <summary>시전 중인 원본 이펙트 배율을 반환해 외부 표시에서도 같은 계산을 사용한다.</summary>
    public Vector3 GetPendingSkillEffectScale(int partValue)
    {
        if (pendingSkillIndex < 0 || pendingSkillIndex >= skills.Length ||
            !System.Enum.IsDefined(typeof(SkillEffectPart), partValue)) return Vector3.one;
        return CalculatePendingEffectScale(skills[pendingSkillIndex], (SkillEffectPart)partValue);
    }

    // 기본 스킬 범위와 스킬범위 보너스 스탯이 적용된 스킬범위 비교해서 이펙트 크기 결정
    private Vector3 CalculatePendingEffectScale(SkillDefinitionSO def, SkillEffectPart part)
    {
        if (def == null)
            return Vector3.one;

        if (part != SkillEffectPart.Main && part != SkillEffectPart.Ground)
            return Vector3.one;

        float baseRange = GetPendingEffectBaseRange(def);

        if (baseRange <= Mathf.Epsilon)
            return Vector3.one;

        float appliedRange = ApplyPendingSkillRangeBonus(def, baseRange);

        float rangeScale = appliedRange / baseRange;

        return Vector3.one * rangeScale;
    }

    // 기본 스킬 범위 가져오는 메서드
    private float GetPendingEffectBaseRange(SkillDefinitionSO def)
    {
        return def.shapeType switch
        {
            SkillShapeType.SectorSlash => def.sectorRange,
            SkillShapeType.LineSlam => def.lineLength,
            SkillShapeType.Dash => def.dashDistance,

            // 아크레이저 진화 1의 직선 범위 이펙트 확대
            SkillShapeType.ArcProjectile when pendingEvo == SkillEvolutionId.Evolution1 => def.evoLaserLength,

            // 백스텝의 공격 범위 이펙트 확대
            SkillShapeType.BackstepShot => def.backstepConeRange,

            // 폭탄 투척은 최대 사거리만 늘어나기에 이펙트 확대X
            SkillShapeType.BombThrow => 0f,

            _ => 0f,
        };
    }

    // 스킬 범위 보너스 적용 메서드
    private float ApplyPendingSkillRangeBonus(SkillDefinitionSO def, float baseRange)
    {
        float flatBonus = 0f;
        float percentBonus = 0f;

        if(skillOwnerStats != null)
        {
            skillOwnerStats.GetSkillRangeBonus(out flatBonus, out percentBonus);
        }

        if(pendingEnhance == SkillEnhancementId.Enhance3)
        {
            percentBonus += def.enhanceRangeBonusPercent;
        }

        return (baseRange + flatBonus) * (1f + percentBonus / 100f);
    }

    // arkbuster 사격 시, 폭발이펙트 재생을 위해 이펙트데이터와 크기, 기초 설정을 세팅하는 메서드
    private void ConfigureArcProjectileEffect(GunnerArcProjectile projectile, SkillDefinitionSO def)
    {
        WBH_PlayerEffectCue cue = PlayerEffectCueUtility.CreateGunnerSkillCue(GetPresentationSkillNumber(pendingSkillIndex), pendingEvo, SkillEffectPart.ProjectileExplosion1);
        Vector3 scaleMultiplier = CalculatePendingEffectScale(def, SkillEffectPart.ProjectileExplosion1);
        projectile.ConfigureExplosionEffect(playerEffect, cue, scaleMultiplier);
    }

    // -------

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
        return ApplyCooldownReduction(def, index, baseCooldown);
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
        {
            int requiredStacks = GetEvolution(index) == SkillEvolutionId.Evolution3 ? skills[index].evoCannonStackCost : 1;
            return arcBusterStacks >= requiredStacks && cooldownRemaining[index] <= 0f;
        }

        return cooldownRemaining[index] <= 0f;
    }

    private void ConsumeSkillUse(int index, SkillDefinitionSO def)
    {
        if (IsArcBusterSlot(index))
        {
            SkillEvolutionId evo = GetEvolution(index);
            if (evo == SkillEvolutionId.Evolution1)
                arcBusterStacks = 0; // 아크 레이저 - "현재의 모든 스택을 소모"
            else if (evo == SkillEvolutionId.Evolution3)
                arcBusterStacks -= def.evoCannonStackCost; // 아크 캐논 - 스택 2개 소모
            else
                arcBusterStacks--;

            cooldownRemaining[index] = def.cooldownSeconds; // 연사 제한(1초) - 강화·쿨감과는 별개 개념이라 안 줄임
            if (arcBusterStackTimer <= 0f)
                arcBusterStackTimer = ApplyCooldownReduction(def, index, def.stackRechargeSeconds);
            return;
        }

        cooldownRemaining[index] = ApplyCooldownReduction(def, index, def.cooldownSeconds);
    }

    /// <summary>
    /// 최종 쿨타임 - 스킬별 강화(Enhance2)와 플레이어 쿨감 스탯(cdr)을 <b>곱연산</b>으로 겹친다.
    /// FighterSkillController와 같은 공식이다.
    ///
    /// !! 쿨감 스탯이 원래 쿨타임 계산에 전혀 반영되지 않고 있었다(표시와 집계만 있었다).
    ///    아이템·패시브·레벨업·버프의 cdrFlat이 전부 무효였던 문제를 여기서 잇는다.
    /// </summary>
    private float ApplyCooldownReduction(SkillDefinitionSO def, int index, float baseCooldown)
    {
        return ApplyCooldownEnhancement(def, index, baseCooldown) * GetCooldownReductionMultiplier();
    }

    /// <summary>쿨감 스탯을 배율로. PlayerStat에서 이미 0~70 클램프지만 여기서도 한 번 더 막는다.</summary>
    private float GetCooldownReductionMultiplier()
    {
        PlayerStatManager stats = skillOwnerStats != null ? skillOwnerStats : PlayerStatManager.Instance;
        if (stats == null || stats.Stat == null)
            return 1f;

        return 1f - Mathf.Clamp(stats.Stat.cdr, 0f, MaxCooldownReductionPercent) / 100f;
    }

    /// <summary>쿨감 상한(%). PlayerStat의 클램프와 같은 값.</summary>
    private const float MaxCooldownReductionPercent = 70f;

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
        if (skillOwnerStats != null)
            skillOwnerStats.GetSkillRangeBonus(out flatBonus, out percentBonus);

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

        if(visibleSkillArea)
            SkillRangeVisual.ShowSector(transform.position, transform.forward, range, def.sectorAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(range, def.sectorAngle))
            ApplyHit(target, def, def.damageMultiplier, index);
    }

    /// <summary>관통 사격 - 정면 직선 범위를 즉시 명중시킨다(투사체 없음, 위 클래스 주석 참고).</summary>
    private void ExecuteLineShot(SkillDefinitionSO def, int index)
    {
        float length = ApplySkillRangeBonus(def, index, def.lineLength);

        if(visibleSkillArea)
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

        WBH_EffectData effectData = GetSkillEffectData(index, pendingEvo, SkillEffectPart.Main);
        WBH_DamageRequest request = combat.CreateDamageRequest(WBH_AttackType.Skill,
                                                               status.CurrentElement,
                                                               damageMultiplier,
                                                               effectData: effectData);

        GameObject projectileGO = Instantiate(def.arcProjectilePrefab, spawnPos, Quaternion.LookRotation(dir));
        GunnerArcProjectile projectile = projectileGO.GetComponent<GunnerArcProjectile>();
        if (projectile == null)
        {
            Debug.LogWarning("[GunnerSkillController] arcProjectilePrefab에 GunnerArcProjectile 컴포넌트가 없습니다.");
            Destroy(projectileGO);
            return;
        }

        projectile.Initialize(dir, def.projectileSpeed, maxDistance, def.explosionRadius, enemyLayer, request);
        ConfigureArcProjectileEffect(projectile, def); // 폭발 이펙트 재생을 위한 세팅
        SkillObjectSpawned?.Invoke(projectileGO, def.arcProjectilePrefab);
    }

    /// <summary>
    /// 진화3: 아크 캐논 - 기본 아크 버스터와 발사 로직은 동일하지만, 스택 2개를 소모하는 대신(ConsumeSkillUse에서
    /// 이미 처리) 피해 배율을 evoCannonDamageMultiplier(250%)로, 폭발 반경을 evoCannonExplosionRadius(3,
    /// 기본 explosionRadius=1의 3배)로 각각 키운 확장 폭발탄을 발사한다. 사거리(Enhance3)는 기본과 동일하게
    /// projectileMaxDistance에 적용되고, 폭발 반경 자체는(기본 아크 버스터와 마찬가지로) 범위 강화의 영향을
    /// 받지 않는다.
    /// </summary>
    private void ExecuteArcCannon(SkillDefinitionSO def, int index)
    {
        GameObject prefab = def.evoCannonProjectilePrefab != null ? def.evoCannonProjectilePrefab : def.arcProjectilePrefab;
        if (prefab == null)
        {
            Debug.LogWarning("[GunnerSkillController] arcProjectilePrefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Vector3 dir = transform.forward;
        float maxDistance = ApplySkillRangeBonus(def, index, def.projectileMaxDistance);

        float damageMultiplier = def.evoCannonDamageMultiplier;
        if (GetEnhancement(index) == SkillEnhancementId.Enhance1)
            damageMultiplier *= 1f + def.enhanceDamageMultiplierBonusPercent / 100f;

        WBH_EffectData effectData = GetSkillEffectData(index, pendingEvo, SkillEffectPart.Main);
        WBH_DamageRequest request = combat.CreateDamageRequest(WBH_AttackType.Skill, 
                                                               status.CurrentElement,
                                                               damageMultiplier,
                                                               effectData: effectData);

        GameObject projectileGO = Instantiate(prefab, spawnPos, Quaternion.LookRotation(dir));
        GunnerArcProjectile projectile = projectileGO.GetComponent<GunnerArcProjectile>();
        if (projectile == null)
        {
            Debug.LogWarning("[GunnerSkillController] arcProjectilePrefab에 GunnerArcProjectile 컴포넌트가 없습니다.");
            Destroy(projectileGO);
            return;
        }

        // 전용 에너지 뭉치 프리팹을 쓸 때는 이미 그 자체로 크게 디자인돼 있어서 추가 배율이 필요 없고,
        // 기본 프리팹으로 폴백된 경우에만 폭발 반경 비율(기본의 3배)만큼 시각 크기를 키운다.
        float visualScale = def.evoCannonProjectilePrefab == null && def.explosionRadius > 0f
            ? def.evoCannonExplosionRadius / def.explosionRadius
            : 1f;
        projectile.Initialize(dir, def.projectileSpeed, maxDistance, def.evoCannonExplosionRadius, enemyLayer, request, visualScale: visualScale);
        ConfigureArcProjectileEffect(projectile, def); // 폭발 이펙트 재생을 위한 세팅
        SkillObjectSpawned?.Invoke(projectileGO, prefab);
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

        if(visibleSkillArea)
            SkillRangeVisual.ShowLine(transform.position, transform.forward, length, def.evoLaserWidth, lineVisualColor);

        int bonusStacks = Mathf.Min(consumedStacks, def.evoLaserMaxBonusStacks);
        float damageMultiplier = def.damageMultiplier * (1f + def.evoLaserDamagePerStackPercent / 100f * bonusStacks);

        foreach (Collider target in GetLineTargets(length, def.evoLaserWidth))
            ApplyHit(target, def, damageMultiplier, index);
    }

    /// <summary>
    /// 진화2: 아크 불릿 - 같은 스택 1개(ConsumeSkillUse에서 이미 1 차감)로 GunnerArcProjectile을
    /// evoArcBulletInterval 간격으로 3번 연달아 발사한다. 발당 피해 배율은 기본 damageMultiplier 대신
    /// evoArcBulletDamageMultiplier를 그대로 쓴다. 기본 아크 버스터(ExecuteArcBuster)와 발사 로직은
    /// 동일하지만, 폭발 속성은 뺐다(사용자 요청 - 118번) - explodeOnHit=false로 넘겨서 맞은 대상
    /// 하나에게만 데미지가 들어가고 explosionRadius 범위 판정은 하지 않는다.
    /// </summary>
    private IEnumerator ExecuteArcBullet(SkillDefinitionSO def, int index)
    {
        if (def.arcProjectilePrefab == null)
        {
            Debug.LogWarning("[GunnerSkillController] arcProjectilePrefab이 연결되지 않았습니다.");
            yield break;
        }

        float maxDistance = ApplySkillRangeBonus(def, index, def.projectileMaxDistance);

        float damageMultiplier = def.evoArcBulletDamageMultiplier;
        if (GetEnhancement(index) == SkillEnhancementId.Enhance1)
            damageMultiplier *= 1f + def.enhanceDamageMultiplierBonusPercent / 100f;

        for (int shot = 0; shot < 3; shot++)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Vector3 dir = transform.forward;

            WBH_EffectData effectData = GetSkillEffectData(index, pendingEvo, SkillEffectPart.Main);
            WBH_DamageRequest request = combat.CreateDamageRequest(WBH_AttackType.Skill,
                                                                   status.CurrentElement,
                                                                   damageMultiplier,
                                                                   effectData: effectData);

            GameObject projectileGO = Instantiate(def.arcProjectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            GunnerArcProjectile projectile = projectileGO.GetComponent<GunnerArcProjectile>();
            if (projectile == null)
            {
                Debug.LogWarning("[GunnerSkillController] arcProjectilePrefab에 GunnerArcProjectile 컴포넌트가 없습니다.");
                Destroy(projectileGO);
                yield break;
            }

            projectile.Initialize(dir, def.projectileSpeed, maxDistance, 0f, enemyLayer, request, explodeOnHit: false);
            SkillObjectSpawned?.Invoke(projectileGO, def.arcProjectilePrefab);

            if (shot < 2)
                yield return new WaitForSeconds(def.evoArcBulletInterval);
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
    /// // 9.1 WBH 입력 당시 진화와 위치정보 매개변수를 추가로 전달받기 위해 매개변수 추가.
    private void ExecuteBombThrow(SkillDefinitionSO def, int index, SkillEvolutionId evo, Vector3 cursorPos)
    {
        if (def.bombPrefab == null)
        {
            Debug.LogWarning("[GunnerSkillController] bombPrefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

        Vector3 toCursor = cursorPos - transform.position;
        toCursor.y = 0f;

        float throwRange = ApplySkillRangeBonus(def, index, def.bombThrowRange);
        Vector3 targetPos = toCursor.sqrMagnitude > throwRange * throwRange
            ? transform.position + toCursor.normalized * throwRange
            : transform.position + toCursor;
        targetPos.y = transform.position.y;

        float explosionRadius = evo == SkillEvolutionId.Evolution2 ? def.evoEnergyBurstRadius : def.bombExplosionRadius;

        // 폭발 반경 표시 - 원형이라 angle=360으로 ShowSector 재사용, forward는 원이라 무의미.
        // 표시 시간은 폭탄이 실제로 위협인 구간(비행 시간 + 퓨즈)만큼 - GunnerBomb.Initialize의 travelTime 계산과 동일.
        float showDuration = Mathf.Max(0.2f, Vector3.Distance(spawnPos, targetPos) / def.bombThrowSpeed) + def.bombFuseSeconds;

        if(visibleSkillArea)
            SkillRangeVisual.ShowSector(targetPos, Vector3.forward, explosionRadius, 360f, sectorVisualColor, showDuration);

        // 진화1(집속 폭탄) - 2차 폭발 범위도 같은 자리에 겹쳐서 표시한다. 1차 원이 사라지는 시점(showDuration)에
        // 맞춰 2차 원이 evoClusterDelaySeconds만큼 더 유지되다 사라지게 해서, "작은 원이 먼저 없어지고 큰 원이
        // 그 다음에 없어짐"으로 두 번 터진다는 걸 시각적으로 알 수 있게 했다.
        if (evo == SkillEvolutionId.Evolution1 && visibleSkillArea)
            SkillRangeVisual.ShowSector(targetPos, Vector3.forward, def.evoClusterRadius, 360f, sectorVisualColor, showDuration + def.evoClusterDelaySeconds);

        float damageMultiplier = def.damageMultiplier;
        if (GetEnhancement(index) == SkillEnhancementId.Enhance1)
            damageMultiplier *= 1f + def.enhanceDamageMultiplierBonusPercent / 100f;

        WBH_EffectData effectData = GetSkillEffectData(index, pendingEvo, SkillEffectPart.Main);
        WBH_DamageRequest request = combat.CreateDamageRequest(WBH_AttackType.Skill,
                                                               status.CurrentElement,
                                                               damageMultiplier,
                                                               effectData: effectData);

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

        WBH_PlayerEffectCue explosionCue = PlayerEffectCueUtility.CreateGunnerSkillCue(GetPresentationSkillNumber(index), evo, SkillEffectPart.ProjectileExplosion1); // 1차 폭발 이펙트
        WBH_PlayerEffectCue secondExplosionCue = evo == SkillEvolutionId.Evolution1 ?
            PlayerEffectCueUtility.CreateGunnerSkillCue(GetPresentationSkillNumber(index), evo, SkillEffectPart.ProjectileExplosion2) : WBH_PlayerEffectCue.None; // 2차 폭발 이펙트

        bomb.ConfigureExplosionEffect(playerEffect, explosionCue, Vector3.one, secondExplosionCue, Vector3.one);
        SkillObjectSpawned?.Invoke(bombGO, def.bombPrefab);
    }

    /// <summary>강화(Enhance1: 위력 강화)가 선택돼 있으면 데미지 계수에 곱해지는 보너스를 곱한다.</summary>
    private void ApplyHit(Collider target, SkillDefinitionSO def, float damageMultiplier, int index)
    {
        if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
            return;

        if (GetEnhancement(index) == SkillEnhancementId.Enhance1)
            damageMultiplier *= 1f + def.enhanceDamageMultiplierBonusPercent / 100f;

        WBH_EffectData effectData = GetSkillEffectData(index, pendingEvo, SkillEffectPart.Main);

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

    /// <summary>백스탭 샷 진화3(제압 사격) 전용 - 맞은 적을 플레이어 반대 방향으로 밀쳐낸다. 기절은 안 걸어서
    /// 순수 거리 제어 효과로만 둔다(FighterSkillController.ApplyKnockbackAndStun과 달리 스턴 없음).</summary>
    private void ApplyBackstepKnockback(Collider target, SkillDefinitionSO def)
    {
        if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
            return;

        Vector3 dir = (target.transform.position - transform.position).normalized;
        dir.y = 0f;

        var knockback = new WBH_StatusEffectData(WBH_StatusEffectType.KnockBack,
            duration: def.evoSuppressKnockbackDuration, direction: dir, force: def.evoSuppressKnockbackForce);

        combatTarget.AddStatusEffect(knockback);
    }

    /// <summary>
    /// 후방 회피 - 커서 방향으로 짧게 대시한다. FighterSkillController.ExecuteDash와 이동 로직은 동일하고,
    /// 무적/스택/피해버프 같은 진화 전용 효과가 아직 없다는 점만 다르다.
    /// 강화(Enhance1: 위력 강화)는 거너도 자체 피해가 없는 이동기라 대신 이동 시간을 줄여 더 빠르게 만든다.
    /// </summary>
    /// // 9.1 WBH 추가. 방향 정보 전달받기 위해 매개변수 추가
    private IEnumerator ExecuteDash(SkillDefinitionSO def, int index, Vector3 direction)
    {
        Vector3 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        float distance = ApplySkillRangeBonus(def, index, def.dashDistance);
        
        if(visibleSkillArea)
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

        ClearPendingSkill();

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
    /// <summary>
    /// 진화1: 디코이 설치 - 원뿔 공격 대신 백스탭 이동 전 위치(spawnPosition)에 GunnerDecoy를 설치한다.
    /// evoDecoyFuseSeconds 후 evoDecoyExplosionRadius 범위로 자동 폭발(GunnerDecoy 내부 처리). "적을
    /// 도발"하는 부분은 구현하지 않았다(사용자 선택 - 120번 로그 참고, BH님 소유 적 AI 타겟팅을
    /// 안 건드리기로 함).
    /// </summary>
    private void ExecuteDecoyDeploy(SkillDefinitionSO def, int index, Vector3 spawnPosition)
    {
        if (def.evoDecoyPrefab == null)
        {
            Debug.LogWarning("[GunnerSkillController] evoDecoyPrefab이 연결되지 않았습니다.");
            return;
        }

        if(visibleSkillArea)
            SkillRangeVisual.ShowSector(spawnPosition,
                                        Vector3.forward,
                                        def.evoDecoyExplosionRadius,
                                        360f,
                                        sectorVisualColor,
                                        def.evoDecoyFuseSeconds);

        float damageMultiplier = def.evoDecoyDamageMultiplier;
        if (GetEnhancement(index) == SkillEnhancementId.Enhance1)
            damageMultiplier *= 1f + def.enhanceDamageMultiplierBonusPercent / 100f;

        WBH_EffectData effectData = GetSkillEffectData(index, pendingEvo, SkillEffectPart.Main);
        WBH_DamageRequest request = combat.CreateDamageRequest(WBH_AttackType.Skill,
                                                               status.CurrentElement,
                                                               damageMultiplier,
                                                               effectData: effectData);

        GameObject decoyGO = Instantiate(def.evoDecoyPrefab, spawnPosition, Quaternion.identity);
        GunnerDecoy decoy = decoyGO.GetComponent<GunnerDecoy>();
        if (decoy == null)
        {
            Debug.LogWarning("[GunnerSkillController] evoDecoyPrefab에 GunnerDecoy 컴포넌트가 없습니다.");
            Destroy(decoyGO);
            return;
        }

        decoy.Initialize(def.evoDecoyFuseSeconds, def.evoDecoyExplosionRadius, enemyLayer, request);

        WBH_PlayerEffectCue explosionCue = PlayerEffectCueUtility.CreateGunnerSkillCue(GetPresentationSkillNumber(index),
                                                                                       SkillEvolutionId.Evolution1,
                                                                                       SkillEffectPart.ProjectileExplosion1);
        decoy.ConfigureExplosionEffect(playerEffect, explosionCue, Vector3.one);
        SkillObjectSpawned?.Invoke(decoyGO, def.evoDecoyPrefab);
    }

    // 9.1 WBH 추가. 매개변수로 진화를 전달받게끔 변경. 공격부분과 이동부분 분리를 위해 주석 처리
    //private IEnumerator ExecuteBackstepShot(SkillDefinitionSO def, int index, SkillEvolutionId evolution)
    //{
    //    SkillEvolutionId evo = evolution;

    //    if (evo == SkillEvolutionId.Evolution1)
    //        ExecuteDecoyDeploy(def, index, transform.position); // 처음 위치(백스탭 이동 전) - 원뿔 공격 대신
    //    else
    //    {
    //        float coneRange = ApplySkillRangeBonus(def, index, def.backstepConeRange);
    //        SkillRangeVisual.ShowSector(transform.position, transform.forward, coneRange, def.backstepConeAngle, sectorVisualColor);

    //        foreach (Collider target in GetSectorTargets(coneRange, def.backstepConeAngle))
    //        {
    //            ApplyHit(target, def, def.damageMultiplier, index);

    //            if (evo == SkillEvolutionId.Evolution3) // 제압 사격 - 넉백 추가
    //                ApplyBackstepKnockback(target, def);
    //        }
    //    }

    //    if (evo == SkillEvolutionId.Evolution2) // 긴급 회피 - 백스탭 이동 중 무적
    //        controller.ApplyInvincibility(def.evoBackstepInvincibleDuration);

    //    Vector3 dir = -transform.forward; // FaceCursor 적용 후라 -forward = 커서 반대 방향(후방)
    //    float distance = ApplySkillRangeBonus(def, index, def.backstepDistance);
    //    SkillRangeVisual.ShowLine(transform.position, dir, distance, 0.6f, dashVisualColor);

    //    NavMeshAgent agent = controller.agent;

    //    Vector3 targetPos = transform.position + dir * distance;
    //    if (NavMesh.Raycast(transform.position, targetPos, out NavMeshHit hit, NavMesh.AllAreas))
    //        targetPos = hit.position;

    //    Vector3 start = transform.position;
    //    float elapsed = 0f;

    //    while (elapsed < def.backstepDuration)
    //    {
    //        elapsed += Time.deltaTime;
    //        float t = Mathf.Clamp01(elapsed / def.backstepDuration);
    //        Vector3 next = Vector3.Lerp(start, targetPos, t);
    //        agent.Move(next - transform.position);
    //        yield return null;
    //    }

    //    agent.Warp(targetPos);

    //    ClearPendingSkill();

    //    if (stateMachine.Is(PlayerState.Skill))
    //        stateMachine.ChangeState(PlayerState.Idle);
    //}

    // 스킬 3 (D 스킬) 공격부분
    private void ExecuteBackstepAction(SkillDefinitionSO def, int index, SkillEvolutionId evo)
    {
        if(evo == SkillEvolutionId.Evolution1)
        {
            ExecuteDecoyDeploy(def, index, transform.position);
            return;
        }

        float coneRange = ApplySkillRangeBonus(def, index, def.backstepConeRange);

        if(visibleSkillArea)
            SkillRangeVisual.ShowSector(transform.position, transform.forward, coneRange, def.backstepConeAngle, sectorVisualColor);

        foreach (Collider target in GetSectorTargets(coneRange, def.backstepConeAngle))
        {
            ApplyHit(target, def, def.damageMultiplier, index);
            
            if(evo == SkillEvolutionId.Evolution3)
            {
                ApplyBackstepKnockback(target, def);
            }
        }
    }

    // 스킬 3 (D 스킬) 이동부분
    public void ExecutePendingBackstepMove()
    {
        if (pendingSkillIndex < 0 || !stateMachine.Is(PlayerState.Skill))
            return;

        int index = pendingSkillIndex;

        SkillDefinitionSO def = skills[index];

        if (def == null || def.shapeType != SkillShapeType.BackstepShot)
            return;

        StartCoroutine(CoExecutePendingBackstepMove(def, index, pendingEvo, pendingAimDirection));
    }

    private IEnumerator CoExecutePendingBackstepMove(SkillDefinitionSO def, int index, SkillEvolutionId evo, Vector3 aimDir)
    {
        if (evo == SkillEvolutionId.Evolution2) // 긴급 회피 - 백스탭 이동 중 무적
            controller.ApplyInvincibility(def.evoBackstepInvincibleDuration);

        Vector3 dir = -aimDir; // 커서 반대 방향(후방)으로 움직임.
        float distance = ApplySkillRangeBonus(def, index, def.backstepDistance);
        
        if(visibleSkillArea)
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

        ClearPendingSkill();

        if (stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }
    // ----

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
    private WBH_EffectData GetSkillEffectData(int index,
                                              SkillEvolutionId evolution,
                                              SkillEffectPart part)
    {
        if (playerEffect == null)
            return null;

        WBH_PlayerEffectCue cue = PlayerEffectCueUtility.CreateGunnerSkillCue(GetPresentationSkillNumber(index), evolution, part);
        playerEffect.TryGetEffectData(cue, out WBH_EffectData data);
        return data;
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

        WBH_PlayerEffectCue cue = PlayerEffectCueUtility.CreateGunnerSkillCue(GetPresentationSkillNumber(pendingSkillIndex),
                                                                              pendingEvo,
                                                                              (SkillEffectPart)partValue);

        playerEffect.ScheduleSfx(cue, animator, animationEvent);
    }
}

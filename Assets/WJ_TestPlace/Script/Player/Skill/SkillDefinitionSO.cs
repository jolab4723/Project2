using UnityEngine;
using ItemSystem;

/// <summary>스킬 하나가 실제로 어떤 모양의 판정을 쓰는지.</summary>
public enum SkillShapeType
{
    /// <summary>부채꼴(반원 등) 범위 - 구체 오버랩 + 각도 필터.</summary>
    SectorSlash,
    /// <summary>정면 직선(사각형) 범위.</summary>
    LineSlam,
    /// <summary>커서 방향으로 짧게 이동만 한다(피해 없음).</summary>
    Dash,
}

/// <summary>
/// 액티브 스킬 하나의 설계 데이터. 지금은 인스펙터에서 직접 값을 채운다(엑셀 파이프라인 없음) -
/// 수치/밸런스를 맞추는 중이라 빠르게 조정하려고 이렇게 시작했고, 안정되면 아이템처럼
/// Excel 기반 파이프라인으로 옮길 수 있다.
///
/// 진화(3가지 형태 중 선택)는 아래 evo* 필드로 시작했다 - 스킬마다 진화 3개의 효과가 서로 달라서
/// 필드도 스킬 형태별로 나눠 정의한다("SectorSlash 진화용" 등 헤더 참고, 실제로 사용되는 필드는
/// 그 스킬의 shapeType과 FighterSkillController에서 선택된 SkillEvolutionId에 따라 갈린다).
/// 강화(위력/쿨타임/범위 중 선택, SkillEnhancementId)는 진화와 별개의 축으로 아래 enhance* 필드에 있다.
/// </summary>
[CreateAssetMenu(menuName = "Skill/SkillDefinition")]
public class SkillDefinitionSO : ScriptableObject
{
    [Header("기본 정보")]
    public ActiveSkillId skillId;
    public string skillName;
    public Sprite icon;

    [Header("공통")]
    public float cooldownSeconds = 5f;
    public float damageMultiplier = 1.5f;
    public SkillShapeType shapeType;

    [Header("SectorSlash일 때만 사용 (부채꼴 범위)")]
    public float sectorRange = 4f;
    [Tooltip("전체 각도. 180이면 정반원.")]
    public float sectorAngle = 180f;

    [Header("LineSlam일 때만 사용 (직선 범위)")]
    public float lineLength = 4f;
    public float lineWidth = 2f;

    [Header("Dash일 때만 사용 (커서 방향 대시, 피해 없음)")]
    public float dashDistance = 4f;
    public float dashDuration = 0.15f;

    [Header("SectorSlash 진화1 전용 (밀치기 + 기절)")]
    [Tooltip("밀려나는 거리. WBH_StatusEffectPresets.Knockback1의 기본값(10)이 너무 멀어서 스킬 전용으로 따로 둠.")]
    public float evoKnockbackForce = 5f;
    [Tooltip("밀려나는 데 걸리는 시간(초).")]
    public float evoKnockbackDuration = 0.3f;

    [Header("SectorSlash 진화3 전용 (원형 + 차징, 차징 시간에 비례해 데미지 증가)")]
    [Tooltip("누르고 있으면 최대 이 시간(초)까지 차징된다. 도달하면 자동 발동.")]
    public float evoChargeMaxSeconds = 3f;
    [Tooltip("0초 차징(즉시 발동)일 때 적용되는 피해 배율.")]
    public float evoChargeMinDamageMultiplier = 1.2f;
    [Tooltip("최대 차징(evoChargeMaxSeconds)일 때 적용되는 피해 배율. Min~Max로 선형 보간.")]
    public float evoChargeMaxDamageMultiplier = 1.6f;

    [Header("LineSlam 진화1 전용 (방어 감소 + 기절)")]
    [Tooltip("방어력 배율. 0.7이면 방어력 30% 감소.")]
    public float evoDefenseDownMultiplier = 0.7f;
    [Tooltip("방어 감소가 유지되는 시간(초).")]
    public float evoDefenseDownDuration = 4f;

    [Header("LineSlam 진화2 전용 (범위 증가 + 에어본)")]
    public float evoWideLineLength = 6f;
    public float evoWideLineWidth = 3f;
    public float evoAirborneHeight = 3f;
    public float evoAirborneDuration = 1.5f;

    [Header("LineSlam 진화3 전용 (범위 감소 + 강한 데미지)")]
    public float evoNarrowLineLength = 2f;
    public float evoNarrowLineWidth = 1f;
    public float evoNarrowDamageMultiplier = 3.5f;

    [Header("Dash 진화1 전용 (무적 부여)")]
    [Tooltip("대시 시작과 동시에 무적이 걸리는 시간(초). dashDuration보다 길게 잡으면 착지 직후까지 무적이 유지된다.")]
    public float evoInvincibleDuration = 0.5f;

    [Header("Dash 진화2 전용 (2스택화 - 쿨타임 대신 스택으로 관리)")]
    public int evoDashMaxStacks = 2;
    [Tooltip("스택 1개가 다시 차는 데 걸리는 시간(초).")]
    public float evoDashStackRechargeSeconds = 4f;

    [Header("Dash 진화3 전용 (대시 후 피해 증가 버프)")]
    public BuffDefinitionSO evoDashDamageBuff;

    [Header("강화 - 위력(Enhance1), 전 스킬 공통이지만 shapeType별로 의미가 다름")]
    [Tooltip("SectorSlash/LineSlam 전용: 데미지 계수(damageMultiplier)에 곱해지는 보너스(%).")]
    public float enhanceDamageMultiplierBonusPercent = 15f;
    [Tooltip("Dash 전용: 자체 피해가 없어서 대신 대시 이동 시간(dashDuration)을 줄이는 비율(%).")]
    public float enhanceDashSpeedBonusPercent = 15f;

    [Header("강화 - 쿨타임 감소(Enhance2), 전 스킬 공통")]
    public float enhanceCooldownReductionPercent = 15f;

    [Header("강화 - 범위 증가(Enhance3), 전 스킬 공통. FighterSkillController.ApplySkillRangeBonus의 % 항에 합류")]
    public float enhanceRangeBonusPercent = 15f;
}

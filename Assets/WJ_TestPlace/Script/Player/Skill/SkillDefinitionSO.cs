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
    /// <summary>정면으로 직선 투사체를 발사, 적에게 닿으면(관통 없이) 그 자리에서 범위 폭발. 스택형 전용.</summary>
    ArcProjectile,
    /// <summary>커서 위치(플레이어 기준 최대 사거리로 clamp)로 포물선 투척, 적 접촉 또는 착지 후 퓨즈 시간 경과 시 범위 폭발.</summary>
    BombThrow,
    /// <summary>전방(커서 방향) 원뿔 범위를 즉시 명중시킨 뒤, 커서 반대 방향(후방)으로 백스탭 이동.</summary>
    BackstepShot,
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

    [Header("ArcProjectile일 때만 사용 (거너 아크 버스터 - 직선 발사, 접촉 시 폭발, 스택형)")]
    [Tooltip("투사체 최대 사거리. 스킬 범위 스탯/강화(범위)가 이 값에 적용된다.")]
    public float projectileMaxDistance = 12f;
    public float projectileSpeed = 15f;
    [Tooltip("적에게 닿는 순간 이 반경 안의 적 전부에게 데미지(관통 없이 첫 접촉 즉시 폭발).")]
    public float explosionRadius = 1f;
    [Tooltip("최대 스택 수. 스택이 있어야 사용 가능하고, cooldownSeconds는 스택과 별개로 연사 속도를 제한한다.")]
    public int maxStacks = 6;
    [Tooltip("스택 1개가 다시 차는 데 걸리는 시간(초).")]
    public float stackRechargeSeconds = 4f;
    [Tooltip("직선으로 날아가다 적에게 닿으면 폭발하는 투사체 프리팹(GunnerArcProjectile 컴포넌트 포함).")]
    public GameObject arcProjectilePrefab;

    [Header("ArcProjectile 진화1 전용 (아크 레이저 - 스택 전부 소모, 직선 판정 즉시 명중)")]
    [Tooltip("레이저 직선 판정 길이.")]
    public float evoLaserLength = 8f;
    [Tooltip("레이저 직선 판정 폭.")]
    public float evoLaserWidth = 1f;
    [Tooltip("소모한 스택 1개당 피해 배율에 곱연산으로 반영되는 증가율(%). 50 = 0.5 증가.")]
    public float evoLaserDamagePerStackPercent = 50f;
    [Tooltip("피해 증가 계산에 반영되는 소모 스택 수의 최대치. 이보다 많이 소모해도 이 값까지만 계산에 들어간다.")]
    public int evoLaserMaxBonusStacks = 3;

    [Header("ArcProjectile 진화2 전용 (트리플 슈팅 - 같은 스택 1개로 약한 투사체 3발 연사)")]
    [Tooltip("발당 피해 배율. 기본 damageMultiplier 대신 이 값을 그대로 쓴다.")]
    public float evoTripleShotDamageMultiplier = 0.5f;
    [Tooltip("3발 사이의 발사 간격(초). 사용자 스펙에 없어서 임의로 지정.")]
    public float evoTripleShotInterval = 0.1f;

    [Header("BombThrow일 때만 사용 (거너 폭탄 투척 - 커서 위치로 포물선 투척, 적 접촉/착지 후 퓨즈 경과 시 폭발)")]
    [Tooltip("플레이어로부터 이 거리보다 먼 커서 위치는 이 거리로 clamp된다. 스킬 범위 스탯/강화(범위)가 이 값에 적용된다.")]
    public float bombThrowRange = 6f;
    [Tooltip("포물선 이동 속도(거리/속도로 비행 시간 산출). 값이 클수록 더 빨리 날아간다.")]
    public float bombThrowSpeed = 10f;
    [Tooltip("포물선 최고 높이(시각적 궤적용).")]
    public float bombArcHeight = 2f;
    [Tooltip("착지(투척 완료) 후 적과 접촉하지 않아도 자동 폭발하기까지 걸리는 시간(초).")]
    public float bombFuseSeconds = 2f;
    [Tooltip("폭발 시 데미지가 들어가는 반경.")]
    public float bombExplosionRadius = 3f;
    [Tooltip("포물선으로 날아가 착지/접촉 시 폭발하는 폭탄 프리팹(GunnerBomb 컴포넌트 포함).")]
    public GameObject bombPrefab;

    [Header("BombThrow 진화1 전용 (집속 폭탄 - 첫 폭발 후 지연 2차 폭발)")]
    [Tooltip("첫 폭발로부터 2차 폭발까지 걸리는 시간(초).")]
    public float evoClusterDelaySeconds = 0.5f;
    [Tooltip("2차 폭발 반경.")]
    public float evoClusterRadius = 4f;
    [Tooltip("2차 폭발 데미지 배율. 첫 폭발 데미지에 곱해진다(0.5 = 첫 폭발의 50%).")]
    public float evoClusterDamageMultiplier = 0.5f;

    [Header("BombThrow 진화2 전용 (에너지 폭발 - 범위 확대 + 기절 + 슬로우 영역)")]
    [Tooltip("이 진화의 기본 폭발 반경(기본 bombExplosionRadius 대신 이 값을 쓴다).")]
    public float evoEnergyBurstRadius = 4f;
    [Tooltip("첫 폭발에 명중한 적에게 거는 기절 시간(초).")]
    public float evoEnergyBurstStunDuration = 1f;
    [Tooltip("슬로우 영역이 유지되는 시간(초).")]
    public float evoEnergyBurstSlowZoneDuration = 5f;
    [Tooltip("슬로우 영역 안에서의 이동속도 배율(0.6 = 40% 감소).")]
    public float evoEnergyBurstSlowMultiplier = 0.6f;

    [Header("BombThrow 진화3 전용 (글리터 폭탄 - 마커 부여, 받는 모든 데미지 증가)")]
    [Tooltip("마커(WBH_StatusEffectType.Marked)가 유지되는 시간(초). 재적용해도 중첩 안 되고 이 값으로 갱신만 된다.")]
    public float evoGlitterMarkDuration = 7f;
    [Tooltip("마커가 걸린 적이 받는 모든 데미지의 배율(1.25 = 25% 증가). WBH_CombatManager.ProcessDamage가 공격자 무관하게 적용한다.")]
    public float evoGlitterMarkedDamageMultiplier = 1.25f;

    [Header("BackstepShot일 때만 사용 (거너 백스탭 샷 - 전방 원뿔 공격 + 커서 반대 방향 백스탭 이동)")]
    [Tooltip("백스탭으로 물러나는 거리.")]
    public float backstepDistance = 2f;
    [Tooltip("백스탭 이동에 걸리는 시간(초).")]
    public float backstepDuration = 0.15f;
    [Tooltip("전방 원뿔 공격 판정 반경. 사용자 스펙에 없어서 임의로 지정(다른 거너 스킬과 동일한 판정 방식 - SectorSlash용 GetSectorTargets 재사용).")]
    public float backstepConeRange = 5f;
    [Tooltip("전방 원뿔 공격 전체 각도. 사용자 스펙에 없어서 임의로 지정.")]
    public float backstepConeAngle = 90f;

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

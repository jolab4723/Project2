using UnityEngine;
using UnityEngine.Serialization;
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
    /// <summary>시전 즉시 자기 주변 원형 범위를 때리고, 일정 시간 자신에게 강화 버프를 건다(파이터 궁극기).</summary>
    AwakeningBurst,
    /// <summary>커서로 지정한 넓은 원형 영역에 폭탄을 여러 발 시간차로 떨어뜨린다(거너 궁극기, 융단폭격).</summary>
    CarpetBombing,
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
    [Tooltip("스킬 사용 시 소모하는 마나(진화 없음일 때 기준). 0이면 마나 소모 없음.")]
    public float manaCost = 0f;
    [Tooltip("진화1 선택 시 소모하는 마나. 0이면 위 manaCost(기본값)를 그대로 쓴다 - 진화별로 다른 코스트가 필요할 때만 0 초과 값을 넣는다.")]
    public float evolution1ManaCost = 0f;
    [Tooltip("진화2 선택 시 소모하는 마나. 0이면 위 manaCost(기본값)를 그대로 쓴다.")]
    public float evolution2ManaCost = 0f;
    [Tooltip("진화3 선택 시 소모하는 마나. 0이면 위 manaCost(기본값)를 그대로 쓴다.")]
    public float evolution3ManaCost = 0f;

    /// <summary>지금 선택된 진화 기준 실제 마나 코스트. 해당 진화 전용 코스트가 0(미설정)이면 기본 manaCost로 대체한다.</summary>
    public float GetManaCost(SkillEvolutionId evolution)
    {
        float evoCost = evolution switch
        {
            SkillEvolutionId.Evolution1 => evolution1ManaCost,
            SkillEvolutionId.Evolution2 => evolution2ManaCost,
            SkillEvolutionId.Evolution3 => evolution3ManaCost,
            _ => 0f,
        };

        return evoCost > 0f ? evoCost : manaCost;
    }
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
    [Tooltip("최대 스택 수. 스택이 있어야 사용 가능하고, 연사 간격은 GunnerSkillController의 고정값(1초)이 제한한다.")]
    public int maxStacks = 6;
    [Tooltip("스택 1개가 다시 차는 데 걸리는 시간(초). SkillData 시트의 cooldownSeconds 값이 파이프라인으로 들어온다.")]
    public float stackRechargeSeconds = 4f;
    [Tooltip("직선으로 날아가다 적에게 닿으면 폭발하는 투사체 프리팹(GunnerArcProjectile 컴포넌트 포함).")]
    public GameObject arcProjectilePrefab;

    [Header("ArcProjectile 진화1 전용 (아크 레이저 - 스택 전부 소모, 직선 판정 즉시 명중)")]
    [Tooltip("레이저 직선 판정 길이.")]
    public float evoLaserLength = 12f;
    [Tooltip("레이저 직선 판정 폭.")]
    public float evoLaserWidth = 2f;
    [Tooltip("소모한 스택 1개당 피해 배율에 곱연산으로 반영되는 증가율(%). 50 = 0.5 증가.")]
    public float evoLaserDamagePerStackPercent = 50f;
    [Tooltip("피해 증가 계산에 반영되는 소모 스택 수의 최대치. 이보다 많이 소모해도 이 값까지만 계산에 들어간다.")]
    public int evoLaserMaxBonusStacks = 6;

    [Header("ArcProjectile 진화2 전용 (아크 불릿 - 같은 스택 1개로 약한 투사체 3발 연사)")]
    [Tooltip("발당 피해 배율. 기본 damageMultiplier 대신 이 값을 그대로 쓴다.")]
    [FormerlySerializedAs("evoTripleShotDamageMultiplier")]
    public float evoArcBulletDamageMultiplier = 0.5f;
    [Tooltip("3발 사이의 발사 간격(초). 사용자 스펙에 없어서 임의로 지정.")]
    [FormerlySerializedAs("evoTripleShotInterval")]
    public float evoArcBulletInterval = 0.1f;

    [Header("ArcProjectile 진화3 전용 (아크 캐논 - 확장 폭발, 스택 2개 소모)")]
    [Tooltip("폭발 반경. 기본 explosionRadius 대신 이 값을 쓴다.")]
    public float evoCannonExplosionRadius = 3f;
    [Tooltip("피해 배율. 기본 damageMultiplier 대신 이 값을 그대로 쓴다.")]
    public float evoCannonDamageMultiplier = 2.5f;
    [Tooltip("발사 1회당 소모하는 스택 수. 기본(1) 대신 이 값만큼 소모한다.")]
    public int evoCannonStackCost = 2;
    [Tooltip("에너지 뭉치 형태의 전용 투사체 프리팹. 비워두면 기본 arcProjectilePrefab을 그대로 쓴다.")]
    public GameObject evoCannonProjectilePrefab;

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

    [Header("BackstepShot 진화1 전용 (디코이 설치 - 원뿔 공격 대신 처음 위치에 디코이 설치)")]
    [Tooltip("디코이 설치 후 자동 폭발까지 걸리는 시간(초).")]
    public float evoDecoyFuseSeconds = 3f;
    [Tooltip("디코이 폭발 반경.")]
    public float evoDecoyExplosionRadius = 3f;
    [Tooltip("디코이 폭발 데미지 배율.")]
    public float evoDecoyDamageMultiplier = 1f;
    [Tooltip("설치되는 디코이 프리팹(GunnerDecoy 컴포넌트 포함).")]
    public GameObject evoDecoyPrefab;

    [Header("BackstepShot 진화2 전용 (긴급 회피 - 백스탭 이동 중 무적)")]
    [Tooltip("백스탭 시작과 동시에 부여되는 무적 시간(초). backstepDuration보다 길게 잡으면 착지 직후까지 무적 유지.")]
    public float evoBackstepInvincibleDuration = 0.3f;

    [Header("BackstepShot 진화3 전용 (제압 사격 - 원뿔 공격에 넉백 추가)")]
    [Tooltip("넉백으로 밀려나는 거리.")]
    public float evoSuppressKnockbackForce = 3f;
    [Tooltip("넉백에 걸리는 시간(초).")]
    public float evoSuppressKnockbackDuration = 0.3f;

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

    [Header("CarpetBombing 전용 (거너 궁극기 - 융단폭격)")]
    [Tooltip("폭격 영역의 반경. 커서 지점을 중심으로 이 범위 전체가 타격 대상이다. 시트의 rangeWidthOrAngle 컬럼과 연결된다.")]
    public float carpetAreaRadius = 8f;
    [Tooltip("영역 전체를 때리는 횟수.")]
    public int carpetWaveCount = 3;
    [Tooltip("타격과 타격 사이 간격(초).")]
    public float carpetWaveInterval = 0.5f;
    [Tooltip("한 번의 타격이 주는 피해 계수. 영역 안 모든 적에게 동일하게 들어간다.")]
    public float carpetDamagePerWave = 1.2f;

    [Header("CarpetBombing 연출 - 하늘에서 떨어지는 폭탄")]
    [Tooltip("타격 한 번에 하늘에서 떨어지는 폭탄 개수. 피해는 영역 전체에 들어가므로 이건 순수 연출용이다.")]
    public int carpetVisualBombsPerWave = 6;
    [Tooltip("폭탄이 생성되는 높이(영역 지면 기준).")]
    public float carpetDropHeight = 14f;
    [Tooltip("폭탄이 떨어지기 시작한 뒤 실제 피해가 들어가기까지의 시간(초). 낙하 연출과 타격 타이밍을 맞추는 값이다.")]
    public float carpetImpactDelay = 0.35f;

    [Header("CarpetBombing 진화1 전용 (레이저 폭격 - 지연 후 단일 타격)")]
    [Tooltip("시전 후 타격까지의 지연(초). 기본 융단폭격의 carpetImpactDelay 대신 쓴다.")]
    public float evoLaserStrikeDelay = 0.5f;
    [Tooltip("한 번에 들어가는 피해 계수. 여러 번 나눠 때리지 않고 이 값이 전부다.")]
    public float evoLaserStrikeDamage = 4.5f;

    [Header("CarpetBombing 진화2 전용 (마커 폭격 - 횟수 증가 + 마커 부여)")]
    [Tooltip("타격 횟수.")]
    public int evoMarkerWaveCount = 4;
    [Tooltip("타격과 타격 사이 간격(초). 기본 융단폭격의 carpetWaveInterval 대신 쓴다.")]
    public float evoMarkerWaveInterval = 1f;
    [Tooltip("회당 피해 계수.")]
    public float evoMarkerDamagePerWave = 1f;
    [Tooltip("맞은 적에게 거는 마커(Marked) 지속시간(초). 0이면 마커 없음. " +
             "이미 걸려 있으면 남은 시간에 이 값을 더한다(갱신이 아니라 누적).")]
    public float evoMarkerDuration = 5f;
    [Tooltip("마커가 걸린 적이 받는 모든 피해의 배율.")]
    public float evoMarkerDamageMultiplier = 1.25f;

    [Header("CarpetBombing 진화3 전용 (산탄 폭격 - 랜덤 위치 소범위 연속 포격)")]
    [Tooltip("산탄 폭격 전용 낙하 투사체.")]
    public GameObject evoBarrageBombPrefab;
    [Tooltip("포탄 발수.")]
    public int evoBarrageShellCount = 9;
    [Tooltip("포탄과 포탄 사이 간격(초).")]
    public float evoBarrageInterval = 0.3f;
    [Tooltip("포탄 한 발의 폭발 반경. 영역 전체가 아니라 이 범위만 맞는다. 스킬 범위 증가의 영향을 받는다.")]
    public float evoBarrageShellRadius = 3f;
    [Tooltip("포탄 한 발의 피해 계수.")]
    public float evoBarrageDamagePerShell = 0.6f;
    [Tooltip("포탄이 떨어질 수 있는 범위(폭격 중심 기준). 좁을수록 한 대상에게 여러 발이 겹친다. " +
             "한 대상의 발당 명중률은 대략 (포탄반경/산포반경)^2다 - 기본 융단폭격 반경인 8로 두면 " +
             "발당 6%라 9발을 쏴도 기대 명중이 1발이 안 된다. 4면 발당 25%로 기대 2.25발이다.")]
    public float evoBarrageScatterRadius = 4f;

    [Header("AwakeningBurst 전용 (파이터 궁극기)")]
    [Tooltip("시전과 동시에 자신에게 거는 강화 버프. 지속시간·스탯 수치는 이 버프 에셋이 들고 있다. " +
             "엑셀에서는 오브젝트 참조를 표현할 수 없어 인스펙터/에디터에서 직접 연결한다(evoDashDamageBuff와 같은 방식).")]
    public BuffDefinitionSO awakeningBuff;

    [Header("AwakeningBurst 진화 전용 - 진화별로 거는 버프가 달라진다")]
    [Tooltip("진화1(가속 각성): 공격속도와 일반공격 피해 특화. 비우면 기본 버프를 쓴다.")]
    public BuffDefinitionSO evoAwakeningBuff1;
    [Tooltip("진화2(연산 각성): 스킬 쿨타임 감소와 스킬 피해 특화. 비우면 기본 버프를 쓴다.")]
    public BuffDefinitionSO evoAwakeningBuff2;
    [Tooltip("진화3(과부하 각성): 지속시간이 짧은 대신 시전 폭발에 투자. 비우면 기본 버프를 쓴다.")]
    public BuffDefinitionSO evoAwakeningBuff3;

    [Tooltip("진화3 전용: 시전 폭발의 데미지 계수. 기본 계수(damageMultiplier) 대신 이 값을 쓴다.")]
    public float evoOverloadDamageMultiplier = 4f;
    [Tooltip("진화3 전용: 시전 폭발 반경 배율. 1.7이면 기본 반경의 1.7배.")]
    public float evoOverloadRangeMultiplier = 1.7f;

    /// <summary>진화에 맞는 각성 버프. 진화용 버프가 비어 있으면 기본 버프로 떨어진다.</summary>
    public BuffDefinitionSO GetAwakeningBuff(SkillEvolutionId evolution)
    {
        BuffDefinitionSO evoBuff = evolution switch
        {
            SkillEvolutionId.Evolution1 => evoAwakeningBuff1,
            SkillEvolutionId.Evolution2 => evoAwakeningBuff2,
            SkillEvolutionId.Evolution3 => evoAwakeningBuff3,
            _ => null,
        };

        return evoBuff != null ? evoBuff : awakeningBuff;
    }

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

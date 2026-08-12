using UnityEngine;

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
/// 진화(3가지 형태 중 선택)·강화(피해량/쿨타임/범위 중 선택)는 아직 구체적인 형태·효과가
/// 정해지지 않아서 이 에셋에는 넣지 않았다 - 실제 설계가 나오면 확장한다.
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
}

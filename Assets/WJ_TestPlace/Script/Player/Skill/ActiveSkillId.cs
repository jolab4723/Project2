/// <summary>
/// 액티브 스킬(Q/W/E 등 즉시 사용 스킬) 식별자.
/// 새 클래스가 추가되면 그 클래스의 스킬들을 여기 이어서 추가한다.
/// PassiveSkillId(영구 스탯 트리)와는 별개 - 이건 쿨타임을 가진 즉시 사용 스킬용이다.
/// </summary>
public enum ActiveSkillId
{
    // Fighter
    FighterHalfCircleSlash,
    FighterLineSlam,
    FighterCursorDash,
}

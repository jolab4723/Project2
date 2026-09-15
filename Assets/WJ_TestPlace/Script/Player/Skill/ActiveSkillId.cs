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

    // Gunner
    GunnerArcBuster,
    GunnerBombThrow,
    GunnerBackstepShot,

    // 궁극기(Skill4). 아직 전용 설계가 없어서 임시로 각 클래스 1번 스킬 데이터를 복사해 쓴다.
    // !! 기존 값의 번호가 밀리지 않도록 반드시 맨 뒤에만 추가한다(에셋에 int로 직렬화됨).
    FighterUltimate,
    GunnerUltimate,
}

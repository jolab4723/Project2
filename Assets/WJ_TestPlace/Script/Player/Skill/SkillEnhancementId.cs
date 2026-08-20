/// <summary>
/// 스킬 하나의 강화 상태(3가지 종류 중 하나 선택, 또는 미강화). 진화(SkillEvolutionId)와 별개의 축이라
/// 스킬 하나가 진화와 강화를 동시에 가질 수 있다.
/// Enhance1/2/3의 실제 의미는 스킬마다 다르다(SkillDefinitionSO.shapeType 기준):
///   - Enhance1(위력 강화): SectorSlash/LineSlam은 데미지 계수 증가, Dash는 자체 피해가 없어서 대신 대시 속도 증가.
///   - Enhance2(쿨타임 감소): 전 스킬 공통.
///   - Enhance3(범위 강화): 전 스킬 공통(대시는 이동 거리에 적용).
/// </summary>
public enum SkillEnhancementId
{
    None,
    Enhance1,
    Enhance2,
    Enhance3,
}

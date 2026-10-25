/// <summary>
/// 스킬 진화·강화 선택 UI(SkillEvolutionSelectUI)와 쿨타임 HUD(KY_SkillView)가 캐릭터 클래스와
/// 무관하게 동작할 수 있도록 뽑아낸 공통 계약. FighterSkillController가 구현하고, 나중에 거너용
/// 컨트롤러가 생기면 이 인터페이스만 구현하면 같은 UI를 그대로 재사용할 수 있다.
///
/// 각 스킬의 실제 판정 모양(부채꼴/직선/대시, 또는 투사체 등)은 이 인터페이스 범위 밖이고,
/// 슬롯 인덱스 기준의 진화/강화/쿨타임/스택 상태만 다룬다.
/// </summary>
public interface ISkillController
{
    /// <summary>이 컨트롤러가 가진 스킬 슬롯 개수.</summary>
    int SkillCount { get; }

    /// <summary>슬롯(0~2)의 스킬 데이터. UI가 이름/설명 등을 표시할 때 사용. 범위 밖이면 null.</summary>
    SkillDefinitionSO GetSkillDefinition(int index);

    /// <summary>스킬 슬롯의 현재 진화. 범위 밖이면 None.</summary>
    SkillEvolutionId GetEvolution(int index);

    /// <summary>스킬 슬롯의 진화를 외부(진화 선택 UI 등)에서 변경한다.</summary>
    void SetEvolution(int index, SkillEvolutionId evolution);

    /// <summary>스킬 슬롯의 현재 강화. 범위 밖이면 None.</summary>
    SkillEnhancementId GetEnhancement(int index);

    /// <summary>스킬 슬롯의 강화를 외부(강화 선택 UI 등)에서 변경한다.</summary>
    void SetEnhancement(int index, SkillEnhancementId enhancement);

    /// <summary>남은 쿨타임(초). 준비됐으면 0.</summary>
    float GetRemainingCooldown(int index);

    /// <summary>지금 적용 중인 최대 쿨타임(초, 강화로 감소됐으면 그 값). 쿨타임 UI가 남은 비율을 계산할 때 분모로 쓴다.</summary>
    float GetEffectiveCooldown(int index);

    /// <summary>슬롯이 스택 기반(예: 대시 2스택)이면 현재/최대 스택 수를 낸다. 스택 기반이 아니면 false.</summary>
    bool TryGetStackInfo(int index, out int current, out int max);

    /// <summary>
    /// SW 추가: 스킬 시전이 확정된 순간(쿨타임·스택 소모 직후) 한 번 발행한다. 아이템 고유효과가 이 사실만 구독하며
    /// 컨트롤러는 아이템을 모른다. 멀티에서는 서버의 원본 컨트롤러에서만 의미가 있다.
    /// </summary>
    event System.Action<SkillCastInfo> SkillCast;

    /// <summary>
    /// SW 추가: 슬롯의 남은 쿨타임을 줄인다. 스택형 슬롯(대시, 아크 버스터)은 다음 스택 충전을 그만큼 진행한다.
    /// 처치 시 쿨타임 감소 고유효과가 사용한다.
    /// </summary>
    void ReduceCooldown(int index, float seconds);
}

/// <summary>SW 추가: 확정된 스킬 시전 한 번의 슬롯(0부터)과 데이터, 진화 단계.</summary>
public readonly struct SkillCastInfo
{
    public readonly int Index;
    public readonly SkillDefinitionSO Definition;
    public readonly SkillEvolutionId Evolution;

    public SkillCastInfo(int index, SkillDefinitionSO definition, SkillEvolutionId evolution)
    {
        Index = index;
        Definition = definition;
        Evolution = evolution;
    }

    /// <summary>고유효과 표의 skillSlot과 같은 1부터 세는 번호.</summary>
    public int Slot => Index + 1;
}

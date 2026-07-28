using UnityEngine;

public interface WBH_IStatusEffect
{
    // 상태이상 종류
    WBH_StatusEffectType EffectType { get; }

    // 남은 지속시간
    float RemainingTime { get; }

    // 적용 여부
    bool IsFinished { get; }

    // 최초 적용
    void Apply();

    // 매 프레임 갱신
    void Tick(float deltaTime);

    // 효과 종료
    void Remove();

    // 동일한 상태이상 재적용
    void Refresh(WBH_StatusEffectData data);
}

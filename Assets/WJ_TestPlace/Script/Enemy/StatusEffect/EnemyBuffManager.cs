using ItemSystem;
using UnityEngine;

/// <summary>
/// PlayerBuffManager와 같은 방식(BuffTracker + IBuffSource/BuffDefinitionSO)의 버프/디버프를
/// 적에게도 걸 수 있게 하는 컴포넌트. 아이템 고유효과, 스킬 등 다양한 소스가 같은
/// BuffDefinitionSO/UniqueEffectSO 에셋을 그대로 재사용해서 적용할 수 있다.
///
/// 대상은 IStatBuffTarget(ApplyBuffStatSet)만 구현하면 되므로 실제 적(WBH_EnemyStatus)과
/// 허수아비(TrainingDummyStatus) 양쪽에 그대로 붙일 수 있다 - 둘을 구분하는 별도 컴포넌트를
/// 만들지 않고 하나로 통합했다.
///
/// WBH_EnemyStatusEffectController(넉백/기절/방어감소 등 지속시간형 상태이상)와는 별개 레이어다 -
/// 그쪽은 WBH_StatusEffectData 기반이고 이쪽은 StatSet(flat/percent 가산) 기반이라 표현 방식이
/// 다르다. 둘 다 최종적으로 대상의 ApplyBuffStatSet/Multiply* 쪽에서 합성되므로 동시에 걸려도
/// 서로 어긋나지 않는다(실제 적 기준, WBH_EnemyStatus.RecalculateAll 참고).
/// </summary>
public class EnemyBuffManager : MonoBehaviour, IBuffTarget
{
    private readonly BuffTracker tracker = new BuffTracker();
    private IStatBuffTarget status;

    public System.Collections.Generic.IReadOnlyList<BuffInstance> ActiveBuffs => tracker.ActiveBuffs;
    // SW 수정: 풀 반환에서 TriggerExit가 생략돼도 오라가 이전 생명의 진입 기록을 지울 수 있다.
    public event System.Action<EnemyBuffManager> Disabled;

    private void Awake()
    {
        status = GetComponent<IStatBuffTarget>();

        if (status == null)
            Debug.LogError($"[EnemyBuffManager] {name}에 IStatBuffTarget을 구현하는 컴포넌트(WBH_EnemyStatus 또는 TrainingDummyStatus)가 없습니다.", this);
    }

    private void Update()
    {
        if (tracker.Tick(Time.deltaTime))
            Recalculate();
    }

    private void OnDisable()
    {
        Disabled?.Invoke(this);
        ClearAllBuffs();
    }

    public void ApplyBuff(IBuffSource source)
    {
        if (tracker.ApplyBuff(source))
            Recalculate();
    }

    public void RemoveBuff(IBuffSource source)
    {
        if (tracker.RemoveBuff(source))
            Recalculate();
    }

    public void ClearAllBuffs()
    {
        if (tracker.ClearAllBuffs())
            Recalculate();
    }

    private void Recalculate()
    {
        status.ApplyBuffStatSet(tracker.GetStatSet());
    }
}

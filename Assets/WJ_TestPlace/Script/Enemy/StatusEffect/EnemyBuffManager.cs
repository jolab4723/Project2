using ItemSystem;
using UnityEngine;

/// <summary>
/// PlayerBuffManager/DummyBuffManager와 같은 방식(BuffTracker + IBuffSource/BuffDefinitionSO)의
/// 버프/디버프를 실제 적에도 걸 수 있게 하는 컴포넌트. 아이템 고유효과, 스킬 등 다양한 소스가
/// 같은 BuffDefinitionSO/UniqueEffectSO 에셋을 그대로 재사용해서 적용할 수 있다.
///
/// WBH_EnemyStatusEffectController(넉백/기절/방어감소 등 지속시간형 상태이상)와는 별개 레이어다 -
/// 그쪽은 WBH_StatusEffectData 기반이고 이쪽은 StatSet(flat/percent 가산) 기반이라 표현 방식이
/// 다르다. 둘 다 최종적으로 WBH_EnemyStatus에서 합성되므로 동시에 걸려도 서로 어긋나지 않는다.
/// </summary>
[RequireComponent(typeof(WBH_EnemyStatus))]
public class EnemyBuffManager : MonoBehaviour, IBuffTarget
{
    private readonly BuffTracker tracker = new BuffTracker();
    private WBH_EnemyStatus status;

    public System.Collections.Generic.IReadOnlyList<BuffInstance> ActiveBuffs => tracker.ActiveBuffs;

    private void Awake()
    {
        status = GetComponent<WBH_EnemyStatus>();
    }

    private void Update()
    {
        if (tracker.Tick(Time.deltaTime))
            Recalculate();
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

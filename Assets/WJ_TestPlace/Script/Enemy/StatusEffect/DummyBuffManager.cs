using ItemSystem;
using UnityEngine;

/// <summary>
/// PlayerBuffManager와 같은 방식(BuffTracker + IBuffSource/BuffDefinitionSO)의 버프/디버프를
/// 허수아비에도 걸 수 있게 하는 컴포넌트. PlayerStatManager가 없어서 PlayerBuffManager를 그대로
/// 붙일 수는 없다(그 컴포넌트는 Awake에서 전역 Instance를 자기 자신으로 등록하려 시도하는데,
/// 이미 실제 플레이어가 Instance를 갖고 있으면 자기 GameObject를 통째로 Destroy해버림 -
/// 허수아비가 사라지는 사고로 이어짐). 그래서 같은 BuffTracker를 별도로 들고 쓰는 전용
/// 컴포넌트를 새로 만들었다.
/// </summary>
[RequireComponent(typeof(TrainingDummyStatus))]
public class DummyBuffManager : MonoBehaviour
{
    [Tooltip("[ContextMenu] 테스트 적용에 쓸 버프. 인스펙터에서 지정.")]
    [SerializeField] private BuffDefinitionSO testDebuff;

    private readonly BuffTracker tracker = new BuffTracker();
    private TrainingDummyStatus dummy;

    public System.Collections.Generic.IReadOnlyList<BuffInstance> ActiveBuffs => tracker.ActiveBuffs;

    private void Awake()
    {
        dummy = GetComponent<TrainingDummyStatus>();
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
        dummy.ApplyBuffStatSet(tracker.GetStatSet());
    }

    [ContextMenu("테스트 버프 적용 (testDebuff)")]
    private void TestApplyDebuff()
    {
        if (testDebuff == null)
        {
            Debug.LogWarning("[DummyBuffManager] testDebuff가 인스펙터에 연결되지 않았습니다.", this);
            return;
        }

        ApplyBuff(testDebuff);
    }
}

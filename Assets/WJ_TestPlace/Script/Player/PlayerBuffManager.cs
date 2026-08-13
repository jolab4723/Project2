using System;
using System.Collections.Generic;
using UnityEngine;
using ItemSystem;

/// <summary>
/// 현재 적용 중인 버프들을 관리하고, 스탯 효과를 합산해서 StatSet으로 제공하는 IStatSetProvider 구현체.
///
/// 실제 목록/틱/스탯 합산 로직은 BuffTracker(재사용 가능한 순수 C# 클래스)에 있고, 여기서는
/// PlayerStatManager.Recalculate() 연동 + 멀티플레이 Instance 패턴만 얹는다 - 허수아비 등
/// PlayerStatManager가 없는 대상은 DummyBuffManager처럼 BuffTracker를 직접 들고 쓰면 된다.
///
/// !! 멀티플레이 대비: Instance는 "내 캐릭터"만 가리킨다 (PlayerHealthManager와 동일 패턴).
///    PlayerStatManager는 "전역 Instance"가 아니라 같은 캐릭터의 컴포넌트를 GetComponent로 찾아서
///    쓴다 - 안 그러면 남의 캐릭터 버프가 내 캐릭터 스탯을 재계산시키는 문제가 생김.
/// </summary>
public class PlayerBuffManager : MonoBehaviour, IStatSetProvider
{
    public static PlayerBuffManager Instance { get; private set; }

    /// <summary>씬에 존재하는 모든 캐릭터의 버프 매니저 (나 + 다른 플레이어).</summary>
    public static readonly List<PlayerBuffManager> All = new List<PlayerBuffManager>();

    private readonly BuffTracker tracker = new BuffTracker();
    private PlayerStatManager statManager;

    /// <summary>UI 등 외부에서 현재 걸린 버프 목록을 읽기 전용으로 조회. 남은시간/스택 값은 매 프레임 바뀌므로
    /// UI 쪽에서 직접 폴링해서 쓰면 되고, 이 리스트 자체는 OnBuffsChanged가 발행될 때만 다시 읽으면 된다.</summary>
    public IReadOnlyList<BuffInstance> ActiveBuffs => tracker.ActiveBuffs;

    /// <summary>버프가 새로 추가되거나 제거돼서 목록 구성 자체가 바뀔 때 발행. 같은 버프의 스택/지속시간만
    /// 갱신되는 경우(RefreshDuration, Stack 재적용)는 목록 구성이 그대로라 발행하지 않는다 - UI는 이미
    /// 표시 중인 항목의 남은시간/스택을 매 프레임 직접 읽어서 갱신하면 되기 때문.</summary>
    public event Action OnBuffsChanged;

    private void Awake()
    {
        All.Add(this);
        statManager = GetComponent<PlayerStatManager>();
        tracker.OnBuffsChanged += () => OnBuffsChanged?.Invoke();

        var identity = GetComponent<Mirror.NetworkIdentity>();
        if (identity != null && !identity.isLocalPlayer)
            return;

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PlayerBuffManager] 이미 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        All.Remove(this);
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (tracker.Tick(Time.deltaTime))
            statManager?.Recalculate();
    }

    /// <summary>
    /// 버프를 적용한다. 이미 같은 버프가 있으면 source.StackBehavior에 따라
    /// 지속시간 갱신(RefreshDuration) / 스택 증가(Stack) / 무시(Ignore) 중 하나로 처리한다.
    ///
    /// !! 같은 버프인지는 source 객체의 **참조**로 판정한다. 고유 효과는 UniqueEffectSO 에셋이,
    ///    그 외 소스는 BuffDefinitionSO 에셋이 각각 고유한 키 역할을 한다.
    /// </summary>
    public void ApplyBuff(IBuffSource source)
    {
        if (tracker.ApplyBuff(source))
            statManager?.Recalculate();
    }

    /// <summary>해당 버프를 스택 상관없이 완전히 제거한다.</summary>
    public void RemoveBuff(IBuffSource source)
    {
        if (tracker.RemoveBuff(source))
            statManager?.Recalculate();
    }

    /// <summary>모든 버프를 제거한다. (예: 사망/씬 전환 시 초기화용)</summary>
    public void ClearAllBuffs()
    {
        if (tracker.ClearAllBuffs())
            statManager?.Recalculate();
    }

    public StatSet GetStatSet() => tracker.GetStatSet();
}

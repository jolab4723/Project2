using System.Collections.Generic;
using UnityEngine;
using ItemSystem;

/// <summary>
/// 현재 적용 중인 버프들을 관리하고, 스탯 효과를 합산해서 StatSet으로 제공하는 IStatSetProvider 구현체.
///
/// 지금은 ApplyBuff/RemoveBuff API와 매니저 골격만 만든 상태 - 실제로 언제 버프가 걸리는지
/// (포션 소비, 스킬, 필드 존 등)는 아직 연결 안 함. 나중에 그 시스템들이 이 매니저의
/// ApplyBuff(def)만 불러주면 됨.
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

    private readonly List<BuffInstance> activeBuffs = new List<BuffInstance>();
    private PlayerStatManager statManager;

    private void Awake()
    {
        All.Add(this);
        statManager = GetComponent<PlayerStatManager>();

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
        if (activeBuffs.Count == 0)
            return;

        bool anyExpired = false;

        // 뒤에서부터 순회해야 RemoveAt으로 인덱스가 밀려도 안전함
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            var buff = activeBuffs[i];
            if (buff.definition == null || buff.definition.IsPermanent)
                continue;

            buff.remainingTime -= Time.deltaTime;
            if (buff.remainingTime <= 0f)
            {
                activeBuffs.RemoveAt(i);
                anyExpired = true;
            }
        }

        if (anyExpired)
            statManager?.Recalculate();
    }

    /// <summary>
    /// 버프를 적용한다. 이미 같은 버프가 있으면 def.stackBehavior에 따라
    /// 지속시간 갱신(RefreshDuration) / 스택 증가(Stack) / 무시(Ignore) 중 하나로 처리한다.
    /// </summary>
    public void ApplyBuff(BuffDefinitionSO def)
    {
        if (def == null)
        {
            Debug.LogWarning("[PlayerBuffManager] def가 null입니다.");
            return;
        }

        var existing = activeBuffs.Find(b => b.definition == def);

        if (existing != null)
        {
            switch (def.stackBehavior)
            {
                case BuffStackBehavior.RefreshDuration:
                    existing.remainingTime = def.duration;
                    break;

                case BuffStackBehavior.Stack:
                    if (def.maxStack <= 0 || existing.stackCount < def.maxStack)
                        existing.stackCount++;
                    existing.remainingTime = def.duration; // 스택될 때도 지속시간은 최신으로 갱신
                    break;

                case BuffStackBehavior.Ignore:
                    return; // 이미 있으면 아무 것도 안 하고 끝
            }
        }
        else
        {
            activeBuffs.Add(new BuffInstance(def));
        }

        statManager?.Recalculate();
    }

    /// <summary>해당 버프를 스택 상관없이 완전히 제거한다.</summary>
    public void RemoveBuff(BuffDefinitionSO def)
    {
        if (def == null)
            return;

        int removed = activeBuffs.RemoveAll(b => b.definition == def);

        if (removed > 0)
        {
            statManager?.Recalculate();
        }
    }

    /// <summary>모든 버프를 제거한다. (예: 사망/씬 전환 시 초기화용)</summary>
    public void ClearAllBuffs()
    {
        if (activeBuffs.Count == 0)
            return;

        activeBuffs.Clear();
        statManager?.Recalculate();
    }

    public StatSet GetStatSet()
    {
        StatSet total = StatSet.Zero;

        foreach (var buff in activeBuffs)
        {
            if (buff?.definition?.statEffects == null)
                continue;

            foreach (var effect in buff.definition.statEffects)
                StatSetMapper.AddStat(ref total, effect.statType, effect.value * buff.stackCount);
        }

        return total;
    }
}

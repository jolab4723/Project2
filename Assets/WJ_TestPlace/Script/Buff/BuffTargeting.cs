using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>
/// 버프를 "누구에게" 걸지 정하는 공용 헬퍼.
/// 필드 존, 스킬, 포션, 이벤트 등 버프를 거는 쪽이면 어디서든 이걸 쓰면 된다.
///
/// 대상 후보는 PlayerBuffManager.All이다. 이 목록에는 내 캐릭터와 다른 플레이어가 모두 들어간다
/// (각 PlayerBuffManager가 Awake에서 자신을 등록하고 OnDestroy에서 뺀다).
///
/// !! 멀티플레이 주의: 지금은 로컬에서 바로 적용한다. Mirror 연동이 본격화되면
///    파티 전체/랜덤 대상은 서버에서 판정하고 각 클라이언트에 알리는 형태로 바꿔야 한다.
///    (지금 구조에서 원격 플레이어의 매니저에 직접 걸면 그 클라이언트에는 반영되지 않는다)
/// </summary>
public static class BuffTargeting
{
    /// <summary>버프를 걸 대상 범위. 인스펙터에서 고르게 할 때 쓴다.</summary>
    public enum Scope
    {
        /// <summary>지정한 한 명에게만. 대상을 안 넘기면 내 캐릭터.</summary>
        Single,

        /// <summary>파티 전원에게.</summary>
        Party,

        /// <summary>파티 중 무작위 한 명에게.</summary>
        RandomPartyMember,
    }

    /// <summary>현재 파티(씬에 있는 모든 캐릭터). 반환 목록을 수정하면 안 된다.</summary>
    public static IReadOnlyList<PlayerBuffManager> Party => PlayerBuffManager.All;

    /// <summary>
    /// scope에 맞춰 버프를 적용한다.
    /// </summary>
    /// <param name="target">Single일 때 대상. null이면 내 캐릭터(PlayerBuffManager.Instance).</param>
    /// <returns>실제로 적용된 대상 수.</returns>
    public static int Apply(BuffDefinitionSO buff, Scope scope, PlayerBuffManager target = null)
    {
        if (buff == null)
        {
            Debug.LogWarning("[BuffTargeting] 버프가 지정되지 않았습니다.");
            return 0;
        }

        switch (scope)
        {
            case Scope.Party:
                return ApplyToParty(buff);

            case Scope.RandomPartyMember:
                return ApplyToRandomPartyMember(buff) != null ? 1 : 0;

            default:
                return ApplyToSingle(buff, target) ? 1 : 0;
        }
    }

    /// <summary>한 명에게 적용. target이 null이면 내 캐릭터에 건다.</summary>
    public static bool ApplyToSingle(BuffDefinitionSO buff, PlayerBuffManager target = null)
    {
        PlayerBuffManager resolved = target != null ? target : PlayerBuffManager.Instance;

        if (buff == null || resolved == null)
        {
            Debug.LogWarning("[BuffTargeting] 버프 또는 대상이 없어 적용하지 못했습니다.");
            return false;
        }

        resolved.ApplyBuff(buff);
        return true;
    }

    /// <summary>파티 전원에게 적용.</summary>
    public static int ApplyToParty(BuffDefinitionSO buff)
    {
        if (buff == null)
            return 0;

        int applied = 0;

        // 적용 도중 목록이 바뀔 수 있으니(사망/스폰) 인덱스로 역순 순회한다.
        for (int i = PlayerBuffManager.All.Count - 1; i >= 0; i--)
        {
            PlayerBuffManager member = PlayerBuffManager.All[i];
            if (member == null)
                continue;

            member.ApplyBuff(buff);
            applied++;
        }

        if (applied == 0)
            Debug.LogWarning("[BuffTargeting] 파티에 적용 대상이 없습니다.");

        return applied;
    }

    /// <summary>파티 중 무작위 한 명에게 적용하고, 선택된 대상을 반환한다.</summary>
    public static PlayerBuffManager ApplyToRandomPartyMember(BuffDefinitionSO buff)
    {
        if (buff == null)
            return null;

        PlayerBuffManager chosen = PickRandomPartyMember();
        if (chosen == null)
        {
            Debug.LogWarning("[BuffTargeting] 파티에 적용 대상이 없습니다.");
            return null;
        }

        chosen.ApplyBuff(buff);
        return chosen;
    }

    /// <summary>파티에서 무작위 한 명을 고른다. 아무도 없으면 null.</summary>
    public static PlayerBuffManager PickRandomPartyMember()
    {
        // null이 섞여 있을 수 있어(파괴 직후 등) 유효한 대상만 모아서 뽑는다.
        var candidates = new List<PlayerBuffManager>(PlayerBuffManager.All.Count);
        foreach (PlayerBuffManager member in PlayerBuffManager.All)
        {
            if (member != null)
                candidates.Add(member);
        }

        if (candidates.Count == 0)
            return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>한 명에게서 제거. target이 null이면 내 캐릭터.</summary>
    public static bool RemoveFromSingle(BuffDefinitionSO buff, PlayerBuffManager target = null)
    {
        PlayerBuffManager resolved = target != null ? target : PlayerBuffManager.Instance;

        if (buff == null || resolved == null)
            return false;

        resolved.RemoveBuff(buff);
        return true;
    }

    /// <summary>파티 전원에게서 제거.</summary>
    public static int RemoveFromParty(BuffDefinitionSO buff)
    {
        if (buff == null)
            return 0;

        int removed = 0;

        for (int i = PlayerBuffManager.All.Count - 1; i >= 0; i--)
        {
            PlayerBuffManager member = PlayerBuffManager.All[i];
            if (member == null)
                continue;

            member.RemoveBuff(buff);
            removed++;
        }

        return removed;
    }
}

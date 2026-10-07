using UnityEngine;

/// <summary>SW 수정 : 같은 적을 공격하는 모든 시전자가 빙결 재준비 시각과 풀 생애를 공유하는 대상 상태다.</summary>
[DisallowMultipleComponent]
public sealed class EnemyFreezeRecovery : MonoBehaviour
{
    private double freezeReadyAt;
    private readonly System.Collections.Generic.Dictionary<PlayerContext, (int count, double endsAt)> indicators = new();
    internal uint Lifetime { get; private set; }

    /// <summary>SW 수정 : 싱글·서버의 공통 시각으로 이 적이 다시 빙결 준비를 받을 수 있는지 확인한다.</summary>
    internal bool CanPrepareFreeze(double now) => isActiveAndEnabled && now >= freezeReadyAt;

    /// <summary>SW 수정 : 실제 빙결 적용이 성공한 경우에만 모든 시전자에게 재준비 제한을 적용한다.</summary>
    internal void BeginRecovery(double now, float seconds)
    {
        freezeReadyAt = now + Mathf.Max(0.1f, seconds);
    }

    internal void PresentCooling(PlayerContext owner, int count, double now, float seconds)
    {
        if (count == 0) indicators.Remove(owner);
        else indicators[owner] = (count, now + seconds);
        int shown = 0;
        double end = now;
        foreach (var entry in indicators)
            if (entry.Key != null && entry.Value.endsAt > now && entry.Value.count >= shown)
            { shown = entry.Value.count; end = entry.Value.endsAt; }
        var network = GetComponent<NetworkEnemyAuthority>();
        if (network != null && network.IsServerDamageHandlingActive) network.ServerSetCoolingIndicator(shown, (float)(end - now));
        else if (!Mirror.NetworkServer.active && !Mirror.NetworkClient.active)
            (GetComponent<EnemyEffectIndicator>() ?? gameObject.AddComponent<EnemyEffectIndicator>()).SetCooling(shown, (float)(end - now));
    }

    /// <summary>SW 수정 : 풀 반환·비활성화 뒤 새 적이 이전 제한이나 플레이어의 누적을 이어받지 않도록 생애를 바꾼다.</summary>
    private void OnDisable()
    {
        freezeReadyAt = 0d;
        indicators.Clear();
        Lifetime++;
    }
}

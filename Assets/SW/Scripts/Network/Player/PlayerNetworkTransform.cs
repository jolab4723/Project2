using Mirror;

/// <summary>서버가 스킬이나 엘리베이터 이동을 처리할 때 이전 위치가 서버 위치를 덮어쓰지 않게 한다.</summary>
public sealed class PlayerNetworkTransform : NetworkTransformReliable
{
    private FighterSkillAuthority skills;

    [Server]
    internal void ServerResetOwnerReceiveState()
    {
        // 플레이어 소유자가 바뀌면 그 소유자가 보내는 위치 계산만 새로 시작한다.
        // 다른 클라이언트에 보내던 위치 기록은 그대로 둔다.
        lastDeserializedPosition = Vector3Long.zero;
        lastDeserializedScale = Vector3Long.zero;
        serverSnapshots.Clear();
    }

    /// <summary>서버 확정 위치 적용 전에 소유자가 보낸 대기 중 위치만 폐기한다.</summary>
    [Server]
    internal void ServerClearSnapshots()
    {
        // 다음 위치값을 계산할 때 쓰는 기준은 유지하고, 이동 잠금 중 쌓인 오래된 위치만 버린다.
        serverSnapshots.Clear();
    }

    /// <summary>SW 수정 : 잡기 연출 전후의 보간만 비우고 압축된 위치의 송수신 기준은 보존합니다.</summary>
    internal void ClearClientInterpolation()
    {
        clientSnapshots.Clear();
    }

    protected override void UpdateClient()
    {
        if (skills == null)
            skills = GetComponent<FighterSkillAuthority>();
        if (skills != null && skills.IsGrabLocked)
        {
            ClearClientInterpolation();
            return;
        }
        base.UpdateClient();
    }

    protected override void UpdateServer()
    {
        if (skills == null)
            skills = GetComponent<FighterSkillAuthority>();
        if (skills != null && (skills.ServerMotionLocked || skills.ServerRideLocked || skills.IsGrabLocked))
        {
            // 새 위치값은 계속 받아 다음 계산 기준을 맞추되, 잠금 중에는 실제 위치에 반영하지 않는다.
            serverSnapshots.Clear();
            return;
        }
        base.UpdateServer();
    }
}

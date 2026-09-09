using Mirror;

/// <summary>서버가 스킬 이동을 처리하는 동안 소유자의 이전 위치가 서버 위치를 덮어쓰지 않게 한다.</summary>
public sealed class PlayerNetworkTransform_MirrorTest : NetworkTransformReliable
{
    private FighterSkillAuthority_MirrorTest skills;

    protected override void UpdateServer()
    {
        if (skills == null) skills = GetComponent<FighterSkillAuthority_MirrorTest>();
        if (skills != null && skills.ServerMotionLocked)
        {
            // 수신 역직렬화는 유지해 델타 압축 기준을 보존하고 위치 적용만 차단한다.
            serverSnapshots.Clear();
            return;
        }
        base.UpdateServer();
    }
}

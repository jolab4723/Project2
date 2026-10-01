using UnityEngine;

/// <summary>HUD의 공통 쿨다운 컨테이너에 로컬 Context를 전달한다.</summary>
[DisallowMultipleComponent]
public sealed class MirrorCooldownHud : MonoBehaviour
{
    [SerializeField] private CooldownIconUIContainer container;
    public PlayerContext BoundContext => container != null ? container.BoundContext : null;
    public int VisibleCooldownCount => container != null ? container.VisibleCooldownCount : 0;
    public void Bind(PlayerContext context) { if (container != null) container.Bind(context); }
    public void Unbind() { if (container != null) container.Unbind(); }
}

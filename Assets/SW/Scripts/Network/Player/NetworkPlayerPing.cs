using System;
using Mirror;
using UnityEngine;

/// <summary>
/// BH 원본 <c>WBH_PlayerInputHandler</c>의 Alt+좌클릭 맵 핑을 Mirror 파티 전체에 공유한다.
/// <para>입력은 로컬 전용 <c>NetworkPlayerInputHandler</c>가 전달하고, 서버가 간격·좌표를 검증한 뒤 모든 Client에 표시한다.</para>
/// <para>미니맵은 로컬 HUD를 연결한 <c>PlayerHudEventBridge</c>가 <see cref="Shown"/>으로 받는다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkPlayerPing : NetworkBehaviour
{
    [SerializeField] private GameObject pingMarkerPrefab;
    [SerializeField] private LayerMask pingGroundLayer;
    [SerializeField, Min(0.1f)] private float pingLifetime = 3f;
    [SerializeField, Min(0f)] private float pingCooldown = 0.3f;
    [SerializeField, Min(0f)] private float pingSurfaceOffset = 0.03f;

    private float nextLocalPingTime;
    private double nextServerPingTime;

    /// <summary>이 Client에 표시된 모든 참가자의 핑(바닥 지점, 유지 시간).</summary>
    public static event Action<Vector3, float> Shown;

    /// <summary>로컬 입력기가 호출한다. 바닥에 닿지 않거나 대기시간 중이면 false.</summary>
    public bool TryRequest(Ray ray)
    {
        if (!isLocalPlayer || Time.time < nextLocalPingTime ||
            !Physics.Raycast(ray, out RaycastHit hit, 500f, pingGroundLayer, QueryTriggerInteraction.Ignore))
            return false;

        nextLocalPingTime = Time.time + pingCooldown;
        CmdPing(hit.point, hit.normal);
        return true;
    }

    [Command]
    private void CmdPing(Vector3 point, Vector3 normal)
    {
        // Client 좌표는 신뢰하지 않는다: 비정상 값과 연타만 서버에서 거른다.
        if (NetworkTime.time < nextServerPingTime || !IsFinite(point) || !IsFinite(normal))
            return;

        nextServerPingTime = NetworkTime.time + pingCooldown;
        RpcShowPing(point, Vector3.ClampMagnitude(normal, 1f));
    }

    [ClientRpc]
    private void RpcShowPing(Vector3 point, Vector3 normal)
    {
        if (pingMarkerPrefab != null)
        {
            // 원본과 같이 프리팹 회전을 유지하고, Play On Awake가 꺼진 파티클을 직접 재생한다.
            GameObject marker = Instantiate(
                pingMarkerPrefab, point + normal * pingSurfaceOffset, pingMarkerPrefab.transform.rotation);
            foreach (ParticleSystem particle in marker.GetComponentsInChildren<ParticleSystem>())
                particle.Play(false);
            Destroy(marker, pingLifetime);
        }

        Shown?.Invoke(point, pingLifetime);
    }

    private static bool IsFinite(Vector3 v) =>
        float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
}

using Mirror;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Act1 전투 카메라를 각 클라이언트의 로컬 <c>PlayerContext</c>에 연결하는 Mirror 테스트 전용 Binder다.
/// <para>원본 <c>CombatCinemachine</c> Prefab의 임의 Player 검색 컴포넌트는 사용하지 않는다.</para>
/// <para>카메라 설정과 경계는 기존 자산을 재사용하고, 추적 대상만 로컬 플레이어의 생성·해제 시점에 교체한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
public sealed class MirrorTestLocalPlayerCameraBinder : MonoBehaviour
{
    [SerializeField] private CinemachineCamera combatCamera;

    private CameraOcclusionFader occlusionFader;
    private MirrorTestNetworkManager networkManager;

    private void Awake()
    {
        combatCamera ??= GetComponent<CinemachineCamera>();
        PrepareOcclusionFader();
    }

    private void OnEnable()
    {
        TryBindNetworkManager();
    }

    private void Update()
    {
        if (networkManager == null)
            TryBindNetworkManager();

        if (occlusionFader == null && PrepareOcclusionFader())
            BindCamera(networkManager != null ? networkManager.LocalPlayerContext : null);
    }

    private void OnDisable()
    {
        if (networkManager != null)
            networkManager.LocalPlayerContextChanged -= HandleLocalPlayerContextChanged;

        networkManager = null;
        BindCamera(null);
    }

    private void TryBindNetworkManager()
    {
        MirrorTestNetworkManager candidate = NetworkManager.singleton as MirrorTestNetworkManager;
        if (candidate == null || candidate == networkManager)
            return;

        if (networkManager != null)
            networkManager.LocalPlayerContextChanged -= HandleLocalPlayerContextChanged;

        networkManager = candidate;
        networkManager.LocalPlayerContextChanged += HandleLocalPlayerContextChanged;
        BindCamera(networkManager.LocalPlayerContext);
    }

    private void HandleLocalPlayerContextChanged(PlayerContext context)
    {
        BindCamera(context);
    }

    private void BindCamera(PlayerContext context)
    {
        Transform target = context != null ? context.transform : null;

        if (combatCamera != null)
            combatCamera.Follow = target;

        PrepareOcclusionFader();
        if (occlusionFader == null)
            return;

        occlusionFader.target = target;
        occlusionFader.enabled = target != null;
    }

    private bool PrepareOcclusionFader()
    {
        if (occlusionFader != null)
            return true;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return false;

        occlusionFader = mainCamera.GetComponent<CameraOcclusionFader>();
        if (occlusionFader == null)
            return false;

        // Mirror Player는 Scene Start 이후 생성될 수 있으므로 임의 Player 태그 검색 전에 대기한다.
        occlusionFader.enabled = false;
        return true;
    }
}

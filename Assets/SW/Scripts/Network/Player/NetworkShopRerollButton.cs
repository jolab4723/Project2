using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 원본 리롤 버튼의 공용 요청을 Mirror 서버 공유 리롤 요청에 연결한다.
/// <para>클릭과 비용·남은 횟수 표시는 <see cref="ShopRerollButton"/>이 담당한다.
/// 이 연결부는 플레이어 권한과 대기 요청만 관리하며, 실패하면 서버 공유 재고와
/// 요청자의 골드를 다시 표시한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class NetworkShopRerollButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private ShopRerollButton originalButton;

    private PlayerContext context;
    private MirrorNetworkManager fallbackManager;
    private NetworkShopPlayerState playerState;
    private NetworkShopState shopState;
    private uint pendingRequestId;
    private MirrorShopRequestCompleted? lastCompletedRequest;
    private bool contextInjected;
    private bool requestStarting;
    private bool buttonLocked;
    private bool idleInteractable;

    private void Awake()
    {
        ResolveControls();
    }

    private void OnEnable()
    {
        BindOriginalRequest();

        if (contextInjected)
            SetPlayerContext(context);
        else
            ObserveFallbackContext();
    }

    private void OnDisable()
    {
        ReleaseBindings();
    }

    private void OnDestroy()
    {
        ReleaseBindings();
        context = null;
    }

    /// <summary>리롤 화면을 로컬 플레이어에 연결한다. null이면 서버 연결 대기 상태를 유지한다.</summary>
    public void Bind(PlayerContext playerContext)
    {
        contextInjected = true;
        StopObservingFallbackContext();
        if (isActiveAndEnabled)
            BindOriginalRequest();

        SetPlayerContext(playerContext);
    }

    /// <summary>플레이어 연결을 해제하고, 활성 화면은 서버 연결 대기 상태로 둔다.</summary>
    public void Unbind()
    {
        Bind(null);
    }

    private void ResolveControls()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (originalButton == null)
            originalButton = GetComponent<ShopRerollButton>();
    }

    private void BindOriginalRequest()
    {
        ResolveControls();
        // Context를 기다리는 동안에도 원본 클릭이 로컬 재고와 골드를 변경하지 않게 한다.
        originalButton?.BindRerollRequest(
            HandleClicked, GetConfirmedRemaining, GetConfirmedCost);
    }

    private void SetPlayerContext(PlayerContext playerContext)
    {
        if (playerState != null)
            playerState.RequestCompleted -= HandleRequestCompleted;

        playerState = null;
        ResetPendingRequest();
        context = playerContext;
        if (!isActiveAndEnabled)
            return;

        playerState = context != null
            ? context.GetComponent<NetworkShopPlayerState>()
            : null;
        if (playerState != null)
            playerState.RequestCompleted += HandleRequestCompleted;

        ResolveShopState();
        originalButton?.RefreshView();
    }

    private void ObserveFallbackContext()
    {
        StopObservingFallbackContext();
        fallbackManager = Mirror.NetworkManager.singleton as MirrorNetworkManager;
        if (fallbackManager != null)
            fallbackManager.LocalPlayerContextChanged += HandleLocalPlayerContextChanged;

        SetPlayerContext(fallbackManager != null ? fallbackManager.LocalPlayerContext : null);
    }

    private void StopObservingFallbackContext()
    {
        if (fallbackManager != null)
            fallbackManager.LocalPlayerContextChanged -= HandleLocalPlayerContextChanged;
        fallbackManager = null;
    }

    private void HandleLocalPlayerContextChanged(PlayerContext playerContext)
    {
        if (!contextInjected && isActiveAndEnabled)
            SetPlayerContext(playerContext);
    }

    private void ResolveShopState()
    {
        NetworkShopState nextState = MirrorSceneMode.FindInActiveMode<NetworkShopState>(gameObject.scene);
        if (shopState == nextState)
            return;

        if (shopState != null)
            shopState.StateChanged -= HandleShopStateChanged;

        shopState = nextState;
        if (shopState != null)
            shopState.StateChanged += HandleShopStateChanged;
    }

    private int GetConfirmedRemaining() => shopState != null
        ? shopState.RemainingFreeRerollCount
        : 0;

    private int GetConfirmedCost() => shopState != null
        ? shopState.PaidRerollGoldCost
        : originalButton != null ? originalButton.PaidRerollCost : 0;

    private void HandleShopStateChanged() => originalButton?.RefreshView();

    private void ReleaseBindings()
    {
        originalButton?.UnbindRerollRequest(HandleClicked);
        StopObservingFallbackContext();

        if (playerState != null)
            playerState.RequestCompleted -= HandleRequestCompleted;
        if (shopState != null)
            shopState.StateChanged -= HandleShopStateChanged;

        playerState = null;
        shopState = null;
        ResetPendingRequest();
    }

    private void ResetPendingRequest()
    {
        pendingRequestId = 0;
        lastCompletedRequest = null;
        requestStarting = false;

        if (buttonLocked && button != null)
            button.interactable = idleInteractable;
        buttonLocked = false;
    }

    private void HandleClicked()
    {
        if (!isActiveAndEnabled || pendingRequestId != 0 || requestStarting)
            return;

        if (!contextInjected &&
            fallbackManager != (Mirror.NetworkManager.singleton as MirrorNetworkManager))
        {
            ObserveFallbackContext();
        }

        ResolveShopState();
        if (playerState == null || shopState == null)
            return;

        NetworkShopPlayerState requestState = playerState;
        lastCompletedRequest = null;
        requestStarting = true;
        bool started = requestState.TryRequestReroll(out uint requestId);
        requestStarting = false;

        if (!started || !isActiveAndEnabled || playerState != requestState)
            return;

        pendingRequestId = requestId;
        if (button != null)
        {
            idleInteractable = button.interactable;
            buttonLocked = true;
            button.interactable = false;
        }

        // Host에서는 Command가 반환하기 전에 완료 이벤트가 올 수 있다.
        if (lastCompletedRequest.HasValue)
            HandleRequestCompleted(lastCompletedRequest.Value);
    }

    private void HandleRequestCompleted(MirrorShopRequestCompleted completed)
    {
        if (completed.Operation != MirrorShopOperation.Reroll)
            return;

        lastCompletedRequest = completed;
        if (pendingRequestId == 0 || completed.RequestId != pendingRequestId)
            return;

        ResetPendingRequest();
        originalButton?.RefreshView();

        if (completed.Result == MirrorShopRequestResult.Success)
            return;

        playerState?.RestoreLocalGold();
        shopState?.ForceRebuildLocalView();
        Debug.LogWarning(
            $"[NetworkShopRerollButton] 서버가 리롤을 거절했습니다: {completed.Result}",
            this);
    }
}

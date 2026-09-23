using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기존 로컬 리롤 버튼을 3-5 서버 공유 리롤 요청 버튼으로 바꾸는 테스트 전용 연결부다.
/// <para>원본 <c>SwTestShopRerollButton</c>은 비활성화하고 이 컴포넌트만
/// InventoryCamp에서 사용한다. 요청 중에는 버튼을 잠그며, 실패하면 서버 공유 재고와
/// 요청자의 골드를 다시 표시한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class NetworkShopRerollButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private NetworkShopPlayerState playerState;
    private NetworkShopState shopState;
    private uint pendingRequestId;

    private void Awake()
    {
        button ??= GetComponent<Button>();
    }

    private void OnEnable()
    {
        button?.onClick.AddListener(HandleClicked);
    }

    private void OnDisable()
    {
        button?.onClick.RemoveListener(HandleClicked);
        Unsubscribe();
        pendingRequestId = 0;
        if (button != null)
            button.interactable = true;
    }

    private void HandleClicked()
    {
        if (pendingRequestId != 0 || !TryResolveServices())
            return;

        Subscribe();
        if (!playerState.TryRequestReroll(out pendingRequestId))
        {
            pendingRequestId = 0;
            Unsubscribe();
            return;
        }

        if (button != null)
            button.interactable = false;
    }

    private bool TryResolveServices()
    {
        MirrorNetworkManager manager = FindFirstObjectByType<MirrorNetworkManager>();
        PlayerContext context = manager?.LocalPlayerContext;
        playerState = context != null
            ? context.GetComponent<NetworkShopPlayerState>()
            : null;
        shopState = FindFirstObjectByType<NetworkShopState>();
        return playerState != null && shopState != null;
    }

    private void Subscribe()
    {
        playerState.RequestCompleted -= HandleRequestCompleted;
        playerState.RequestCompleted += HandleRequestCompleted;
    }

    private void Unsubscribe()
    {
        if (playerState != null)
            playerState.RequestCompleted -= HandleRequestCompleted;
    }

    private void HandleRequestCompleted(MirrorShopRequestCompleted completed)
    {
        if (completed.RequestId != pendingRequestId ||
            completed.Operation != MirrorShopOperation.Reroll)
        {
            return;
        }

        pendingRequestId = 0;
        Unsubscribe();
        if (button != null)
            button.interactable = true;

        if (completed.Result == MirrorShopRequestResult.Success)
            return;

        playerState.RestoreLocalGold();
        shopState.ForceRebuildLocalView();
        Debug.LogWarning(
            $"[NetworkShopRerollButton] 서버가 리롤을 거절했습니다: {completed.Result}",
            this);
    }
}

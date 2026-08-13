using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 기존 드래그·우클릭 상점 거래 결과를 3-5 서버 공유 상점 요청으로 확정하는 테스트 어댑터다.
/// <para>원본과의 차이: <see cref="ShopController"/>의 배치와 거래 화면 처리는 그대로 먼저 실행한다.
/// 이 컴포넌트는 처리 전후에 아이템이 상점에서 플레이어로 갔는지, 플레이어에서 상점으로 갔는지만
/// 확인해 <see cref="NetworkShopPlayerState_MirrorTest"/>에 Command 요청을 보낸다.</para>
/// <para>다른 플레이어의 리롤·거래가 먼저 확정되어 상태 번호가 달라지면 서버가 요청을 거절한다.
/// 그때 플레이어 인벤토리와 공유 상점 화면을 서버 기록으로 모두 다시 구성하므로 드래그 중이던
/// 임시 상태나 두 클라이언트가 동시에 집은 아이템이 남지 않는다.</para>
/// <para>이 컴포넌트는 ItemPrefab_MirrorTest에서만 사용하며 운영 ItemUI와 원본 상점 코드는 수정하지 않는다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemUI), typeof(CanvasGroup))]
public sealed class NetworkShopItemTrade_MirrorTest :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerClickHandler,
    IEndDragHandler
{
    private enum TradeOrigin : byte
    {
        None = 0,
        Shop = 1,
        Player = 2,
    }

    [SerializeField] private ItemUI itemUI;
    [SerializeField] private CanvasGroup canvasGroup;

    private NetworkShopPlayerState_MirrorTest playerState;
    private NetworkShopState_MirrorTest shopState;
    private InventoryGrid playerGrid;
    private InventoryGrid shopGrid;
    private TradeOrigin pointerDownOrigin;
    private uint pendingRequestId;
    private Coroutine evaluationRoutine;
    private float idleAlpha = 1f;
    private bool idleInteractable = true;
    private bool idleBlocksRaycasts = true;

    private void Awake()
    {
        itemUI ??= GetComponent<ItemUI>();
        canvasGroup ??= GetComponent<CanvasGroup>();
    }

    private void OnDestroy()
    {
        if (evaluationRoutine != null)
            StopCoroutine(evaluationRoutine);
        Unsubscribe();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pendingRequestId != 0 ||
            (eventData.button != PointerEventData.InputButton.Left &&
             eventData.button != PointerEventData.InputButton.Right))
        {
            return;
        }

        pointerDownOrigin = CaptureOrigin();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            ScheduleEvaluation();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ScheduleEvaluation();
    }

    private void ScheduleEvaluation()
    {
        if (pendingRequestId != 0 || pointerDownOrigin == TradeOrigin.None)
            return;

        if (evaluationRoutine != null)
            StopCoroutine(evaluationRoutine);
        evaluationRoutine = StartCoroutine(EvaluateAfterOriginalTrade());
    }

    private IEnumerator EvaluateAfterOriginalTrade()
    {
        yield return null;
        evaluationRoutine = null;

        if (!TryResolveServices() || itemUI?.Item?.itemData == null)
        {
            pointerDownOrigin = TradeOrigin.None;
            yield break;
        }

        InventoryItem item = itemUI.Item;
        bool isBuy = pointerDownOrigin == TradeOrigin.Shop &&
                     playerGrid.ContainsItem(item) &&
                     itemUI.CurrentGrid == playerGrid;
        bool isSell = pointerDownOrigin == TradeOrigin.Player &&
                      shopGrid.ContainsItem(item) &&
                      itemUI.CurrentGrid == shopGrid;
        pointerDownOrigin = TradeOrigin.None;

        if (!isBuy && !isSell)
            yield break;

        Subscribe();
        bool requested = isBuy
            ? playerState.TryRequestBuy(
                item.itemData.instanceId,
                item.x,
                item.y,
                item.isRotated,
                out pendingRequestId)
            : playerState.TryRequestSell(
                item.itemData.instanceId,
                item.x,
                item.y,
                item.isRotated,
                out pendingRequestId);

        if (requested)
        {
            LockItemView();
            yield break;
        }

        pendingRequestId = 0;
        Unsubscribe();
        RestoreAuthoritativeState();
        Debug.LogWarning("[NetworkShopItemTrade_MirrorTest] 상점 서버 요청을 시작하지 못해 화면을 복구했습니다.", this);
    }

    private TradeOrigin CaptureOrigin()
    {
        if (!TryResolveServices() || itemUI?.Item?.itemData == null)
            return TradeOrigin.None;

        InventoryItem item = itemUI.Item;
        if (itemUI.CurrentGrid == shopGrid && shopGrid.ContainsItem(item))
            return TradeOrigin.Shop;

        if ((itemUI.CurrentGrid == playerGrid && playerGrid.ContainsItem(item)) || item.isEquipped)
            return TradeOrigin.Player;

        return TradeOrigin.None;
    }

    private bool TryResolveServices()
    {
        if (playerState != null && shopState != null && playerGrid != null && shopGrid != null)
            return true;

        MirrorTestNetworkManager manager = FindFirstObjectByType<MirrorTestNetworkManager>();
        PlayerContext localContext = manager?.LocalPlayerContext;
        playerState = localContext != null
            ? localContext.GetComponent<NetworkShopPlayerState_MirrorTest>()
            : null;
        shopState = FindFirstObjectByType<NetworkShopState_MirrorTest>();
        ShopController shopController =
            FindFirstObjectByType<ShopController>(FindObjectsInactive.Include);
        playerGrid = localContext?.Inventory?.PlayerGrid;
        shopGrid = shopController?.ShopGrid;
        return playerState != null && shopState != null && playerGrid != null && shopGrid != null;
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

    private void HandleRequestCompleted(MirrorTestShopRequestCompleted completed)
    {
        if (completed.RequestId != pendingRequestId ||
            (completed.Operation != MirrorTestShopOperation.Buy &&
             completed.Operation != MirrorTestShopOperation.Sell))
        {
            return;
        }

        pendingRequestId = 0;
        Unsubscribe();
        UnlockItemView();

        if (completed.Result == MirrorTestShopRequestResult.Success)
            return;

        RestoreAuthoritativeState();
        Debug.LogWarning(
            $"[NetworkShopItemTrade_MirrorTest] 서버가 상점 거래를 거절했습니다: {completed.Result}",
            this);
    }

    private void RestoreAuthoritativeState()
    {
        PlayerInventorySync_MirrorTest inventorySync =
            playerState?.Context?.GetComponent<PlayerInventorySync_MirrorTest>();
        inventorySync?.TryRestoreGridFromAuthoritativeSnapshots();
        playerState?.RestoreLocalGold();
        shopState?.ForceRebuildLocalView();
    }

    private void LockItemView()
    {
        if (canvasGroup == null)
            return;

        idleAlpha = canvasGroup.alpha;
        idleInteractable = canvasGroup.interactable;
        idleBlocksRaycasts = canvasGroup.blocksRaycasts;
        canvasGroup.alpha = Mathf.Min(idleAlpha, 0.55f);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void UnlockItemView()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = idleAlpha;
        canvasGroup.interactable = idleInteractable;
        canvasGroup.blocksRaycasts = idleBlocksRaycasts;
    }
}

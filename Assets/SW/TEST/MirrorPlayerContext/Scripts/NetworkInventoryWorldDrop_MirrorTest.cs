using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Mirror 테스트 아이템 UI에만 붙는 실제 드래그 월드 드랍 어댑터다.
/// <para>원본과의 차이: 기존 <see cref="ItemDragHandler"/>와 <see cref="ItemDropHandler"/>는
/// 그리드 이동·상점·강화·장비 복구를 그대로 처리하고, 이 컴포넌트는 빈 월드로 끝난 드래그만
/// 해당 아이템의 instanceId와 함께 <see cref="PlayerInventorySync_MirrorTest"/>로 보낸다.</para>
/// <para>서버 응답 전에는 같은 UI의 입력을 잠그고, 실패하면 다시 활성화한다. 성공 시에는
/// Owner 전용 Inventory Snapshot이 모델과 UI를 제거하므로 클라이언트가 소유권을 먼저 확정하지 않는다.</para>
/// <para>테스트 Prefab의 기존 <see cref="InventoryItemUISpawner"/>에는
/// WorldItemDropService를 연결하지 않아 로컬 드랍과 네트워크 드랍이 동시에 실행되지 않게 한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemUI), typeof(CanvasGroup))]
public sealed class NetworkInventoryWorldDrop_MirrorTest : MonoBehaviour, IEndDragHandler
{
    [SerializeField] private ItemUI itemUI;
    [SerializeField] private CanvasGroup canvasGroup;

    private PlayerInventorySync_MirrorTest inventorySync;
    private uint pendingRequestId;
    private float idleAlpha = 1f;
    private bool idleInteractable = true;
    private bool idleBlocksRaycasts = true;

    public bool IsPending => pendingRequestId != 0;

    private void Awake()
    {
        itemUI ??= GetComponent<ItemUI>();
        canvasGroup ??= GetComponent<CanvasGroup>();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (IsPending || !IsWorldDrop(eventData) || !TryResolveInventorySync())
            return;

        string instanceId = itemUI.Item.itemData.instanceId;
        Subscribe();

        if (!inventorySync.TryRequestDropInventoryItem(instanceId, out uint requestId))
        {
            Unsubscribe();
            Debug.LogWarning(
                "[NetworkInventoryWorldDrop_MirrorTest] 서버 드랍 요청을 시작하지 못했습니다.",
                this);
            return;
        }

        pendingRequestId = requestId;
        LockItemView();
    }

    private bool IsWorldDrop(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.pointerCurrentRaycast.gameObject != null ||
            itemUI?.Item?.itemData == null ||
            itemUI.OriginalWasEquipped ||
            itemUI.OriginalGrid == null)
        {
            return false;
        }

        if (itemUI.OriginalGrid == ShopController.Instance?.ShopGrid)
            return false;

        RectTransform gridRect = itemUI.OriginalGrid.GridRect;
        return gridRect != null &&
               !RectTransformUtility.RectangleContainsScreenPoint(
                   gridRect,
                   eventData.position,
                   eventData.pressEventCamera);
    }

    private bool TryResolveInventorySync()
    {
        if (inventorySync != null)
            return true;

        MirrorTestNetworkManager networkManager =
            FindFirstObjectByType<MirrorTestNetworkManager>();
        PlayerContext localContext = networkManager?.LocalPlayerContext;
        PlayerInventorySync_MirrorTest candidate =
            localContext != null
                ? localContext.GetComponent<PlayerInventorySync_MirrorTest>()
                : null;

        if (candidate == null ||
            localContext.Inventory == null ||
            localContext.Inventory.PlayerGrid != itemUI.OriginalGrid)
        {
            Debug.LogWarning(
                "[NetworkInventoryWorldDrop_MirrorTest] 이 아이템 UI의 로컬 PlayerContext를 찾지 못했습니다.",
                this);
            return false;
        }

        inventorySync = candidate;
        return true;
    }

    private void Subscribe()
    {
        inventorySync.RequestCompleted -= HandleRequestCompleted;
        inventorySync.RequestCompleted += HandleRequestCompleted;
    }

    private void Unsubscribe()
    {
        if (inventorySync != null)
            inventorySync.RequestCompleted -= HandleRequestCompleted;
    }

    private void HandleRequestCompleted(MirrorTestInventoryRequestCompleted completed)
    {
        if (completed.RequestId != pendingRequestId ||
            completed.Operation != MirrorTestInventoryOperation.DropInventoryItem)
        {
            return;
        }

        pendingRequestId = 0;
        Unsubscribe();

        if (completed.Result == MirrorTestInventoryRequestResult.Success)
        {
            Destroy(gameObject);
            return;
        }

        UnlockItemView();
        Debug.LogWarning(
            $"[NetworkInventoryWorldDrop_MirrorTest] 서버가 월드 드랍을 거절했습니다: {completed.Result}",
            this);
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

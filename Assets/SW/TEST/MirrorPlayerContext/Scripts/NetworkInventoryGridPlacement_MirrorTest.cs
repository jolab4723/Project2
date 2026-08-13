using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 기존 인벤토리 드래그 결과를 Mirror에 전달하는 테스트 전용 어댑터다.
/// <para>팀 원본과의 차이: <see cref="ItemDragHandler"/>, <see cref="ItemDropHandler"/>,
/// <see cref="InventoryMoveService"/>와 교환 계산은 수정하지 않는다. 이 컴포넌트가 기존 처리 뒤에 실행되어
/// 플레이어 그리드의 최종 위치와 회전을 읽고, 그 의도만 <see cref="PlayerInventorySync_MirrorTest"/>로 보낸다.</para>
/// <para>로컬 결과는 서버 승인 전 임시 표시다. Command 대기 중에는 아이템 UI를 잠그고,
/// 성공하면 서버 스냅샷 결과를 유지한다. 실패하면 Owner 스냅샷으로 로컬 그리드 전체를 다시 구성해
/// 교환된 두 아이템을 함께 복구한다.</para>
/// <para>이 컴포넌트는 ItemPrefab_MirrorTest에서만 참조한다.
/// 원본 인벤토리 아이템 프리팹과 운영 싱글플레이 씬에는 네트워크 동작을 추가하지 않는다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemUI), typeof(ItemDragHandler), typeof(CanvasGroup))]
public sealed class NetworkInventoryGridPlacement_MirrorTest : MonoBehaviour, IEndDragHandler
{
    [SerializeField] private ItemUI itemUI;
    [SerializeField] private CanvasGroup canvasGroup;

    private PlayerInventorySync_MirrorTest inventorySync;
    private InventoryGrid playerGrid;
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
        InventoryItem item = itemUI?.Item;
        InventoryPlacementSnapshot original = itemUI != null
            ? itemUI.OriginalPlacement
            : default;

        if (IsPending ||
            item?.itemData == null ||
            itemUI.OriginalWasEquipped ||
            !original.IsValid ||
            !TryResolveInventorySync() ||
            itemUI.CurrentGrid != playerGrid ||
            item.isEquipped ||
            !playerGrid.ContainsItem(item) ||
            IsUnchanged(item, original))
        {
            return;
        }

        Subscribe();
        if (!inventorySync.TryRequestMoveGridItem(
                item.itemData.instanceId,
                item.x,
                item.y,
                item.isRotated,
                out uint requestId))
        {
            Unsubscribe();
            RestoreAuthoritativeGrid();
            Debug.LogWarning(
                "[NetworkInventoryGridPlacement_MirrorTest] Could not start the server grid-move request.",
                this);
            return;
        }

        pendingRequestId = requestId;
        LockItemView();
    }

    private bool TryResolveInventorySync()
    {
        if (inventorySync != null && playerGrid != null)
            return itemUI.OriginalGrid == playerGrid;

        MirrorTestNetworkManager networkManager =
            FindFirstObjectByType<MirrorTestNetworkManager>();
        PlayerContext localContext = networkManager?.LocalPlayerContext;
        PlayerInventorySync_MirrorTest candidate = localContext != null
            ? localContext.GetComponent<PlayerInventorySync_MirrorTest>()
            : null;
        InventoryGrid candidateGrid = localContext?.Inventory?.PlayerGrid;

        if (candidate == null ||
            candidateGrid == null ||
            itemUI.OriginalGrid != candidateGrid)
        {
            return false;
        }

        inventorySync = candidate;
        playerGrid = candidateGrid;
        return true;
    }

    private static bool IsUnchanged(
        InventoryItem item,
        InventoryPlacementSnapshot original)
    {
        return item.x == original.Rect.X &&
               item.y == original.Rect.Y &&
               item.isRotated == original.IsRotated;
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
            completed.Operation != MirrorTestInventoryOperation.MoveGridItem)
        {
            return;
        }

        pendingRequestId = 0;
        Unsubscribe();
        UnlockItemView();

        if (completed.Result == MirrorTestInventoryRequestResult.Success)
            return;

        RestoreAuthoritativeGrid();
        Debug.LogWarning(
            $"[NetworkInventoryGridPlacement_MirrorTest] Server rejected the grid move: {completed.Result}",
            this);
    }

    private void RestoreAuthoritativeGrid()
    {
        if (inventorySync != null &&
            !inventorySync.TryRestoreGridFromAuthoritativeSnapshots())
        {
            Debug.LogError(
                "[NetworkInventoryGridPlacement_MirrorTest] Failed to rebuild the local grid from server snapshots.",
                inventorySync);
        }
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

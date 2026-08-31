using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class WorldItemPickupInteractor : MonoBehaviour
{
    [SerializeField]
    private WorldItemTooltipScanner scanner;

    [SerializeField]
    private Camera worldCamera;

    [SerializeField]
    private LayerMask worldItemLayer;

    [SerializeField]
    private MonoBehaviour receiverBehaviour;

    [SerializeField, Min(0f)]
    private float rayDistance = 500f;

    private IItemReceiver Receiver =>
        receiverBehaviour as IItemReceiver;

    private void Awake()
    {
        if (scanner == null)
            scanner = GetComponent<WorldItemTooltipScanner>();

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (receiverBehaviour != null && Receiver == null)
        {
            Debug.LogError(
                "[WorldItemPickupInteractor] " +
                "Receiver가 IItemReceiver를 구현하지 않았습니다.");
        }
    }

    /// <summary>
    /// 클릭을 월드 아이템 상호작용으로 처리했으면 true.
    /// 획득 성공 여부가 아니라 클릭 소비 여부를 반환합니다.
    /// </summary>
    public bool TryHandleClick(Vector2 screenPosition)
    {
        // UI를 클릭했다면 월드 공격이나 획득으로 넘기지 않는다.
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        if (scanner == null || worldCamera == null)
            return false;

        Ray ray =
            worldCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                rayDistance,
                worldItemLayer,
                QueryTriggerInteraction.Collide))
        {
            return false;
        }

        ItemDataStorage pickup =
            hit.collider.GetComponentInParent<ItemDataStorage>();

        if (pickup == null)
            return false;

        // 월드 아이템은 클릭했지만 범위 밖이므로 획득하지 않는다.
        // 공격으로 넘어가지 않도록 클릭은 소비한다.
        if (!scanner.CanInteract(pickup))
        {
            Debug.Log(
                "[월드 아이템] 획득할 수 있는 거리보다 멉니다.");

            return true;
        }

        TryAcquire(pickup);
        return true;
    }

    /// <summary>
    /// 임시 Get 버튼에서도 동일한 획득 로직을 재사용할 수 있습니다.
    /// </summary>
    public bool TryAcquireCurrentTarget()
    {
        if (scanner == null ||
            !scanner.TryGetCurrentTarget(
                out ItemDataStorage pickup))
        {
            return false;
        }

        return TryAcquire(pickup);
    }

    private bool TryAcquire(ItemDataStorage pickup)
    {
        if (pickup == null ||
            pickup.Item?.definition == null)
        {
            return false;
        }

        if (Receiver == null)
        {
            Debug.LogWarning(
                "[WorldItemPickupInteractor] " +
                "아이템을 받을 인벤토리가 연결되지 않았습니다.");

            return false;
        }

        if (!pickup.TryGetComponent(
            out WorldItemPickupClaim pickupClaim))
        {
            Debug.LogError(
                "[WorldItemPickupInteractor] " +
                "월드 아이템에 WorldItemPickupClaim이 없습니다.");

            return false;
        }

        // 다른 클릭이나 획득 요청이 먼저 처리 중이면 중단한다.
        if (!pickupClaim.TryClaim())
            return false;

        bool success = Receiver is InventoryController inventory
            ? inventory.AddWorldItem(pickup.Item)
            : ItemAcquisition.Acquire(pickup.Item, Receiver);

        if (!success)
        {
            // 인벤토리 공간 부족 등의 경우 다시 획득할 수 있어야 한다.
            pickupClaim.Release();
            return false;
        }

        // Destroy는 프레임 마지막에 실행되므로 즉시 비활성화해
        // Collider와 다른 획득 입력을 먼저 차단한다.
        pickup.gameObject.SetActive(false);
        Destroy(pickup.gameObject);

        return true;
    }
}

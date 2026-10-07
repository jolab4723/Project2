using System.Collections;
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

        StartCoroutine(PlayPickupAndDestroy(
            pickup.gameObject,
            scanner.BoundPlayer != null
                ? scanner.BoundPlayer
                : receiverBehaviour.transform));

        return true;
    }

    /// <summary>
    /// 획득이 확정된 월드 아이템을 플레이어에게 흡수시킨 뒤 로컬 오브젝트를 제거한다.
    /// </summary>
    private IEnumerator PlayPickupAndDestroy(
        GameObject pickupObject,
        Transform target)
    {
        yield return WorldItemPickupPresentation.Play(
            pickupObject,
            target);

        if (pickupObject != null)
            Destroy(pickupObject);
    }
}

/// <summary>
/// 월드 아이템의 권한과 데이터는 건드리지 않고, 기존 시각 루트만 짧게 흡수 연출한다.
/// </summary>
internal static class WorldItemPickupPresentation
{
    internal const float Duration = 0.22f;

    /// <summary>
    /// 연출 중 중복 획득과 기존 Loot Beam 표시를 즉시 차단한다.
    /// </summary>
    internal static void Prepare(GameObject pickupObject)
    {
        if (pickupObject == null)
            return;

        foreach (Collider targetCollider in
                 pickupObject.GetComponentsInChildren<Collider>(true))
        {
            targetCollider.enabled = false;
        }

        Transform lootVfx = pickupObject.transform.Find("LootVFX");
        if (lootVfx != null)
            lootVfx.gameObject.SetActive(false);

        pickupObject
            .GetComponent<WorldItemCategoryVisualView>()?
            .SetHovered(false);
    }

    /// <summary>
    /// CategoryVisuals를 플레이어 상체 방향으로 이동·축소하고 렌더링을 종료한다.
    /// </summary>
    internal static IEnumerator Play(
        GameObject pickupObject,
        Transform target)
    {
        if (pickupObject == null)
            yield break;

        Prepare(pickupObject);

        Transform visual =
            pickupObject.transform.Find("CategoryVisuals") ??
            pickupObject.transform;

        Vector3 startPosition = visual.position;
        Vector3 startScale = visual.localScale;
        float elapsed = 0f;

        while (elapsed < Duration && pickupObject != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float eased = t * t;
            Vector3 targetPosition = target != null
                ? target.position + Vector3.up
                : startPosition;

            visual.position = Vector3.LerpUnclamped(
                startPosition,
                targetPosition,
                eased);
            visual.localScale = Vector3.LerpUnclamped(
                startScale,
                startScale * 0.3f,
                eased);
            yield return null;
        }

        if (pickupObject == null)
            yield break;

        foreach (Renderer targetRenderer in
                 pickupObject.GetComponentsInChildren<Renderer>(true))
        {
            targetRenderer.enabled = false;
        }
    }
}

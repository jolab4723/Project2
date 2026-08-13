using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class WorldItemTooltipScanner : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private WorldItemTooltipView tooltipView;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask worldItemLayer;
    [SerializeField] private float detectionRadius = 2.5f;
    [SerializeField] private float rayDistance = 500f;

    private ItemDataStorage currentTarget;

    private void Awake()
    {
        if (player == null)
            player = transform;

        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void Update()
    {
        UpdateHoveredItem();
    }

    private void UpdateHoveredItem()
    {
        ItemDataStorage hoveredItem = FindHoveredItem();

        if (hoveredItem == currentTarget)
            return;

        currentTarget = hoveredItem;

        if (currentTarget == null)
        {
            tooltipView?.Hide();
            return;
        }

        tooltipView?.Show(
            currentTarget.Item,
            currentTarget.transform);
    }

    private ItemDataStorage FindHoveredItem()
    {
        if (player == null)
            return null;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null)
            return null;

        // UI 뒤에 있는 월드 아이템의 툴팁은 표시하지 않는다.
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return null;
        }

        Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                rayDistance,
                worldItemLayer,
                QueryTriggerInteraction.Collide))
        {
            return null;
        }

        ItemDataStorage target =
            hit.collider.GetComponentInParent<ItemDataStorage>();

        if (!IsWithinDetectionRadius(target))
            return null;

        return target;
    }

    private bool IsWithinDetectionRadius(ItemDataStorage target)
    {
        if (player == null ||
            target == null ||
            target.Item?.definition == null)
        {
            return false;
        }

        float sqrDistance =
            (target.transform.position - player.position).sqrMagnitude;

        return sqrDistance <= detectionRadius * detectionRadius;
    }

    public bool TryGetCurrentTarget(out ItemDataStorage target)
    {
        target = currentTarget;

        return target != null &&
               target.Item?.definition != null;
    }

    public bool CanInteract(ItemDataStorage target)
    {
        // 호버 갱신과 클릭 처리의 실행 순서에 영향받지 않도록
        // 클릭 가능 여부는 거리만 독립적으로 검사한다.
        return IsWithinDetectionRadius(target);
    }

    private void OnDisable()
    {
        currentTarget = null;
        tooltipView?.Hide();
    }
}
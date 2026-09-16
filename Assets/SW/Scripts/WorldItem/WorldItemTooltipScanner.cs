using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class WorldItemTooltipScanner : MonoBehaviour
{
    [Tooltip("거리 판정 기준을 특정 대상으로 고정하고 싶을 때만 지정한다. " +
             "비워두면 현재 플레이어(InventoryController.Instance)를 매번 따라간다.")]
    [SerializeField] private Transform playerOverride;
    [SerializeField] private WorldItemTooltipView tooltipView;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask worldItemLayer;
    [SerializeField] private float detectionRadius = 2.5f;
    [SerializeField] private float rayDistance = 500f;

    private ItemDataStorage currentTarget;

    /// <summary>
    /// 거리 판정 기준이 되는 플레이어. 인스펙터에 고정 대상이 있으면 그것을, 없으면 현재 플레이어를 쓴다.
    ///
    /// !! 캐시하지 않고 매번 조회한다 - 캐릭터가 런타임에 교체되면(캐릭터 선택, 재생성 등) 예전엔 씬에
    ///    박아둔 Transform을 계속 가리켜서, 발밑 아이템인데도 "거리가 멀다"로 판정되는 문제가 있었다.
    ///    PlayerHealthManager.Instance는 "내 캐릭터"를 가리키는 기존 계약이고 **캐릭터 오브젝트 자체에**
    ///    붙어 있어서 그 transform이 곧 플레이어 위치다. 교체되면 Instance도 같이 바뀐다.
    ///    (InventoryController.Instance도 "내 캐릭터"용이지만 캐릭터가 아니라 별도 매니저 오브젝트에
    ///     붙어 있어 위치 기준으로는 쓸 수 없다.)
    ///    새 전역 참조를 만들지 않으려고 이미 있는 계약을 재사용한다. 향후 PlayerContext로 옮길 때도
    ///    이 프로퍼티 한 곳만 고치면 된다.
    /// </summary>
    private Transform Player
    {
        get
        {
            if (playerOverride != null)
                return playerOverride;

            PlayerHealthManager health = PlayerHealthManager.Instance;
            return health != null ? health.transform : null;
        }
    }

    public Transform BoundPlayer => Player;

    private void Awake()
    {
        // 예전엔 플레이어를 못 찾으면 transform(= 이 컴포넌트가 붙은 캔버스)으로 폴백했는데,
        // 그러면 플레이어가 아니라 UI 오브젝트 위치로 거리를 재서 조용히 틀린 결과가 나왔다.
        // 지금은 Player가 null이면 감지 자체를 하지 않는다(IsWithinDetectionRadius의 null 검사).

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

        if (object.ReferenceEquals(hoveredItem, currentTarget))
            return;

        SetHovered(currentTarget, false);
        currentTarget = hoveredItem;
        SetHovered(currentTarget, true);

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
        if (Player == null)
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
        Transform player = Player;

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

    /// <summary>
    /// 거리 판정 기준을 특정 플레이어로 고정한다(= playerOverride 지정). null을 넣으면 고정을 풀고
    /// 다시 현재 플레이어를 자동으로 따라간다.
    /// </summary>
    public void BindPlayer(Transform targetPlayer)
    {
        if (playerOverride == targetPlayer)
            return;

        playerOverride = targetPlayer;
        SetHovered(currentTarget, false);
        currentTarget = null;
        tooltipView?.Hide();
    }

    private void OnDisable()
    {
        SetHovered(currentTarget, false);
        currentTarget = null;
        tooltipView?.Hide();
    }

    private static void SetHovered(ItemDataStorage target, bool hovered)
    {
        if (target != null &&
            target.TryGetComponent(out WorldItemCategoryVisualView visualView))
        {
            visualView.SetHovered(hovered);
        }
    }
}

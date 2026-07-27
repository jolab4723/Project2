using ItemSystem;
using UnityEngine;
using UnityEngine.InputSystem;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance
    {
        get; private set;
    }

    [Header("Canvas")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform canvasRect;

    [Header("Equipment")]
    [SerializeField] private EquipmentSystem equipmentSystem;

    [Header("Tooltip View")]
    [SerializeField] private TooltipUI primaryTooltip;

    [Header("Position")]
    [SerializeField]
    private Vector2 offset = new Vector2(15f, 15f);

    [SerializeField, Min(0f)] private float comparisonGap = 12f;

    // 실행 시 primaryTooltip을 복제해서 만든다.
    private TooltipUI comparisonTooltip;

    // 장비 변경 이벤트가 발생했을 때
    // 현재 표시 중인 비교를 다시 계산하기 위해 보관한다.
    private ItemInstance currentHoveredItem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TooltipManager] 중복 인스턴스가 발견되었습니다.");

            enabled = false;
            return;
        }

        Instance = this;

        if (primaryTooltip == null)
        {
            Debug.LogWarning("[TooltipManager] Primary Tooltip이 연결되지 않았습니다.");
            return;
        }

        CreateComparisonTooltip();

        primaryTooltip.Hide();
        comparisonTooltip.Hide();

        if (equipmentSystem != null)
            equipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;
        else
            Debug.LogWarning("[TooltipManager] EquipmentSystem이 연결되지 않아 비교 툴팁이 표시되지 않습니다.");
    }

    private void OnDestroy()
    {
        if (equipmentSystem != null)
            equipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;

        if (comparisonTooltip != null)
            Destroy(comparisonTooltip.gameObject);

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (primaryTooltip == null || !primaryTooltip.IsVisible || Mouse.current == null)
            return;

        PositionVisibleTooltips(Mouse.current.position.ReadValue());
    }

    public void ShowTooltip(ItemInstance itemData)
    {
        if (itemData == null || itemData.definition == null)
        {
            Debug.LogWarning("[TooltipManager] 유효하지 않은 itemData가 전달되었습니다.");
            return;
        }

        if (primaryTooltip == null)
        {
            Debug.LogWarning("[TooltipManager] Primary Tooltip이 연결되지 않았습니다.");
            return;
        }

        currentHoveredItem = itemData;

        ItemMainStatComparisonResult result = null;

        bool hasComparison =
            comparisonTooltip != null &&
            equipmentSystem != null &&
            TooltipComparisonResolver.TryResolveComparison(
                    itemData,
                    equipmentSystem,
                    out result);

        if (hasComparison)
            ShowComparedTooltips(itemData, result);
        else
            ShowSingleTooltip(itemData);

        if (Mouse.current != null)
            PositionVisibleTooltips(Mouse.current.position.ReadValue());
    }

    private void ShowSingleTooltip(ItemInstance itemData)
    {
        primaryTooltip.Show(itemData);

        if (comparisonTooltip != null)
            comparisonTooltip.Hide();
    }

    private void ShowComparedTooltips(
        ItemInstance candidateItem,
        ItemMainStatComparisonResult result)
    {
        if (result == null || result.EquippedItem == null)
        {
            ShowSingleTooltip(candidateItem);
            return;
        }

        // 후보 툴팁:
        // 후보 실제 수치 + 초록/빨강 변화량
        bool candidateShown = primaryTooltip.Show(candidateItem, result);

        // 현재 장비 툴팁:
        // 비교 색상 없이 현재 장비의 실제 정보
        bool equippedShown = comparisonTooltip.Show(result.EquippedItem);

        if (!candidateShown)
        {
            primaryTooltip.Hide();
            comparisonTooltip.Hide();
            return;
        }

        if (!equippedShown)
            comparisonTooltip.Hide();
    }

    public void HideTooltip()
    {
        currentHoveredItem = null;

        if (primaryTooltip != null)
            primaryTooltip.Hide();

        if (comparisonTooltip != null)
            comparisonTooltip.Hide();
    }

    /// <summary>
    /// Primary Tooltip을 복제해 비교용 View를 만든다.
    ///
    /// TooltipUI 내부의 자식 오브젝트 참조는
    /// Unity Instantiate가 복제본의 자식으로 자동 재연결한다.
    /// </summary>
    private void CreateComparisonTooltip()
    {
        comparisonTooltip = Instantiate(primaryTooltip, primaryTooltip.transform.parent);

        comparisonTooltip.gameObject.name = "EquippedItemTooltip";

        int primarySiblingIndex = primaryTooltip.transform.GetSiblingIndex();

        comparisonTooltip.transform.SetSiblingIndex(primarySiblingIndex + 1);

        comparisonTooltip.Hide();
    }

    /// <summary>
    /// 장비가 교체되거나 해제됐을 때
    /// 현재 열린 툴팁의 비교 대상을 다시 찾는다.
    /// </summary>
    private void HandleEquipmentChanged(EquippedItemInfo[] equippedItems)
    {
        if (currentHoveredItem == null || primaryTooltip == null || !primaryTooltip.IsVisible)
            return;

        ShowTooltip(currentHoveredItem);
    }

    private void PositionVisibleTooltips(Vector2 screenPosition)
    {
        RectTransform targetCanvasRect = GetCanvasRect();

        if (targetCanvasRect == null || primaryTooltip == null)
            return;

        SetTooltipAtScreenPosition(primaryTooltip, screenPosition, targetCanvasRect);

        if (comparisonTooltip != null && comparisonTooltip.IsVisible)
        {
            PlaceComparisonNextToPrimary(targetCanvasRect);

            ClampTooltipGroupInsideCanvas(
                primaryTooltip.RootRect,
                comparisonTooltip.RootRect,
                targetCanvasRect);
        }
        else
        {
            ClampInsideCanvas(
primaryTooltip.RootRect,
                targetCanvasRect);
        }
    }

    private void SetTooltipAtScreenPosition(
        TooltipUI tooltip,
        Vector2 screenPosition,
        RectTransform targetCanvasRect)
    {
        if (tooltip == null || tooltip.RootRect == null)
            return;

        RectTransform tooltipRect = tooltip.RootRect;

        Camera eventCamera = GetCanvasCamera();

        Vector2 targetScreenPosition = screenPosition + offset;

        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            tooltipRect.position =
                new Vector3(
                    targetScreenPosition.x,
                    targetScreenPosition.y,
                    tooltipRect.position.z);
        }
        else if (
            RectTransformUtility
                .ScreenPointToWorldPointInRectangle(
                    targetCanvasRect,
                    targetScreenPosition,
                    eventCamera,
                    out Vector3 worldPoint))
        {
            tooltipRect.position = worldPoint;
        }
    }

    /// <summary>
    /// 장착 중 툴팁을 후보 툴팁의 왼쪽에 배치한다.
    /// 왼쪽 공간이 부족하면 오른쪽으로 전환한다.
    /// 두 툴팁의 윗부분은 같은 높이로 정렬한다.
    /// </summary>
    private void PlaceComparisonNextToPrimary(RectTransform targetCanvasRect)
    {
        RectTransform primaryRect = primaryTooltip.RootRect;

        RectTransform comparisonRect = comparisonTooltip.RootRect;

        if (primaryRect == null || comparisonRect == null)
            return;

        var primaryCorners = new Vector3[4];
        var comparisonCorners = new Vector3[4];
        var canvasCorners = new Vector3[4];

        primaryRect.GetWorldCorners(primaryCorners);
        comparisonRect.GetWorldCorners(comparisonCorners);
        targetCanvasRect.GetWorldCorners(canvasCorners);

        float comparisonWidth = comparisonCorners[2].x - comparisonCorners[0].x;

        float canvasScale = Mathf.Abs(targetCanvasRect.lossyScale.x);

        float gapInWorld = comparisonGap * canvasScale;

        float requiredSpace = comparisonWidth + gapInWorld;

        float leftSpace = primaryCorners[0].x - canvasCorners[0].x;

        float rightSpace = canvasCorners[2].x - primaryCorners[2].x;

        bool placeOnLeft = leftSpace >= requiredSpace || leftSpace >= rightSpace;

        float targetX;

        if (placeOnLeft)
        {
            // 비교 툴팁의 오른쪽 끝을
            // 후보 툴팁의 왼쪽에 맞춘다.
            targetX = primaryCorners[0].x - gapInWorld;

            float moveX = targetX - comparisonCorners[2].x;

            comparisonRect.position += new Vector3(moveX, 0f, 0f);
        }
        else
        {
            // 비교 툴팁의 왼쪽 끝을
            // 후보 툴팁의 오른쪽에 맞춘다.
            targetX = primaryCorners[2].x + gapInWorld;

            float moveX = targetX - comparisonCorners[0].x;

            comparisonRect.position += new Vector3(moveX, 0f, 0f);
        }

        // 두 툴팁의 위쪽 높이를 맞춘다.
        comparisonRect.GetWorldCorners(comparisonCorners);

        float moveY = primaryCorners[2].y - comparisonCorners[2].y;

        comparisonRect.position += new Vector3(0f, moveY, 0f);
    }

    /// <summary>
    /// 두 툴팁을 하나의 묶음으로 취급해
    /// Canvas 바깥으로 나간 만큼 함께 이동한다.
    /// </summary>
    private void ClampTooltipGroupInsideCanvas(
        RectTransform primaryRect,
        RectTransform comparisonRect,
        RectTransform targetCanvasRect)
    {
        if (primaryRect == null || comparisonRect == null || targetCanvasRect == null)
            return;

        var primaryCorners = new Vector3[4];

        var comparisonCorners = new Vector3[4];

        var canvasCorners = new Vector3[4];

        primaryRect.GetWorldCorners(primaryCorners);

        comparisonRect.GetWorldCorners(comparisonCorners);

        targetCanvasRect.GetWorldCorners(canvasCorners);

        float groupLeft =
            Mathf.Min(primaryCorners[0].x, comparisonCorners[0].x);

        float groupRight =
            Mathf.Max(primaryCorners[2].x, comparisonCorners[2].x);

        float groupBottom =
            Mathf.Min(primaryCorners[0].y, comparisonCorners[0].y);

        float groupTop =
            Mathf.Max(primaryCorners[2].y, comparisonCorners[2].y);

        Vector3 correction = Vector3.zero;

        if (groupRight > canvasCorners[2].x)
        {
            correction.x = canvasCorners[2].x -groupRight;
        }
        else if (groupLeft < canvasCorners[0].x)
        {
            correction.x = canvasCorners[0].x - groupLeft;
        }

        if (groupTop > canvasCorners[2].y)
        {
            correction.y = canvasCorners[2].y - groupTop;
        }
        else if (groupBottom < canvasCorners[0].y)
        {
            correction.y = canvasCorners[0].y - groupBottom;
        }

        primaryRect.position += correction;
        comparisonRect.position += correction;
    }
    private RectTransform GetCanvasRect()
    {
        if (canvasRect != null)
            return canvasRect;

        if (canvas != null)
            return canvas.transform as RectTransform;

        return null;
    }

    private Camera GetCanvasCamera()
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    /// <summary>
    /// 툴팁의 네 모서리를 Canvas의 네 모서리와 비교해
    /// 화면 밖으로 벗어난 만큼 안쪽으로 이동한다.
    /// </summary>
    private void ClampInsideCanvas(
        RectTransform tooltipRect,
        RectTransform targetCanvasRect)
    {
        var tooltipCorners = new Vector3[4];
        var canvasCorners = new Vector3[4];

        tooltipRect.GetWorldCorners(tooltipCorners);
        targetCanvasRect.GetWorldCorners(canvasCorners);

        Vector3 correction = Vector3.zero;

        // 오른쪽 또는 왼쪽 경계 보정
        if (tooltipCorners[2].x > canvasCorners[2].x)
        {
            correction.x = canvasCorners[2].x - tooltipCorners[2].x;
        }
        else if (tooltipCorners[0].x < canvasCorners[0].x)
        {
            correction.x = canvasCorners[0].x - tooltipCorners[0].x;
        }

        // 위쪽 또는 아래쪽 경계 보정
        if (tooltipCorners[2].y > canvasCorners[2].y)
        {
            correction.y = canvasCorners[2].y - tooltipCorners[2].y;
        }
        else if (tooltipCorners[0].y < canvasCorners[0].y)
        {
            correction.y = canvasCorners[0].y - tooltipCorners[0].y;
        }

        tooltipRect.position += correction;
    }
}
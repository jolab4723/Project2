using System.Collections;
using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private ItemEquipHandler equipmentHandler;
    private InventoryGrid currentGrid;
    private InventoryGrid originalGrid;

    public EquipSlotUI CurrentEquipSlot => currentEquipSlot;


    public RectTransform Rect => rect;
    public Vector2 SizeDelta => rect.sizeDelta;
    public InventoryGrid OriginalGrid => originalGrid;
    private RectTransform rect;
    
    public Transform ItemTransform => itemTransform;

    private InventoryItem inventoryItem;

    private Vector2 originalPosition;
    private bool originalWasEquipped;
    private EquipSlotUI originalEquipSlot;
    private InventoryPlacementSnapshot originalPlacement;
    private float cellSize;
    private float cellSpacing;
    [SerializeField, Min(0f)] private float swapMoveDuration = 0.14f;

    private Coroutine gridPositionAnimation;
    private Vector2 gridPositionAnimationTarget;

    private Transform itemTransform;
    private EquipSlotUI currentEquipSlot = null;
    public bool IsEquipped => currentEquipSlot != null;
    public InventoryGrid CurrentGrid => currentGrid;
    public InventoryItem Item => inventoryItem;
    public bool OriginalWasEquipped => originalWasEquipped;
    public EquipSlotUI OriginalEquipSlot => originalEquipSlot;
    public InventoryPlacementSnapshot OriginalPlacement => originalPlacement;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Image rarityBackground;
    [SerializeField] private Image rarityFrame;
    [SerializeField] private Image hoverGlow;
    
    private void Awake()
    {
        EnsureUIReferences();
    }

    public void Setup(InventoryItem item, InventoryGrid grid)
    {
        EnsureUIReferences();

        if (rect == null || itemIcon == null || itemTransform == null)
        {
            Debug.LogError(
                "[ItemUI] UI 표시를 위한 RectTransform 또는 아이콘 Image를 찾지 못했습니다.",
                this);
            return;
        }

        inventoryItem = item;
        currentGrid = grid;
        cellSize = grid.CellSize;
        cellSpacing = grid.CellSpacing;

        float step = grid.Step;

        // 가장 중요한 세팅: 피벗과 앵커를 좌측 상단(0, 1)으로 강제 고정합니다.
        rect.pivot = new Vector2(0, 1);
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);

        // 크기 계산: (칸 수 * 슬롯 크기) + (칸 수 - 1) * 여백
        float itemWidth = (item.CurrentWidth * cellSize) + ((item.CurrentWidth - 1) * cellSpacing);
        float itemHeight = (item.CurrentHeight * cellSize) + ((item.CurrentHeight - 1) * cellSpacing);
        rect.sizeDelta = new Vector2(itemWidth, itemHeight);

        // 위치 계산: step(73)을 곱해줍니다.
        rect.anchoredPosition = new Vector2(item.x * step, -item.y * step);

        itemIcon.sprite = inventoryItem.itemData.definition.icon;
        ApplyRarityVisuals(inventoryItem.itemData.definition.rarity);
        itemTransform.localRotation = Quaternion.Euler(0, 0, inventoryItem.isRotated ? 90f : 0f);
        RestoreGridSettings();
    }

    private void EnsureUIReferences()
    {
        if (rect == null)
            rect = GetComponent<RectTransform>();

        if (equipmentHandler == null)
            equipmentHandler = GetComponent<ItemEquipHandler>();

        if (itemIcon == null)
            itemIcon = transform.Find("IconImage")?.GetComponent<Image>();

        if (rarityBackground == null)
            rarityBackground = transform.Find("RarityBackground")?.GetComponent<Image>();

        if (rarityFrame == null)
            rarityFrame = transform.Find("RarityFrame")?.GetComponent<Image>();

        if (hoverGlow == null)
            hoverGlow = transform.Find("HoverInnerGlow")?.GetComponent<Image>();

        if (itemTransform == null && itemIcon != null)
            itemTransform = itemIcon.transform;
    }

    /// <summary>
    /// 공용 등급 색상표를 아이템 점유 면적 전체에 불투명하게 적용한다.
    /// 배경 RectTransform은 아이템 루트에 Stretch되어 다칸·회전 크기를 자동으로 따라간다.
    /// </summary>
    private void ApplyRarityVisuals(ItemRarity rarity)
    {
        if (rarityBackground == null && rarityFrame == null && hoverGlow == null)
            return;

        if (!ItemDisplayNames.GradeColorHex.TryGetValue(rarity, out string colorHex) ||
            !ColorUtility.TryParseHtmlString(colorHex, out Color gradeColor))
        {
            gradeColor = Color.white;
        }

        gradeColor.a = 1f;
        Color frameColor = gradeColor;
        frameColor.a = 0.5f;
        Color hoverColor = gradeColor;
        hoverColor.a = 0.42f;

        if (rarityBackground != null)
            rarityBackground.color = gradeColor;

        if (rarityFrame != null)
            rarityFrame.color = frameColor;

        if (hoverGlow != null)
            hoverGlow.color = hoverColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (ShopController.Instance != null && ShopController.Instance.TryHandleRightClick(this))
            return;

        equipmentHandler.TryHandleRightClick();
    }
    public void RestoreGridSettings()
    {
        rect.pivot = new Vector2(0, 1);
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);

        // 장비 칸에서만 사용한 비율 유지 설정이 인벤토리로 돌아온 뒤 남지 않게 원래 표시 방식으로 되돌립니다.
        if (itemIcon != null)
            itemIcon.preserveAspect = false;

        UpdateRotationUI();
    }

    void UpdateRotationUI()
    {
        const float inventoryIconFill = 0.9f;
        float itemWidth = (inventoryItem.CurrentWidth * cellSize) + ((inventoryItem.CurrentWidth - 1) * cellSpacing);
        float itemHeight = (inventoryItem.CurrentHeight * cellSize) + ((inventoryItem.CurrentHeight - 1) * cellSpacing);
        rect.sizeDelta = new Vector2(itemWidth, itemHeight);

        if (inventoryItem.isRotated)
        {
            itemTransform.localRotation = Quaternion.Euler(0, 0, -90f);
            (itemTransform as RectTransform).sizeDelta = new Vector2(
                itemHeight * inventoryIconFill,
                itemWidth * inventoryIconFill);
        }
        else
        {
            itemTransform.localRotation = Quaternion.Euler(0, 0, 0);
            (itemTransform as RectTransform).sizeDelta = new Vector2(
                itemWidth * inventoryIconFill,
                itemHeight * inventoryIconFill);
        }
    }

    public bool TryReturnToOriginalPosition()
    {
        if (originalGrid == null || !originalPlacement.IsValid)
            return false;

        if (inventoryItem.isRotated != originalPlacement.IsRotated)
        {
            inventoryItem.isRotated = originalPlacement.IsRotated;
            UpdateRotationUI();
        }

        if (!originalGrid.TryPlaceItem(
                inventoryItem,
                originalPlacement.Rect.X,
                originalPlacement.Rect.Y))
        {
            return false;
        }

        currentGrid = originalGrid;
        transform.SetParent(originalGrid.ItemsContainer, false);
        transform.SetAsLastSibling();
        rect.anchoredPosition = originalPosition;
        return true;
    }

    public void SetGridPosition(InventoryGrid grid, int x, int y)
    {
        StopGridPositionAnimation(false);
        PrepareGridPosition(grid);

        float step = grid.Step;
        rect.anchoredPosition = new Vector2(x * step, -y * step);
    }

    public void SetGridPositionAnimated(InventoryGrid grid, int x, int y)
    {
        StopGridPositionAnimation(false);
        PrepareGridPosition(grid);

        Vector2 startPosition = rect.anchoredPosition;
        float step = grid.Step;
        Vector2 targetPosition = new Vector2(x * step, -y * step);

        if (!isActiveAndEnabled ||
            swapMoveDuration <= 0f ||
            startPosition == targetPosition)
        {
            rect.anchoredPosition = targetPosition;
            return;
        }

        gridPositionAnimationTarget = targetPosition;
        gridPositionAnimation = StartCoroutine(
            AnimateGridPosition(
                startPosition,
                targetPosition,
                swapMoveDuration));
    }

    /// <summary>
    /// Grid 모델 좌표·회전과 현재 UI 또는 진행 중인 이동 목표가 일치하는지 확인한다.
    /// 복구 완료 판단이 정상적인 스왑 애니메이션을 중단하지 않도록 사용한다.
    /// </summary>
    public bool IsGridPlacementVisualized(InventoryGrid grid)
    {
        if (grid == null ||
            inventoryItem == null ||
            currentGrid != grid ||
            currentEquipSlot != null ||
            transform.parent != grid.ItemsContainer)
        {
            return false;
        }

        Vector2 expectedPosition = new Vector2(
            inventoryItem.x * grid.Step,
            -inventoryItem.y * grid.Step);

        bool positionMatches = gridPositionAnimation != null
            ? (gridPositionAnimationTarget - expectedPosition).sqrMagnitude < 0.01f
            : (rect.anchoredPosition - expectedPosition).sqrMagnitude < 0.01f;

        Quaternion expectedRotation = inventoryItem.isRotated
            ? Quaternion.Euler(0f, 0f, -90f)
            : Quaternion.identity;

        bool rotationMatches =
            Quaternion.Angle(itemTransform.localRotation, expectedRotation) < 0.1f;

        return positionMatches && rotationMatches;
    }

    private void PrepareGridPosition(InventoryGrid grid)
    {
        currentGrid = grid;
        cellSize = grid.CellSize;
        cellSpacing = grid.CellSpacing;

        transform.SetParent(grid.ItemsContainer, false);
        transform.SetAsLastSibling();


        RestoreGridSettings();
        currentEquipSlot = null;
    }

    private IEnumerator AnimateGridPosition(
        Vector2 startPosition,
        Vector2 targetPosition,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition = Vector2.LerpUnclamped(
                startPosition,
                targetPosition,
                easedT);
            yield return null;
        }

        rect.anchoredPosition = targetPosition;
        gridPositionAnimation = null;
    }

    private void StopGridPositionAnimation(bool snapToTarget)
    {
        if (gridPositionAnimation == null)
            return;

        StopCoroutine(gridPositionAnimation);
        gridPositionAnimation = null;

        if (snapToTarget)
            rect.anchoredPosition = gridPositionAnimationTarget;
    }

    public Vector2Int GetCellFromItemRect(InventoryGrid grid)
    {
        // rect.position은 Pivot이 (0, 1)이므로 아이템의 좌측 상단 월드 좌표입니다.
        // 이를 타겟 그리드 컨테이너의 로컬 좌표(grid.ItemsContainer)로 변환합니다.
        Vector3 localPos = grid.ItemsContainer.InverseTransformPoint(rect.position);

        // 반올림을 사용하여 아이템이 걸쳐 있는 가장 가까운 칸으로 스냅되게 합니다.
        int x = Mathf.RoundToInt(localPos.x / grid.Step);
        int y = Mathf.RoundToInt(-localPos.y / grid.Step);

        return new Vector2Int(x, y);
    }

    public Vector2Int GetCellFromScreenPoint(
        InventoryGrid grid,
        Vector2 screenPosition,
        Camera eventCamera)
    {
        if (grid == null || grid.ItemsContainer == null)
            return new Vector2Int(int.MinValue, int.MinValue);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                grid.ItemsContainer,
                screenPosition,
                eventCamera,
                out Vector2 localPoint))
        {
            return new Vector2Int(int.MinValue, int.MinValue);
        }

        int x = Mathf.FloorToInt(localPoint.x / grid.Step);
        int y = Mathf.FloorToInt(-localPoint.y / grid.Step);
        return new Vector2Int(x, y);
    }

    public void SetCurrentEquipSlot(EquipSlotUI slot)
    {
        currentEquipSlot = slot;
    }

    public void ClearCurrentEquipSlot()
    {
        currentEquipSlot = null;
    }

    public bool TryFindUnequipSpace(
    out int foundX,
    out int foundY,
    out bool targetRotated)
    {
        foundX = -1;
        foundY = -1;
        targetRotated = false;

        if (currentGrid == null || inventoryItem == null)
            return false;

        // 장비 교체와 동일한 공용 규칙으로 현재 방향을 먼저 찾고,
        // 자리가 없을 때만 회전 방향을 확인한다.
        if (!currentGrid.TryFindEmptySpaceForItem(
                inventoryItem,
                inventoryItem.isRotated,
                out InventoryPlacementSnapshot placement))
        {
            return false;
        }

        foundX = placement.Rect.X;
        foundY = placement.Rect.Y;
        targetRotated = placement.IsRotated;
        return true;
    }

    /// <summary>
    /// 장착 시 인벤토리 칸에서 사용하던 회전 데이터를 초기화하고 장착 슬롯용 아이콘 방향을 적용한다.
    /// 파이터 무기는 정방향, 가로로 긴 거너 무기는 +90도로 세워 표시한다.
    /// 이 회전은 아이콘에만 적용되며 실제 무기의 장착 방향이나 인벤토리 점유 칸은 바꾸지 않는다.
    /// </summary>
    public void ResetRotationForEquipSlot(Vector2 slotSize)
    {
        if (inventoryItem == null)
            return;

        inventoryItem.isRotated = false;

        bool isGunnerWeapon =
            inventoryItem.itemData != null &&
            inventoryItem.itemData.definition != null &&
            inventoryItem.itemData.definition.category == ItemCategory.Weapon &&
            inventoryItem.itemData.definition.characterClass == CharacterClass.Gunner;

        if (itemTransform != null)
        {
            // 거너 무기 아이콘은 가로가 길어 정방향으로 정사각 장착 슬롯에 넣으면 폭이 눌려 보입니다.
            // 장착 슬롯에서만 +90도로 세우고, 다시 인벤토리로 돌아가면 RestoreGridSettings가 원래 방향을 복구합니다.
            itemTransform.localRotation = Quaternion.Euler(
                0f,
                0f,
                isGunnerWeapon ? 90f : 0f);
        }

        if (itemIcon != null)
        {
            // 회전 뒤에도 원본 아이콘의 가로세로 비율을 유지해 정사각 슬롯 크기에 억지로 늘어나지 않게 합니다.
            itemIcon.preserveAspect = true;
        }

        if (rect != null)
        {
            rect.sizeDelta = slotSize;
        }

        if (itemTransform is RectTransform iconRect)
        {
            // 무기 장착 칸은 세로로 긴 180×360 크기입니다.
            // 가로로 긴 거너 아이콘을 90도 돌릴 때 그림 영역까지 같은 180×360으로 두면,
            // 회전 전의 짧은 180 길이만 사용해서 장착 칸 위아래에 큰 여백이 생깁니다.
            // 거너 무기만 그림 영역의 가로세로를 먼저 360×180으로 바꾸면,
            // 회전한 뒤에는 장착 칸의 긴 세로 길이를 자연스럽게 채웁니다.
            iconRect.sizeDelta = isGunnerWeapon
                ? new Vector2(slotSize.y, slotSize.x)
                : slotSize;
        }
    }

    public void RotateDraggingItem()
    {
        inventoryItem.isRotated = !inventoryItem.isRotated;
        UpdateRotationUI();
    }
    public void SaveOriginalState()
    {
        StopGridPositionAnimation(true);
        originalPosition = rect.anchoredPosition;
        originalWasEquipped = IsEquipped;
        originalEquipSlot = currentEquipSlot;
        originalGrid = currentGrid;
        originalPlacement = InventoryPlacementSnapshot.Capture(
            currentGrid,
            inventoryItem);
    }

    public bool TryDetachFromCurrentSlotOrGrid()
    {
        if (inventoryItem == null)
            return false;

        if (IsEquipped)
        {
            if (currentEquipSlot == null)
                return false;

            currentEquipSlot.ClearItemUI();
            return true;
        }

        return currentGrid != null &&
               currentGrid.TryRemoveItem(inventoryItem);
    }

    public void MoveByDelta(Vector2 delta)
    {
        rect.anchoredPosition += delta;
    }

    public void SetParentToCurrentGrid(bool worldPositionStays)
    {
        transform.SetParent(currentGrid.ItemsContainer, worldPositionStays);
    }

    public Vector2 ScreenToCurrentGridLocalPoint(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            currentGrid.ItemsContainer as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        return localPoint;
    }
    public void SetAnchoredPosition(Vector2 position)
    {
        rect.anchoredPosition = position;
    }
}

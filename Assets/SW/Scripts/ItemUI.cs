using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Canvas itemCanvas;
    private bool originalOverrideSorting;
    private int originalSortingOrder;

    private InventoryGrid currentGrid;
    private InventoryGrid originalGrid;
    private RectTransform rect;
    private CanvasGroup canvasGroup;

    private InventoryItem inventoryItem;

    private Vector2 originalPosition;
    private int originalX;
    private int originalY;
    private bool originalRotated;
    private Vector2 currentScreenPosition;
    private float cellSize;
    private float cellSpacing;

    private Image itemIcon;
    private Transform itemTransform;
    private bool isDragging = false;
    
    public EquipSlotUI currentEquipSlot = null;
    public bool IsEquipped => currentEquipSlot != null;
    public InventoryGrid CurrentGrid => currentGrid;
    public InventoryItem Item => inventoryItem;


    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        itemCanvas = GetComponent<Canvas>();

        if (itemCanvas == null)
        {
            itemCanvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
        }
            

        originalOverrideSorting = itemCanvas.overrideSorting;
        originalSortingOrder = itemCanvas.sortingOrder;
    }

    private void RaiseForDrag()
    {
        itemCanvas.overrideSorting = true;
        itemCanvas.sortingOrder = 1000;
        transform.SetAsLastSibling();
    }

    private void RestoreSorting()
    {
        itemCanvas.overrideSorting = originalOverrideSorting;
        itemCanvas.sortingOrder = originalSortingOrder;
    }
    public void Setup(InventoryItem item, InventoryGrid grid)
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

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

        itemIcon = transform.GetChild(0).GetComponent<Image>();
        itemTransform = itemIcon.transform;
        itemIcon.color = Color.white;
        itemIcon.sprite = inventoryItem.itemData.itemIcon;
        itemTransform.localRotation = Quaternion.Euler(0, 0, inventoryItem.isRotated ? 90f : 0f);
        RestoreGridSettings();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("마우스가 아이템에 닿았습니다!");
        // 드래그 중이 아닐 때만 툴팁을 켭니다.
        if (!isDragging)
        {
            TooltipManager.Instance.ShowTooltip(inventoryItem.itemData);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.Instance.HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (ShopController.Instance != null && ShopController.Instance.TryRightClick(this))
            {
                return;
            }
            if (IsEquipped)
                UnEquip();
            else
                Equip();              
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        currentScreenPosition = eventData.position;
        isDragging = true;
        TooltipManager.Instance.HideTooltip();
        bool wasEquipped = IsEquipped;
        itemIcon.color = new Color(1f, 1f, 1f, 0.8f);

        canvasGroup.blocksRaycasts = false;

        originalPosition = rect.anchoredPosition;
        originalX = inventoryItem.x;
        originalY = inventoryItem.y;
        originalRotated = inventoryItem.isRotated;
        originalGrid = currentGrid;

        if (wasEquipped)
            currentEquipSlot.equipItemUI = null;
        else
            currentGrid.RemoveItem(inventoryItem);

        transform.SetParent(currentGrid.ItemsContainer, true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
        currentGrid.ItemsContainer as RectTransform,
        eventData.position,
        eventData.pressEventCamera,
        out Vector2 localPoint);

        if (wasEquipped)
        {
            RestoreGridSettings();
            // 피벗이 좌측 상단이므로, 마우스 위치에서 크기의 절반만큼 올려줘야 마우스가 아이템 중앙에 옵니다.
            rect.anchoredPosition = localPoint + new Vector2(-rect.sizeDelta.x / 2f, rect.sizeDelta.y / 2f);
        }

        RaiseForDrag();

        if (ShopController.Instance != null && CurrentGrid == ShopController.Instance.ShopGrid)
            ShopController.Instance.ShowHighlight(inventoryItem.CurrentWidth, inventoryItem.CurrentHeight, cellSize, cellSpacing);
        else
            InventoryController.Instance.ShowHighlight(inventoryItem.CurrentWidth, inventoryItem.CurrentHeight, cellSize, cellSpacing);
    }

    public void OnDrag(PointerEventData eventData)
    {
        currentScreenPosition = eventData.position;
        rect.anchoredPosition += eventData.delta;
        RefreshHighlight();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Vector2Int targetCell = GetCellFromItemRect(currentGrid);
        int targetX = targetCell.x;
        int targetY = targetCell.y;

        float step = currentGrid.Step;
        //float step = cellSize + cellSpacing;
        //int targetX = Mathf.RoundToInt(rect.anchoredPosition.x / step);
        //int targetY = Mathf.RoundToInt(-rect.anchoredPosition.y / step);
        
        isDragging = false;
        canvasGroup.blocksRaycasts = true;
        itemIcon.color = Color.white;

        if (ShopController.Instance != null)
            ShopController.Instance.HideHighlight();

        InventoryController.Instance.HideHighlight();

        RestoreSorting();

        EquipSlotUI targetEquipSlot = InventoryController.Instance.hoveredEquipSlot;

        if (ShopController.Instance != null && ShopController.Instance.TradeItem(this, originalGrid))
        {
            return;
        }

        if (targetEquipSlot != null && targetEquipSlot.CanAccept(inventoryItem.itemData))
        {
            EquipDirectly(targetEquipSlot);
        }
        else
        {
                
            bool canPlace = currentGrid.CanPlaceItem(targetX, targetY, inventoryItem.CurrentWidth, inventoryItem.CurrentHeight);

            if (canPlace)
            {
                currentGrid.PlaceItem(inventoryItem, targetX, targetY);
                rect.anchoredPosition = new Vector2(targetX * step, -targetY * step);
                currentEquipSlot = null;
            }
            else
            {
                if (IsEquipped)
                    EquipDirectly(currentEquipSlot);
                else
                {
                    if (inventoryItem.isRotated != originalRotated)
                    {
                        inventoryItem.isRotated = originalRotated;
                        UpdateRotationUI();
                    }

                    canPlace = currentGrid.CanPlaceItem(originalX, originalY, inventoryItem.CurrentWidth, inventoryItem.CurrentHeight);

                    if (canPlace)
                    {
                        currentGrid.PlaceItem(inventoryItem, originalX, originalY);
                        rect.anchoredPosition = originalPosition;
                    }
                    else
                    {
                        if (currentGrid.FindEmptySpace(inventoryItem.CurrentWidth, inventoryItem.CurrentHeight, out int foundX, out int foundY))
                        {
                            currentGrid.PlaceItem(inventoryItem, foundX, foundY);
                            rect.anchoredPosition = new Vector2(foundX * step, -foundY * step);
                        }
                        else
                        {
                            InventoryController.Instance.PrintLog("드래그 도중 인벤토리가 가득 차 넣을 수 없어서 파괴되었습니다.");
                            Destroy(gameObject);
                        }
                    }
                }
            }
        }
    }

    private void EquipDirectly(EquipSlotUI slot)
    {
        slot.equipItemUI = this;
        currentEquipSlot = slot;

        if (inventoryItem.isRotated)
        {
            inventoryItem.isRotated = false;
            itemTransform.localRotation = Quaternion.Euler(0, 0, 0);
            rect.sizeDelta = new Vector2(inventoryItem.CurrentWidth * cellSize, inventoryItem.CurrentHeight * cellSize);
        }

        transform.SetParent(slot.transform);
        transform.position = slot.transform.position;
        RectTransform slotRect = slot.transform as RectTransform;
        rect.sizeDelta = slotRect.sizeDelta;
        (itemTransform as RectTransform).sizeDelta = slotRect.sizeDelta;
    }

    private void Equip()
    {
        foreach (EquipSlotUI slot in InventoryController.Instance.allEquipSlots)
        {
            if (slot.CanAccept(inventoryItem.itemData))
            {
                currentGrid.RemoveItem(inventoryItem);
                EquipDirectly(slot);
                return;
            }
        }
        InventoryController.Instance.PrintLog("장착할 수 있는 슬롯이 없거나 꽉 찼습니다!");
    }

    private void UnEquip()
    {
        if (currentGrid.FindEmptySpace(inventoryItem.CurrentWidth, inventoryItem.CurrentHeight, out int foundX, out int foundY))
        {
            currentEquipSlot.equipItemUI = null;
            currentEquipSlot = null;

            transform.SetParent(currentGrid.ItemsContainer);
            currentGrid.PlaceItem(inventoryItem, foundX, foundY);

            float step = cellSize + cellSpacing;
            rect.anchoredPosition = new Vector2(foundX * step, -foundY * step);

            RestoreGridSettings();
        }
        else if(currentGrid.FindEmptySpace(inventoryItem.CurrentHeight, inventoryItem.CurrentWidth, out foundX, out foundY))
        {
            inventoryItem.isRotated = !inventoryItem.isRotated;
            currentEquipSlot.equipItemUI = null;
            currentEquipSlot = null;

            transform.SetParent(currentGrid.ItemsContainer);
            currentGrid.PlaceItem(inventoryItem, foundX, foundY);

            float step = cellSize + cellSpacing;
            rect.anchoredPosition = new Vector2(foundX * step, -foundY * step);

            RestoreGridSettings();
            InventoryController.Instance.PrintLog("자리가 부족해 아이템을 회전하여 보관했습니다.");
        }
        else
        {
            InventoryController.Instance.PrintLog("인벤토리가 꽉 찼습니다!");
        }
            
    }

    private void Update()
    {
        if (isDragging && Keyboard.current.rKey.wasPressedThisFrame)
        {
            inventoryItem.isRotated = !inventoryItem.isRotated;

            UpdateRotationUI();
            RefreshHighlight();
        }
    }

    void RefreshHighlight()
    {
        if (ShopController.Instance != null &&
        ShopController.Instance.IsTradingToShop(originalGrid, this))
        {
            InventoryController.Instance.SetHighlightActive(false);

            
            InventoryGrid shopGrid = ShopController.Instance.ShopGrid;
            Vector2Int cell = GetCellFromItemRect(shopGrid);

            bool canPlace = shopGrid.CanPlaceItem(
                cell.x,
                cell.y,
                inventoryItem.CurrentWidth,
                inventoryItem.CurrentHeight
            );

            ShopController.Instance.ShowHighlight(
        inventoryItem.CurrentWidth,
        inventoryItem.CurrentHeight,
        shopGrid.CellSize,
        shopGrid.CellSpacing
    );
            ShopController.Instance.MoveHighlight(
                cell.x,
                cell.y,
                canPlace,
                shopGrid.CellSize,
                shopGrid.CellSpacing
            );

            return;
        }

        if (ShopController.Instance != null &&
        ShopController.Instance.IsTradingToPlayer(originalGrid, this))
        {
            ShopController.Instance.SetHighlightActive(false);

            
            InventoryGrid playerGrid = ShopController.Instance.PlayerGrid;
            Vector2Int cell = GetCellFromItemRect(playerGrid);

            bool canPlace = playerGrid.CanPlaceItem(
                cell.x,
                cell.y,
                inventoryItem.CurrentWidth,
                inventoryItem.CurrentHeight
            );

            InventoryController.Instance.ShowHighlight(
        inventoryItem.CurrentWidth,
        inventoryItem.CurrentHeight,
        playerGrid.CellSize,
        playerGrid.CellSpacing
    );
            InventoryController.Instance.MoveHighlight(
                cell.x,
                cell.y,
                canPlace,
                playerGrid.CellSize,
                playerGrid.CellSpacing
            );

            return;
        }

        if (ShopController.Instance != null)
        {
            ShopController.Instance.SetHighlightActive(false);
        }
        Vector2Int targetCell = GetCellFromItemRect(currentGrid);
        int targetX = targetCell.x;
        int targetY = targetCell.y;


        if (targetX + inventoryItem.CurrentWidth <= 0 ||  
        targetX >= currentGrid.GridWidth ||
        targetY + inventoryItem.CurrentHeight <= 0 ||
        targetY >= currentGrid.GridHeight)
        {
            if (ShopController.Instance != null && CurrentGrid == ShopController.Instance.ShopGrid)
                ShopController.Instance.SetHighlightActive(false);
            else
                InventoryController.Instance.SetHighlightActive(false);
        }
        else
        {
            if(ShopController.Instance != null && CurrentGrid == ShopController.Instance.ShopGrid)
            {
                ShopController.Instance.SetHighlightActive(true);
                bool canPlace = currentGrid.CanPlaceItem(targetX, targetY, inventoryItem.CurrentWidth, inventoryItem.CurrentHeight);

                ShopController.Instance.MoveHighlight(targetX, targetY, canPlace, cellSize, cellSpacing);
            }
            else
            {
                InventoryController.Instance.SetHighlightActive(true);
                bool canPlace = currentGrid.CanPlaceItem(targetX, targetY, inventoryItem.CurrentWidth, inventoryItem.CurrentHeight);

                InventoryController.Instance.MoveHighlight(targetX, targetY, canPlace, cellSize, cellSpacing);
            }
                
        }
    }

    private void RestoreGridSettings()
    {
        rect.pivot = new Vector2(0, 1);
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);

        UpdateRotationUI();
    }

    void UpdateRotationUI()
    {
        float itemWidth = (inventoryItem.CurrentWidth * cellSize) + ((inventoryItem.CurrentWidth - 1) * cellSpacing);
        float itemHeight = (inventoryItem.CurrentHeight * cellSize) + ((inventoryItem.CurrentHeight - 1) * cellSpacing);
        rect.sizeDelta = new Vector2(itemWidth, itemHeight);

        if (inventoryItem.isRotated)
        {
            itemTransform.localRotation = Quaternion.Euler(0, 0, -90f);
            (itemTransform as RectTransform).sizeDelta = new Vector2(itemHeight, itemWidth);
        }
        else
        {
            itemTransform.localRotation = Quaternion.Euler(0, 0, 0);
            (itemTransform as RectTransform).sizeDelta = new Vector2(itemWidth, itemHeight);
        }
    }

    public void ReturnToOriginalPosition()
    {
        if (inventoryItem.isRotated != originalRotated)
        {
            inventoryItem.isRotated = originalRotated;
            UpdateRotationUI();
        }


        currentGrid = originalGrid;
        transform.SetParent(currentGrid.ItemsContainer, false);
        transform.SetAsLastSibling();
        currentGrid.PlaceItem(inventoryItem, originalX, originalY);
        rect.anchoredPosition = originalPosition;
    }

    public void SetGridPosition(InventoryGrid grid, int x, int y)
    {
        currentGrid = grid;
        cellSize = grid.CellSize;
        cellSpacing = grid.CellSpacing;

        transform.SetParent(grid.ItemsContainer, false);
        transform.SetAsLastSibling();
        float step = grid.Step;
        rect.anchoredPosition = new Vector2(x * step, -y * step);

        currentEquipSlot = null;
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

}




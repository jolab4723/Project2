using UnityEngine;

[DisallowMultipleComponent]
public sealed class InventoryItemUISpawner : MonoBehaviour
{
    [SerializeField] private GameObject itemUIPrefab;
    [SerializeField] private WorldItemDropService worldItemDropService;

    private InventoryController inventoryController;
    private InventoryGrid playerGrid;
    private InventoryView inventoryView;
    private bool subscribed;

    private void Awake()
    {
        if (worldItemDropService == null)
            worldItemDropService = GetComponentInParent<WorldItemDropService>();

        // 기존 싱글플레이 Prefab은 Controller와 Spawner가 같은 오브젝트에 있다.
        InventoryController localOwner = GetComponent<InventoryController>();
        if (localOwner != null)
        {
            inventoryController = localOwner;
            playerGrid = localOwner.PlayerGrid;
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool Bind(InventoryController owner, InventoryView view)
    {
        Unbind(inventoryController);

        inventoryController = owner;
        inventoryView = view;
        playerGrid = owner != null ? owner.PlayerGrid : null;

        if (inventoryController == null ||
            inventoryView == null ||
            playerGrid == null ||
            !playerGrid.HasView)
        {
            Debug.LogError(
                "[InventoryItemUISpawner] owner와 InventoryView가 완전히 Bind되지 않았습니다.",
                this);
            return false;
        }

        Subscribe();
        RebuildPlayerItems();
        return true;
    }

    public void Unbind(InventoryController owner)
    {
        if (owner != null && inventoryController != owner)
            return;

        Unsubscribe();
        ClearPlayerItemViews();
        inventoryController = null;
        inventoryView = null;
        playerGrid = null;
    }

    private void Subscribe()
    {
        if (!isActiveAndEnabled || subscribed || inventoryController == null)
            return;

        inventoryController.OnItemAdded += HandleItemAdded;
        inventoryController.OnItemRemoved += HandleItemRemoved;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || inventoryController == null)
            return;

        inventoryController.OnItemAdded -= HandleItemAdded;
        inventoryController.OnItemRemoved -= HandleItemRemoved;
        subscribed = false;
    }

    /// <summary>모델을 변경하지 않고 현재 플레이어 그리드의 아이템 화면을 다시 만든다.</summary>
    public void RebuildPlayerItems()
    {
        ClearPlayerItemViews();

        foreach (InventoryItem item in inventoryController.GetAllInventoryItems())
            HandleItemAdded(item);
    }

    private void ClearPlayerItemViews()
    {
        if (playerGrid?.ItemsContainer == null)
            return;

        ItemUI[] itemViews =
            playerGrid.ItemsContainer.GetComponentsInChildren<ItemUI>(true);

        foreach (ItemUI itemView in itemViews)
            Destroy(itemView.gameObject);
    }

    private void HandleItemAdded(InventoryItem item)
    {
        if (SpawnItemUIAndGet(item) != null)
            return;

        string itemName = item?.itemData?.definition != null
                ? item.itemData.definition.itemName
                : "알 수 없는 아이템";

        Debug.LogError(
            $"[InventoryItemUISpawner] {itemName}의 " +
            "모델 추가는 성공했지만 Item UI 생성에 실패했습니다.");
    }

    private void HandleItemRemoved(InventoryItem item)
    {
        ItemUI itemUI = ItemUIFinder.FindInGrid(playerGrid, item);

        if (itemUI == null)
            return;

        TooltipManager.Instance?.HideTooltip();
        Destroy(itemUI.gameObject);
    }

    public ItemUI SpawnItemUIAndGet(InventoryItem item)
    {
        return SpawnItemUIAndGet(item, playerGrid);
    }

    public ItemUI SpawnItemUIAndGet(InventoryItem item, InventoryGrid targetGrid)
    {
        if (item?.itemData?.definition == null)
        {
            Debug.LogWarning("[InventoryItemUISpawner] 유효하지 않은 InventoryItem입니다.");
            return null;
        }

        if (targetGrid == null || targetGrid.ItemsContainer == null)
        {
            Debug.LogWarning("[InventoryItemUISpawner] 대상 Grid 또는 ItemsContainer가 없습니다.");
            return null;
        }

        if (itemUIPrefab == null)
        {
            Debug.LogWarning("[InventoryItemUISpawner] itemUIPrefab이 연결되지 않았습니다.");
            return null;
        }

        GameObject newObject = Instantiate(itemUIPrefab, targetGrid.ItemsContainer);

        ItemUI itemUI = newObject.GetComponent<ItemUI>();

        if (itemUI == null)
        {
            Debug.LogWarning("[InventoryItemUISpawner] itemUIPrefab에 ItemUI가 없습니다.");

            Destroy(newObject);
            return null;
        }

        ItemDropHandler dropHandler = newObject.GetComponent<ItemDropHandler>();
        ItemEquipHandler equipHandler = newObject.GetComponent<ItemEquipHandler>();
        EquipSlotUI[] equipmentSlots = inventoryView != null
            ? inventoryView.EquipmentSlots
            : inventoryController?.allEquipSlots;

        if (dropHandler != null)
            dropHandler.Bind(
                worldItemDropService,
                inventoryController,
                equipmentSlots);

        if (equipHandler != null)
            equipHandler.Bind(
                inventoryController,
                equipmentSlots);

        itemUI.Setup(item, targetGrid);

        return itemUI;
    }
}

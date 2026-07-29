using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(InventoryController))]

public sealed class InventoryItemUISpawner : MonoBehaviour
{
    [SerializeField] private GameObject itemUIPrefab;
    [SerializeField] private WorldItemDropService worldItemDropService;

    private InventoryController inventoryController;
    private InventoryGrid playerGrid;

    private void Awake()
    {
        inventoryController = GetComponent<InventoryController>();
        if (worldItemDropService == null)
        {
            worldItemDropService = GetComponentInParent<WorldItemDropService>();
        }

        playerGrid = inventoryController != null ? inventoryController.PlayerGrid : null;

        if (inventoryController == null ||  playerGrid == null)
        {
            Debug.LogError(
                "[InventoryItemUISpawner] " +
                "InventoryController 또는 PlayerGrid를 찾지 못했습니다.");
        }
    }

    private void OnEnable()
    {
        if (inventoryController == null)
            return;

        inventoryController.OnItemAdded += HandleItemAdded;
        inventoryController.OnItemRemoved += HandleItemRemoved;
    }

    private void OnDisable()
    {
        if (inventoryController == null)
            return;

        inventoryController.OnItemAdded -= HandleItemAdded;
        inventoryController.OnItemRemoved -= HandleItemRemoved;
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

        itemUI.Setup(item, targetGrid);

        ItemDropHandler dropHandler = newObject.GetComponent<ItemDropHandler>();

        if (dropHandler != null)
            dropHandler.Bind(worldItemDropService, inventoryController);

        return itemUI;
    }
}
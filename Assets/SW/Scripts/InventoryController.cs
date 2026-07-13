using ItemSystem;
using TMPro;
using UnityEngine;

public class InventoryController : MonoBehaviour, IItemReceiver
{
    public static InventoryController Instance { get; private set; }
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private InventoryGrid playerGrid;
    [SerializeField] private EquipmentSystem equipmentSystem;
    public EquipmentSystem EquipmentSystem => equipmentSystem;
    public InventoryGrid PlayerGrid => playerGrid;
    public PlayerWallet PlayerWallet => playerWallet;

    public TextMeshProUGUI logText;
    public EquipSlotUI hoveredEquipSlot;
    public EquipSlotUI[] allEquipSlots;
    public GameObject itemUIPrefab;

    public TextMeshProUGUI goldText;

    void Awake()
    {
        Instance = this;
        RefreshGoldText(playerWallet.Gold);
    }
    private void OnEnable()
    {
        playerWallet.OnGoldChanged += RefreshGoldText;
        RefreshGoldText(playerWallet.Gold);
    }

    private void OnDisable()
    {
        playerWallet.OnGoldChanged -= RefreshGoldText;
    }

    public bool AddItem(ItemInstance itemData)
    {
        InventoryAddResultData result = TryAddItemData(itemData);
        
        if (result.Result != InventoryAddResult.Success)
        {
            PrintLog(GetAddItemFailMessage(result.Result));
            return false;
        }
        var data = result.Item.itemData.definition;
        if (!SpawnItemUI(result.Item))
        {
            // UI 생성이 실패했으면 그리드 데이터도 되돌려서 데이터-화면 불일치를 막는다.
            playerGrid.RemoveItem(result.Item);
            PrintLog($"{data.itemName} 아이템 UI 생성에 실패했습니다.");
            return false;
        }

        PrintLog($"{data.itemName} 아이템을 획득했습니다. 위치 : {result.X}, {result.Y}");
        return true;
    }

    /// <summary>
    /// itemUIPrefab을 생성해 화면에 표시. 성공 여부를 반환한다.
    /// </summary>
    public ItemUI SpawnItemUIAndGet(InventoryItem itemData)
    {
        if (itemUIPrefab == null)
        {
            Debug.LogWarning("[InventoryController] itemUIPrefab이 비어있습니다. 인스펙터에서 연결해주세요.");
            return null;
        }

        GameObject newObj = Instantiate(itemUIPrefab, playerGrid.ItemsContainer);
        ItemUI ui = newObj.GetComponent<ItemUI>();

        if (ui == null)
        {
            Debug.LogWarning("[InventoryController] itemUIPrefab에 ItemUI 컴포넌트가 없습니다.");
            Destroy(newObj);
            return null;
        }

        ui.Setup(itemData, playerGrid);
        return ui;
    }

    public bool SpawnItemUI(InventoryItem itemData)
    {
        return SpawnItemUIAndGet(itemData) != null;
    }

    public void PrintLog(string message)
    {
        if (logText != null)
        {
            logText.text = message;
        }
    }
    public void RefreshGoldText(int gold)
    {
        goldText.text = gold.ToString();
    }
    public InventoryAddResultData TryAddItemData(ItemInstance itemData)
    {
        if (itemData == null || itemData.definition == null)
            return InventoryAddResultData.Failed(InventoryAddResult.InvalidItem);

        var data = itemData.definition;

        if (!playerGrid.FindEmptySpace(data.itemWidth, data.itemHeight, out int x, out int y))
            return InventoryAddResultData.Failed(InventoryAddResult.NoSpace);

        InventoryItem item = new InventoryItem(itemData);
        playerGrid.TryPlaceItem(item, x, y);

        return InventoryAddResultData.Success(item, x, y);
    }

    private string GetAddItemFailMessage(InventoryAddResult result)
    {
        switch (result)
        {
            case InventoryAddResult.InvalidItem:
                return "[InventoryController] AddItem에 유효하지 않은 itemData가 전달되었습니다.";

            case InventoryAddResult.NoSpace:
                return "인벤토리가 꽉 찼습니다!";

            default:
                return "아이템 획득에 실패했습니다.";
        }
    }
}
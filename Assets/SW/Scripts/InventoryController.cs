using System.Collections.Generic;
using ItemSystem;
using TMPro;
using UnityEngine;

/// <summary>
/// !! 멀티플레이 대비: Instance는 "내 캐릭터"의 인벤토리만 가리킨다 (PlayerHealthManager와 동일 패턴).
/// </summary>
public class InventoryController : MonoBehaviour, IItemReceiver
{
    public static InventoryController Instance { get; private set; }
    public event System.Action<InventoryItem> OnItemAdded;

    /// <summary>씬에 존재하는 모든 캐릭터의 인벤토리 컨트롤러 (나 + 다른 플레이어).</summary>
    public static readonly List<InventoryController> All = new List<InventoryController>();

    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private InventoryGrid playerGrid;
    [SerializeField] private EquipmentSystem equipmentSystem;
    public EquipmentSystem EquipmentSystem => equipmentSystem;
    public InventoryGrid PlayerGrid => playerGrid;
    public PlayerWallet PlayerWallet => playerWallet;

    public TextMeshProUGUI logText;
    public EquipSlotUI hoveredEquipSlot;
    public EquipSlotUI[] allEquipSlots;

    public TextMeshProUGUI goldText;

    void Awake()
    {
        All.Add(this);

        var identity = GetComponent<Mirror.NetworkIdentity>();
        if (identity != null && !identity.isLocalPlayer)
            return;

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[InventoryController] 이미 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        RefreshGoldText(playerWallet.Gold);
    }

    private void OnDestroy()
    {
        All.Remove(this);
        if (Instance == this)
            Instance = null;
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

        string itemName = itemData?.definition?.itemName ?? "아이템";

        PrintLog(
            InventoryMessageMapper.GetMessage(
                result.Result,
                itemName,
                result.X,
                result.Y));

        return result.Result == InventoryAddResult.Success;
    }

    public void PrintLog(string message)
    {
        if (logText != null)
        {
            logText.text = message;
        }
    }

    public InventoryAddResultData TryAddItemAt(InventoryItem item, int x, int y)
    {
        if (item?.itemData?.definition == null)
        {
            return InventoryAddResultData.Failed(
                InventoryAddResult.InvalidItem);
        }

        if (playerGrid == null)
        {
            return InventoryAddResultData.Failed(
                InventoryAddResult.GridUnavailable);
        }

        if (!playerGrid.TryPlaceItem(item, x, y))
        {
            return InventoryAddResultData.Failed(
                InventoryAddResult.PlacementFailed);
        }

        InventoryAddResultData result =
            InventoryAddResultData.Success(item, x, y);

        OnItemAdded?.Invoke(item);

        return result;
    }
    public void RefreshGoldText(int gold)
    {
        goldText.text = gold.ToString();
    }
    public InventoryAddResultData TryAddItemData(ItemInstance itemData)
    {
        if (itemData == null || itemData.definition == null)
            return InventoryAddResultData.Failed(InventoryAddResult.InvalidItem);

        if (playerGrid == null)
        {
            return InventoryAddResultData.Failed(
                InventoryAddResult.GridUnavailable);
        }

        var definition = itemData.definition;

        if (!playerGrid.FindEmptySpace(
                definition.itemWidth,
                definition.itemHeight,
                out int x,
                out int y))
        {
            return InventoryAddResultData.Failed(
                InventoryAddResult.NoSpace);
        }

        InventoryItem item = new InventoryItem(itemData);

        return TryAddItemAt(item, x, y);
    }
}

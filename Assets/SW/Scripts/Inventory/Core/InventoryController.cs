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
    public event System.Action<InventoryItem> OnItemRemoved;
    public event System.Action<InventoryItem> OnItemOwnershipGained;
    public event System.Action<InventoryItem> OnItemOwnershipLost;

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

    /// <summary>
    /// 플레이어 인벤토리 그리드에 보관 중인 모든 아이템의 목록을 반환합니다.
    /// 반환된 목록을 수정해도 실제 인벤토리 배치는 변경되지 않습니다.
    /// 장착 슬롯에 있는 아이템은 포함하지 않습니다.
    /// </summary>
    public IReadOnlyList<InventoryItem> GetAllInventoryItems()
    {
        if (playerGrid == null)
            return System.Array.Empty<InventoryItem>();

        return playerGrid.GetAllItems();
    }

    /// <summary>
    /// 플레이어 인벤토리 그리드에 보관 중인 아이템을 제거합니다.
    /// 장착 중인 아이템은 이 메서드로 제거하지 않습니다.
    /// </summary>
    public InventoryDiscardResult TryRemoveInventoryItem(InventoryItem item)
    {
        if (item?.itemData?.definition == null)
            return InventoryDiscardResult.InvalidItem;

        if (playerGrid == null)
            return InventoryDiscardResult.InventoryUnavailable;

        if (!playerGrid.ContainsItem(item))
            return InventoryDiscardResult.NotPlayerInventory;

        if (!playerGrid.TryRemoveItem(item))
            return InventoryDiscardResult.RemoveFailed;

        OnItemRemoved?.Invoke(item);
        NotifyItemOwnershipLost(item);

        return InventoryDiscardResult.Success;
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
        NotifyItemOwnershipGained(item);

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

    internal void NotifyItemOwnershipGained(InventoryItem item)
    {
        if (item == null)
            return;

        OnItemOwnershipGained?.Invoke(item);
    }

    internal void NotifyItemOwnershipLost(InventoryItem item)
    {
        if (item == null)
            return;

        OnItemOwnershipLost?.Invoke(item);
    }
}

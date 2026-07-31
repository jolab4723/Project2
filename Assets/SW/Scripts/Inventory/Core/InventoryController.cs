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

    private static readonly List<InventoryController> all = new List<InventoryController>();

    /// <summary>
    /// 씬에 존재하는 인벤토리 후보 목록이며 대상 선택이나 권한을 판정하지 않는다.
    /// 특정 대상은 전달받은 Controller를 우선하고, 멀티플레이에서는 서버만 순회한다.
    /// </summary>
    public static IReadOnlyList<InventoryController> All => all;

    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private InventoryGrid playerGrid;
    [SerializeField] private EquipmentSystem equipmentSystem;
    public EquipmentSystem EquipmentSystem => equipmentSystem;
    public InventoryGrid PlayerGrid => playerGrid;
    public PlayerWallet PlayerWallet => playerWallet;

    public TextMeshProUGUI logText;
    public EquipSlotUI[] allEquipSlots;

    public TextMeshProUGUI goldText;

    void Awake()
    {
        if (!all.Contains(this))
            all.Add(this);

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
        all.Remove(this);
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
    /// 대상 플레이어의 Grid 아이템을 중복 없이 반환한다.
    /// 특수 던전은 전달받은 Controller로 호출하며 장비·상점·월드 아이템은 제외한다.
    /// </summary>
    public IReadOnlyList<InventoryItem> GetAllInventoryItems()
    {
        if (playerGrid == null)
            return System.Array.Empty<InventoryItem>();

        return playerGrid.GetAllItems();
    }

    /// <summary>
    /// 대상 플레이어의 Grid 아이템을 제거하고 UI·소유권 이벤트를 함께 발행한다.
    /// 장착 아이템 제거 정책은 별도 합의 대상이므로 이 경로에서 처리하지 않는다.
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

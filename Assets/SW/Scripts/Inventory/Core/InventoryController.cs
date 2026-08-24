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
    public event System.Action<string> OnLogMessage;

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

    // ponytail: 구형 싱글플레이 Prefab/Scene의 직렬화 참조를 보존한다.
    // 해당 자산들이 InventoryView로 전환된 뒤 함께 제거한다.
    public TextMeshProUGUI logText;
    [SerializeField] public EquipSlotUI[] allEquipSlots = System.Array.Empty<EquipSlotUI>();
    public TextMeshProUGUI goldText;

    private EquipSlotUI[] equipmentSlotsBeforeBind;
    private bool hasRuntimeEquipmentSlotBinding;

    internal void BindEquipmentSlots(EquipSlotUI[] slots)
    {
        if (!hasRuntimeEquipmentSlotBinding)
            equipmentSlotsBeforeBind = allEquipSlots;

        allEquipSlots = slots ?? System.Array.Empty<EquipSlotUI>();
        hasRuntimeEquipmentSlotBinding = true;
    }

    internal void UnbindEquipmentSlots(EquipSlotUI[] slots)
    {
        if (!hasRuntimeEquipmentSlotBinding || !ReferenceEquals(allEquipSlots, slots))
            return;

        allEquipSlots = equipmentSlotsBeforeBind;
        equipmentSlotsBeforeBind = null;
        hasRuntimeEquipmentSlotBinding = false;
    }

    void Awake()
    {
        if (!all.Contains(this))
            all.Add(this);

        var identity = GetComponentInParent<Mirror.NetworkIdentity>();
        if (identity != null && !identity.isLocalPlayer)
            return;

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[InventoryController] 이미 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        all.Remove(this);
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        if (playerWallet == null || goldText == null)
            return;

        playerWallet.OnGoldChanged += RefreshGoldText;
        RefreshGoldText(playerWallet.Gold);
    }

    private void OnDisable()
    {
        if (playerWallet != null)
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
    /// 대상 플레이어의 Grid에서 무작위 아이템을 최대 count개 제거한다.
    /// 보유 수량이 부족하거나 제거에 실패하면 실제 제거된 개수를 반환한다.
    /// </summary>
    public int RemoveRandomInventoryItems(int count)
    {
        if (count <= 0)
            return 0;

        var candidates = new List<InventoryItem>(GetAllInventoryItems());
        int removedCount = 0;

        while (removedCount < count && candidates.Count > 0)
        {
            int index = Random.Range(0, candidates.Count);
            InventoryItem item = candidates[index];
            candidates[index] = candidates[candidates.Count - 1];
            candidates.RemoveAt(candidates.Count - 1);

            if (TryRemoveInventoryItem(item) == InventoryRemoveResult.Success)
                removedCount++;
        }

        return removedCount;
    }

    /// <summary>
    /// ItemManager의 데이터베이스에서 무작위 아이템을 최대 count개 생성해 대상 플레이어의 Grid에 추가한다.
    /// 빈 공간이나 유효한 후보가 부족하면 실제 추가된 개수를 반환한다.
    /// </summary>
    public int AddRandomInventoryItems(int count)
    {
        if (count <= 0)
            return 0;

        IReadOnlyList<ItemDefinitionSO> definitions =
            Core.ItemManager.Instance?.ItemDatabase?.allItems;

        if (definitions == null)
            return 0;

        var candidates = new List<ItemDefinitionSO>(definitions.Count);
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] != null)
                candidates.Add(definitions[i]);
        }

        int addedCount = 0;
        while (addedCount < count && candidates.Count > 0)
        {
            int index = Random.Range(0, candidates.Count);
            InventoryAddResultData result = TryAddItemData(
                ItemDataCreator.CreateItemData(candidates[index]));

            if (result.Result == InventoryAddResult.Success)
            {
                addedCount++;
                continue;
            }

            candidates[index] = candidates[candidates.Count - 1];
            candidates.RemoveAt(candidates.Count - 1);
        }

        return addedCount;
    }

    /// <summary>
    /// 대상 플레이어의 Grid 아이템을 제거하고 UI·소유권 이벤트를 함께 발행한다.
    /// 장착 아이템 제거 정책은 별도 합의 대상이므로 이 경로에서 처리하지 않는다.
    /// </summary>
    public InventoryRemoveResult TryRemoveInventoryItem(InventoryItem item)
    {
        if (item?.itemData?.definition == null)
            return InventoryRemoveResult.InvalidItem;

        if (playerGrid == null)
            return InventoryRemoveResult.InventoryUnavailable;

        if (!playerGrid.ContainsItem(item))
            return InventoryRemoveResult.NotPlayerInventory;

        if (!playerGrid.TryRemoveItem(item))
            return InventoryRemoveResult.RemoveFailed;

        OnItemRemoved?.Invoke(item);
        NotifyItemOwnershipLost(item);

        return InventoryRemoveResult.Success;
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
        OnLogMessage?.Invoke(message);

        if (logText != null)
            logText.text = message;
    }

    public void RefreshGoldText(int gold)
    {
        if (goldText != null)
            goldText.text = gold.ToString();
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
            InventoryAddResultData.Success(x, y);

        OnItemAdded?.Invoke(item);
        NotifyItemOwnershipGained(item);

        return result;
    }
    /// <summary>
    /// 아이템의 기본 방향으로 빈자리를 먼저 찾고, 필요한 경우 회전한 방향까지 확인해 인벤토리에 추가한다.
    /// 실제 배치가 확정되기 전에는 아이템 상태를 바꾸지 않으며, 선택된 회전값을 적용한 뒤 추가 이벤트를 전달한다.
    /// </summary>
    public InventoryAddResultData TryAddItemData(ItemInstance itemData)
    {
        if (itemData == null || itemData.definition == null)
            return InventoryAddResultData.Failed(InventoryAddResult.InvalidItem);

        if (playerGrid == null)
        {
            return InventoryAddResultData.Failed(
                InventoryAddResult.GridUnavailable);
        }

        InventoryItem item = new InventoryItem(itemData);

        if (!playerGrid.TryFindEmptySpaceForItem(
                item,
                item.isRotated,
                out InventoryPlacementSnapshot placement))
        {
            return InventoryAddResultData.Failed(
                InventoryAddResult.NoSpace);
        }

        item.isRotated = placement.IsRotated;

        return TryAddItemAt(
            item,
            placement.Rect.X,
            placement.Rect.Y);
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

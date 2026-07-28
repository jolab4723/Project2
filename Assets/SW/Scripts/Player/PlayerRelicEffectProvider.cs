using ItemSystem;
using UnityEngine;

/// <summary>
/// 플레이어 인벤토리에 들어 있는 유물의 스탯을 합산해서
/// PlayerStatManager에 제공한다.
///
/// 유물의 실제 소유 상태는 InventoryController가 관리하며,
/// 이 컴포넌트는 인벤토리를 읽어서 StatSet으로 변환하는 역할만 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRelicEffectProvider : MonoBehaviour, IStatSetProvider
{
    [Header("References")]
    [SerializeField]
    private InventoryController inventoryController;

    [SerializeField]
    private PlayerStatManager statManager;

    private void Awake()
    {
        // 일반적으로 PlayerStatManager와 같은 플레이어 오브젝트에
        // 붙이는 것을 기준으로 한다.
        if (statManager == null)
            statManager = GetComponent<PlayerStatManager>();

        if (inventoryController == null)
        {
            Debug.LogWarning(
                "[PlayerRelicEffectProvider] InventoryController가 연결되지 않았습니다.");
        }

        if (statManager == null)
        {
            Debug.LogWarning(
                "[PlayerRelicEffectProvider] PlayerStatManager를 찾지 못했습니다.");
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

    private void Start()
    {
        // 씬 시작 시 이미 인벤토리에 들어 있는 유물도 반영한다.
        RefreshRelicEffects();
    }

    /// <summary>
    /// 현재 플레이어 인벤토리에 들어 있는 모든 유물의 스탯을 합산한다.
    ///
    /// 계산 결과를 내부에 따로 저장하지 않고, 호출 시점의 인벤토리를
    /// 다시 읽기 때문에 항상 현재 상태를 기준으로 계산한다.
    /// </summary>
    public StatSet GetStatSet()
    {
        StatSet total = StatSet.Zero;

        if (inventoryController == null)
            return total;

        InventoryGrid playerGrid =
            inventoryController.PlayerGrid;

        if (playerGrid == null)
            return total;

        foreach (InventoryItem inventoryItem in playerGrid.GetAllItems())
        {
            if (!IsRelic(inventoryItem))
                continue;

            AddRelicStats(
                ref total,
                inventoryItem.itemData);
        }

        return total;
    }

    /// <summary>
    /// 유물 소유 상태가 바뀐 뒤 최종 스탯을 다시 계산한다.
    ///
    /// 현재는 유물 획득 시 자동 호출된다.
    /// 유물 판매와 월드 드롭 이벤트는 이후 이 메서드와 연결하면 된다.
    /// </summary>
    [ContextMenu("유물 효과 재계산")]
    public void RefreshRelicEffects()
    {
        if (statManager == null)
            return;

        statManager.Recalculate();
    }

    private void HandleItemAdded(InventoryItem addedItem)
    {
        // 일반 아이템을 획득했을 때는 불필요한 스탯 재계산을 하지 않는다.
        if (!IsRelic(addedItem))
            return;

        RefreshRelicEffects();
    }

    private void HandleItemRemoved(InventoryItem removedItem)
    {
        if (!IsRelic(removedItem))
            return;

        RefreshRelicEffects();
    }
    private static bool IsRelic(InventoryItem inventoryItem)
    {
        return inventoryItem?.itemData?.definition != null &&
               inventoryItem.itemData.definition.category == ItemCategory.Relic;
    }

    /// <summary>
    /// 유물 하나의 메인 옵션과 굴려진 서브 옵션을 StatSet에 더한다.
    ///
    /// 현재 PlayerEquipManager가 장비 스탯을 계산하는 방식과
    /// 동일한 계산 방식을 사용한다.
    /// </summary>
    private static void AddRelicStats(ref StatSet total, ItemInstance relic)
    {
        if (relic?.definition == null)
            return;

        // 메인 옵션이 없는 유물 데이터도 안전하게 처리한다.
        if (relic.definition.mainOptions != null)
        {
            foreach (RolledSubStat mainOption in relic.GetEffectiveMainOptions())
            {
                StatSetMapper.AddStat(
                    ref total,
                    mainOption.statType,
                    mainOption.value);
            }
        }

        if (relic.rolledSubStats == null)
            return;

        foreach (RolledSubStat subStat in relic.rolledSubStats)
        {
            StatSetMapper.AddStat(
                ref total,
                subStat.statType,
                subStat.value);
        }
    }
}
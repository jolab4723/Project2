using ItemSystem;
using UnityEngine;

/// <summary>
/// 플레이어 인벤토리에서 유물의 소유 상태가 바뀔 때 그 유물의 고유 효과(uniqueEffect)를 적용/해제한다.
///
/// 유물은 장비처럼 별도의 메인/서브 스탯(장비 스펙)을 갖지 않는다 - 인벤토리에 보유하는 동안
/// 고유 효과가 상시 적용되는 개념이라, 장비 슬롯의 EquipmentTransaction이 장착/해제 시
/// uniqueEffect.OnEquip/OnUnequip을 부르는 것과 동일한 훅을 소유권 획득/상실 시점에 그대로 부른다.
/// (예: PassiveBuffUniqueEffectSO면 내부적으로 PlayerBuffManager.ApplyBuff/RemoveBuff가 호출되고,
///  그 버프 매니저가 알아서 PlayerStatManager.Recalculate까지 처리하므로 이 클래스는 스탯 계산에
///  전혀 관여하지 않는다.)
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRelicEffectProvider : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private InventoryController inventoryController;

    private void Awake()
    {
        if (inventoryController == null)
        {
            Debug.LogWarning(
                "[PlayerRelicEffectProvider] InventoryController가 연결되지 않았습니다.");
        }
    }

    private void OnEnable()
    {
        if (inventoryController == null)
            return;

        inventoryController.OnItemOwnershipGained += HandleOwnershipGained;
        inventoryController.OnItemOwnershipLost += HandleOwnershipLost;
    }

    private void OnDisable()
    {
        if (inventoryController == null)
            return;

        inventoryController.OnItemOwnershipGained -= HandleOwnershipGained;
        inventoryController.OnItemOwnershipLost -= HandleOwnershipLost;
    }

    private void Start()
    {
        // 씬 시작 시 이미 인벤토리에 들어 있는 유물도 효과를 적용한다.
        if (inventoryController == null)
            return;

        InventoryGrid playerGrid = inventoryController.PlayerGrid;
        if (playerGrid == null)
            return;

        foreach (InventoryItem inventoryItem in playerGrid.GetAllItems())
        {
            if (IsRelic(inventoryItem))
                inventoryItem.itemData.definition.uniqueEffect?.OnEquip(inventoryItem.itemData);
        }
    }

    private void HandleOwnershipGained(InventoryItem item)
    {
        if (!IsRelic(item))
            return;

        item.itemData.definition.uniqueEffect?.OnEquip(item.itemData);
    }

    private void HandleOwnershipLost(InventoryItem item)
    {
        if (!IsRelic(item))
            return;

        item.itemData.definition.uniqueEffect?.OnUnequip(item.itemData);
    }

    private static bool IsRelic(InventoryItem inventoryItem)
    {
        return inventoryItem?.itemData?.definition != null &&
               inventoryItem.itemData.definition.category == ItemCategory.Relic;
    }
}
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

    /// <summary>지금 구독 중인 대상. OnEnable과 Start 양쪽에서 해석을 시도해도 중복 구독되지 않게 한다.</summary>
    private InventoryController subscribed;

    private bool warnedMissingInventory;
    private bool appliedExistingRelics;

    /// <summary>
    /// 인벤토리를 확보한다. 인스펙터가 비어 있으면 InventoryController.Instance로 찾는다.
    ///
    /// !! InventoryController는 캐릭터가 아니라 씬의 공용 오브젝트에 붙어 있다. 프리팹은 씬 오브젝트
    ///    참조를 직렬화할 수 없어서, 런타임에 프리팹으로 생성되는 캐릭터(YJ_PlayerSpawner)에서는
    ///    이 값이 **항상 null**이었다. 그러면 소유권 이벤트 구독도, 시작 시 일괄 적용도 통째로 건너뛰어
    ///    **유물의 상시 고유 효과가 전혀 걸리지 않는다**(버프 아이콘도 스탯 증감도 없음).
    ///    발동형 효과는 ItemTriggerManager가 인벤토리를 직접 훑어서 이 문제와 무관했기 때문에,
    ///    "상시형만 안 되는" 증상으로 나타났다.
    /// </summary>
    private bool TryResolveInventory(string phase)
    {
        if (inventoryController == null)
            inventoryController = InventoryController.Instance;

        if (inventoryController != null)
            return true;

        if (!warnedMissingInventory)
        {
            Debug.LogWarning(
                $"[PlayerRelicEffectProvider] InventoryController를 찾지 못해 유물 상시 효과를 적용하지 않습니다. ({phase})",
                this);
            warnedMissingInventory = true;
        }

        return false;
    }

    private void Subscribe()
    {
        if (subscribed == inventoryController)
            return;

        Unsubscribe();

        subscribed = inventoryController;
        subscribed.OnItemOwnershipGained += HandleOwnershipGained;
        subscribed.OnItemOwnershipLost += HandleOwnershipLost;
    }

    private void Unsubscribe()
    {
        if (subscribed == null)
            return;

        subscribed.OnItemOwnershipGained -= HandleOwnershipGained;
        subscribed.OnItemOwnershipLost -= HandleOwnershipLost;
        subscribed = null;
    }

    private void OnEnable()
    {
        // OnEnable이 Start보다 먼저 돌아서 이 시점엔 Instance가 아직 없을 수 있다.
        // 실패해도 Start에서 다시 시도한다.
        if (TryResolveInventory("OnEnable"))
            Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        if (!TryResolveInventory("Start"))
            return;

        Subscribe();
        ApplyExistingRelics();
    }

    /// <summary>씬 시작 시 이미 인벤토리에 들어 있는 유물의 상시 효과를 적용한다.</summary>
    private void ApplyExistingRelics()
    {
        if (appliedExistingRelics)
            return;

        InventoryGrid playerGrid = inventoryController.PlayerGrid;
        if (playerGrid == null)
        {
            Debug.LogWarning(
                "[PlayerRelicEffectProvider] PlayerGrid가 없어 기존 유물의 상시 효과를 적용하지 못했습니다.",
                this);
            return;
        }

        appliedExistingRelics = true;

        int applied = 0;
        foreach (InventoryItem inventoryItem in playerGrid.GetAllItems())
        {
            if (!IsRelic(inventoryItem))
                continue;

            inventoryItem.itemData.definition.uniqueEffect?.OnEquip(inventoryItem.itemData);
            applied++;
        }

        if (applied > 0)
            Debug.Log($"[PlayerRelicEffectProvider] 인벤토리에 있던 유물 {applied}개의 상시 효과를 적용했습니다.", this);
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
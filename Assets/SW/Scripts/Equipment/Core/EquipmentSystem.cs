using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

public class EquipmentSystem : MonoBehaviour
{
    public event System.Action<EquippedItemInfo[]> OnEquipmentChanged;

    [Tooltip("저마나 투구 효과를 플레이어별로 처리합니다. 기존 효과 콜백과 중복 실행하지 않습니다.")]
    [SerializeField] private bool useLowManaHelmetEffect;

    /// <summary>
    /// 이 아이템을 플레이어별 저마나 투구 효과 처리에 맡길지 확인합니다.
    /// 현재 마나를 검사하거나 버프를 적용하지는 않습니다.
    /// </summary>
    /// <returns>설정이 켜져 있고 지원하는 투구 효과이면 true입니다.</returns>
    public bool UsesLowManaHelmetEffect(ItemInstance item)
    {
        ItemDefinitionSO definition = item?.definition;
        if (!useLowManaHelmetEffect || definition == null)
            return false;

        if (definition.category != ItemCategory.Armor ||
            definition.armorType != ArmorType.Helmet)
            return false;

        return definition.uniqueEffect is StatThresholdBuffUniqueEffectSO threshold &&
            threshold.referenceStat == StatReference.CurrentManaPercent &&
            threshold.comparisonOperator == ComparisonOperator.LessOrEqual;
    }

    private Dictionary<EquipSlotType, InventoryItem> equippedItems = new();

    /// <summary>
    /// 지금 이 인벤토리를 쓰고 있는 캐릭터(Fighter/Gunner)의 클래스. 인벤토리/장비 상태는
    /// 캐릭터와 무관하게 공용이라 이 값을 별도로 기억해야 무기 장착 시 클래스를 검증할 수 있다.
    /// PlayerWeaponVisualPresenter.OnEnable()이 자기 캐릭터가 활성화될 때마다 알려준다.
    /// 아직 아무도 알려준 적 없으면(테스트 씬 등) null이고, 그때는 클래스 검증을 건너뛴다.
    /// </summary>
    public CharacterClass? CurrentCharacterClass { get; private set; }

    public void SetActiveCharacterClass(CharacterClass characterClass)
    {
        CurrentCharacterClass = characterClass;
    }

    internal EquipResult TryEquipState(
        InventoryItem item,
        EquipSlotType slotType)
    {
        EquipResult validation = ValidateEquip(item, slotType);

        if (validation != EquipResult.Success)
            return validation;

        if (equippedItems.TryGetValue(
                slotType,
                out InventoryItem currentItem) &&
            currentItem != null)
        {
            return EquipResult.SlotOccupied;
        }

        // 인벤토리에서 회전된 아이템도 장비 슬롯 안에서는 항상 정방향 상태를 가진다.
        item.isRotated = false;
        equippedItems[slotType] = item;
        item.isEquipped = true;

        // 여기서는 EquipmentChanged를 호출하지 않는다.
        return EquipResult.Success;
    }
    internal EquipResult TryUnequipState(
        EquipSlotType slotType)
    {
        if (!equippedItems.TryGetValue(
                slotType,
                out InventoryItem item) ||
            item == null)
        {
            return EquipResult.NotEquipped;
        }

        equippedItems.Remove(slotType);
        item.isEquipped = false;

        // 여기서는 EquipmentChanged를 호출하지 않는다.
        return EquipResult.Success;
    }

    internal EquipResult TrySwapState(
        EquipSlotType slotType,
        InventoryItem incomingItem)
    {
        EquipResult validation =
            ValidateEquip(incomingItem, slotType);

        if (validation != EquipResult.Success)
        {
            return validation;
        }

        if (!equippedItems.TryGetValue(
                slotType,
                out InventoryItem outgoingItem) ||
            outgoingItem == null)
        {
            return EquipResult.NotEquipped;
        }

        if (outgoingItem == incomingItem)
        {
            return EquipResult.Failed;
        }

        // 교체로 들어오는 아이템 역시 장비 상태에서는 회전값을 유지하지 않는다.
        incomingItem.isRotated = false;
        equippedItems[slotType] = incomingItem;

        outgoingItem.isEquipped = false;
        incomingItem.isEquipped = true;

        // 여기서는 EquipmentChanged를 호출하지 않는다.
        return EquipResult.Swapped;
    }

    internal void PublishChanged()
    {
        EquipmentChanged();
    }
    public IEnumerable<KeyValuePair<EquipSlotType, InventoryItem>> GetEquippedItems()
    {
        return equippedItems;
    }

    public bool TryGetEquippedItem(EquipSlotType slotType, out InventoryItem item)
    {
        return equippedItems.TryGetValue(slotType, out item);
    }

    public bool TryGetEquippedItemInstance(
    EquipSlotType slotType,
    out ItemInstance itemInstance)
    {
        itemInstance = null;

        if (!equippedItems.TryGetValue(slotType, out InventoryItem inventoryItem))
            return false;

        if (inventoryItem?.itemData?.definition == null)
            return false;

        itemInstance = inventoryItem.itemData;
        return true;
    }

    private void EquipmentChanged()
    {
        EquippedItemInfo[] infos = new EquippedItemInfo[equippedItems.Count];

        int index = 0;
        foreach (var item in equippedItems)
        {
            infos[index] = new EquippedItemInfo(item.Key, item.Value);
            index++;
        }

        OnEquipmentChanged?.Invoke(infos);
    }

    public bool NotifyEquippedItemChanged(ItemInstance changedItem)
    {
        if (changedItem == null)
            return false;

        foreach (InventoryItem equippedItem
                 in equippedItems.Values)
        {
            if (ReferenceEquals(
                    equippedItem?.itemData,
                    changedItem))
            {
                PublishChanged();
                return true;
            }
        }
        return false;
    }

    private EquipResult ValidateEquip(InventoryItem item, EquipSlotType slotType)
    {
        if (item == null || item.itemData == null || item.itemData.definition == null)
            return EquipResult.InvalidItem;

        if (!EquipSlotRules.CanEquipTo(item.itemData.definition, slotType))
            return EquipResult.InvalidSlot;

        // 무기는 캐릭터 전용(Fighter/Gunner)이 정해져 있다. 지금 캐릭터를 아직 모르면(테스트 씬 등)
        // 검증을 건너뛴다 - 실제 플레이 흐름에서는 PlayerWeaponVisualPresenter가 항상 미리 알려준다.
        if (slotType == EquipSlotType.Weapon &&
            CurrentCharacterClass.HasValue &&
            item.itemData.definition.characterClass != CurrentCharacterClass.Value)
        {
            return EquipResult.WrongCharacterClass;
        }

        return EquipResult.Success;
    }
}

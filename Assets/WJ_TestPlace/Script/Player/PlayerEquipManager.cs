using UnityEngine;
using ItemSystem;

/// <summary>
/// 장착된 아이템들(무기/투구/갑옷/부츠 - 포션 제외)의 스탯을 합산해서 StatSet으로 제공하는
/// IStatSetProvider 구현체. 실제 장착/해제 자체는 ItemUI.cs가 처리하고, 여기서는 각
/// EquipSlotUI.equipItemUI를 읽어서 호출 시점마다 다시 합산한다(pull 방식이라 항상 최신 상태).
/// </summary>
public class PlayerEquipManager : MonoBehaviour, IStatSetProvider
{
    [SerializeField] private EquipmentSystem equipmentSystem;


    /// <summary>
    /// 장비 원본을 확보한다. 인스펙터가 비어 있으면(프리팹으로 생성된 캐릭터는 씬 오브젝트 참조를
    /// 직렬화할 수 없어 항상 비어 있다) 공용 헬퍼로 찾아 채운다.
    ///
    /// !! 헬퍼는 null을 돌려줄 수 있다 - 내 소유가 아닌 원격 플레이어이거나, InventoryController.Instance가
    ///    아직 준비되지 않은 시점이다. 그대로 역참조하면 NRE가 나므로 호출부에서 반드시 확인한다.
    /// </summary>
    private bool TryGetEquipmentSystem(out EquipmentSystem equipment)
    {
        if (equipmentSystem == null)
            equipmentSystem = InventoryController.GetLocalEquipmentSystem(this);

        equipment = equipmentSystem;
        return equipment != null;
    }

    public StatSet GetStatSet()
    {
        StatSet total = StatSet.Zero;

        // 장비를 아직(또는 영영) 못 찾으면 장비 레이어 기여분은 0이다. 스탯 재계산마다 호출되는
        // 경로라 경고를 찍지 않는다 - 캐릭터 생성 직후처럼 정상적으로 비는 프레임이 있다.
        if (!TryGetEquipmentSystem(out EquipmentSystem equipment))
            return total;

        foreach (var pair in equipment.GetEquippedItems())
        {
            EquipSlotType slotType = pair.Key;
            InventoryItem invItem = pair.Value;

            if (slotType == EquipSlotType.Potion)
                continue;

            if (invItem == null || invItem.itemData == null)
                continue;

            total += ToStatSet(invItem.itemData);
        }

        return total;
    }
    /// <summary>
    /// 실제 장착 상태를 변경하지 않고 지정한 슬롯의 현재 장비를 후보 아이템으로
    /// 교체했다고 가정한 전체 장비 StatSet을 계산한다.
    /// 포션은 제외하며 후보 아이템의 강화 적용 메인 옵션과 모든 부가 옵션을 포함한다.
    /// </summary>
    /// <returns>후보 아이템이 유효하고 지정 슬롯에 장착할 수 있으면 true.</returns>
    public bool TryGetStatSetAfterReplacing(
        EquipSlotType replacingSlot,
        ItemInstance candidateItem,
        out StatSet result)
    {
        result = StatSet.Zero;

        // 비교 결과를 낼 수 없으므로 여기서는 false로 알린다(호출부가 비교 UI를 숨긴다).
        if (!TryGetEquipmentSystem(out EquipmentSystem equipment))
            return false;

        if (candidateItem == null || candidateItem.definition == null)
        {
            return false;
        }

        if (!EquipSlotRules.CanEquipTo(
                candidateItem.definition,
                replacingSlot))
        {
            return false;
        }

        foreach (var pair in equipment.GetEquippedItems())
        {
            EquipSlotType currentSlot = pair.Key;
            InventoryItem inventoryItem = pair.Value;

            if (currentSlot == EquipSlotType.Potion)
                continue;

            // 교체할 슬롯의 기존 아이템은 합산하지 않는다.
            if (currentSlot == replacingSlot)
                continue;

            if (inventoryItem == null || inventoryItem.itemData == null)
                continue;

            result += ToStatSet(inventoryItem.itemData);
        }

        // 기존 장비 대신 후보 아이템을 합산한다.
        result += ToStatSet(candidateItem);

        return true;
    }

    /// <summary>메인 옵션(강화 적용됨) + 서브 옵션(속성 보너스 포함)을 전부 StatSet에 더한다.</summary>
    public static StatSet ToStatSet(ItemInstance itemData)
    {
        StatSet result = StatSet.Zero;

        foreach (var opt in itemData.GetEffectiveMainOptions())
            StatSetMapper.AddStat(ref result, opt.statType, opt.value);

        foreach (var sub in itemData.rolledSubStats)
            StatSetMapper.AddStat(ref result, sub.statType, sub.value);

        return result;
    }
}

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


    public StatSet GetStatSet()
    {
        StatSet total = StatSet.Zero;

        if (equipmentSystem == null)
        {
            Debug.LogWarning("[PlayerEquipManager] EquipmentSystem이 연결되지 않았습니다.");
            return total;
        }

        foreach (var pair in equipmentSystem.GetEquippedItems())
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

    /// <summary>메인 옵션(강화 적용됨) + 서브 옵션(속성 보너스 포함)을 전부 StatSet에 더한다.</summary>
    private static StatSet ToStatSet(ItemInstance itemData)
    {
        StatSet result = StatSet.Zero;

        foreach (var opt in itemData.GetEffectiveMainOptions())
            StatSetMapper.AddStat(ref result, opt.statType, opt.value);

        foreach (var sub in itemData.rolledSubStats)
            StatSetMapper.AddStat(ref result, sub.statType, sub.value);

        return result;
    }
}

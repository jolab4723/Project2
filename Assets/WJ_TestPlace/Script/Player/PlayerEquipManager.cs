using UnityEngine;
using ItemSystem;

/// <summary>
/// 장착된 아이템들(무기/투구/갑옷/부츠 - 포션 제외)의 스탯을 합산해서 StatSet으로 제공하는
/// IStatSetProvider 구현체. 실제 장착/해제 자체는 ItemUI.cs가 처리하고, 여기서는 각
/// EquipSlotUI.equipItemUI를 읽어서 호출 시점마다 다시 합산한다(pull 방식이라 항상 최신 상태).
/// </summary>
public class PlayerEquipManager : MonoBehaviour, IStatSetProvider
{
    public StatSet GetStatSet()
    {
        StatSet total = StatSet.Zero;

        if (InventoryController.Instance == null || InventoryController.Instance.allEquipSlots == null)
        {
            Debug.LogWarning("[PlayerEquipManager] InventoryController.allEquipSlots를 찾을 수 없습니다.");
            return total;
        }

        foreach (var slot in InventoryController.Instance.allEquipSlots)
        {
            // 포션 슬롯은 스탯에 영향 없음 (소비 로직은 다른 곳에서 처리)
            if (slot == null || slot.requiredCategory == ItemCategory.Potion)
                continue;

            var invItem = slot.equipItemUI != null ? slot.equipItemUI.Item : null;
            var itemData = invItem != null ? invItem.itemData : null;
            if (itemData == null)
                continue;

            total += ToStatSet(itemData);
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

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

        if (InventoryController.Instance == null || InventoryController.Instance.allEquipSlots == null)
        {
            Debug.LogWarning("[PlayerEquipManager] InventoryController.allEquipSlots를 찾을 수 없습니다.");
            return total;
        }

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
            AddStat(ref result, opt.statType, opt.value);

        foreach (var sub in itemData.rolledSubStats)
            AddStat(ref result, sub.statType, sub.value);

        return result;
    }

    /// <summary>
    /// StatType 하나를 StatSet의 대응 필드에 더한다.
    /// !! StatType과 StatSet 필드 이름이 완전히 1:1은 아니라서 PlayerStatManager와 동일하게 명시적으로 매핑함:
    ///    - healthFlat/Percent -> maxHealthFlat/Percent (이름만 다름)
    ///    - penetrationFlat -> penFlat (StatType엔 Percent 버전이 없음)
    ///    - mpMaxFlat -> maxManaFlat (StatSet에 원래 없어서 PlayerStatManager 작업 때 추가함)
    /// </summary>
    private static void AddStat(ref StatSet s, StatType type, float value)
    {
        switch (type)
        {
            case StatType.healthFlat: s.maxHealthFlat += value; break;
            case StatType.healthPercent: s.maxHealthPercent += value; break;
            case StatType.attackPowerFlat: s.attackPowerFlat += value; break;
            case StatType.attackPowerPercent: s.attackPowerPercent += value; break;
            case StatType.defensePowerFlat: s.defensePowerFlat += value; break;
            case StatType.defensePowerPercent: s.defensePowerPercent += value; break;
            case StatType.moveSpeedFlat: s.moveSpeedFlat += value; break;
            case StatType.moveSpeedPercent: s.moveSpeedPercent += value; break;
            case StatType.attackSpeedFlat: s.attackSpeedFlat += value; break;
            case StatType.attackSpeedPercent: s.attackSpeedPercent += value; break;
            case StatType.critRateFlat: s.critRateFlat += value; break;
            case StatType.critMultFlat: s.critMultFlat += value; break;
            case StatType.cdrFlat: s.cdrFlat += value; break;
            case StatType.mpRegenFlat: s.mpRegenFlat += value; break;
            case StatType.mpRegenPercent: s.mpRegenPercent += value; break;
            case StatType.mpMaxFlat: s.maxManaFlat += value; break;
            case StatType.penetrationFlat: s.penFlat += value; break;
            case StatType.skillRangeFlat: s.skillRangeFlat += value; break;
            case StatType.fireBonusFlat: s.fireBonusFlat += value; break;
            case StatType.iceBonusFlat: s.iceBonusFlat += value; break;
            case StatType.electricBonusFlat: s.electricBonusFlat += value; break;
            default:
                Debug.LogWarning($"[PlayerEquipManager] 매핑되지 않은 StatType: {type}");
                break;
        }
    }
}

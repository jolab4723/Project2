using UnityEngine;

public static class EquipMessageMapper
{
    private static UILabelDatabaseSO labelsCache;

    private static UILabelDatabaseSO Labels =>
        labelsCache ??= Resources.Load<UILabelDatabaseSO>("DataFiles/UIData/3. GeneratedAssets/UILabelDatabase");

    private static string GetLabel(string key, string fallback)
    {
        string label = Labels != null ? Labels.GetLabel(key) : null;
        return string.IsNullOrEmpty(label) ? fallback : label;
    }

    public static string GetMessage(EquipResult result)
    {
        switch (result)
        {
            case EquipResult.Success:
                return GetLabel("chat_ui.equip_success", "장비 처리가 완료되었습니다.");

            case EquipResult.Swapped:
                return GetLabel("chat_ui.equip_swapped", "장비를 교체했습니다.");

            case EquipResult.InvalidItem:
                return GetLabel("chat_ui.equip_invalid_item", "장착할 수 없는 아이템입니다.");

            case EquipResult.InvalidSlot:
                return GetLabel("chat_ui.equip_invalid_slot", "해당 슬롯에 장착할 수 없습니다.");

            case EquipResult.SlotOccupied:
                return GetLabel("chat_ui.equip_slot_occupied", "이미 장비가 장착되어 있습니다.");

            case EquipResult.NotEquipped:
                return GetLabel("chat_ui.equip_not_equipped", "해제할 장비가 없습니다.");

            case EquipResult.NoReturnSpace:
                return GetLabel("chat_ui.equip_no_return_space", "기존 장비를 인벤토리에 내려놓을 공간이 없습니다.");

            case EquipResult.Failed:
                return GetLabel("chat_ui.equip_failed", "장비 처리가 실패했습니다.");

            case EquipResult.WrongCharacterClass:
                return GetLabel("chat_ui.equip_wrong_class", "이 캐릭터가 장착할 수 없는 무기입니다.");

            default:
                return GetLabel("chat_ui.equip_unknown", "알 수 없는 장비 처리 결과입니다.");
        }
    }
}

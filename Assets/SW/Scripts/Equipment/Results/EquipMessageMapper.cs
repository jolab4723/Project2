public static class EquipMessageMapper
{
    public static string GetMessage(EquipResult result)
    {
        switch (result)
        {
            case EquipResult.Success:
                return "장비 처리가 완료되었습니다.";

            case EquipResult.Swapped:
                return "장비를 교체했습니다.";

            case EquipResult.InvalidItem:
                return "장착할 수 없는 아이템입니다.";

            case EquipResult.InvalidSlot:
                return "해당 슬롯에 장착할 수 없습니다.";

            case EquipResult.SlotOccupied:
                return "이미 장비가 장착되어 있습니다.";

            case EquipResult.NotEquipped:
                return "해제할 장비가 없습니다.";

            case EquipResult.NoReturnSpace:
                return "기존 장비를 인벤토리에 내려놓을 공간이 없습니다.";

            case EquipResult.Failed:
                return "장비 처리가 실패했습니다.";

            case EquipResult.WrongCharacterClass:
                return "이 캐릭터가 장착할 수 없는 무기입니다.";

            default:
                return "알 수 없는 장비 처리 결과입니다.";
        }
    }
}
public static class InventoryMessageMapper
{
    public static string GetAcquisitionMessage(string itemName) =>
        $"{itemName} 아이템을 획득했습니다.";

    public static string GetColoredAcquisitionMessage(string itemName, ItemSystem.ItemRarity rarity)
    {
        string name = string.IsNullOrWhiteSpace(itemName) ? "아이템" : itemName;
        char last = name[name.Length - 1];
        string particle = last >= '가' && last <= '힣' && (last - '가') % 28 != 0 ? "을" : "를";
        string color = ItemSystem.ItemDisplayNames.GradeColorHex.TryGetValue(rarity, out string hex) ? hex : "#FFFFFF";
        // 서버 아이템명도 표시 태그를 끊지 않게 한다. 일반 채팅 본문에는 Rich Text를 켜지 않는다.
        name = name.Replace("<", "＜").Replace(">", "＞");
        return $"<color={color}>{name}</color>{particle} 획득했습니다.";
    }

    public static string GetMessage(
        InventoryAddResult result,
        string itemName,
        int x,
        int y)
    {
        switch (result)
        {
            case InventoryAddResult.Success:
                return $"{GetAcquisitionMessage(itemName)} 위치 : {x}, {y}";

            case InventoryAddResult.InvalidItem:
                return "유효하지 않은 아이템입니다.";

            case InventoryAddResult.NoSpace:
                return "인벤토리가 꽉 찼습니다!";

            case InventoryAddResult.GridUnavailable:
                return "인벤토리를 사용할 수 없습니다.";

            case InventoryAddResult.PlacementFailed:
                return "인벤토리에 아이템을 배치하지 못했습니다.";

            default:
                return "아이템 획득에 실패했습니다.";
        }
    }
}

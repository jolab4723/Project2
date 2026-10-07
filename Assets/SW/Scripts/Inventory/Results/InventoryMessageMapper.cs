using UnityEngine;

public static class InventoryMessageMapper
{
    private static UILabelDatabaseSO labelsCache;

    private static UILabelDatabaseSO Labels =>
        labelsCache ??= Resources.Load<UILabelDatabaseSO>("DataFiles/UIData/3. GeneratedAssets/UILabelDatabase");

    private static string GetLabel(string key, string fallback)
    {
        string label = Labels != null ? Labels.GetLabel(key) : null;
        return string.IsNullOrEmpty(label) ? fallback : label;
    }

    public static string GetAcquisitionMessage(string itemName) =>
        string.Format(GetLabel("chat_ui.inventory_acquisition", "{0} 아이템을 획득했습니다."), itemName);

    /// <summary>
    /// 한국어는 아이템 이름 마지막 글자의 받침 유무에 따라 조사(을/를)를 골라 붙인다(기존 로직 그대로 유지).
    /// 다른 언어는 이 조사 규칙이 없어서, DB의 "{0} 획득했습니다." 형태 템플릿에 색이 입혀진 이름만 끼워 넣는다.
    /// </summary>
    public static string GetColoredAcquisitionMessage(string itemName, ItemSystem.ItemRarity rarity)
    {
        string name = string.IsNullOrWhiteSpace(itemName) ? "아이템" : itemName;
        string color = ItemSystem.ItemDisplayNames.GradeColorHex.TryGetValue(rarity, out string hex) ? hex : "#FFFFFF";
        // 서버 아이템명도 표시 태그를 끊지 않게 한다. 일반 채팅 본문에는 Rich Text를 켜지 않는다.
        name = name.Replace("<", "＜").Replace(">", "＞");
        string coloredName = $"<color={color}>{name}</color>";

        bool isKorean = YJ_LanguageManager.Instance == null || YJ_LanguageManager.Instance.CurrentLanguage == GameLanguage.KOR;
        if (isKorean)
        {
            char last = name.Length > 0 ? name[name.Length - 1] : ' ';
            string particle = last >= '가' && last <= '힣' && (last - '가') % 28 != 0 ? "을" : "를";
            return $"{coloredName}{particle} 획득했습니다.";
        }

        return string.Format(GetLabel("chat_ui.inventory_acquisition_colored", "{0} 획득했습니다."), coloredName);
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
                return string.Format(
                    GetLabel("chat_ui.inventory_success_position", "{0} 위치 : {1}, {2}"),
                    GetAcquisitionMessage(itemName), x, y);

            case InventoryAddResult.InvalidItem:
                return GetLabel("chat_ui.inventory_invalid_item", "유효하지 않은 아이템입니다.");

            case InventoryAddResult.NoSpace:
                return GetLabel("chat_ui.inventory_no_space", "인벤토리가 꽉 찼습니다!");

            case InventoryAddResult.GridUnavailable:
                return GetLabel("chat_ui.inventory_grid_unavailable", "인벤토리를 사용할 수 없습니다.");

            case InventoryAddResult.PlacementFailed:
                return GetLabel("chat_ui.inventory_placement_failed", "인벤토리에 아이템을 배치하지 못했습니다.");

            default:
                return GetLabel("chat_ui.inventory_add_failed", "아이템 획득에 실패했습니다.");
        }
    }
}

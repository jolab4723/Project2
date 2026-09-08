using UnityEngine;

public static class InventoryRemoveMessageMapper
{
    private static UILabelDatabaseSO labelsCache;

    private static UILabelDatabaseSO Labels =>
        labelsCache ??= Resources.Load<UILabelDatabaseSO>("DataFiles/UIData/3. GeneratedAssets/UILabelDatabase");

    private static string GetLabel(string key, string fallback)
    {
        string label = Labels != null ? Labels.GetLabel(key) : null;
        return string.IsNullOrEmpty(label) ? fallback : label;
    }

    public static string GetMessage(InventoryRemoveResult result, string itemName)
    {
        switch (result)
        {
            case InventoryRemoveResult.Success:
                return string.Format(GetLabel("chat_ui.inventory_remove_success", "{0}을(를) 삭제했습니다."), itemName);

            case InventoryRemoveResult.InvalidItem:
                return GetLabel("chat_ui.inventory_remove_invalid_item", "유효하지 않은 아이템입니다.");

            case InventoryRemoveResult.InventoryUnavailable:
                return GetLabel("chat_ui.inventory_remove_unavailable", "인벤토리를 사용할 수 없습니다.");

            case InventoryRemoveResult.NotPlayerInventory:
                return GetLabel("chat_ui.inventory_remove_not_player", "내 인벤토리의 아이템만 삭제할 수 있습니다.");

            case InventoryRemoveResult.EquippedItemNotAllowed:
                return GetLabel("chat_ui.inventory_remove_equipped_not_allowed", "장착 중인 아이템은 바로 삭제할 수 없습니다.");

            case InventoryRemoveResult.RemoveFailed:
                return GetLabel("chat_ui.inventory_remove_failed", "아이템 삭제에 실패했습니다.");

            default:
                return GetLabel("chat_ui.inventory_remove_default_failed", "아이템을 삭제하지 못했습니다.");
        }
    }
}

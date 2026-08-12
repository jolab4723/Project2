using UnityEngine;
public static class ItemUIFinder
{
    public static ItemUI FindInGrid(InventoryGrid grid, InventoryItem item)
    {
        if (grid == null || grid.ItemsContainer == null || item == null)
            return null;

        foreach (Transform child in grid.ItemsContainer)
        {
            ItemUI ui = child.GetComponent<ItemUI>();

            if (ui != null && ui.Item == item)
                return ui;
        }

        return null;
    }
}

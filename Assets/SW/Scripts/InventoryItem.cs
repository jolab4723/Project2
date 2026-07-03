using UnityEngine;
using ItemSystem;

[System.Serializable]

public class InventoryItem
{
    public ItemInstance itemData { get; private set; }
    public int itemInstanceID;
    public int upgradeLevel;

    public int x;
    public int y;
    public bool isRotated;
    public bool isEquipped;
    

    public int CurrentWidth => isRotated ? itemData.definition.itemHeight : itemData.definition.itemWidth;
    public int CurrentHeight => isRotated ? itemData.definition.itemWidth : itemData.definition.itemHeight;
    public InventoryItem (ItemInstance data)
    {
        itemData = data;
        x = 0;
        y = 0;
        isRotated = false;
        isEquipped = false;
    }

}

using UnityEngine;

[System.Serializable]
public class InventoryItem
{
    public ItemData itemData { get; private set; }

    public int x;
    public int y;
    public bool isRotated;
    public bool isEquipped;

    public int CurrentWidth => isRotated ? itemData.height : itemData.width;
    public int CurrentHeight => isRotated ? itemData.width : itemData.height;
    public InventoryItem (ItemData data)
    {
        itemData = data;
        x = 0;
        y = 0;
        isRotated = false;
        isEquipped = false;
    }

}

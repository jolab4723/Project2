[System.Serializable]
public struct EquippedItemInfo
{
    public int itemID;
    public int upgradeLevel;

    public EquippedItemInfo(int itemID, int upgradeLevel)
    {
        this.itemID = itemID;
        this.upgradeLevel = upgradeLevel;
    }
}
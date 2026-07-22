using ItemSystem;

// 전투 시 장비 아이템 정보를 넘기기 위한 구조체
public struct EquippedWeaponInfo
{
    public WeaponType weaponType;
    public ElementType elementType;
    public int upgradeLevel;

    public EquippedWeaponInfo(
        WeaponType weaponType,
        ElementType elementType,
        int upgradeLevel)
    {
        this.weaponType = weaponType;
        this.elementType = elementType;
        this.upgradeLevel = upgradeLevel;
    }

}
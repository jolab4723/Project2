using UnityEngine;

public enum CharacterClass { All, Fighter, Gunner} // 직업 별 무기 아이템을 위한 분류
public enum ItemType {Weapon, Armor, Core, Potion } // 대분류

public enum EquipItem { None, Weapon, Helmet, Chest, Boots, Potion} // 소분류 (장비 아이템)

public enum WeaponType {None, Sword, Axe, Blunt, Rifle, ShotGun, GrenadeLauncher}

public enum StatType { Attack, Defense, Heal}

public enum Grade
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum AttributeType
{
    None,
    Fire,
    Ice,
    Lightning
}
[CreateAssetMenu (fileName = "New Item", menuName ="Inventory/Item")]
public class ItemData : ScriptableObject
{
    public CharacterClass characterClass;
    public string itemName;
    public Sprite itemIcon;
    public EquipItem equipItem;
    public ItemType itemType;
    public WeaponType weaponType;
    public AttributeType type;
    public Grade itemGrade;
    public StatType statType;
    public int width = 1;
    public int height = 1;

    public int statValue;

    public int buyPrice;
    public int sellPrice;

}

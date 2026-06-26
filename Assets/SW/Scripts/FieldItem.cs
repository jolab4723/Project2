using UnityEngine;
using UnityEngine.InputSystem;

public class FieldItem : MonoBehaviour
{
    public ItemData potionData;
    public ItemData[] weaponData;
    public ItemData bootsData;

    private InventoryGrid currentGrid;

    private void Awake()
    {
    }
    private void Update()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            InventoryController.Instance.AddItem(potionData);
        }
        else if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            InventoryController.Instance.AddItem(weaponData[Random.Range(0, weaponData.Length)]);
        }
        else if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            InventoryController.Instance.AddItem(bootsData);
        }
    }
}

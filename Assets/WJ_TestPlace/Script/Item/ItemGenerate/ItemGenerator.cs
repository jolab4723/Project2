using ItemSystem;
using UnityEngine;
using UnityEngine.InputSystem;

public class ItemGenerator : MonoBehaviour
{
    [Header("Item Data Structure")]
    public ItemDefinitionSO potionData;
    public ItemDefinitionSO[] weaponData;
    public ItemDefinitionSO bootsData;

    [Header("드랍 처리")]
    [Tooltip("ItemDataStorage 컴포넌트가 붙어있는 프리팹")]
    public GameObject itemPickupPrefab;

    /// <summary>가장 최근 Drop()으로 스폰된 픽업 오브젝트. 테스트 버튼 등에서 획득 처리 후 파괴할 때 사용.</summary>
    public GameObject LastSpawnedPickup { get; private set; }

    // Test Input
    private void Update()
    {
        // 아이템 드랍 (월드에 실제 오브젝트 생성)
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            ItemDefinitionSO def = (weaponData != null && weaponData.Length > 0) ? weaponData[0]
                                    : (potionData != null ? potionData : bootsData);
            Drop(def, transform.position, transform.rotation);
        }

        else if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            InventoryController.Instance.AddItem(GenerateItem(bootsData));
        }
    }

    /// <summary>
    /// def로부터 데이터만 생성한다 (월드 스폰 없음).
    /// </summary>
    public ItemInstance GenerateItem(ItemDefinitionSO structure)
    {
        // 아이템 데이터 생성
        return ItemDataCreator.Generate(structure);
    }

    /// <summary>
    /// def로부터 새 ItemInstance를 생성하고, 지정된 위치/회전에 픽업 오브젝트를 스폰한다.
    /// (아이템 오브젝트 생성 후 데이터 주입, 드랍 역할)
    /// </summary>
    public ItemInstance Drop(ItemDefinitionSO def, Vector3 position, Quaternion rotation)
    {
        if (def == null)
        {
            Debug.LogWarning("[ItemGenerator] def가 null입니다.");
            return null;
        }

        ItemInstance instance = GenerateItem(def);
        SpawnPickup(instance, position, rotation);
        return instance;
    }

    void SpawnPickup(ItemInstance instance, Vector3 position, Quaternion rotation)
    {
        if (itemPickupPrefab == null)
        {
            Debug.LogWarning("[ItemGenerator] itemPickupPrefab이 비어있습니다. 인스펙터에서 연결해주세요.");
            return;
        }

        GameObject obj = Instantiate(itemPickupPrefab, position, rotation);
        var storage = obj.GetComponent<ItemDataStorage>();

        if (storage == null)
        {
            Debug.LogWarning("[ItemGenerator] itemPickupPrefab에 ItemDataStorage 컴포넌트가 없습니다.");
            Destroy(obj);
            return;
        }

        storage.Init(instance);
        LastSpawnedPickup = obj;
    }
}

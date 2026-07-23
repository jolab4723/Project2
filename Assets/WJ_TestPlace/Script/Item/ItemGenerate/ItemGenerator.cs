using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 아이템 데이터를 오브젝트에 주입하고 지정된 위치/회전에 드랍(스폰)하는 것만 담당한다.
    /// 아이템 데이터 생성 자체는 ItemDataCreator의 역할이라 여기서는 안 한다 - 호출부가 먼저
    /// ItemDataCreator.CreateItemData(def)로 ItemInstance를 만든 뒤 DropGeneratedItem()에 넘겨줘야 한다.
    /// </summary>
    public class ItemGenerator : MonoBehaviour
    {
        [Header("드랍 처리")]
        [Tooltip("ItemDataStorage 컴포넌트가 붙어있는 프리팹")]
        public GameObject itemPickupPrefab;

        /// <summary>가장 최근 DropGeneratedItem()으로 스폰된 픽업 오브젝트. 테스트 버튼 등에서 획득 처리 후 파괴할 때 사용.</summary>
        public GameObject LastSpawnedPickup { get; private set; }

        /// <summary>
        /// instance를 지정된 위치/회전에 픽업 오브젝트로 스폰한다 (데이터 주입 + 드랍만 담당).
        /// </summary>
        public void DropGeneratedItem(ItemInstance instance, Vector3 position, Quaternion rotation)
        {
            if (instance == null)
            {
                Debug.LogWarning("[ItemGenerator] instance가 null입니다.");
                return;
            }

            SpawnPickup(instance, position, rotation);
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
}

using UnityEngine;

namespace ItemSystem
{
    public class ItemGenerator
    {
        private readonly GameObject itemPickupPrefab;

        /// <summary>가장 최근 DropGeneratedItem()으로 스폰된 픽업 오브젝트. 테스트 버튼 등에서 획득 처리 후 파괴할 때 사용.</summary>
        public GameObject LastSpawnedPickup { get; private set; }

        public ItemGenerator(GameObject itemPickupPrefab)
        {
            this.itemPickupPrefab = itemPickupPrefab;
        }

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
                Debug.LogWarning("[ItemGenerator] itemPickupPrefab이 비어있습니다. ItemSystemController 인스펙터에서 연결해주세요.");
                return;
            }

            GameObject obj = Object.Instantiate(itemPickupPrefab, position, rotation);
            var storage = obj.GetComponent<ItemDataStorage>();

            if (storage == null)
            {
                Debug.LogWarning("[ItemGenerator] itemPickupPrefab에 ItemDataStorage 컴포넌트가 없습니다.");
                Object.Destroy(obj);
                return;
            }

            storage.Init(instance);
            LastSpawnedPickup = obj;
        }
    }
}

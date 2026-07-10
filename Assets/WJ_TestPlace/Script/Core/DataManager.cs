using UnityEngine;

namespace Core
{
    public class DataManager : Singleton<DataManager>, IManagerModule
    {
        [Header("정적 데이터")]
        [Tooltip("모든 아이템 정의를 itemId로 조회할 수 있는 데이터베이스")]
        [SerializeField] private ItemSystem.ItemDatabaseSO itemDatabase;

        public ItemSystem.ItemDatabaseSO ItemDatabase { get { return itemDatabase; } }

        public bool IsStaticDataLoaded { get; private set; }

        public string ModuleName { get { return "DataManager"; } }

        public void Activate()
        {
            if (itemDatabase == null)
            {
                Debug.LogWarning("[DataManager] itemDatabase가 연결되지 않았습니다.");
                IsStaticDataLoaded = false;
                return;
            }

            IsStaticDataLoaded = true;
            Debug.Log("[DataManager] 활성화 완료 (itemDatabase 연결됨, 아이템 " + itemDatabase.allItems.Count + "개)");
        }
    }
}

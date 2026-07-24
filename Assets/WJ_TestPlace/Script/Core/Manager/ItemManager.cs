using UnityEngine;
using ItemSystem;

namespace Core
{
    /// <summary>
    /// 아이템 정적 데이터(아이템 정의 데이터베이스)를 전담하는 매니저.
    /// </summary>
    public class ItemManager : Singleton<ItemManager>, IManagerModule
    {
        [Header("정적 데이터")]
        [Tooltip("모든 아이템 정의를 itemId로 조회할 수 있는 데이터베이스")]
        [SerializeField] private ItemDatabaseSO itemDatabase;

        public ItemDatabaseSO ItemDatabase => itemDatabase;
        public bool IsStaticDataLoaded { get; private set; }
        public string ModuleName => "ItemManager";

        public void Activate()
        {
            if (itemDatabase == null)
            {
                Debug.LogWarning("[ItemManager] itemDatabase가 연결되지 않았습니다.");
                IsStaticDataLoaded = false;
                return;
            }

            IsStaticDataLoaded = true;
            Debug.Log("[ItemManager] 활성화 완료 (itemDatabase 연결됨, 아이템 " + itemDatabase.allItems.Count + "개)");
        }

        /*
        적 등급과 드랍 위치만 넘기면 씬의 ItemSystemController를 통해 아이템을 생성/드랍한다.
        적 사망 처리 등 외부 코드는 이 메서드 하나만 호출하면 되고, 실제 롤/생성/스폰 로직은
        ItemSystemController가 그대로 담당한다 (ItemManager는 전역 진입점 역할만).
        */

        /// <summary>
        /// 적 등급에 따른 아이템 랜덤 드롭
        /// </summary>
        /// <param name="grade">적 등급</param>
        /// <param name="position">아이템 드롭 위치</param>
        public void DropRandomItem(EnemyGrade grade, Vector3 position)
        {
            if (ItemSystemController.Instance == null)
            {
                Debug.LogWarning("[ItemManager] ItemSystemController.Instance가 없어 아이템을 드랍하지 못했습니다.");
                return;
            }

            ItemSystemController.Instance.DropGeneratedItem(grade, position);
        }

        /// <summary>
        /// 특정 아이템 드롭
        /// </summary>
        /// <param name="SO">드랍시킬 특정 아이템 정의</param>
        /// <param name="position">아이템 드롭 위치</param>
        public void DropSpecificItem(ItemDefinitionSO SO, Vector3 position)
        {
            if (ItemSystemController.Instance == null)
            {
                Debug.LogWarning("[ItemManager] ItemSystemController.Instance가 없어 아이템을 드랍하지 못했습니다.");
                return;
            }

            ItemSystemController.Instance.DropGeneratedItem(SO, position);
        }

        /// <summary>
        // 아이템 ID 반환 메소드
        /// </summary>
        public int SearchItemID(string name)
        {
            return 0;
        }
    }
}

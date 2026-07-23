using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 아이템 시스템 통합 컨트롤러
    /// </summary>
    public class ItemSystemController : MonoBehaviour
    {
        /// <summary>씬에 있는 ItemSystemController. ItemManager가 이걸 통해 드랍을 위임한다.</summary>
        public static ItemSystemController Instance { get; private set; }

        [Header("드랍 오브젝트")]
        [Tooltip("ItemDataStorage 컴포넌트가 붙어있는 프리팹")]
        public GameObject itemPickupPrefab;

        [Header("드랍 처리기")]
        public ItemGenerator itemGenerator;

        [Header("드랍 확률 테이블")]
        public ItemDropTableSO itemDropTable;

        [Tooltip("itemDropTable에서 어떤 몬스터 등급 규칙을 적용할지")]
        public EnemyGrade enemyGrade = EnemyGrade.Normal;

        private readonly ItemDropRollService dropRollService = new ItemDropRollService();

        [Header("획득 대상 인벤토리")]
        [Tooltip("IItemReceiver를 구현한 컴포넌트(예: InventoryController)를 연결. " +
                 "비워두면 인벤토리에 넣지 않고 콘솔 로그만 출력한다.")]
        [SerializeField] private MonoBehaviour receiverBehaviour; // IItemReceiver 구현체
        private IItemReceiver Receiver => receiverBehaviour as IItemReceiver;

        [Header("스폰 위치")]
        [Tooltip("비워두면 이 오브젝트의 위치에 생성")]
        public Transform spawnPoint;

        [Header("테스트 옵션")]
        public int testUpgradeLevel = 0; // 강화 보너스 확인용. 드랍 후 이 값으로 강제 세팅


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ItemSystemController] 이미 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            // 인스펙터에서 IItemReceiver가 아닌 컴포넌트를 잘못 연결한 경우 조기 경고
            if (receiverBehaviour != null && Receiver == null)
                Debug.LogWarning($"[ItemSystemController] {receiverBehaviour.GetType().Name}은(는) " +
                                 "IItemReceiver를 구현하지 않았습니다. 획득 시 인벤토리에 넣을 수 없습니다.");

        }

        /// <summary>고정 필드(enemyGrade/spawnPoint) 기준으로 드랍한다. 수동 테스트/버튼용.</summary>
        public void DropGeneratedItem()
        {
            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
            DropGeneratedItem(enemyGrade, pos, rot);
        }

        /// <summary>
        /// grade/position/rotation을 그때그때 받아서 드랍한다 (적 사망 처리 등 외부 호출용).
        /// itemDropTable에서 grade 기준으로 랜덤으로 하나 뽑아, itemGenerator로 생성해서 월드에 드랍한다.
        /// </summary>
        public void DropGeneratedItem(EnemyGrade grade, Vector3 position, Quaternion rotation)
        {
            if (itemGenerator == null)
            {
                Debug.LogWarning("[ItemSystemController] itemGenerator가 연결되지 않았습니다.");
                return;
            }

            ItemDefinitionSO def = GetRandomItemSO(grade);
            if (def == null)
                return;

            // 강화 수치를 스폰 "전"에 반영: 데이터 생성 -> 값 세팅 -> 드랍(스폰) 순서.
            ItemInstance instance = ItemDataCreator.CreateItemData(def);
            instance.upgradeLevel = testUpgradeLevel;

            itemGenerator.DropGeneratedItem(instance, position, rotation);
        }

        /// <summary>고정 필드(enemyGrade) 기준으로 랜덤 아이템을 뽑는다.</summary>
        public ItemDefinitionSO GetRandomItemSO() => GetRandomItemSO(enemyGrade);

        /// <summary>
        /// itemDropTable + ItemManager.ItemDatabase 기준으로 grade 규칙에 따라 아이템 하나를 랜덤으로 뽑는다.
        /// 이름은 GetRandomItemID이지만 실제로는 ItemDefinitionSO를 반환한다 (id 문자열이 아니라 정의 자체가 필요해서).
        /// 드랍 확률에 걸리지 않았거나 설정 문제가 있으면 null을 반환한다.
        /// </summary>
        public ItemDefinitionSO GetRandomItemSO(EnemyGrade grade)
        {
            var itemDatabase = Core.ItemManager.Instance != null ? Core.ItemManager.Instance.ItemDatabase : null;
            if (itemDatabase == null)
            {
                Debug.LogWarning("[ItemSystemController] ItemManager.ItemDatabase를 찾을 수 없습니다.");
                return null;
            }

            if (itemDropTable == null)
            {
                Debug.LogWarning("[ItemSystemController] itemDropTable이 연결되지 않았습니다.");
                return null;
            }

            ItemDropRollResultData result = dropRollService.Roll(itemDropTable, itemDatabase, grade);

            if (result.Result == ItemDropRollResult.NoDrop)
                return null; // 확률상 정상적으로 드랍 안 됨

            if (!result.HasDrop)
            {
                Debug.LogWarning($"[ItemSystemController] 아이템 롤 실패: {result.Result}");
                return null;
            }

            return result.ItemDefinition;
        }


        
    }
}

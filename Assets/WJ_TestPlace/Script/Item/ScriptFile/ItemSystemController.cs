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

        [Header("월드 드랍")]
        [Tooltip("빈자리 탐색과 월드 픽업 생성을 담당하는 서비스")]
        [SerializeField] private WorldItemDropService worldItemDropService;

        private ItemDataStorage lastSpawnedPickup;

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
        [Tooltip("아이템이 바닥에 절반쯤 묻히지 않도록 스폰 위치를 y축으로 띄우는 높이")]
        [SerializeField] private float spawnHeightOffset = 0.5f;

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


        /// <summary>
        /// grade/position을 그때그때 받아서 드랍한다 (적 사망 처리 등 외부 호출용).
        /// itemDropTable에서 grade 기준으로 랜덤으로 하나 뽑아 WorldItemDropService로 드랍한다.
        /// </summary>
        public void DropGeneratedItem(EnemyGrade grade, Vector3 position)
        {
            TryDropGeneratedItem(grade, position, out _, out _, out _);
        }

        /// <summary>
        /// 적 등급에 따라 아이템을 추첨하고 월드에 생성한다.
        /// 추첨 결과와 월드 배치 결과를 분리해서 반환하므로 호출자가 실패 원인을 판단할 수 있다.
        /// </summary>
        public bool TryDropGeneratedItem(
            EnemyGrade grade,
            Vector3 position,
            out ItemDropRollResultData rollResult,
            out WorldItemDropResult? worldDropResult,
            out ItemDataStorage spawnedPickup)
        {
            worldDropResult = null;
            spawnedPickup = null;

            ItemDatabaseSO itemDatabase =
                Core.ItemManager.Instance != null
                    ? Core.ItemManager.Instance.ItemDatabase
                    : null;

            rollResult = dropRollService.Roll(
                itemDropTable,
                itemDatabase,
                grade);

            if (!rollResult.HasDrop)
            {
                if (rollResult.Result != ItemDropRollResult.NoDrop)
                {
                    Debug.LogWarning(
                        $"[ItemSystemController] {ItemDropMessageMapper.GetMessage(rollResult)}");
                }

                return false;
            }

            return TrySpawnWorldItem(
                rollResult.ItemDefinition,
                position,
                out worldDropResult,
                out spawnedPickup);
        }

        /// <summary>이미 정해진 특정 아이템(SO)을 그대로 드랍한다 (랜덤 롤 없음).</summary>
        public void DropGeneratedItem(ItemDefinitionSO SO, Vector3 position)
        {
            TryDropGeneratedItem(SO, position, out _, out _);
        }

        /// <summary>
        /// 특정 아이템을 추첨 없이 월드에 생성한다.
        /// </summary>
        public bool TryDropGeneratedItem(
            ItemDefinitionSO itemDefinition,
            Vector3 position,
            out WorldItemDropResult? worldDropResult,
            out ItemDataStorage spawnedPickup)
        {
            return TrySpawnWorldItem(
                itemDefinition,
                position,
                out worldDropResult,
                out spawnedPickup);
        }

        private bool TrySpawnWorldItem(
            ItemDefinitionSO itemDefinition,
            Vector3 position,
            out WorldItemDropResult? worldDropResult,
            out ItemDataStorage spawnedPickup)
        {
            worldDropResult = null;
            spawnedPickup = null;

            if (itemDefinition == null)
            {
                worldDropResult = WorldItemDropResult.InvalidItem;
                return false;
            }

            if (worldItemDropService == null)
            {
                Debug.LogWarning("[ItemSystemController] WorldItemDropService가 연결되지 않았습니다.");
                return false;
            }

            ItemInstance instance =
                ItemDataCreator.CreateItemData(itemDefinition);

            if (instance == null)
            {
                worldDropResult = WorldItemDropResult.InvalidItem;
                Debug.LogWarning("[ItemSystemController] ItemInstance 생성에 실패했습니다.");
                return false;
            }

            instance.upgradeLevel = testUpgradeLevel;

            WorldItemDropResult result =
                worldItemDropService.TryDropAt(
                    instance,
                    position + Vector3.up * spawnHeightOffset,
                    Quaternion.identity,
                    out spawnedPickup);

            worldDropResult = result;

            if (result != WorldItemDropResult.Success)
            {
                Debug.LogWarning($"[ItemSystemController] 월드 아이템 생성 실패: {result}");
                return false;
            }

            lastSpawnedPickup = spawnedPickup;
            return true;
        }

        /// <summary>가장 최근에 스폰된 픽업 오브젝트. 테스트 버튼 등에서 획득 처리 후 파괴할 때 사용.</summary>
        public GameObject LastSpawnedPickup =>
            lastSpawnedPickup != null ? lastSpawnedPickup.gameObject : null;

        /// <summary>고정 필드(enemyGrade) 기준으로 랜덤 아이템을 뽑는다.</summary>
        public ItemDefinitionSO GetRandomItemSO() => GetRandomItemSO(enemyGrade);

        /// <summary>
        /// itemDropTable + ItemManager.ItemDatabase 기준으로 grade 규칙에 따라 아이템 하나를 랜덤으로 뽑는다.
        /// 드랍 확률에 걸리지 않았거나 설정 문제가 있으면 null을 반환한다.
        /// </summary>
        public ItemDefinitionSO GetRandomItemSO(EnemyGrade grade)
        {
            var itemDatabase = Core.ItemManager.Instance != null ? Core.ItemManager.Instance.ItemDatabase : null;
            ItemDropRollResultData result = dropRollService.Roll(itemDropTable, itemDatabase, grade);

            if (!result.HasDrop)
            {
                if (result.Result != ItemDropRollResult.NoDrop)
                {
                    Debug.LogWarning(
                        $"[ItemSystemController] {ItemDropMessageMapper.GetMessage(result)}");
                }

                return null;
            }

            return result.ItemDefinition;
        }
    }
}

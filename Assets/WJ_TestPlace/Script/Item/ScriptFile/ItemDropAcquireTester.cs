using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 아이템 드랍 -> 획득 흐름을 테스트하기 위한 스크립트.
    /// 실제 로직(드랍: ItemGenerator, 획득: ItemAcquisition)은 별도 클래스에 있고,
    /// 이 스크립트는 키 입력으로 그 둘을 호출해서 결과를 확인하는 역할만 한다.
    /// 빈 GameObject에 붙이고 testItem, itemGenerator를 인스펙터에서 연결해서 사용.
    /// </summary>
    public class ItemDropAcquireTester : MonoBehaviour
    {
        [Header("테스트할 아이템 정의")]
        public ItemDefinitionSO testItem;

        [Header("드랍 처리기")]
        public ItemGenerator itemGenerator;

        [Header("획득 대상 인벤토리")]
        [Tooltip("IItemReceiver를 구현한 컴포넌트(예: InventoryController)를 연결. " +
                 "비워두면 인벤토리에 넣지 않고 콘솔 로그만 출력한다.")]
        [SerializeField] private MonoBehaviour receiverBehaviour; // IItemReceiver 구현체
        private IItemReceiver Receiver => receiverBehaviour as IItemReceiver;

        [Header("스폰 위치")]
        [Tooltip("비워두면 이 오브젝트의 위치에 생성")]
        public Transform spawnPoint;

        [Header("테스트 옵션")]
        public bool dropOnStart = true;
        public KeyCode dropKey = KeyCode.Space;
        public KeyCode acquireKey = KeyCode.DownArrow;
        public int testUpgradeLevel = 0; // 강화 보너스 확인용. 드랍 후 이 값으로 강제 세팅

        ItemInstance lastDropped; // 가장 최근에 드랍된 아이템 (획득 테스트용)


        void Start()
        {
            // 인스펙터에서 IItemReceiver가 아닌 컴포넌트를 잘못 연결한 경우 조기 경고
            if (receiverBehaviour != null && Receiver == null)
                Debug.LogWarning($"[ItemDropAcquireTester] {receiverBehaviour.GetType().Name}은(는) " +
                                 "IItemReceiver를 구현하지 않았습니다. 획득 시 인벤토리에 넣을 수 없습니다.");

            if (dropOnStart)
                TestDrop();
        }

        void Update()
        {
            if (Input.GetKeyDown(dropKey))
                TestDrop();

            if (Input.GetKeyDown(acquireKey))
                TestAcquire();
        }

        [ContextMenu("아이템 드랍 테스트")]
        public void TestDrop()
        {
            if (testItem == null)
            {
                Debug.LogWarning("[ItemDropAcquireTester] testItem이 비어있습니다. 인스펙터에서 연결해주세요.");
                return;
            }

            if (itemGenerator == null)
            {
                Debug.LogWarning("[ItemDropAcquireTester] itemGenerator가 연결되지 않았습니다.");
                return;
            }

            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            lastDropped = itemGenerator.Drop(testItem, pos, rot);
            if (lastDropped == null)
                return;

            lastDropped.upgradeLevel = testUpgradeLevel; // 강화 보너스 확인용 강제 세팅
            Debug.Log("[드랍]\n" + ItemLogFormatter.Build(lastDropped));
        }

        [ContextMenu("아이템 획득 테스트")]
        public void TestAcquire()
        {
            if (lastDropped == null)
            {
                Debug.LogWarning("[ItemDropAcquireTester] 획득할 아이템이 없습니다. 먼저 드랍을 실행하세요.");
                return;
            }

            bool success = ItemAcquisition.Acquire(lastDropped, Receiver);

            // 인벤토리에 성공적으로 들어갔으면 같은 아이템을 중복 획득하지 않도록 참조를 비운다.
            if (success)
                lastDropped = null;
        }
    }
}

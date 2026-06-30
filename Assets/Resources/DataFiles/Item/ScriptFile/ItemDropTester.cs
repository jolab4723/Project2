using System.Text;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 빈 GameObject에 붙이고 testItem에 샘플 ItemDefinitionSO를 연결해서 사용.
    /// Play 시작하면 자동으로 한 번 드랍하고, dropKey를 누르면 itemPickupPrefab을 spawnPoint에 생성하면서
    /// 그 결과를 콘솔에 같이 출력한다.
    /// </summary>
    public class ItemDropTester : MonoBehaviour
    {
        [Header("테스트할 아이템 정의")]
        public ItemDefinitionSO testItem;

        [Header("프리팹 생성")]
        [Tooltip("ItemPickup 컴포넌트가 붙어있는 프리팹")]
        public GameObject itemPickupPrefab;
        [Tooltip("비워두면 이 오브젝트의 위치에 생성")]
        public Transform spawnPoint;

        [Header("테스트 옵션")]
        public bool dropOnStart = true;
        public KeyCode dropKey = KeyCode.Space;
        public int testUpgradeLevel = 0; // 강화 보너스 확인용. 드랍 후 이 값으로 강제 세팅

        void Start()
        {
            if (dropOnStart)
                DropAndSpawn();
        }

        void Update()
        {
            if (Input.GetKeyDown(dropKey))
                DropAndSpawn();
        }

        [ContextMenu("아이템 드랍 테스트")]
        public void DropAndSpawn()
        {
            ItemDefinitionSO def = SelectItemToDrop();
            if (def == null)
            {
                Debug.LogWarning("[ItemDropTester] 드랍할 아이템 정의가 없습니다. testItem을 연결해주세요.");
                return;
            }

            ItemInstance instance = ItemOptionRoller.Generate(def);
            instance.upgradeLevel = testUpgradeLevel;

            SpawnPickup(instance);
            Debug.Log(BuildLog(instance));
        }

        /// <summary>
        /// 어떤 아이템을 드랍시킬지 결정하는 부분.
        /// TODO: 등급/카테고리 확률에 따른 랜덤 드랍 테이블 구현 예정. 지금은 testItem 고정 반환.
        /// </summary>
        ItemDefinitionSO SelectItemToDrop()
        {
            return testItem;
        }

        // 실 아이템 드랍
        void SpawnPickup(ItemInstance instance)
        {
            if (itemPickupPrefab == null)
            {
                Debug.LogWarning("[ItemDropTester] itemPickupPrefab이 비어있습니다. 인스펙터에서 연결해주세요.");
                return;
            }

            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            GameObject obj = Instantiate(itemPickupPrefab, pos, rot);
            var pickup = obj.GetComponent<ItemDataStorage>();

            if (pickup == null)
            {
                Debug.LogWarning("[ItemDropTester] itemPickupPrefab에 ItemPickup 컴포넌트가 없습니다.");
                return;
            }

            pickup.Init(instance);
        }

        string BuildLog(ItemInstance instance)
        {
            var def = instance.definition;
            var sb = new StringBuilder();

            sb.AppendLine("===== 아이템 드랍 결과 =====");
            sb.AppendLine($"이름 : {def.itemName}");
            sb.AppendLine($"분류 : {def.category}");
            sb.AppendLine($"등급 : {def.grade}");
            sb.AppendLine($"강화 수치 : +{instance.upgradeLevel}");

            sb.AppendLine("--- 메인 옵션 (강화 적용) ---");
            var effectiveMain = instance.GetEffectiveMainOptions();
            if (effectiveMain.Count == 0)
            {
                sb.AppendLine("  (없음)");
            }
            else
            {
                foreach (var main in effectiveMain)
                    sb.AppendLine($"  {main.statType} : {main.value:F2}{(main.IsPercent ? "%" : "")}");
            }

            sb.AppendLine("--- 서브 옵션 (랜덤 결과, 속성 보너스 포함) ---");
            if (instance.rolledSubStats.Count == 0)
            {
                sb.AppendLine("  (없음)");
            }
            else
            {
                foreach (var stat in instance.rolledSubStats)
                    sb.AppendLine($"  {stat.statType} : {stat.value:F2}{(stat.IsPercent ? "%" : "")}");
            }

            sb.AppendLine("--- 속성 결과 ---");
            sb.AppendLine($"  rolledElement : {instance.rolledElement}");

            sb.AppendLine("--- 고유 효과 ---");
            sb.AppendLine(def.uniqueEffect != null
                ? $"  {def.uniqueEffect.EffectDescription}"
                : "  (없음)");

            sb.AppendLine("============================");
            return sb.ToString();
        }
    }
}

using System.Collections.Generic;
using ItemSystem;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SW.Test.RandomDrop
{
    /// <summary>
    /// 기존 팀원 드랍/획득 테스트 흐름을 건드리지 않고,
    /// SW/TEST 랜덤 드랍 테이블을 씬의 DropItem / GetItem 버튼에 연결하는 테스트용 어댑터입니다.
    /// </summary>
    public class SwTestRandomDropAcquireTester : MonoBehaviour
    {
        private const string DefaultDropTablePath = "Assets/SW/TEST/RandomDrop/Assets/SwTestDefaultEquipmentDropTable.asset";
        private const string DefaultGeneratedItemFolder = "Assets/SW/TEST/ItemTablePipeline/GeneratedAssets/Items";

        [SerializeField] private WorldItemTooltipScanner worldItemScanner;

        [Header("버튼 연결")]
        [SerializeField] private Button dropItemButton;
        [SerializeField] private Button getItemButton;
        [SerializeField] private string dropButtonName = "btn_DropItem";
        [SerializeField] private string getItemButtonName = "btn_GetItem";
        [SerializeField] private bool bindButtonsOnEnable = false;

        [Header("랜덤 드랍 설정")]
        [SerializeField] private SwTestEquipmentDropTableSO dropTable;
        [SerializeField] private EnemyGrade monsterGrade = EnemyGrade.Normal;
        [SerializeField] private List<ItemDefinitionSO> itemDefinitions = new List<ItemDefinitionSO>();
        [SerializeField] private string generatedItemFolder = DefaultGeneratedItemFolder;

        [Header("기존 아이템 시스템 연결")]
        [SerializeField]private WorldItemDropService worldItemDropService;
        [SerializeField] private GameObject itemPickupPrefabOverride;
        [SerializeField] private MonoBehaviour receiverBehaviour;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private int testUpgradeLevel;
        [SerializeField] private bool destroyPickupAfterAcquire = true;

        [Header("유물 직접 획득 테스트")]
        [SerializeField] private ItemDefinitionSO directRelicDefinition;

        private ItemInstance lastDropped;
        private GameObject lastSpawnedPickup;
        private bool buttonsBound;

        [ContextMenu("SW TEST/유물 직접 인벤토리 획득")]
        public void AcquireDirectRelicToInventory()
        {
            AutoResolveReferences();

            if (directRelicDefinition == null)
            {
                Debug.LogWarning("[SW 유물 테스트] 유물 SO가 연결되지 않았습니다.");
                return;
            }

            if (Receiver == null)
            {
                Debug.LogWarning("[SW 유물 테스트] InventoryController가 연결되지 않았습니다.");
                return;
            }

            if (directRelicDefinition.category != ItemCategory.Relic)
            {
                Debug.LogWarning("[SW 유물 테스트] 연결된 아이템이 유물이 아닙니다.");
                return;
            }

            // SO의 메인 옵션, 서브 옵션 풀, 원소 보너스 설정을
            // 정식 아이템 생성 경로로 전부 반영한다.
            ItemInstance instance =
                ItemDataCreator.CreateItemData(directRelicDefinition);

            if (instance == null)
            {
                Debug.LogWarning("[SW 유물 테스트] 유물 ItemInstance 생성에 실패했습니다.");
                return;
            }

            bool success =
                ItemAcquisition.Acquire(instance, Receiver);

            Debug.Log(
                success
                    ? $"[SW 유물 테스트] {directRelicDefinition.itemName} 획득 성공"
                    : $"[SW 유물 테스트] {directRelicDefinition.itemName} 획득 실패");
        }
        public bool HasLastDroppedItem => lastDropped != null;
private IItemReceiver Receiver => receiverBehaviour as IItemReceiver;

        private void Awake()
        {
            AutoResolveReferences();
        }

private void OnEnable()
        {
            AutoResolveReferences();
            RefreshGetItemButton();

            if (bindButtonsOnEnable)
                BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

[ContextMenu("SW TEST/랜덤 아이템 필드 드랍")]
        public void DropRandomItemToField()
        {
            if (!TryRollRandomItem(out SwTestEquipmentDropResult result))
            {
                RefreshGetItemButton();
                return;
            }

            ItemInstance instance =
                CreateItemInstance(result.itemDefinition);

            if (instance == null)
            {
                Debug.LogWarning(
                    "[SW TEST 랜덤 드랍] 아이템 생성에 실패했습니다.");

                RefreshGetItemButton();
                return;
            }

            instance.upgradeLevel = testUpgradeLevel;

            // 월드 드랍 서비스가 없는 순수 UI 테스트 씬에서도 Get 버튼으로 획득할 수 있도록
            // 생성된 아이템 데이터를 먼저 보관한다.
            lastDropped = instance;
            lastSpawnedPickup = null;

            string actionName = "아이템 데이터 생성";

            // 실제 월드 드랍 서비스가 연결된 씬에서는 같은 아이템 인스턴스를 필드에도 생성한다.
            // 드랍에 실패하더라도 보관한 데이터는 유지해 테스트 획득 흐름이 끊기지 않게 한다.
            if (worldItemDropService != null)
            {
                WorldItemDropResult dropResult =
                    worldItemDropService.TryDrop(
                        instance,
                        out ItemDataStorage spawnedPickup);

                if (dropResult == WorldItemDropResult.Success)
                {
                    lastSpawnedPickup = spawnedPickup.gameObject;
                    actionName = "필드 드랍";
                }
                else
                {
                    Debug.LogWarning(
                        $"[SW TEST 랜덤 드랍] 월드 드롭 실패: {dropResult}. " +
                        "생성된 아이템 데이터는 Get 버튼으로 획득할 수 있습니다.");
                }
            }

            RefreshGetItemButton();

            Debug.Log(
                BuildResultLog(
                    actionName,
                    result,
                    lastDropped));
        }

        [ContextMenu("SW TEST/랜덤 아이템 인벤토리 추가")]
        public void GetRandomItemToInventory()
        {
            if (!TryRollRandomItem(out SwTestEquipmentDropResult result))
                return;

            ItemInstance instance = CreateItemInstance(result.itemDefinition);
            if (instance == null)
            {
                Debug.LogWarning("[SW TEST 랜덤 획득] 아이템 인스턴스 생성에 실패했습니다.");
                return;
            }

            instance.upgradeLevel = testUpgradeLevel;
            bool success = ItemAcquisition.Acquire(instance, Receiver);

            if (success)
                Debug.Log(BuildResultLog("인벤토리 추가", result, instance));
        }

[ContextMenu("SW TEST/마지막 필드 드랍 아이템 획득")]
        public void AcquireLastDroppedItem()
        {
            if (lastDropped == null)
            {
                Debug.LogWarning("[SW TEST 랜덤 획득] 아직 필드에 드랍한 테스트 아이템이 없습니다.");
                RefreshGetItemButton();
                return;
            }

            bool success = ItemAcquisition.Acquire(lastDropped, Receiver);
            if (!success)
            {
                RefreshGetItemButton();
                return;
            }

            if (destroyPickupAfterAcquire && lastSpawnedPickup != null)
                Destroy(lastSpawnedPickup);

            Debug.Log("[SW TEST 랜덤 획득] 마지막으로 필드에 드랍한 아이템을 인벤토리에 추가했습니다.");
            lastDropped = null;
            lastSpawnedPickup = null;
            RefreshGetItemButton();
        }

        public void SetMonsterGradeNormal() => monsterGrade = EnemyGrade.Normal;
        public void SetMonsterGradeAdvanced() => monsterGrade = EnemyGrade.Advanced;
        public void SetMonsterGradeElite() => monsterGrade = EnemyGrade.Elite;
        public void SetMonsterGradeBoss() => monsterGrade = EnemyGrade.Boss;
        public void PrepareRandomItemData()
        {
            if (!TryRollRandomItem(
                    out SwTestEquipmentDropResult result))
            {
                RefreshGetItemButton();
                return;
            }

            ItemInstance instance =
                CreateItemInstance(result.itemDefinition);

            if (instance == null)
            {
                Debug.LogWarning(
                    "[SW TEST 랜덤 드랍] 아이템 데이터 생성에 실패했습니다.");

                RefreshGetItemButton();
                return;
            }

            instance.upgradeLevel = testUpgradeLevel;

            lastDropped = instance;
            lastSpawnedPickup = null;

            RefreshGetItemButton();

            Debug.Log(
                BuildResultLog(
                    "아이템 데이터 생성",
                    result,
                    lastDropped));
        }
        private void BindButtons()
        {
            if (buttonsBound)
                return;

            if (dropItemButton != null)
                dropItemButton.onClick.AddListener(DropRandomItemToField);

            if (getItemButton != null)
                getItemButton.onClick.AddListener(AcquireLastDroppedItem);

            buttonsBound = true;
            RefreshGetItemButton();
        }

private void UnbindButtons()
        {
            if (!buttonsBound)
                return;

            if (dropItemButton != null)
                dropItemButton.onClick.RemoveListener(DropRandomItemToField);

            if (getItemButton != null)
                getItemButton.onClick.RemoveListener(AcquireLastDroppedItem);

            buttonsBound = false;
        }

        private void AutoResolveReferences()
        {
#if UNITY_EDITOR
            if (dropTable == null)
                dropTable = AssetDatabase.LoadAssetAtPath<SwTestEquipmentDropTableSO>(DefaultDropTablePath);

            if (itemDefinitions.Count == 0)
                CollectGeneratedItemDefinitions();
#endif

            if (dropItemButton == null)
                dropItemButton = FindButtonByName(dropButtonName);

            if (getItemButton == null)
                getItemButton = FindButtonByName(getItemButtonName);

            if (worldItemScanner == null)
            {
                worldItemScanner =
                    Object.FindFirstObjectByType<WorldItemTooltipScanner>();
            }

            // 슬롯이 비어 있거나 IItemReceiver가 아닌 컴포넌트가 잘못 연결된 경우,
            // 씬 안의 실제 인벤토리 수신자를 다시 찾아 테스트 획득 흐름을 복구한다.
            if (Receiver == null)
                receiverBehaviour = FindItemReceiverBehaviour();
        }

        private bool TryRollRandomItem(out SwTestEquipmentDropResult result)
        {
            AutoResolveReferences();
            result = SwTestEquipmentDropService.Roll(dropTable, itemDefinitions, monsterGrade);

            if (result.success)
                return true;

            Debug.LogWarning($"[SW TEST 랜덤 드랍] 랜덤 생성 실패: {result.failReason}");
            return false;
        }

        private ItemInstance CreateItemInstance(ItemDefinitionSO definition)
        {
            if (definition == null)
                return null;

            return ItemDataCreator.CreateItemData(definition);
        }

        private string BuildResultLog(string actionName, SwTestEquipmentDropResult result, ItemInstance instance)
        {
            string monsterGradeName = SwTestEquipmentDropService.GetMonsterGradeName(result.monsterGrade);
            string rarityName = SwTestEquipmentDropService.GetRarityName(result.rarity);
            string kindName = SwTestEquipmentDropService.GetItemKindName(result.itemKind);
            string itemName = instance?.definition != null ? instance.definition.itemName : result.itemDefinition.name;

            return $"[SW TEST 랜덤 {actionName}]\n" +
                   $"몬스터 등급: {monsterGradeName}\n" +
                   $"아이템 등급: {rarityName}\n" +
                   $"장비 종류: {kindName}\n" +
                   $"아이템 이름: {itemName}\n" +
                   $"강화 레벨: +{testUpgradeLevel}";
        }

        private static Button FindButtonByName(string buttonName)
        {
            if (string.IsNullOrWhiteSpace(buttonName))
                return null;

            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Button button in buttons)
            {
                if (button != null && button.gameObject.name == buttonName)
                    return button;
            }

            return null;
        }

        private static MonoBehaviour FindItemReceiverBehaviour()
        {
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IItemReceiver)
                    return behaviour;
            }

            return null;
        }

#if UNITY_EDITOR
        [ContextMenu("SW TEST/생성된 아이템 SO 목록 다시 수집")]
        private void CollectGeneratedItemDefinitions()
        {
            itemDefinitions.Clear();

            if (string.IsNullOrWhiteSpace(generatedItemFolder))
                generatedItemFolder = DefaultGeneratedItemFolder;

            string[] guids = AssetDatabase.FindAssets("t:ItemDefinitionSO", new[] { generatedItemFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemDefinitionSO definition = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);

                if (definition == null || definition.category == ItemCategory.Relic)
                    continue;

                itemDefinitions.Add(definition);
            }

            EditorUtility.SetDirty(this);
            Debug.Log($"[SW TEST 랜덤 드랍] 생성된 아이템 SO {itemDefinitions.Count}개를 수집했습니다.");
        }
#endif
    

        public void AcquireNearestWorldItem()
        {
            if (worldItemScanner == null ||
                !worldItemScanner.TryGetCurrentTarget(
                    out ItemDataStorage pickup))
            {
                Debug.LogWarning(
                    "[SW TEST 획득] 감지 범위 안에 아이템이 없습니다.");
                return;
            }

            ItemInstance item = pickup.Item;

            if (!ItemAcquisition.Acquire(item, Receiver))
                return;

            bool wasLastSpawned =
                pickup.gameObject == lastSpawnedPickup;

            Destroy(pickup.gameObject);

            if (wasLastSpawned)
            {
                lastDropped = null;
                lastSpawnedPickup = null;
            }

            RefreshGetItemButton();
        }


        private void RefreshGetItemButton()
        {
            if (getItemButton != null)
                getItemButton.interactable = lastDropped != null || worldItemScanner != null;
        }
}
}

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ItemSystem;

namespace Core
{
    public class DataManager : Singleton<DataManager>, IManagerModule
    {
        [Header("정적 데이터")]
        [Tooltip("모든 아이템 정의를 itemId로 조회할 수 있는 데이터베이스")]
        [SerializeField] private ItemDatabaseSO itemDatabase;

        public ItemDatabaseSO ItemDatabase => itemDatabase;
        public bool IsStaticDataLoaded { get; private set; }
        public string ModuleName => "DataManager";

        private const string GameplaySaveFileName = "gamesave.json";
        private const string InventorySaveFileName = "inventory.json";
        private const string PlayerStatusSaveFileName = "playerstatus.json";
        private const string SkillTreeSaveFileName = "skilltree.json";
        private const string StageSaveFileName = "stage.json";
        private const string ProfileSaveFileName = "profile.json";
        private const string OptionsSaveFileName = "options.json";

        private static string GetSavePath(string fileName)
        {
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        // ===================== 1. 정적 데이터 =====================

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

        // ===================== 2. 플레이어 프로필 =====================

        public void SavePlayerProfile(PlayerProfileData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[DataManager] SavePlayerProfile - data가 null입니다.");
                return;
            }

            data.lastPlayedUtc = System.DateTime.UtcNow.ToString("O");
            WriteJson(GetSavePath(ProfileSaveFileName), data);
        }

        public PlayerProfileData LoadPlayerProfile()
        {
            return ReadJson<PlayerProfileData>(GetSavePath(ProfileSaveFileName));
        }

        // ===================== 3. 게임플레이 데이터 (전체 묶음) =====================

        [ContextMenu("게임플레이 데이터 전체 저장")]
        public void SaveGameplayData()
        {
            var data = new GameSaveData();
            data.status = BuildPlayerStatusData();
            data.inventory = BuildInventorySaveData();
            // skillTree/stage는 시스템이 아직 없어서 기본값(빈 데이터) 그대로 둠

            WriteJson(GetSavePath(GameplaySaveFileName), data);
        }

        [ContextMenu("게임플레이 데이터 전체 불러오기")]
        public void LoadGameplayData()
        {
            var data = ReadJson<GameSaveData>(GetSavePath(GameplaySaveFileName));
            if (data == null)
                return;

            ApplyPlayerStatusData(data.status);
            ApplyInventorySaveData(data.inventory);
            // TODO: 스킬트리/스테이지 시스템이 생기면 여기서 같이 복원
        }

        // ===================== 3-1. 인벤토리 =====================

        [ContextMenu("인벤토리만 저장")]
        public void SaveInventory()
        {
            WriteJson(GetSavePath(InventorySaveFileName), BuildInventorySaveData());
        }

        [ContextMenu("인벤토리만 불러오기")]
        public void LoadInventory()
        {
            var data = ReadJson<InventorySaveData>(GetSavePath(InventorySaveFileName));
            if (data != null)
                ApplyInventorySaveData(data);
        }

        private InventorySaveData BuildInventorySaveData()
        {
            var data = new InventorySaveData();

            if (InventoryController.Instance == null)
            {
                Debug.LogWarning("[DataManager] InventoryController.Instance가 없어 인벤토리를 저장하지 못했습니다 (같은 씬에 있는지 확인).");
                return data;
            }

            if (InventoryController.Instance.PlayerGrid != null)
            {
                foreach (var item in InventoryController.Instance.PlayerGrid.GetAllItems())
                    data.items.Add(ToItemSaveData(item, false, default(EquipSlotType)));
            }

            if (InventoryController.Instance.EquipmentSystem != null)
            {
                foreach (var pair in InventoryController.Instance.EquipmentSystem.GetEquippedItems())
                    data.items.Add(ToItemSaveData(pair.Value, true, pair.Key));
            }

            return data;
        }

        private static ItemSaveData ToItemSaveData(InventoryItem invItem, bool equipped, EquipSlotType slotType)
        {
            var itemData = invItem.itemData;

            var saved = new ItemSaveData();
            saved.instanceId = itemData.instanceId;
            saved.itemId = itemData.definition != null ? itemData.definition.itemId : null;
            saved.rolledSubStats = itemData.rolledSubStats;
            saved.rolledElement = itemData.rolledElement;
            saved.upgradeLevel = itemData.upgradeLevel;
            saved.gridX = invItem.x;
            saved.gridY = invItem.y;
            saved.isRotated = invItem.isRotated;
            saved.isEquipped = equipped;
            saved.equippedSlotType = slotType;
            return saved;
        }

        /// <summary>
        /// 저장된 아이템을 런타임 상태로 불러온다.
        /// 일반 아이템은 InventoryController.TryAddItemAt을 통해
        /// 저장된 좌표에 배치하고 Item UI 생성 이벤트를 발행한다.
        /// 장착 아이템은 EquipmentTransaction을 통해 장비 상태로 복원한다.
        /// </summary>
        private void ApplyInventorySaveData(InventorySaveData data)
        {
            if (data == null)
                return;

            InventoryController controller = InventoryController.Instance;

            if (controller == null || itemDatabase == null ||controller.PlayerGrid == null || controller.EquipmentSystem == null)
            {
                Debug.LogWarning("[DataManager] 인벤토리를 복원하지 못했습니다 (InventoryController 또는 itemDatabase, EquipmentSystem이 없음).");
                return;
            }

            InventoryItemUISpawner itemUISpawner = controller.GetComponent<InventoryItemUISpawner>();

            if (itemUISpawner == null)
            {
                Debug.LogWarning(
                    "[DataManager] InventoryItemUISpawner가 없어 " +
                    "인벤토리 UI를 불러올 수 없습니다.");

                return;
            }
            EquipmentTransaction equipmentTransaction = new EquipmentTransaction(controller.EquipmentSystem);

            int equippedRestoredCount = 0;

            foreach (var saved in data.items)
            {
                var definition = itemDatabase.GetById(saved.itemId);
                if (definition == null)
                    continue; // GetById가 이미 경고를 남김

                var itemInstance = new ItemInstance();
                itemInstance.instanceId = saved.instanceId;
                itemInstance.definition = definition;
                itemInstance.rolledSubStats = saved.rolledSubStats ?? new List<RolledSubStat>();
                itemInstance.rolledElement = saved.rolledElement;
                itemInstance.upgradeLevel = saved.upgradeLevel;

                var invItem = new InventoryItem(itemInstance);
                invItem.isRotated = saved.isRotated;

                if (saved.isEquipped)
                {
                    if (RestoreEquippedItemVisual(
                            controller,
                            itemUISpawner,
                            equipmentTransaction,
                            invItem,
                            saved.equippedSlotType))
                    {
                        equippedRestoredCount++;
                    }
                }
                else
                {
                    InventoryAddResultData loadResult =
                        controller.TryAddItemAt(invItem, saved.gridX, saved.gridY);

                    if (loadResult.Result != InventoryAddResult.Success)
                    {
                        Debug.LogWarning(
                            $"[DataManager] 인벤토리 아이템 불러오기 실패: " +
                            $"{definition.itemName}, " +
                            $"position=({saved.gridX}, {saved.gridY}), " +
                            $"result={loadResult.Result}");

                        continue;
                    }
                }
            }

            Debug.Log("[DataManager] 인벤토리 복원 완료 (" + data.items.Count + "개 아이템, 장착 " + equippedRestoredCount + "개)");
        }

        /// <summary>
        /// 저장된 장착 아이템 하나를 런타임 장비 상태로 복원한다.
        /// ItemUI와 ItemEquipHandler를 먼저 확인한 뒤
        /// EquipmentTransaction을 통해 장비 상태와 이벤트를 반영하고,
        /// 성공한 경우 장비 슬롯 UI에 배치한다.
        /// </summary>
        private bool RestoreEquippedItemVisual(InventoryController controller, InventoryItemUISpawner itemUISpawner,
            EquipmentTransaction transaction, InventoryItem invItem, EquipSlotType slotType)
        {
            if (controller == null || itemUISpawner == null || transaction == null || invItem?.itemData?.definition == null)
            {
                return false;
            }

            EquipSlotUI targetSlot = FindEquipSlot(controller, slotType);

            if (targetSlot == null)
            {
                Debug.LogWarning(
                    $"[DataManager] {slotType} 슬롯을 찾지 못해 " +
                    $"장비를 복원하지 못했습니다: " +
                    $"{invItem.itemData.definition.itemName}");

                return false;
            }

            // 슬롯 점유 여부와 장착 가능한 아이템 종류를
            // 상태 변경 전에 먼저 확인한다.
            if (!targetSlot.CanAccept(invItem.itemData))
            {
                Debug.LogWarning(
                    $"[DataManager] {slotType} 슬롯에 장착할 수 없거나 " +
                    $"이미 UI가 존재합니다: " +
                    $"{invItem.itemData.definition.itemName}");

                return false;
            }

            // 장비 상태를 바꾸기 전에 UI 생성 가능 여부부터 확인한다.
            ItemUI spawnedUI = itemUISpawner.SpawnItemUIAndGet(invItem);

            if (spawnedUI == null)
            {
                Debug.LogWarning(
                    "[DataManager] 장착 아이템 UI 생성 실패: " +
                    invItem.itemData.definition.itemName);

                return false;
            }

            ItemEquipHandler equipHandler = spawnedUI.GetComponent<ItemEquipHandler>();

            if (equipHandler == null)
            {
                Destroy(spawnedUI.gameObject);

                Debug.LogWarning(
                    "[DataManager] 생성된 ItemUI에 " +
                    "ItemEquipHandler가 없습니다: " +
                    invItem.itemData.definition.itemName);

                return false;
            }

            EquipmentTransactionResult result = transaction.TryRestoreEquippedItem(invItem, slotType);

            if (!result.IsSuccess)
            {
                Destroy(spawnedUI.gameObject);

                Debug.LogWarning(
                    $"[DataManager] 장비 복원 실패: " +
                    $"{invItem.itemData.definition.itemName}, " +
                    $"result={result.EquipmentResult.Result}");

                return false;
            }

            // 상태 복원 성공 후 화면에 배치한다.
            equipHandler.SetEquipSlotVisual(targetSlot);

            return true;
        }

        private static EquipSlotUI FindEquipSlot(InventoryController controller, EquipSlotType slotType)
        {
            if (controller == null || controller.allEquipSlots == null)
            {
                return null;
            }

            foreach (EquipSlotUI slot in controller.allEquipSlots)
            {
                if (slot != null &&
                    slot.SlotType == slotType)
                {
                    return slot;
                }
            }

            return null;
        }

        // ===================== 3-2. 스킬트리 (자리만 잡아둠) =====================

        [ContextMenu("스킬트리 저장 (TODO)")]
        public void SaveSkillTree()
        {
            Debug.Log("[DataManager] SaveSkillTree - 스킬트리 시스템이 아직 없어서 빈 데이터만 저장합니다.");
            WriteJson(GetSavePath(SkillTreeSaveFileName), new SkillTreeSaveData());
        }

        [ContextMenu("스킬트리 불러오기 (TODO)")]
        public void LoadSkillTree()
        {
            Debug.Log("[DataManager] LoadSkillTree - 스킬트리 시스템이 아직 없어서 실제로 복원할 데이터가 없습니다.");
        }

        // ===================== 3-3. 플레이어 스테이터스 =====================

        [ContextMenu("플레이어 스테이터스만 저장")]
        public void SavePlayerStatus()
        {
            WriteJson(GetSavePath(PlayerStatusSaveFileName), BuildPlayerStatusData());
        }

        [ContextMenu("플레이어 스테이터스만 불러오기")]
        public void LoadPlayerStatus()
        {
            var data = ReadJson<PlayerStatusData>(GetSavePath(PlayerStatusSaveFileName));
            if (data != null)
                ApplyPlayerStatusData(data);
        }

        private PlayerStatusData BuildPlayerStatusData()
        {
            var data = new PlayerStatusData();

            if (PlayerStatManager.Instance != null)
            {
                data.playerLevel = PlayerStatManager.Instance.Stat.currentLevel;
                data.playerExp = PlayerStatManager.Instance.Stat.currentExp;
            }
            else
            {
                Debug.LogWarning("[DataManager] PlayerStatManager.Instance가 없어 레벨/경험치를 저장하지 못했습니다.");
            }

            if (PlayerHealthManager.Instance != null)
                data.currentHealth = PlayerHealthManager.Instance.CurrentHealth;

            if (PlayerManaManager.Instance != null)
                data.currentMana = PlayerManaManager.Instance.CurrentMana;

            if (InventoryController.Instance != null && InventoryController.Instance.PlayerWallet != null)
                data.gold = InventoryController.Instance.PlayerWallet.Gold;

            return data;
        }

        /// <summary>
        /// 순서 중요: 레벨/경험치를 먼저 반영해 Recalculate로 maxHealth/maxMana를 확정한 뒤,
        /// 그 기준으로 현재 체력/마나를 clamp해서 복원한다.
        /// </summary>
        private void ApplyPlayerStatusData(PlayerStatusData data)
        {
            if (data == null)
                return;

            if (PlayerStatManager.Instance != null)
            {
                PlayerStatManager.Instance.Stat.currentLevel = data.playerLevel;
                PlayerStatManager.Instance.Stat.currentExp = data.playerExp;
                PlayerStatManager.Instance.Recalculate();
            }
            else
            {
                Debug.LogWarning("[DataManager] PlayerStatManager.Instance가 없어 레벨/경험치를 복원하지 못했습니다.");
            }

            if (PlayerHealthManager.Instance != null)
                PlayerHealthManager.Instance.SetCurrentHealth(data.currentHealth);

            if (PlayerManaManager.Instance != null)
                PlayerManaManager.Instance.SetCurrentMana(data.currentMana);

            if (InventoryController.Instance != null && InventoryController.Instance.PlayerWallet != null)
                InventoryController.Instance.PlayerWallet.SetGold(data.gold);
        }

        // ===================== 3-4. 스테이지 데이터 (자리만 잡아둠) =====================

        [ContextMenu("스테이지 데이터 저장 (TODO)")]
        public void SaveStageData()
        {
            Debug.Log("[DataManager] SaveStageData - 스테이지 시스템이 아직 없어서 빈 데이터만 저장합니다.");
            WriteJson(GetSavePath(StageSaveFileName), new StageSaveData());
        }

        [ContextMenu("스테이지 데이터 불러오기 (TODO)")]
        public void LoadStageData()
        {
            Debug.Log("[DataManager] LoadStageData - 스테이지 시스템이 아직 없어서 실제로 복원할 데이터가 없습니다.");
        }

        // ===================== 4. 시스템 옵션 =====================

        public void SaveSystemOptions(SystemOptionsData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[DataManager] SaveSystemOptions - data가 null입니다.");
                return;
            }

            WriteJson(GetSavePath(OptionsSaveFileName), data);
        }

        /// <summary>옵션 파일이 없으면(최초 실행 등) 기본값을 반환한다.</summary>
        public SystemOptionsData LoadSystemOptions()
        {
            var data = ReadJson<SystemOptionsData>(GetSavePath(OptionsSaveFileName));
            return data != null ? data : new SystemOptionsData();
        }

        // ===================== 공용 JSON 파일 입출력 =====================

        private static void WriteJson<T>(string path, T data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            Debug.Log("[DataManager] 저장 완료: " + path);
        }

        private static T ReadJson<T>(string path) where T : class
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("[DataManager] 파일이 없습니다: " + path);
                return null;
            }

            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<T>(json);
        }
    }
}

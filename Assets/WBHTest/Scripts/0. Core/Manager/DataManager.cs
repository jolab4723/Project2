using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ItemSystem;

namespace Core
{
    public class DataManager : Singleton<DataManager>, IManagerModule
    {
        public string ModuleName => "DataManager";

        private const string GameplaySaveFileName = "gamesave.json";
        private const string InventorySaveFileName = "inventory.json";
        private const string PlayerStatusSaveFileName = "playerstatus.json";
        private const string SkillTreeSaveFileName = "skilltree.json";
        private const string StageSaveFileName = "stage.json";
        private const string ActiveSkillSaveFileName = "activeskill.json";
        private const string QuestSaveFileName = "quest.json";
        private const string OptionsSaveFileName = "options.json";

        private const string SinglePlayerSlotFileName = "profile_singleplayer.json";
        private const int MultiplayerSlotCount = 3;

        private static string MultiplayerSlotFileName(int slotIndex) => "profile_multiplayer_" + slotIndex + ".json";

        private static string GetSavePath(string fileName)
        {
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        public void Activate()
        {
            Debug.Log("[DataManager] 활성화 완료.");
        }

        #region ===================== 2. 플레이어 프로필 (세이브 슬롯) =====================
        // 싱글플레이 슬롯 1개 + 멀티플레이 슬롯 3개(다크소울 스타일, 서로 독립).
        // 멀티플레이 슬롯은 공유 파티 세이브 - 게스트 프로필도 전부 호스트 슬롯 안에 통째로 저장된다.

        /// <summary>새 플레이어 프로필용 고유 ID를 생성한다 (기기 로컬 GUID, 계정 시스템 없음).</summary>
        public static string GenerateNewPlayerId()
        {
            return System.Guid.NewGuid().ToString();
        }

        [ContextMenu("싱글플레이 슬롯 저장")]
        public void SaveSinglePlayerSlot(SinglePlayerSlotData data)
        {
            if (data == null || data.profile == null)
            {
                Debug.LogWarning("[DataManager] SaveSinglePlayerSlot - data 또는 profile이 null입니다.");
                return;
            }

            data.profile.lastPlayedUtc = System.DateTime.UtcNow.ToString("O");
            WriteJson(GetSavePath(SinglePlayerSlotFileName), data);
        }

        public SinglePlayerSlotData LoadSinglePlayerSlot()
        {
            return ReadJson<SinglePlayerSlotData>(GetSavePath(SinglePlayerSlotFileName));
        }

        /// <summary>slotIndex: 0~2 (멀티플레이 슬롯 3개 중 하나). 호스트가 참가자 전원의 데이터를 이 한 번의 호출로 저장한다.</summary>
        public void SaveMultiplayerSlot(int slotIndex, MultiplayerSlotData data)
        {
            if (!IsValidMultiplayerSlotIndex(slotIndex))
                return;

            if (data == null || data.hostProfile == null)
            {
                Debug.LogWarning("[DataManager] SaveMultiplayerSlot - data 또는 hostProfile이 null입니다.");
                return;
            }

            data.hostProfile.lastPlayedUtc = System.DateTime.UtcNow.ToString("O");
            WriteJson(GetSavePath(MultiplayerSlotFileName(slotIndex)), data);
        }

        public MultiplayerSlotData LoadMultiplayerSlot(int slotIndex)
        {
            if (!IsValidMultiplayerSlotIndex(slotIndex))
                return null;

            return ReadJson<MultiplayerSlotData>(GetSavePath(MultiplayerSlotFileName(slotIndex)));
        }

        private static bool IsValidMultiplayerSlotIndex(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MultiplayerSlotCount)
            {
                Debug.LogWarning($"[DataManager] 멀티플레이 슬롯 인덱스는 0~{MultiplayerSlotCount - 1}만 유효합니다: {slotIndex}");
                return false;
            }

            return true;
        }

        #endregion

        #region ===================== 2-1. 패시브 스킬 프로필 (저장/불러오기 전담) =====================
        [ContextMenu("패시브 데이터 저장")]
        public void SavePassiveData()
        {
            if (PassiveSkillManager.Instance == null || PassiveSkillManager.Instance.CurrentProfile == null)
            {
                Debug.LogWarning("[DataManager] SavePassiveData - PassiveSkillManager 또는 CurrentProfile이 없습니다.");
                return;
            }

            SaveSinglePlayerSlot(new SinglePlayerSlotData { profile = PassiveSkillManager.Instance.CurrentProfile });
        }

        /// <summary>
        /// 싱글플레이 슬롯에서 프로필을 불러와 PassiveSkillManager에 활성 프로필로 설정한다.
        /// 저장된 슬롯이 없으면 새 프로필을 만들어서 설정하고 false를 반환한다(진짜 첫 실행 여부 판단용).
        /// </summary>
        [ContextMenu("패시브 데이터 로드")]
        public bool LoadPassiveData()
        {
            if (PassiveSkillManager.Instance == null)
            {
                Debug.LogWarning("[DataManager] LoadPassiveData - PassiveSkillManager.Instance가 없습니다.");
                return false;
            }

            var slot = LoadSinglePlayerSlot();
            if (slot != null && slot.profile != null)
            {
                PassiveSkillManager.Instance.SetActiveProfile(slot.profile);
                return true;
            }

            PassiveSkillManager.Instance.SetActiveProfile(new PlayerProfileData { playerId = GenerateNewPlayerId() });
            return false;
        }

        /// <summary>
        /// 런 종료 시 호출. 현재 인벤토리의 골드(PlayerWallet.Gold)를 profile의 영구 크레딧에 더하고,
        /// 인게임 골드는 0으로 초기화한다. 실제로 언제 부를지(스테이지 클리어/사망/메뉴 복귀 등)는 호출부에서 결정.
        /// 여기서는 이전만 하고 파일 저장은 안 함 - 필요하면 호출부에서 SaveSinglePlayerSlot/SaveMultiplayerSlot을 이어서 불러야 함.
        /// </summary>
        public void TransferRunGoldToProfile(PlayerProfileData profile)
        {
            if (profile == null)
            {
                Debug.LogWarning("[DataManager] TransferRunGoldToProfile - profile이 null입니다.");
                return;
            }

            if (InventoryController.Instance == null || InventoryController.Instance.PlayerWallet == null)
            {
                Debug.LogWarning("[DataManager] TransferRunGoldToProfile - PlayerWallet을 찾을 수 없어 골드를 이전하지 못했습니다.");
                return;
            }

            int runGold = InventoryController.Instance.PlayerWallet.Gold;
            if (runGold <= 0)
                return;

            profile.credit += runGold;
            InventoryController.Instance.PlayerWallet.SetGold(0);

            Debug.Log("[DataManager] 런 골드 " + runGold + " 이전 완료. 프로필 영구 크레딧 = " + profile.credit);
        }

        #endregion

        #region ===================== 3. 게임플레이 데이터 =====================

        [ContextMenu("게임플레이 데이터 전체 세아브")]
        public void SaveGameplayData()
        {
            if ( ! TryGetGameplayPlayer(out var stats, out var health, out _) || stats.Stat == null
                                                                              || stats.Stat.currentLevel < 1
                                                                              || health.MaxHealth <= 0f)
            {
                Debug.LogWarning("[DataManager] 플레이어가 준비되지 않아 저장하지 않습니다.");
                return;
            }

            var data = new GameSaveData();
            data.status = BuildPlayerStatusData();
            data.inventory = BuildInventorySaveData();
            data.activeSkill = BuildActiveSkillSaveData();
            // TODO : 스킬트리 데이터 세이브
            // TODO : 스테이지 데이터 세이브

            WriteJson(GetSavePath(GameplaySaveFileName), data);
        }

        [ContextMenu("게임플레이 데이터 전체 로드")]
        public void LoadGameplayData()
        {
            TryLoadGameplayData();
        }

        // 플레이어와 관련 시스템의 Start 초기화가 끝난 뒤 호출합니다.
        public bool TryLoadGameplayData()
        {
            if ( ! TryGetGameplayPlayer(out var stats, out var health, out var mana))
            {
                Debug.LogWarning("[DataManager] 활성 플레이어가 없어 로드를 보류합니다.");
                return false;
            }

            string path = GetSavePath(GameplaySaveFileName);

            // 읽기 오류나 손상된 JSON은 새 게임으로 덮어쓰지 않습니다.
            GameSaveData data;
            try
            {
                data = ReadJson<GameSaveData>(path);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[DataManager] 게임플레이 데이터 읽기 실패: {exception.Message}");
                return false;
            }

            if (data == null && File.Exists(path))
            {
                Debug.LogError("[DataManager] 저장 파일의 내용이 유효하지 않습니다.");
                return false;
            }

            bool isNewGame = data == null ||
                             data.needsPlayerInitialization ||
                             data.status == null ||
                             data.status.playerLevel < 1;

            if (isNewGame)
            {
                // 이전의 레벨 0 초기화 데이터도 정상적인 새 게임으로 전환합니다.
                data = new GameSaveData{needsPlayerInitialization = true};
                data.status.playerLevel = 1;
                data.status.playerExp = 0f;
            }

            // 1. 레벨과 경험치 적용
            var stat = stats.EnsureInitialized();
            stat.currentLevel = data.status.playerLevel;
            stat.currentExp = data.status.playerExp;

            // 장비 복원 과정에서 스탯을 조회할 수 있으므로 먼저 계산합니다.
            stats.Recalculate();

            // 2. 이어하기일 때 저장된 장비·스킬 복원
            // 새 게임은 새로 생성된 씬/프리팹의 초기 상태를 사용합니다.
            if ( ! isNewGame)
            {
                ApplyInventorySaveData(data.inventory);
                ApplyActiveSkillSaveData(data.activeSkill);
            }

            if (InventoryController.Instance != null &&
                InventoryController.Instance.PlayerWallet != null)
            {
                InventoryController.Instance.PlayerWallet.SetGold(isNewGame ? 0 : data.status.gold);
            }

            // 3. 장비·스킬 적용 후 최종 최대치 갱신
            stats.Recalculate();
            health.RefreshMaxHealth();
            mana.RefreshMaxMana();

            if (health.MaxHealth <= 0f)
            {
                Debug.LogError("[DataManager] 최대 체력이 0 이하입니다. 기본 스탯 연결을 확인하세요.");
                return false;
            }

            // 4. 현재 체력·마나 적용
            if (isNewGame)
            {
                health.FillHealth();
                mana.FillMana();

                // 실제 초기화된 스탯을 저장하여 다음 씬부터 이어서 진행합니다.
                data.status.playerLevel = stat.currentLevel;
                data.status.playerExp = stat.currentExp;
                data.status.currentHealth = health.CurrentHealth;
                data.status.currentMana = mana.CurrentMana;
                data.status.gold = 0;
                data.needsPlayerInitialization = false;

                try
                {
                    WriteJson(path, data);
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"[DataManager] 새 게임 초기 상태 저장 실패: {exception.Message}");
                    return false;
                }
            }
            else
            {
                health.SetCurrentHealth(data.status.currentHealth);
                mana.SetCurrentMana(data.status.currentMana);
            }

            return true;
        }


        #endregion

        #region ===================== 3-1. 인벤토리 =====================

        [ContextMenu("인벤토리만 세이브")]
        public void SaveInventory()
        {
            WriteJson(GetSavePath(InventorySaveFileName), BuildInventorySaveData());
        }

        [ContextMenu("인벤토리만 로드")]
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
            saved.persistedStackCount = itemData.persistedStackCount;
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
            ItemDatabaseSO itemDatabase = ItemManager.Instance != null ? ItemManager.Instance.ItemDatabase : null;

            if (controller == null || itemDatabase == null || controller.PlayerGrid == null || controller.EquipmentSystem == null)
            {
                Debug.LogWarning("[DataManager] 인벤토리를 복원하지 못했습니다 (InventoryController 또는 ItemManager.ItemDatabase, EquipmentSystem이 없음).");
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
                itemInstance.persistedStackCount = saved.persistedStackCount;

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
                    $"result={result.Result}");

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

        #endregion

        #region ===================== 3-2. 스킬트리 (TODO) =====================

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

        #endregion

        #region ===================== 3-3. 플레이어 스테이터스 =====================

        [ContextMenu("플레이어 스테이터스만 세이브")]
        public void SavePlayerStatus()
        {
            WriteJson(GetSavePath(PlayerStatusSaveFileName), BuildPlayerStatusData());
        }

        [ContextMenu("플레이어 스테이터스만 로드")]
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
            {
                PlayerHealthManager.Instance.RefreshMaxHealth();
                PlayerHealthManager.Instance.SetCurrentHealth(data.currentHealth);
            }

            if (PlayerManaManager.Instance != null)
            {
                PlayerManaManager.Instance.RefreshMaxMana();
                PlayerManaManager.Instance.SetCurrentMana(data.currentMana);
            }

            if (InventoryController.Instance != null && InventoryController.Instance.PlayerWallet != null)
                InventoryController.Instance.PlayerWallet.SetGold(data.gold);
        }

        #endregion

        #region ===================== 3-4. 스테이지 데이터 (TODO) =====================

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

        #endregion

        #region ===================== 3-5. 액티브 스킬 진화/강화 =====================
        // Skill1~3(A/S/D)의 진화(SkillEvolutionId)/강화(SkillEnhancementId) 선택 상태를 저장/복원한다.
        // ISkillController(106번에서 뽑아낸 캐릭터 클래스 무관 인터페이스)만 바라봐서, 지금은
        // FighterSkillController가 유일한 구현체지만 나중에 거너 컨트롤러가 생겨도 이 코드는 그대로 쓴다.

        [ContextMenu("액티브 스킬 진화/강화 저장")]
        public void SaveActiveSkillData()
        {
            WriteJson(GetSavePath(ActiveSkillSaveFileName), BuildActiveSkillSaveData());
        }

        [ContextMenu("액티브 스킬 진화/강화 불러오기")]
        public void LoadActiveSkillData()
        {
            var data = ReadJson<ActiveSkillSaveData>(GetSavePath(ActiveSkillSaveFileName));
            if (data != null)
                ApplyActiveSkillSaveData(data);
        }

        private ActiveSkillSaveData BuildActiveSkillSaveData()
        {
            var data = new ActiveSkillSaveData();

            ISkillController controller = FindActiveSkillController();
            if (controller == null)
            {
                Debug.LogWarning("[DataManager] BuildActiveSkillSaveData - ISkillController를 찾을 수 없어 빈 데이터를 저장합니다.");
                return data;
            }

            data.evolutions = new SkillEvolutionId[controller.SkillCount];
            data.enhancements = new SkillEnhancementId[controller.SkillCount];
            for (int i = 0; i < controller.SkillCount; i++)
            {
                data.evolutions[i] = controller.GetEvolution(i);
                data.enhancements[i] = controller.GetEnhancement(i);
            }

            return data;
        }

        private void ApplyActiveSkillSaveData(ActiveSkillSaveData data)
        {
            if (data == null || data.evolutions == null || data.enhancements == null)
                return;

            ISkillController controller = FindActiveSkillController();
            if (controller == null)
            {
                Debug.LogWarning("[DataManager] ApplyActiveSkillSaveData - ISkillController를 찾을 수 없어 복원하지 못했습니다.");
                return;
            }

            int count = Mathf.Min(controller.SkillCount, Mathf.Min(data.evolutions.Length, data.enhancements.Length));
            for (int i = 0; i < count; i++)
            {
                controller.SetEvolution(i, data.evolutions[i]);
                controller.SetEnhancement(i, data.enhancements[i]);
            }
        }

        /// <summary>씬에 있는 활성 캐릭터의 ISkillController를 찾는다. 실제 스캔 로직은
        /// ActiveSkillControllerLocator로 뺐다(118번) - SkillEvolutionSelectUI(K키 설정창)도 같은
        /// 로직이 필요해져서 공용 유틸로 공유한다.</summary>
        private static ISkillController FindActiveSkillController() => ActiveSkillControllerLocator.Find();

        #endregion

        #region ===================== 3-6. 퀘스트 =====================
        // 진행 중/완료된 퀘스트 목록(ActiveQuestData)을 저장/복원한다. 디자인 데이터(조건/보상)는
        // QuestDatabaseSO에 있고 questId로만 연결하므로 여기선 진행 상태만 다룬다.

        [ContextMenu("퀘스트 저장")]
        public void SaveQuestData()
        {
            if (QuestManager.Instance == null)
            {
                Debug.LogWarning("[DataManager] SaveQuestData - QuestManager.Instance가 없습니다.");
                return;
            }

            WriteJson(GetSavePath(QuestSaveFileName), QuestManager.Instance.GetSaveData());
        }

        [ContextMenu("퀘스트 불러오기")]
        public void LoadQuestData()
        {
            if (QuestManager.Instance == null)
            {
                Debug.LogWarning("[DataManager] LoadQuestData - QuestManager.Instance가 없습니다.");
                return;
            }

            var data = ReadJson<QuestSaveData>(GetSavePath(QuestSaveFileName));
            if (data != null)
                QuestManager.Instance.ApplySaveData(data);
        }

        #endregion

        #region ===================== 4. 시스템 옵션 =====================
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

        #endregion

        #region ===================== 5. 전체 데이터 초기화 =====================
        // !! 플레이어/패시브 초기화는 PassiveSkillManager.CurrentProfile도 같이 갱신해준다.
        //    안 그러면 초기화 이후 아무 패시브 레벨이나 바꿀 때 자동 저장(SavePassiveData)이
        //    예전 CurrentProfile을 다시 파일에 덮어써서 초기화가 무효화된다.

        /// <summary> 모든 데이터를 전부 기본값으로 초기화 </summary>
        [ContextMenu("전체 데이터 초기화")]
        public void ResetAllData()
        {
            ResetPlayerProfile();
            ResetPassiveData();
            ResetGameplayData();

            Debug.Log("[DataManager] 전체 데이터를 기본값으로 초기화했습니다.");
        }

        /// <summary>플레이어 프로필(골드/이름/플레이타임 등, 패시브 트리 포함)을 완전히 새 프로필로 되돌려서 저장한다.</summary>
        [ContextMenu("플레이어 데이터 초기화")]
        public void ResetPlayerProfile()
        {
            var profile = new PlayerProfileData { playerId = GenerateNewPlayerId() };

            if (PassiveSkillManager.Instance != null)
                PassiveSkillManager.Instance.SetActiveProfile(profile);

            SaveSinglePlayerSlot(new SinglePlayerSlotData { profile = profile });
        }

        /// <summary>현재 프로필은 그대로 두고 패시브 스킬트리(해금/적용 레벨)만 기본값(빈 트리)으로 되돌려서 저장한다.</summary>
        [ContextMenu("패시브 데이터 초기화")]
        public void ResetPassiveData()
        {
            var profile = PassiveSkillManager.Instance != null ? PassiveSkillManager.Instance.CurrentProfile : null;
            if (profile == null)
                profile = new PlayerProfileData { playerId = GenerateNewPlayerId() };
            else
                profile.passiveSkillTree = new PassiveSkillTreeData();

            if (PassiveSkillManager.Instance != null)
                PassiveSkillManager.Instance.SetActiveProfile(profile);

            SaveSinglePlayerSlot(new SinglePlayerSlotData { profile = profile });
        }

        /// <summary>
        /// 게임플레이 데이터(인벤토리+스테이터스 묶음, gamesave.json)를 빈 기본값으로 되돌려서 저장한다.
        /// 현재 씬에서 돌고 있는 인벤토리/스테이터스 자체는 안 건드리고 파일만 초기화한다 (다음 로드 시 반영됨).
        /// </summary>
        [ContextMenu("게임플레이 데이터 초기화")]
        public void ResetGameplayData()
        {
            var data = new GameSaveData
            {
                needsPlayerInitialization = true
            };

            data.status.playerLevel = 1;
            data.status.playerExp = 0f;

            WriteJson(GetSavePath(GameplaySaveFileName), data);
        }
        private bool TryGetGameplayPlayer(out PlayerStatManager stats, out PlayerHealthManager health, out PlayerManaManager mana)
        {
            stats = PlayerStatManager.Instance;
            health = null;
            mana = null;

            if (stats == null || !stats.isActiveAndEnabled)
                return false;

            health = stats.GetComponent<PlayerHealthManager>();
            mana = stats.GetComponent<PlayerManaManager>();

            return health != null && health.isActiveAndEnabled && mana != null && mana.isActiveAndEnabled;
        }
        #endregion

        #region ===================== 공용 JSON 파일 입출력 =====================

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

        #endregion
    }
}

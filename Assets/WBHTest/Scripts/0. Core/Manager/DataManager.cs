using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using ItemSystem;

namespace Core
{
    /// <summary>싱글 런이 끝난 방식. 계정으로 옮기는 크레딧 규칙이 달라진다(DataManager.CalculateRunEndCredits).</summary>
    public enum RunEndReason
    {
        /// <summary>Act3 최종 보스 클리어.</summary>
        Clear,
        /// <summary>액트를 1개 이상 클리어한 뒤 비전투 상황에서 일시정지 메뉴의 정산으로 종료.</summary>
        Settle,
        /// <summary>사망.</summary>
        Death
    }

    public class DataManager : Singleton<DataManager>, IManagerModule
    {
        public string ModuleName => "DataManager";
        private int loadedGameplaySceneHandle = -1;
        public bool IsRestoringGameplay { get; private set; }
        public bool IsGameplayReady =>
            loadedGameplaySceneHandle == UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;

        private const string GameplaySaveFileName = "gamesave.json";
        private const string InventorySaveFileName = "inventory.json";
        private const string PlayerStatusSaveFileName = "playerstatus.json";
        private const string SkillTreeSaveFileName = "skilltree.json";
        private const string StageSaveFileName = "stage.json";
        private const string ActiveSkillSaveFileName = "activeskill.json";
        private const string QuestSaveFileName = "quest.json";
        private const string OptionsSaveFileName = "options.json";

        private const string SinglePlayerSlotFileName = "profile_singleplayer.json";
        private const string SinglePlayerProfileOwnerFileName = "profile_singleplayer_owner.json";
        private const int MultiplayerSlotCount = 3;

        [System.Serializable]
        private sealed class SinglePlayerProfileOwnerData
        {
            public string firebaseUserId;
        }

        private static string MultiplayerSlotFileName(int slotIndex) => "profile_multiplayer_" + slotIndex + ".json";

        // SW 수정 : 로그인 상태와 로컬 저장의 소유자를 분리해 로그아웃·오프라인에서도 같은 진행도를 사용한다.
        private static string LocalProfileUserId
        {
            get
            {
                if (FirebaseService.Default.IsLocalTestAccount)
                    return FirebaseService.Default.LocalTestUserId;

                string path = Path.Combine(Application.persistentDataPath, SinglePlayerProfileOwnerFileName);
                return File.Exists(path)
                    ? ReadJson<SinglePlayerProfileOwnerData>(path)?.firebaseUserId ?? string.Empty
                    : string.Empty;
            }
        }

        private static string GetSavePath(string fileName)
        {
            // SW 수정: 계정의 런/퀘스트 작업 파일을 분리하고 기기 옵션은 기존 위치를 유지합니다.
            if (fileName is SinglePlayerSlotFileName or GameplaySaveFileName or QuestSaveFileName or
                InventorySaveFileName or PlayerStatusSaveFileName or SkillTreeSaveFileName or StageSaveFileName or ActiveSkillSaveFileName)
                return GetAccountSavePath(fileName, LocalProfileUserId);
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        private static string GetAccountSavePath(string fileName, string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return Path.Combine(Application.persistentDataPath, fileName);
            if (userId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || userId is "." or "..")
                throw new System.InvalidOperationException("유효하지 않은 저장 계정입니다.");

            string directory = Path.Combine(Application.persistentDataPath, "PlayerSaves", userId);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
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

            // 다른 계정(또는 로그아웃 상태)에서 불러온 메모리 프로필을 현재 계정에 덮어쓰지 않는다.
            if (IsPassiveProfileFromOtherAccount && ReferenceEquals(data.profile, PassiveSkillManager.Instance.CurrentProfile))
                throw new System.InvalidOperationException("다른 계정에서 불러온 프로필은 저장할 수 없습니다.");

            data.profile.lastPlayedUtc = System.DateTime.UtcNow.ToString("O");
            QueueProfileSave(data.profile);
            WriteJson(GetSavePath(SinglePlayerSlotFileName), data);
        }

        public SinglePlayerSlotData LoadSinglePlayerSlot()
        {
            return ReadJson<SinglePlayerSlotData>(GetSavePath(SinglePlayerSlotFileName));
        }

        /// <summary>
        /// 로그인한 사용자의 revision으로 확정된 프로필·게임·퀘스트를 계정별 작업 파일에 반영합니다.
        /// </summary>
        public static async Task<SaveDataOperationResult> SynchronizeSinglePlayerProfileWithFirebaseAsync()
        {
            try
            {
                var result = await SynchronizePlayerAccountCoreAsync();
                // 로그인 전(로그아웃 상태)에 불러온 전역 프로필이 메모리에 남지 않도록 이 계정의 프로필로 교체한다.
                if (result.IsSuccess)
                {
                    Instance?.LoadPassiveData();
                    Instance?.LoadQuestData();
                }
                return result;
            }
            catch (System.Exception exception)
            {
                return SaveDataOperationResult.Failure(SaveDataFailureReason.FileAccessFailed,
                    $"계정 저장을 복원하지 못했습니다: {exception.Message}");
            }
        }

        /// <summary>SW 수정 : 프로필·게임·퀘스트 동기화가 모두 끝난 뒤 로컬 저장의 소유 계정을 전환합니다.</summary>
        private static async Task<SaveDataOperationResult> SynchronizePlayerAccountCoreAsync()
        {
            string uid = FirebaseService.Default.CurrentUserId;
            var owner = ReadJson<SinglePlayerProfileOwnerData>(GetSavePath(SinglePlayerProfileOwnerFileName));
            bool canMigrate = !string.IsNullOrEmpty(uid) && owner?.firebaseUserId == uid;
            var profile = await SynchronizeProfileCoreAsync();
            if (!profile.IsSuccess)
                return profile;
            if (!profile.IsCloudSynchronized && !FirebaseService.Default.IsLocalTestAccount)
                return SaveDataOperationResult.Failure(SaveDataFailureReason.CloudAccessFailed,
                    "로컬 진행도는 보존했습니다. 인터넷 연결을 확인하고 멀티플레이 동기화를 다시 시도해 주세요.");
            if (uid != FirebaseService.Default.CurrentUserId)
                return SaveDataOperationResult.Failure(SaveDataFailureReason.AuthenticationRequired, "로그인 계정이 변경되었습니다.");
            if (string.IsNullOrEmpty(owner?.firebaseUserId))
            {
                var legacy = ReadJson<SinglePlayerSlotData>(Path.Combine(Application.persistentDataPath, SinglePlayerSlotFileName));
                var adopted = ReadJson<SinglePlayerSlotData>(GetAccountSavePath(SinglePlayerSlotFileName, uid));
                canMigrate = !string.IsNullOrEmpty(legacy?.profile?.playerId) && legacy.profile.playerId == adopted?.profile?.playerId;
            }
            if (canMigrate)
            {
                foreach (string fileName in new[]
                {
                    InventorySaveFileName, PlayerStatusSaveFileName, SkillTreeSaveFileName,
                    StageSaveFileName, ActiveSkillSaveFileName
                })
                {
                    string legacyPath = Path.Combine(Application.persistentDataPath, fileName);
                    string target = GetAccountSavePath(fileName, uid);
                    if (!File.Exists(target) && File.Exists(legacyPath))
                        File.Copy(legacyPath, target);
                }
            }

            foreach (var category in new[] { SaveDataCategory.Gameplay, SaveDataCategory.Quest })
            {
                if (uid != FirebaseService.Default.CurrentUserId)
                    return SaveDataOperationResult.Failure(SaveDataFailureReason.AuthenticationRequired, "로그인 계정이 변경되었습니다.");
                var result = await SynchronizePlayerDataAsync(category, uid, canMigrate);
                if (!result.IsSuccess)
                    return result;
                if (!result.IsCloudSynchronized && !FirebaseService.Default.IsLocalTestAccount)
                    return SaveDataOperationResult.Failure(SaveDataFailureReason.CloudAccessFailed,
                        "로컬 진행도는 보존했습니다. 멀티플레이 동기화가 끝나지 않아 진입하지 않았습니다.");
            }
            // 모든 종류의 동기화가 끝난 뒤에만 로컬 프로필을 전환한다. 다른 계정의 파일은 그대로 보존한다.
            if (uid != FirebaseService.Default.CurrentUserId)
                return SaveDataOperationResult.Failure(SaveDataFailureReason.AuthenticationRequired, "로그인 계정이 변경되었습니다.");
            if (!FirebaseService.Default.IsLocalTestAccount)
                WriteSinglePlayerProfileOwner(GetSavePath(SinglePlayerProfileOwnerFileName), uid);
            return profile;
        }

        /// <summary>SW 수정: 기존 프로필 이관을 수행하며 게임·퀘스트 연결은 상위 로그인 경계에서 기다립니다.</summary>
        private static async Task<SaveDataOperationResult> SynchronizeProfileCoreAsync()
        {
            string firebaseUserId = FirebaseService.Default.CurrentUserId;
            if (string.IsNullOrWhiteSpace(firebaseUserId))
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.AuthenticationRequired,
                    "로그인한 Firebase 사용자가 없습니다.");
            }

            string localPath = GetAccountSavePath(SinglePlayerSlotFileName, firebaseUserId);
            string ownerPath = GetSavePath(SinglePlayerProfileOwnerFileName);
            SinglePlayerSlotData localSlot;
            SinglePlayerProfileOwnerData ownerData;
            try
            {
                ownerData = ReadJson<SinglePlayerProfileOwnerData>(ownerPath);
                bool belongsToAnotherUser =
                    !string.IsNullOrWhiteSpace(ownerData?.firebaseUserId) &&
                    ownerData.firebaseUserId != firebaseUserId;
                localSlot = ReadJson<SinglePlayerSlotData>(localPath);
                if (localSlot == null && !belongsToAnotherUser)
                    localSlot = ReadJson<SinglePlayerSlotData>(Path.Combine(Application.persistentDataPath, SinglePlayerSlotFileName));
            }
            catch (System.Exception exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.SerializationFailed,
                    $"로컬 프로필을 읽지 못했습니다: {exception.Message}");
            }

            var pending = await FlushPendingPlayerSaveAsync(SaveDataCategory.PlayerProfile, firebaseUserId);
            if (!pending.IsSuccess) return pending;
            SaveDataReadResult cloudResult = await SaveDataService.Default.LoadAsync(
                SaveDataCategory.PlayerProfile, firebaseUserId);
            if (firebaseUserId != FirebaseService.Default.CurrentUserId)
                return SaveDataOperationResult.Failure(SaveDataFailureReason.AuthenticationRequired, "로그인 계정이 변경되었습니다.");
            if (cloudResult.IsSuccess)
            {
                PlayerProfileData cloudProfile;
                try
                {
                    cloudProfile = JsonUtility.FromJson<PlayerProfileData>(
                        cloudResult.Envelope.payloadJson);
                }
                catch (System.Exception exception)
                {
                    return SaveDataOperationResult.Failure(
                        SaveDataFailureReason.SerializationFailed,
                        $"Firestore 프로필을 변환하지 못했습니다: {exception.Message}");
                }

                if (cloudProfile == null)
                {
                    return SaveDataOperationResult.Failure(
                        SaveDataFailureReason.SerializationFailed,
                        "Firestore 프로필의 내용이 비어 있습니다.");
                }

                try
                {
                    localSlot ??= new SinglePlayerSlotData();
                    localSlot.profile = cloudProfile;
                    WriteJson(localPath, localSlot);
                }
                catch (System.Exception exception)
                {
                    return SaveDataOperationResult.Failure(
                        SaveDataFailureReason.FileAccessFailed,
                        $"Firebase 프로필을 로컬에 반영하지 못했습니다: {exception.Message}");
                }

                return SaveDataOperationResult.Success(
                    cloudResult.IsCloudSynchronized,
                    cloudResult.Message);
            }

            if (localSlot?.profile == null &&
                cloudResult.FailureReason == SaveDataFailureReason.NotFound)
            {
                localSlot = new SinglePlayerSlotData
                {
                    profile = new PlayerProfileData
                    {
                        playerId = GenerateNewPlayerId(),
                        lastPlayedUtc = System.DateTime.UtcNow.ToString("O")
                    }
                };

                try
                {
                    WriteJson(localPath, localSlot);
                }
                catch (System.Exception exception)
                {
                    return SaveDataOperationResult.Failure(
                        SaveDataFailureReason.FileAccessFailed,
                        $"새 로컬 프로필을 만들지 못했습니다: {exception.Message}");
                }
            }

            if (localSlot?.profile != null &&
                (cloudResult.FailureReason == SaveDataFailureReason.NotFound ||
                cloudResult.FailureReason == SaveDataFailureReason.CloudAccessFailed)
               )
            {
                SaveDataOperationResult uploadResult =
                    await SaveProfilePayloadAsync(localSlot.profile, firebaseUserId);
                if (firebaseUserId != FirebaseService.Default.CurrentUserId)
                    return SaveDataOperationResult.Failure(SaveDataFailureReason.AuthenticationRequired, "로그인 계정이 변경되었습니다.");
                if (uploadResult.IsSuccess)
                {
                    WriteJson(localPath, localSlot);
                }

                return uploadResult;
            }

            return cloudResult;
        }

        /// <summary>현재 싱글플레이 작업 파일을 소유한 Firebase 사용자 ID를 기록합니다.</summary>
        private static void WriteSinglePlayerProfileOwner(
            string ownerPath,
            string firebaseUserId)
        {
            WriteJson(
                ownerPath,
                new SinglePlayerProfileOwnerData
                {
                    firebaseUserId = firebaseUserId
                });
        }

        /// <summary>
        /// 기존 동기식 로컬 저장을 유지하면서 프로필을 Firestore에 비동기로 저장하도록 요청합니다.
        /// </summary>
        private static void QueueProfileSave(PlayerProfileData profile)
        {
            if (profile == null)
                return;

            QueuePlayerDataSave(SaveDataCategory.PlayerProfile, JsonUtility.ToJson(profile, true));
        }

        /// <summary>
        /// 기존 프로필 DTO를 JSON으로 변환해 공통 Firebase 저장 서비스에 전달합니다.
        /// </summary>
        private static Task<SaveDataOperationResult> SaveProfilePayloadAsync(
            PlayerProfileData profile, string expectedUserId = null)
        {
            return SaveDataService.Default.SaveAsync(
                SaveDataCategory.PlayerProfile,
                JsonUtility.ToJson(profile, true), expectedUserId);
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
        // PassiveSkillManager.CurrentProfile은 씬·로그인 전환 후에도 유지되므로, 어느 계정 기준으로 불러왔는지 기록한다.
        // null은 로그아웃 상태(전역 파일)에서 불러온 프로필이다.
        private string passiveProfileUserId;

        private bool IsPassiveProfileFromOtherAccount =>
            PassiveSkillManager.Instance != null && PassiveSkillManager.Instance.CurrentProfile != null &&
            passiveProfileUserId != (string.IsNullOrEmpty(LocalProfileUserId) ? null : LocalProfileUserId);

        [ContextMenu("패시브 데이터 저장")]
        public void SavePassiveData()
        {
            if (PassiveSkillManager.Instance == null || PassiveSkillManager.Instance.CurrentProfile == null)
            {
                Debug.LogWarning("[DataManager] SavePassiveData - PassiveSkillManager 또는 CurrentProfile이 없습니다.");
                return;
            }

            if (IsPassiveProfileFromOtherAccount)
            {
                Debug.LogWarning("[DataManager] 다른 계정에서 불러온 패시브 프로필이라 저장하지 않고 현재 계정 프로필을 다시 불러옵니다.");
                LoadPassiveData();
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

            string uid = LocalProfileUserId;
            passiveProfileUserId = string.IsNullOrEmpty(uid) ? null : uid;
            var slot = LoadSinglePlayerSlot();
            if (slot != null && slot.profile != null)
            {
                PassiveSkillManager.Instance.SetActiveProfile(slot.profile);
                return true;
            }

            PassiveSkillManager.Instance.SetActiveProfile(new PlayerProfileData { playerId = GenerateNewPlayerId() });
            return false;
        }

        /// <summary>사망 시 계정으로 옮기는 보유 크레딧 비율(%).</summary>
        public const int DeathCreditPercent = 30;

        /// <summary>클리어·정산 시 크레딧으로 바꿔 주는 인벤토리·장착 아이템 원가 합의 비율(%).</summary>
        public const int ItemValueCreditPercent = 50;

        /// <summary>
        /// 싱글 런이 끝날 때 계정 크레딧으로 옮길 금액을 계산한다. 적립은 하지 않는다.
        /// - 클리어·정산: 보유 크레딧 전부 + 인벤토리·장착 아이템 원가 합의 50%
        /// - 사망: 보유 크레딧의 30% (아이템 제외)
        /// 소수점은 버린다. 결과 화면에 미리 금액을 보여줄 때도 이 값을 쓴다.
        /// </summary>
        public int CalculateRunEndCredits(RunEndReason reason)
        {
            if (!TryGetRunValue(out long gold, out long itemBasePrice, out _))
                return 0;

            long credits = reason == RunEndReason.Death
                ? gold * DeathCreditPercent / 100
                : gold + itemBasePrice * ItemValueCreditPercent / 100;

            return (int)System.Math.Min(credits, int.MaxValue);
        }

        /// <summary>
        /// 런 종료 크레딧 계산에 쓸 보유 크레딧과 아이템 원가 합을 구한다.
        /// 인벤토리가 있는 씬(캠프·전투)은 실제 인벤토리를, 없는 씬(스테이지 선택)은 gamesave를 쓴다.
        /// 스테이지 선택으로 오기 전 노드를 나올 때(YJ_StageManager.EndScene) gamesave가 저장되므로 값이 같다.
        /// </summary>
        private bool TryGetRunValue(out long gold, out long itemBasePrice, out bool fromLiveInventory)
        {
            InventoryController inventory = InventoryController.Instance;
            fromLiveInventory = inventory != null && inventory.PlayerWallet != null;
            if (fromLiveInventory)
            {
                gold = Mathf.Max(0, inventory.PlayerWallet.Gold);
                itemBasePrice = ItemValueCalculator.GetOwnedItemsBasePrice(inventory);
                return true;
            }

            gold = 0;
            itemBasePrice = 0;
            string path = GetSavePath(GameplaySaveFileName);
            if (!File.Exists(path))
                return false;

            try
            {
                GameSaveData data = ReadJson<GameSaveData>(path);
                if (data == null)
                    return false;
                // 새 게임 직후(아직 한 번도 저장 전)는 보유 크레딧·아이템이 없다.
                if (data.needsPlayerInitialization)
                    return true;

                ItemDatabaseSO database = ItemManager.Instance != null ? ItemManager.Instance.ItemDatabase : null;
                if (database == null && data.inventory?.items != null && data.inventory.items.Count > 0)
                {
                    Debug.LogWarning("[DataManager] 아이템 DB가 없어 저장된 아이템 원가를 계산하지 못했습니다.");
                    return false;
                }

                gold = Mathf.Max(0, data.status != null ? data.status.gold : 0);
                itemBasePrice = ItemValueCalculator.GetSavedItemsBasePrice(data.inventory, database);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[DataManager] 저장된 런 정보를 읽지 못했습니다: {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// 싱글 런 종료 크레딧(CalculateRunEndCredits)을 현재 프로필에 더해 로컬과 Firebase 저장을 요청하고,
        /// 저장 요청이 시작된 뒤에만 인게임 크레딧을 0으로 초기화한다.
        /// 액트 중간 보스에서는 부르지 않는다 - 크레딧은 런이 끝날 때(클리어·정산·사망) 한 번만 옮긴다.
        /// 아이템은 인벤토리에 남아 있으므로, 같은 런에서 두 번 부르지 않도록 호출하는 쪽이 막아야 한다.
        /// </summary>
        public bool SettleRunCredits(RunEndReason reason)
        {
            PlayerProfileData profile = PassiveSkillManager.Instance?.CurrentProfile ?? LoadSinglePlayerSlot()?.profile;
            if (profile == null)
            {
                Debug.LogWarning("[DataManager] SettleRunCredits - 프로필을 찾지 못해 크레딧을 이전하지 못했습니다.");
                return false;
            }

            if (!TryGetRunValue(out _, out _, out bool fromLiveInventory))
            {
                Debug.LogWarning("[DataManager] SettleRunCredits - 보유 크레딧(지갑 또는 저장 데이터)을 찾을 수 없어 크레딧을 이전하지 못했습니다.");
                return false;
            }

            int credits = CalculateRunEndCredits(reason);
            if (credits > 0 && !TrySaveRunCredits(profile, credits))
                return false;

            // 저장 데이터로 정산한 경우(스테이지 선택)는 지갑이 없다. 호출하는 쪽이 곧바로 런을 초기화(ResetGameplayData)한다.
            if (fromLiveInventory)
                InventoryController.Instance.PlayerWallet.SetGold(0);
            Debug.Log($"[DataManager] 런 종료({reason}) 크레딧 {credits} 이전 완료.{(fromLiveInventory ? "" : " (저장 데이터 기준)")}");
            return true;
        }

        /// <summary>
        /// 서버가 확정한 런 크레딧을 지정한 프로필에 더하고 로컬 저장 후 Firebase 저장을 요청합니다.
        /// </summary>
        public bool TrySaveRunCredits(PlayerProfileData profile, int runCredits, string settlementId = null)
        {
            if (profile == null)
            {
                Debug.LogWarning("[DataManager] TrySaveRunCredits - profile이 null입니다.");
                return false;
            }

            if (!string.IsNullOrEmpty(settlementId) && profile.lastRunSettlementId == settlementId)
                return true;
            if (runCredits <= 0)
                return true;

            int previousCredits = profile.credit;
            string previousSettlement = profile.lastRunSettlementId;
            if (!profile.TryApplyCredit(runCredits))
            {
                Debug.LogWarning($"[DataManager] 유효하지 않은 런 크레딧을 저장하지 않았습니다: {runCredits}");
                return false;
            }

            try
            {
                if (!string.IsNullOrEmpty(settlementId)) profile.lastRunSettlementId = settlementId;
                SaveSinglePlayerSlot(new SinglePlayerSlotData { profile = profile });
            }
            catch (System.Exception exception)
            {
                profile.credit = previousCredits;
                profile.lastRunSettlementId = previousSettlement;
                Debug.LogError($"[DataManager] 런 크레딧 저장 실패: {exception.Message}");
                return false;
            }

            Debug.Log("[DataManager] 런 크레딧 " + runCredits + " 저장 완료. 프로필 영구 크레딧 = " + profile.credit);
            return true;
        }

        #endregion

        #region ===================== 3. 게임플레이 데이터 =====================

        [ContextMenu("게임플레이 데이터 전체 세이브")]
        public void SaveGameplayData()
        {
            TrySaveGameplayData();
        }

        private bool TrySaveGameplayData(string completedUnknownBattleKey = null)
        {
            if ( ! TryGetGameplayPlayer(out var stats, out var health, out _) || stats.Stat == null
                                                                              || stats.Stat.currentLevel < 1
                                                                              || health.MaxHealth <= 0f)
            {
                Debug.LogWarning("[DataManager] 플레이어가 준비되지 않아 저장하지 않습니다.");
                return false;
            }

            if ( ! TryGetSavedCharacter(out CharacterClass character))
            {
                Debug.LogError(
                    "[DataManager] 캐릭터 정보를 확인하지 못해 저장을 중단합니다.");
                return false;
            }

            try
            {
                string path = GetSavePath(GameplaySaveFileName);
                // 현재 런의 선택 처리 기록 등 플레이어 객체에 없는 데이터도 보존한다.
                GameSaveData data = ReadJson<GameSaveData>(path);
                if (data == null || data.selectedCharacter != character)
                {
                    Debug.LogError("[DataManager] 기존 런을 확인하지 못해 저장을 중단합니다.");
                    return false;
                }
                data.status = BuildPlayerStatusData();
                data.inventory = BuildInventorySaveData();
                data.activeSkill = BuildActiveSkillSaveData();
                data.quests = QuestManager.Instance.GetSaveData();
                data.needsPlayerInitialization = false;
                if (!string.IsNullOrEmpty(completedUnknownBattleKey))
                {
                    if (!YJ_UnknownRunBuffSource.TryValidateRecords(data.unknownStageBuffs, out string error))
                        throw new System.InvalidOperationException(error);
                    if (stats.GetComponent<PlayerBuffManager>() == null)
                        throw new System.InvalidOperationException("PlayerBuffManager가 없어 전투 효과를 종료할 수 없습니다.");
                    YJ_UnknownRunBuffSource.CompleteBattle(data, completedUnknownBattleKey);
                }
                WriteGameplayDataAtomic(path, data);
                if (!string.IsNullOrEmpty(completedUnknownBattleKey))
                    YJ_UnknownRunBuffSource.RemoveCompletedBattle(stats.GetComponent<PlayerBuffManager>(), completedUnknownBattleKey);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[DataManager] 게임플레이 저장 실패: {exception.Message}");
                return false;
            }
        }

        public bool TryPrepareUnknownBattle(string battleKey, out bool completed)
        {
            completed = false;
            if (Mirror.NetworkClient.active || Mirror.NetworkServer.active) return false;
            try
            {
                string path = GetSavePath(GameplaySaveFileName);
                var data = ReadJson<GameSaveData>(path);
                // 새 게임의 첫 전투에는 이어받을 효과가 없고, 실제 스탯 초기화는 아래 로드 단계가 담당한다.
                if (data != null && data.needsPlayerInitialization) return true;
                if (data == null || data.needsPlayerInitialization || data.status == null || data.status.playerLevel < 1)
                    throw new System.InvalidOperationException("초기화된 런 저장이 없습니다.");
                if (!YJ_UnknownRunBuffSource.TryBindBattle(data, battleKey, out bool changed, out string error))
                    throw new System.InvalidOperationException(error);
                if (changed) WriteGameplayDataAtomic(path, data);
                completed = data.lastCompletedUnknownBattleKey == battleKey;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataManager] 다음 전투 효과 준비 실패: {e.Message}");
                return false;
            }
        }

        public bool TryCompleteUnknownBattle(string battleKey) =>
            !Mirror.NetworkClient.active && !Mirror.NetworkServer.active &&
            !string.IsNullOrWhiteSpace(battleKey) && TrySaveGameplayData(battleKey);

        /// <summary>
        /// 실제 플레이어가 없는 싱글 Unknown 씬에서만 사용한다.
        /// 확률 효과, 크레딧/아이템 지급·폐기, None, 런/다음 전투 스탯, 회복/비치명 피해를 지원한다.
        /// 미지원 효과가 섞이면 전체 지급을 보류한다.
        /// </summary>
        public bool TryApplyUnknownStageChoice(string nodeKey, YJ_UnknownStageDefinitionSO stage,
            int choiceIndex, out string error, IReadOnlyList<string> discardedItemIds = null)
        {
            error = null;
            if (Mirror.NetworkClient.active || Mirror.NetworkServer.active ||
                PlayerStatManager.Instance != null && PlayerStatManager.Instance.isActiveAndEnabled)
            {
                error = "플레이어가 없는 싱글 Unknown 씬에서만 저장 보상을 적용할 수 있습니다.";
                return false;
            }

            try
            {
                string path = GetSavePath(GameplaySaveFileName);
                GameSaveData data = ReadJson<GameSaveData>(path);
                if (!TryApplyUnknownChoiceToData(data, nodeKey, stage, choiceIndex, out bool changed, out error, discardedItemIds))
                    return false;

                // 보상과 중복 방지 기록은 반드시 동일 파일 교체로 확정한다.
                if (changed)
                    WriteGameplayDataAtomic(path, data);
                return true;
            }
            catch (System.Exception exception)
            {
                error = $"Unknown 선택 저장 실패: {exception.Message}";
                return false;
            }
        }

        // SW 수정: 서버도 같은 계산을 사용하되 호출자가 소유한 메모리 데이터만 변경합니다.
        /// <summary>보상 검증과 계산만 수행합니다. 파일 저장 없이 실패 시 기존 데이터를 유지합니다.</summary>
        public static bool TryApplyUnknownChoiceToData(GameSaveData data, string nodeKey,
            YJ_UnknownStageDefinitionSO stage, int choiceIndex, out bool changed, out string error,
            IReadOnlyList<string> discardedItemIds = null, ItemDatabaseSO database = null,
            StatSet? passiveStats = null, string rewardScope = null)
        {
            // 같은 맵/노드/선택은 저장 실패나 공간 부족 후에도 같은 결과를 낸다.
            // ItemDataCreator의 기존 옵션 생성기를 쓰되 다른 시스템의 Random에는 영향을 주지 않는다.
            var state = UnityEngine.Random.state;
            try
            {
                byte[] hash = GetUnknownRewardHash($"{nodeKey}|{stage?.StageId}|{choiceIndex}" + (rewardScope == null ? "" : "|" + rewardScope));
                int seed = hash[0] | hash[1] << 8 | hash[2] << 16 | hash[3] << 24;
                UnityEngine.Random.InitState(seed);
                return TryApplyUnknownChoiceCore(data, nodeKey, stage, choiceIndex, out changed, out error, discardedItemIds, database, passiveStats, rewardScope);
            }
            finally { UnityEngine.Random.state = state; }
        }

        private static byte[] GetUnknownRewardHash(string key)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        }

        private static bool TryApplyUnknownChoiceCore(GameSaveData data, string nodeKey,
            YJ_UnknownStageDefinitionSO stage, int choiceIndex, out bool changed, out string error,
            IReadOnlyList<string> discardedItemIds, ItemDatabaseSO database, StatSet? passiveStats, string rewardScope)
        {
            changed = false;
            error = null;
            if (data == null || data.needsPlayerInitialization || data.status == null ||
                data.status.playerLevel < 1 || data.status.gold < 0 ||
                (data.selectedCharacter != CharacterClass.Fighter && data.selectedCharacter != CharacterClass.Gunner))
            {
                error = "초기화된 런 저장 데이터가 없습니다. 정상적인 새 게임 흐름으로 진입하세요.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(nodeKey) || stage == null || string.IsNullOrWhiteSpace(stage.StageId))
            {
                error = "Unknown 노드 또는 이벤트 정보가 없습니다.";
                return false;
            }

            UnknownStageChoiceRecord previous = data.unknownStageChoices?.Find(r => r != null && r.nodeKey == nodeKey);
            if (previous != null)
            {
                if (previous.stageId == stage.StageId && previous.choiceIndex == choiceIndex)
                    return true; // 지급 후 노드 완료가 실패한 재시도: 재지급하지 않는다.
                error = "이 노드는 이미 다른 선택으로 처리되었습니다.";
                return false;
            }
            if (!stage.TryGetChoice(choiceIndex, out YJ_UnknownStageChoice choice, out error))
                return false;
            if (!YJ_UnknownRunBuffSource.TryValidateRecords(data.unknownStageBuffs, out error))
                return false;

            // 미지원 효과를 확률 판정으로 건너뛰어 선택이 부분 완료되는 것을 막는다.
            foreach (var effect in choice.Effects)
            {
                if (effect.Type != YJ_UnknownEffectType.None && effect.Type != YJ_UnknownEffectType.AddGold &&
                    effect.Type != YJ_UnknownEffectType.SpendGold && effect.Type != YJ_UnknownEffectType.LoseAllGold &&
                    effect.Type != YJ_UnknownEffectType.ModifyStats && effect.Type != YJ_UnknownEffectType.GrantItem &&
                    effect.Type != YJ_UnknownEffectType.GrantRandomEquipment &&
                    effect.Type != YJ_UnknownEffectType.DiscardSelectedItems &&
                    effect.Type != YJ_UnknownEffectType.DiscardRandomItems &&
                    effect.Type != YJ_UnknownEffectType.MoveToStage &&
                    effect.Type != YJ_UnknownEffectType.HealMaxHealthPercent &&
                    effect.Type != YJ_UnknownEffectType.DamageMaxHealthPercent)
                {
                    error = "미지원 효과가 포함되어 선택 전체를 적용하지 않았습니다.";
                    return false;
                }
                if (effect.Type == YJ_UnknownEffectType.MoveToStage && effect.Probability != 1f)
                {
                    error = "목적지 이동은 확정 효과로만 설정할 수 있습니다.";
                    return false;
                }
            }
            if (!TryGetUnknownDiscardCount(choice, out int required, out error)) return false;
            if ((discardedItemIds?.Count ?? 0) != required)
            {
                error = $"폐기할 아이템을 정확히 {required}개 선택해야 합니다.";
                return false;
            }

            long gold = data.status.gold;
            float health = data.status.currentHealth;
            InventorySaveData rewardInventory = null;
            var newBuffs = new List<UnknownStageBuffRecord>();
            for (int i = 0; i < choice.Effects.Count; i++)
            {
                YJ_UnknownStageEffect effect = choice.Effects[i];
                List<ItemDefinitionSO> candidates = null;
                if (effect.Type == YJ_UnknownEffectType.GrantRandomEquipment &&
                    !TryGetUnknownEquipmentCandidates(effect.Rarity, out candidates, out error, database))
                    return false;
                if (effect.Probability <= 0f ||
                    effect.Probability < 1f && UnityEngine.Random.value >= effect.Probability)
                    continue; // 미당첨도 아래에서 선택 완료 기록을 저장한다.

                if ((effect.Type == YJ_UnknownEffectType.DiscardSelectedItems ||
                     effect.Type == YJ_UnknownEffectType.DiscardRandomItems) &&
                    !TryDiscardUnknownItems(rewardInventory ?? data.inventory, effect.Amount,
                        effect.Type == YJ_UnknownEffectType.DiscardSelectedItems ? discardedItemIds : null,
                        out rewardInventory, out error))
                    return false;

                if (effect.Type == YJ_UnknownEffectType.GrantItem ||
                    effect.Type == YJ_UnknownEffectType.GrantRandomEquipment)
                {
                    for (int count = 0; count < effect.Amount; count++)
                    {
                        var definition = candidates == null ? effect.Item : candidates[UnityEngine.Random.Range(0, candidates.Count)];
                        string rewardKey = $"{nodeKey}|{stage.StageId}|{choiceIndex}|{i}|{count}" + (rewardScope == null ? "" : "|" + rewardScope);
                        if (!TryGrantUnknownItem(rewardInventory ?? data.inventory, definition, rewardKey,
                            out rewardInventory, out error, database))
                            return false;
                    }
                }
                if (effect.Type == YJ_UnknownEffectType.ModifyStats)
                {
                    string effectKey = $"{nodeKey}:{choiceIndex}:{i}";
                    if (data.unknownStageBuffs != null && data.unknownStageBuffs.Exists(b => b.effectKey == effectKey))
                    {
                        error = "Unknown 보상 기록과 지속 효과 기록이 일치하지 않습니다.";
                        return false;
                    }
                    newBuffs.Add(new UnknownStageBuffRecord
                    {
                        effectKey = effectKey, stageId = stage.StageId, displayName = stage.StageName,
                        lifetime = effect.Lifetime,
                        statEffects = YJ_UnknownRunBuffSource.CopyStats(effect.StatEffects)
                    });
                }
                if (effect.Type == YJ_UnknownEffectType.AddGold)
                    gold += effect.Amount;
                else if (effect.Type == YJ_UnknownEffectType.SpendGold)
                {
                    if (gold < effect.Amount)
                    {
                        error = $"크레딧이 부족합니다. 필요: {effect.Amount}, 보유: {gold}. 선택 전체를 적용하지 않았습니다.";
                        return false;
                    }
                    gold -= effect.Amount;
                }
                else if (effect.Type == YJ_UnknownEffectType.LoseAllGold)
                    gold = 0; // 잔액이 0이어도 정상 완료. 영구 프로필 크레딧은 변경하지 않는다.
                if (effect.Type == YJ_UnknownEffectType.HealMaxHealthPercent ||
                    effect.Type == YJ_UnknownEffectType.DamageMaxHealthPercent)
                {
                    if (float.IsNaN(health) || float.IsInfinity(health) || health <= 0f)
                    {
                        error = "체력 효과를 적용할 수 있는 생존 상태의 저장 체력이 없습니다.";
                        return false;
                    }
                    if (!TryGetUnknownMaxHealth(data, newBuffs, out float maxHealth, out error, rewardInventory, database, passiveStats))
                        return false;
                    double amount = (double)maxHealth * effect.HealthPercent / 100d;
                    if (effect.Type == YJ_UnknownEffectType.HealMaxHealthPercent)
                    {
                        // 실제 Heal과 동일하게 회복량을 올림한다.
                        health = (float)System.Math.Min(maxHealth, health + System.Math.Ceiling(amount));
                    }
                    else
                    {
                        // 실제 TakeDamage처럼 버림하되 이벤트 피해는 체력 1을 보장한다.
                        // 사망/부활 이벤트를 발생시키거나 부활 횟수를 소모하지 않는다.
                        health = (float)System.Math.Max(1d, health - System.Math.Floor(amount));
                    }
                }
                if (gold > int.MaxValue)
                {
                    error = "크레딧 보유 한도를 초과하여 지급하지 않았습니다.";
                    return false;
                }
            }

            data.status.gold = (int)gold;
            if (rewardInventory != null)
                data.inventory = rewardInventory;
            data.status.currentHealth = health;
            data.unknownStageBuffs ??= new List<UnknownStageBuffRecord>();
            data.unknownStageBuffs.AddRange(newBuffs);
            data.unknownStageChoices ??= new List<UnknownStageChoiceRecord>();
            data.unknownStageChoices.Add(new UnknownStageChoiceRecord
            {
                nodeKey = nodeKey, stageId = stage.StageId, choiceIndex = choiceIndex
            });
            changed = true;
            return true;
        }

        // UI에는 독립적인 저장 스냅샷만 전달한다. 확정 시 파일을 다시 읽고 ID/수량을 재검증한다.
        public bool TryGetUnknownDiscardOptions(string nodeKey, YJ_UnknownStageDefinitionSO stage, int choiceIndex,
            out List<ItemSaveData> items, out int required, out bool completed, out string error)
        {
            items = null; required = 0; completed = false; error = null;
            if (Mirror.NetworkClient.active || Mirror.NetworkServer.active || stage == null || string.IsNullOrWhiteSpace(nodeKey))
            {
                error = "싱글 Unknown 선택 정보가 필요합니다.";
                return false;
            }
            try
            {
                var data = ReadJson<GameSaveData>(GetSavePath(GameplaySaveFileName));
                if (data == null || data.needsPlayerInitialization)
                {
                    error = "초기화된 런 저장 데이터가 없습니다.";
                    return false;
                }
                var record = data.unknownStageChoices?.Find(r => r != null && r.nodeKey == nodeKey);
                if (record != null)
                {
                    completed = record.stageId == stage.StageId && record.choiceIndex == choiceIndex;
                    if (!completed) error = "이미 다른 선택으로 완료된 노드입니다.";
                    return completed;
                }
                if (!stage.TryGetChoice(choiceIndex, out var choice, out error) ||
                    !TryGetUnknownDiscardCount(choice, out required, out error) ||
                    !TryGetUnknownBackpack(data.inventory, out items, out error)) return false;
                if (items.Count >= required) return true;
                error = $"가방 아이템이 부족합니다. 필요: {required}, 보유: {items.Count}.";
                return false;
            }
            catch (System.Exception exception) { error = $"폐기 목록 조회 실패: {exception.Message}"; return false; }
        }

        private static bool TryGetUnknownDiscardCount(YJ_UnknownStageChoice choice, out int required, out string error)
        {
            required = 0; error = null;
            foreach (var effect in choice.Effects)
            {
                if (effect.Type != YJ_UnknownEffectType.DiscardSelectedItems) continue;
                if (required != 0 || effect.Probability != 1f)
                {
                    error = "직접 선택 폐기는 한 선택지에 확정 효과 하나만 설정할 수 있습니다.";
                    return false;
                }
                required = effect.Amount;
            }
            return true;
        }

        private static bool TryGetUnknownBackpack(InventorySaveData inventory, out List<ItemSaveData> items, out string error)
        {
            items = new List<ItemSaveData>(); error = null;
            var ids = new HashSet<string>();
            if (inventory?.items == null) { error = "인벤토리 저장 데이터가 없습니다."; return false; }
            foreach (var item in inventory.items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.instanceId) || !ids.Add(item.instanceId))
                { error = "인벤토리 아이템 ID가 누락되거나 중복되었습니다."; return false; }
                if (!item.isEquipped) items.Add(item);
            }
            items.Sort((a, b) => string.CompareOrdinal(a.instanceId, b.instanceId));
            return true;
        }

        private static bool TryDiscardUnknownItems(InventorySaveData inventory, int amount,
            IReadOnlyList<string> selectedIds, out InventorySaveData result, out string error)
        {
            result = null;
            if (!TryGetUnknownBackpack(inventory, out var candidates, out error)) return false;
            if (amount <= 0 || candidates.Count < amount)
            { error = $"가방 아이템이 부족합니다. 필요: {amount}, 보유: {candidates.Count}."; return false; }
            var removed = new HashSet<string>();
            if (selectedIds != null)
            {
                if (selectedIds.Count != amount) { error = "폐기 수량이 일치하지 않습니다."; return false; }
                foreach (string id in selectedIds)
                    if (string.IsNullOrWhiteSpace(id) || !removed.Add(id) || !candidates.Exists(item => item.instanceId == id))
                    { error = "폐기 대상이 중복되었거나 더 이상 가방에 없습니다. 다시 선택하세요."; return false; }
            }
            else
            {
                for (int i = 0; i < amount; i++)
                {
                    int index = UnityEngine.Random.Range(0, candidates.Count);
                    removed.Add(candidates[index].instanceId);
                    candidates.RemoveAt(index);
                }
            }
            result = new InventorySaveData { gridWidth = inventory.gridWidth, gridHeight = inventory.gridHeight,
                items = inventory.items.FindAll(item => !removed.Contains(item.instanceId)) };
            return true;
        }

        private static bool TryGetUnknownEquipmentCandidates(ItemRarity rarity,
            out List<ItemDefinitionSO> candidates, out string error, ItemDatabaseSO database = null)
        {
            candidates = new List<ItemDefinitionSO>();
            error = null;
            database ??= ItemManager.Instance != null ? ItemManager.Instance.ItemDatabase : null;
            if (database == null || database.allItems == null)
            {
                error = "무작위 장비 지급에 필요한 아이템 DB가 없습니다.";
                return false;
            }
            var ids = new HashSet<string>();
            foreach (var item in database.allItems)
            {
                if (item == null || item.rarity != rarity ||
                    (item.category != ItemCategory.Weapon && item.category != ItemCategory.Armor))
                    continue;
                if (string.IsNullOrWhiteSpace(item.itemId) || item.itemWidth <= 0 || item.itemHeight <= 0 ||
                    database.GetById(item.itemId) != item)
                {
                    error = "무작위 장비 후보의 ID, 크기 또는 DB 등록이 잘못되었습니다.";
                    return false;
                }
                if (ids.Add(item.itemId)) candidates.Add(item);
            }
            // DB 목록 순서가 바뀌어도 추첨 결과를 유지한다. 후보/옵션 데이터 수정까지 고정하는 스냅샷은 아니다.
            candidates.Sort((a, b) => string.CompareOrdinal(a.itemId, b.itemId));
            if (candidates.Count > 0) return true;
            error = $"{rarity} 등급의 무기/방어구 후보가 없습니다.";
            return false;
        }

        private static bool TryGrantUnknownItem(InventorySaveData inventory, ItemDefinitionSO reward, string rewardKey,
            out InventorySaveData result, out string error, ItemDatabaseSO database = null)
        {
            result = null;
            error = null;
            if (inventory == null || inventory.items == null || inventory.gridWidth <= 0 || inventory.gridHeight <= 0)
            {
                error = "인벤토리 크기 정보가 없습니다. 전투 스테이지에서 저장한 후 다시 시도하세요.";
                return false;
            }
            database ??= ItemManager.Instance != null ? ItemManager.Instance.ItemDatabase : null;
            if (database == null || reward == null || database.GetById(reward.itemId) != reward ||
                reward.itemWidth <= 0 || reward.itemHeight <= 0)
            {
                error = "지급 아이템이 아이템 데이터베이스에 등록되지 않았거나 크기/수량이 잘못되었습니다.";
                return false;
            }

            var occupied = new List<RectInt>();
            var ids = new HashSet<string>();
            foreach (var saved in inventory.items)
            {
                var definition = saved != null && !string.IsNullOrWhiteSpace(saved.itemId)
                    ? database.GetById(saved.itemId) : null;
                if (definition == null || string.IsNullOrWhiteSpace(saved.instanceId) || !ids.Add(saved.instanceId))
                {
                    error = "저장 아이템의 원본 또는 인스턴스 ID가 잘못되었습니다.";
                    return false;
                }
                if (saved.isEquipped)
                    continue;
                int width = saved.isRotated ? definition.itemHeight : definition.itemWidth;
                int height = saved.isRotated ? definition.itemWidth : definition.itemHeight;
                var rect = new RectInt(saved.gridX, saved.gridY, width, height);
                if (width <= 0 || height <= 0 || rect.x < 0 || rect.y < 0 ||
                    width > inventory.gridWidth || height > inventory.gridHeight ||
                    rect.x > inventory.gridWidth - width || rect.y > inventory.gridHeight - height ||
                    occupied.Exists(other => other.Overlaps(rect)))
                {
                    error = "저장 인벤토리의 아이템 배치가 겹치거나 범위를 벗어났습니다.";
                    return false;
                }
                occupied.Add(rect);
            }

            var pending = new InventorySaveData
            {
                gridWidth = inventory.gridWidth, gridHeight = inventory.gridHeight,
                items = new List<ItemSaveData>(inventory.items)
            };
            bool placed = false;
            // InventoryGrid와 같은 순서: 기본 방향을 먼저 탐색하고, 다음 회전 방향.
            for (int rotation = 0; rotation < 2 && !placed; rotation++)
            {
                int width = rotation == 0 ? reward.itemWidth : reward.itemHeight;
                int height = rotation == 0 ? reward.itemHeight : reward.itemWidth;
                for (int y = 0; y <= inventory.gridHeight - height && !placed; y++)
                for (int x = 0; x <= inventory.gridWidth - width && !placed; x++)
                {
                    var rect = new RectInt(x, y, width, height);
                    if (occupied.Exists(other => other.Overlaps(rect)))
                        continue;
                    var instance = ItemDataCreator.CreateItemData(reward);
                    byte[] id = new byte[16];
                    System.Array.Copy(GetUnknownRewardHash(rewardKey), id, id.Length);
                    instance.instanceId = new System.Guid(id).ToString();
                    if (ids.Contains(instance.instanceId))
                    {
                        error = "보상 아이템 ID와 선택 완료 기록이 일치하지 않습니다.";
                        return false;
                    }
                    var item = new InventoryItem(instance);
                    item.x = x;
                    item.y = y;
                    item.isRotated = rotation != 0;
                    pending.items.Add(ToItemSaveData(item, false, default));
                    occupied.Add(rect);
                    placed = true;
                }
            }
            if (!placed)
            {
                error = "인벤토리 공간이 부족합니다. 선택 전체를 적용하지 않았습니다.";
                return false;
            }
            result = pending;
            return true;
        }

        private static bool TryGetUnknownMaxHealth(GameSaveData data,
            List<UnknownStageBuffRecord> pendingBuffs, out float maxHealth, out string error,
            InventorySaveData pendingInventory = null, ItemDatabaseSO database = null, StatSet? passiveStats = null)
        {
            maxHealth = 0f;
            error = null;
            // 실제 스테이지에 사용하는 Resources 캐릭터 프리팹의 레벨 데이터가 원본이다.
            var prefab = Resources.Load<GameObject>("Prefabs/Character/Player/" + data.selectedCharacter);
            var levels = prefab != null ? prefab.GetComponent<PlayerLevelManager>() : null;
            var passive = passiveStats.HasValue ? null : PassiveSkillManager.Instance;
            database ??= ItemManager.Instance != null ? ItemManager.Instance.ItemDatabase : null;
            if (levels == null || (!passiveStats.HasValue && (passive == null || passive.CurrentProfile == null ||
                passive.GetDefinition(PassiveSkillId.MaxHealth) == null)) || database == null)
            {
                error = "체력 효과 계산에 필요한 캐릭터, 패시브 프로필 또는 아이템 데이터가 준비되지 않았습니다.";
                return false;
            }

            StatSet character = StatSet.Zero;
            foreach (var stat in levels.GetStatsForLevel(data.status.playerLevel))
                StatSetMapper.AddStat(ref character, stat.Key, stat.Value);
            if (character.maxHealthFlat <= 0f)
            {
                error = "캐릭터의 기본 최대 체력을 조회하지 못했습니다.";
                return false;
            }

            StatSet equipment = StatSet.Zero;
            var tracker = new BuffTracker(); // 런타임 플레이어/인벤토리를 변경하지 않는 독립 계산용.
            var slots = new HashSet<EquipSlotType>();
            foreach (var saved in (pendingInventory ?? data.inventory)?.items ?? new List<ItemSaveData>())
            {
                var definition = saved != null ? database.GetById(saved.itemId) : null;
                if (definition == null)
                {
                    error = "저장 아이템의 원본이 없어 최대 체력을 계산할 수 없습니다.";
                    return false;
                }
                if (saved.isEquipped)
                {
                    if (!slots.Add(saved.equippedSlotType) || !EquipSlotRules.CanEquipTo(definition, saved.equippedSlotType))
                    {
                        error = "저장된 장비 슬롯이 중복되었거나 장착할 수 없는 아이템입니다.";
                        return false;
                    }
                    if (saved.equippedSlotType != EquipSlotType.Potion)
                        equipment += PlayerEquipManager.ToStatSet(CreateSavedItem(saved, definition));
                }
                if (!saved.isEquipped && definition.category != ItemCategory.Relic)
                    continue;
                // OnEquip을 호출하면 전역 버프/오브젝트가 변경되므로 순수 BuffTracker API만 재사용한다.
                if (definition.uniqueEffect is PassiveBuffUniqueEffectSO always)
                    tracker.ApplyBuff(always);
                else if (definition.uniqueEffect is TriggeredBuffUniqueEffectSO triggered)
                {
                    if (triggered.persistStackOnItem && saved.persistedStackCount > 0)
                        tracker.SetStack(triggered, saved.persistedStackCount);
                }
                else if (definition.uniqueEffect is IBuffSource conditional && conditional.StatEffects != null)
                {
                    foreach (var stat in conditional.StatEffects)
                        if (stat.statType == StatType.healthFlat || stat.statType == StatType.healthPercent)
                        {
                            error = "조건부 최대 체력 효과가 있어 저장 데이터만으로 체력 변화량을 확정할 수 없습니다.";
                            return false;
                        }
                }
            }
            StatSet buffs = tracker.GetStatSet();
            if (data.unknownStageBuffs != null)
                foreach (var record in data.unknownStageBuffs)
                    if (YJ_UnknownRunBuffSource.IsActive(record))
                    foreach (var stat in record.statEffects)
                        StatSetMapper.AddStat(ref buffs, stat.statType, stat.value);
            // 앞선 효과로 추가된 최대 체력도 뒤의 회복량에 반영한다.
            foreach (var record in pendingBuffs)
                if (YJ_UnknownRunBuffSource.IsActive(record))
                foreach (var stat in record.statEffects)
                    StatSetMapper.AddStat(ref buffs, stat.statType, stat.value);

            var calculated = new PlayerStat();
            calculated.Recalculate(character, equipment, buffs, passiveStats ?? passive.GetStatSet());
            maxHealth = calculated.maxHealth;
            if (float.IsNaN(maxHealth) || float.IsInfinity(maxHealth) || maxHealth <= 0f)
            {
                error = "계산한 최대 체력이 유효하지 않습니다.";
                return false;
            }
            return true;
        }

        private static ItemInstance CreateSavedItem(ItemSaveData saved, ItemDefinitionSO definition)
        {
            return new ItemInstance
            {
                instanceId = saved.instanceId, definition = definition,
                rolledSubStats = saved.rolledSubStats ?? new List<RolledSubStat>(),
                rolledElement = saved.rolledElement, upgradeLevel = saved.upgradeLevel,
                persistedStackCount = saved.persistedStackCount
            };
        }

        private static void WriteGameplayDataAtomic(string path, GameSaveData data)
        {
            QueuePlayerDataSave(SaveDataCategory.Gameplay, JsonUtility.ToJson(data, true));
            string temporaryPath = path + "." + System.Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, path + ".bak");
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch (System.Exception exception) { Debug.LogWarning($"[DataManager] 임시 파일 정리 실패: {exception.Message}"); }
                }
            }
        }

        [ContextMenu("게임플레이 데이터 전체 로드")]
        public void LoadGameplayData()
        {
            TryLoadGameplayData();
        }

        // 플레이어와 관련 시스템의 Start 초기화가 끝난 뒤 호출합니다.
        public bool TryLoadGameplayData(string unknownBattleKey = null)
        {
            // SW 수정 : 복원 중 발생하는 장비 이벤트가 퀘스트 진행이나 저장을 다시 실행하지 않게 한다.
            IsRestoringGameplay = true;
            loadedGameplaySceneHandle = -1;
            try
            {
                bool loaded = RestoreGameplayData(unknownBattleKey);
                if (loaded)
                    loadedGameplaySceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;

                return loaded;
            }
            finally
            {
                IsRestoringGameplay = false;
            }
        }

        private bool RestoreGameplayData(string unknownBattleKey)
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
                CharacterClass character = data != null ? data.selectedCharacter : CharacterClass.Fighter;

                if (character != CharacterClass.Fighter && character != CharacterClass.Gunner)
                {
                    Debug.LogError($"[DataManager] 초기화할 캐릭터 값이 유효하지 않습니다: {character}");
                    return false;
                }

                data = new GameSaveData
                {
                    selectedCharacter = character,
                    needsPlayerInitialization = true,
                    quests = new QuestSaveData()
                };

                data.status.playerLevel = 1;
                data.status.playerExp = 0f;
            }

            // 1. 레벨과 경험치 적용
            // SW 수정 : 장비 복원 이벤트가 퀘스트 진행으로 집계되기 전에 같은 저장의 상태를 복원한다.
            QuestManager.Instance.ApplySaveData(
                data.quests ?? ReadJson<QuestSaveData>(GetSavePath(QuestSaveFileName)) ?? new QuestSaveData());
            var stat = stats.EnsureInitialized();
            stat.currentLevel = data.status.playerLevel;
            stat.currentExp = data.status.playerExp;

            // 최대 체력/마나 및 장비 복원에서 스탯을 조회하기 전에 런 보상을 복원한다.
            // 새 게임의 빈 목록은 이전 런의 이벤트 버프만 제거한다.
            if (!YJ_UnknownRunBuffSource.TryRestore(stats.GetComponent<PlayerBuffManager>(), data.unknownStageBuffs, out string buffError, unknownBattleKey))
            {
                Debug.LogError($"[DataManager] {buffError}");
                return false;
            }

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
                // 체력은 저장값을 그대로 이어가고, 마나는 스테이지에 들어올 때마다 최대치로 채운다.
                // (저장된 currentMana는 참고용으로 계속 기록만 하고 복원에는 쓰지 않는다.)
                health.SetCurrentHealth(data.status.currentHealth);
                mana.FillMana();
            }

            return true;
        }

        [ContextMenu("선택 캐릭터로 새 게임을 생성하는 메서드")]
        public bool BeginNewGame(CharacterClass character)
        {
            if (character != CharacterClass.Fighter && character != CharacterClass.Gunner)
            {
                Debug.LogError($"[DataManager] 지원하지 않는 캐릭터입니다: {character}");
                return false;
            }

            var data = new GameSaveData
            {
                selectedCharacter = character,
                needsPlayerInitialization = true,
                quests = new QuestSaveData()
            };
            data.status.playerLevel = 1;
            data.status.playerExp = 0f;

            try
            {
                WriteJson(GetSavePath(GameplaySaveFileName), data);
                loadedGameplaySceneHandle = -1;
                QuestManager.Instance.ApplySaveData(data.quests);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[DataManager] 새 게임 저장 실패: {exception.Message}");
                return false;
            }
        }

        public bool TryGetSavedCharacter(out CharacterClass character)
        {
            character = CharacterClass.Fighter;
            string path = GetSavePath(GameplaySaveFileName);

            // 전투 씬 직접 실행 테스트: 저장 파일이 없으면 Fighter 사용
            if ( ! File.Exists(path))
                return true;

            try
            {
                GameSaveData data = ReadJson<GameSaveData>(path);

                if (data == null)
                {
                    Debug.LogError("[DataManager] 게임 저장 데이터가 비어 있습니다.");
                    return false;
                }

                if (data.selectedCharacter != CharacterClass.Fighter && data.selectedCharacter != CharacterClass.Gunner)
                {
                    Debug.LogError($"[DataManager] 저장된 캐릭터 값이 유효하지 않습니다: " + $"{data.selectedCharacter}");
                    return false;
                }

                character = data.selectedCharacter;
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[DataManager] 캐릭터 정보 읽기 실패: {exception.Message}");
                return false;
            }
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
                data.gridWidth = InventoryController.Instance.PlayerGrid.GridWidth;
                data.gridHeight = InventoryController.Instance.PlayerGrid.GridHeight;
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

                var itemInstance = CreateSavedItem(saved, definition);

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

            if (Mirror.NetworkClient.active || Mirror.NetworkServer.active || IsRestoringGameplay)
                return;

            // SW 수정 : 실제 플레이어가 준비되면 보상과 지급 기록을 하나의 저장으로 남긴다.
            if (IsGameplayReady)
            {
                TrySaveGameplayData();
                return;
            }

            string path = GetSavePath(GameplaySaveFileName);
            var gameplay = ReadJson<GameSaveData>(path);
            if (gameplay != null)
            {
                gameplay.quests = QuestManager.Instance.GetSaveData();
                WriteGameplayDataAtomic(path, gameplay);
            }
            else
            {
                WriteJson(GetSavePath(QuestSaveFileName), QuestManager.Instance.GetSaveData());
            }
        }

        [ContextMenu("퀘스트 불러오기")]
        public void LoadQuestData()
        {
            if (QuestManager.Instance == null)
            {
                Debug.LogWarning("[DataManager] LoadQuestData - QuestManager.Instance가 없습니다.");
                return;
            }

            if (Mirror.NetworkClient.active || Mirror.NetworkServer.active)
                return;

            var gameplay = ReadJson<GameSaveData>(GetSavePath(GameplaySaveFileName));
            var data = gameplay?.quests ?? ReadJson<QuestSaveData>(GetSavePath(QuestSaveFileName));
            QuestManager.Instance.ApplySaveData(data ?? new QuestSaveData());
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

        /// <summary>플레이어 프로필(크레딧/이름/플레이타임 등, 패시브 트리 포함)을 완전히 새 프로필로 되돌려서 저장한다.</summary>
        [ContextMenu("플레이어 데이터 초기화")]
        public void ResetPlayerProfile()
        {
            var profile = new PlayerProfileData { playerId = GenerateNewPlayerId() };

            if (PassiveSkillManager.Instance != null)
            {
                string uid = LocalProfileUserId;
                passiveProfileUserId = string.IsNullOrEmpty(uid) ? null : uid;
                PassiveSkillManager.Instance.SetActiveProfile(profile);
            }

            SaveSinglePlayerSlot(new SinglePlayerSlotData { profile = profile });
        }

        /// <summary>현재 프로필은 그대로 두고 패시브 스킬트리(해금/적용 레벨)만 기본값(빈 트리)으로 되돌려서 저장한다.</summary>
        [ContextMenu("패시브 데이터 초기화")]
        public void ResetPassiveData()
        {
            if (IsPassiveProfileFromOtherAccount)
                LoadPassiveData();

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
            BeginNewGame(CharacterClass.Fighter);
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
            if (data is GameSaveData) QueuePlayerDataSave(SaveDataCategory.Gameplay, json);
            else if (data is QuestSaveData) QueuePlayerDataSave(SaveDataCategory.Quest, json);
            // SW 수정: 정산 ID와 잔액이 같은 파일 교체로 확정되도록 기존 파일을 백업합니다.
            string temporary = path + "." + System.Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json);
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            Debug.Log("[DataManager] 저장 완료: " + path);
        }

        /// <summary>SW 수정: 싱글의 확정 저장만 계정 캐시에 전달하며 멀티의 전역 복제 상태는 저장하지 않습니다.</summary>
        private static void QueuePlayerDataSave(SaveDataCategory category, string json)
        {
            if (category != SaveDataCategory.PlayerProfile &&
                (Mirror.NetworkClient.active || Mirror.NetworkServer.active))
                return;

            string uid = LocalProfileUserId;
            if (string.IsNullOrEmpty(uid))
                return;

            string pendingPath = PendingPlayerSavePath(category, uid);
            string temporary = pendingPath + ".tmp";
            File.WriteAllText(temporary, json);
            if (File.Exists(pendingPath))
                File.Replace(temporary, pendingPath, pendingPath + ".bak");
            else
                File.Move(temporary, pendingPath);

            // SW 수정 : 싱글은 업로드 대기 파일만 로컬에 남긴다. 인증·클라우드 접근은 멀티 진입/플레이 때만 수행한다.
            if (Mirror.NetworkClient.active && uid == FirebaseService.Default.CurrentUserId)
                _ = UploadPendingPlayerSaveAsync(category, uid, json);
        }

        /// <summary>작업 파일 저장 직전 남긴 계정별 기록을 업로드하고, 더 새 기록이 없을 때만 지웁니다.</summary>
        private static async Task UploadPendingPlayerSaveAsync(SaveDataCategory category, string uid, string json)
        {
            try
            {
                var result = await SaveDataService.Default.SaveAsync(category, json, uid);
                string pendingPath = PendingPlayerSavePath(category, uid);
                if (result.IsSuccess && File.Exists(pendingPath) && File.ReadAllText(pendingPath) == json)
                    File.Delete(pendingPath);
                if (!result.IsSuccess || !result.IsCloudSynchronized)
                    Debug.LogWarning($"[DataManager] {category} 저장 동기화: {result.Message}");
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[DataManager] 저장 동기화 실패: {exception.Message}");
            }
        }

        /// <summary>비동기 캐시 저장 전에 종료돼도 복구할 수 있는 계정별 기록 경로입니다.</summary>
        private static string PendingPlayerSavePath(SaveDataCategory category, string uid)
        {
            if (string.IsNullOrEmpty(uid) || uid.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || uid is "." or "..")
                throw new System.InvalidOperationException("유효하지 않은 저장 계정입니다.");
            string directory = Path.Combine(Application.persistentDataPath, "PlayerSaves", uid);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, category + ".pending.json");
        }

        /// <summary>클라우드 복원에 앞서 종료 전의 미전송 작업 기록부터 같은 UID 캐시에 확정합니다.</summary>
        private static async Task<SaveDataOperationResult> FlushPendingPlayerSaveAsync(SaveDataCategory category, string uid)
        {
            string path = PendingPlayerSavePath(category, uid);
            if (!File.Exists(path))
                return SaveDataOperationResult.Success(false);

            string json = File.ReadAllText(path);
            var saved = await SaveDataService.Default.SaveAsync(category, json, uid);
            if (saved.IsSuccess && File.Exists(path) && File.ReadAllText(path) == json)
                File.Delete(path);

            return saved;
        }

        /// <summary>SW 수정: 로그인한 계정의 게임·퀘스트를 복원하고 소유자가 확인된 구형 파일만 한 번 이관합니다.</summary>
        private static async Task<SaveDataOperationResult> SynchronizePlayerDataAsync(
            SaveDataCategory category, string uid, bool canMigrate)
        {
            var pending = await FlushPendingPlayerSaveAsync(category, uid);
            if (!pending.IsSuccess)
                return pending;

            var loaded = await SaveDataService.Default.LoadAsync(category, uid);
            if (uid != FirebaseService.Default.CurrentUserId)
                return SaveDataOperationResult.Failure(SaveDataFailureReason.AuthenticationRequired, "로그인 계정이 변경되었습니다.");
            string fileName = category == SaveDataCategory.Gameplay ? GameplaySaveFileName : QuestSaveFileName;
            string path = GetAccountSavePath(fileName, uid);
            if (loaded.IsSuccess)
            {
                // 업로드 큐를 다시 호출하지 않고 로그인 시 확정된 DTO만 작업 파일에 복원합니다.
                string json = loaded.Envelope.payloadJson;
                bool valid = category == SaveDataCategory.Gameplay
                    ? JsonUtility.FromJson<GameSaveData>(json) != null
                    : JsonUtility.FromJson<QuestSaveData>(json) != null;
                if (!valid)
                    return SaveDataOperationResult.Failure(SaveDataFailureReason.SerializationFailed, "저장 DTO가 비어 있습니다.");

                string temporary = path + ".tmp";
                File.WriteAllText(temporary, json);
                if (File.Exists(path))
                    File.Replace(temporary, path, path + ".bak");
                else
                    File.Move(temporary, path);

                return loaded;
            }

            if (loaded.FailureReason != SaveDataFailureReason.NotFound)
                return loaded;

            string legacy = Path.Combine(Application.persistentDataPath, fileName);
            string source = File.Exists(path) ? path : null;
            if (source == null && canMigrate && File.Exists(legacy))
                source = legacy;
            if (source == null)
                return SaveDataOperationResult.Success(true);

            string payload = File.ReadAllText(source);
            var saved = await SaveDataService.Default.SaveAsync(category, payload, uid);
            if (uid != FirebaseService.Default.CurrentUserId)
                return SaveDataOperationResult.Failure(SaveDataFailureReason.AuthenticationRequired, "로그인 계정이 변경되었습니다.");
            if (saved.IsSuccess && source != path)
                File.WriteAllText(path, payload);

            return saved;
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

using System;
using System.Threading.Tasks;

namespace Core
{
    public sealed class SaveDataService
    {
        private static readonly SaveDataService DefaultInstance = new SaveDataService(
            FirebaseService.Default,
            LocalJsonSaveDataStore.CreateDefault());

        private readonly FirebaseService firebaseService;
        private readonly ISaveDataStore localStore;

        public static SaveDataService Default => DefaultInstance;

        /// <summary>
        /// Firebase 준비 상태와 로컬 캐시 저장소를 조합하는 저장 서비스를 만듭니다.
        /// </summary>
        public SaveDataService(
            FirebaseService firebaseService,
            ISaveDataStore localStore)
        {
            this.firebaseService = firebaseService ??
                throw new ArgumentNullException(nameof(firebaseService));
            this.localStore = localStore ??
                throw new ArgumentNullException(nameof(localStore));
        }

        /// <summary>
        /// 데이터 종류에 따라 로컬 전용 또는 Firestore와 로컬 캐시에 JSON을 저장합니다.
        /// </summary>
        public async Task<SaveDataOperationResult> SaveAsync(
            SaveDataCategory category,
            string payloadJson)
        {
            SaveDataDefinition definition = SaveDataCatalog.GetDefinition(category);
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.InvalidRequest,
                    "저장할 JSON이 비어 있습니다.");
            }

            if (definition.StorageLocation == SaveStorageLocation.LocalOnly)
            {
                SaveDataEnvelope localEnvelope = SaveDataEnvelope.Create(
                    payloadJson,
                    definition.SchemaVersion,
                    1,
                    false);
                return await localStore.SaveAsync(
                    string.Empty,
                    definition,
                    localEnvelope);
            }

            FirebaseInitializationResult initialization =
                await firebaseService.InitializeAsync();
            if (!initialization.IsSuccess)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.FirebaseUnavailable,
                    initialization.Message);
            }

            string userId = firebaseService.CurrentUserId;
            if (string.IsNullOrEmpty(userId))
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.AuthenticationRequired,
                    "로그인한 Firebase 사용자가 없습니다.");
            }

            FirestoreSaveDataStore cloudStore =
                new FirestoreSaveDataStore(firebaseService.Firestore);
            SaveDataReadResult localResult = await localStore.LoadAsync(userId, definition);
            SaveDataReadResult cloudResult = await cloudStore.LoadAsync(userId, definition);

            long localRevision = localResult.IsSuccess
                ? localResult.Envelope.revision
                : 0;
            long cloudRevision = cloudResult.IsSuccess
                ? cloudResult.Envelope.revision
                : 0;
            long nextRevision = Math.Max(localRevision, cloudRevision) + 1;

            SaveDataEnvelope envelope = SaveDataEnvelope.Create(
                payloadJson,
                definition.SchemaVersion,
                nextRevision,
                true);
            SaveDataOperationResult cacheResult = await localStore.SaveAsync(
                userId,
                definition,
                envelope);
            if (!cacheResult.IsSuccess)
            {
                return cacheResult;
            }

            if (!cloudResult.IsSuccess &&
                cloudResult.FailureReason != SaveDataFailureReason.NotFound)
            {
                return SaveDataOperationResult.Success(
                    false,
                    "Firestore 상태를 확인하지 못해 로컬 캐시에만 저장했습니다.");
            }

            SaveDataOperationResult cloudSaveResult = await cloudStore.SaveAsync(
                userId,
                definition,
                envelope);
            if (!cloudSaveResult.IsSuccess)
            {
                return SaveDataOperationResult.Success(
                    false,
                    "로컬 캐시에 저장했지만 Firestore 업로드는 완료하지 못했습니다.");
            }

            envelope.pendingCloudUpload = false;
            SaveDataOperationResult finalizedCacheResult = await localStore.SaveAsync(
                userId,
                definition,
                envelope);
            if (!finalizedCacheResult.IsSuccess)
            {
                return finalizedCacheResult;
            }

            return SaveDataOperationResult.Success(true);
        }

        /// <summary>
        /// Firestore 데이터를 우선 불러오고 실패하면 같은 사용자의 로컬 캐시를 사용합니다.
        /// </summary>
        public async Task<SaveDataReadResult> LoadAsync(SaveDataCategory category)
        {
            SaveDataDefinition definition = SaveDataCatalog.GetDefinition(category);
            if (definition.StorageLocation == SaveStorageLocation.LocalOnly)
            {
                return await localStore.LoadAsync(string.Empty, definition);
            }

            FirebaseInitializationResult initialization =
                await firebaseService.InitializeAsync();
            if (!initialization.IsSuccess)
            {
                return SaveDataReadResult.Failure(
                    SaveDataFailureReason.FirebaseUnavailable,
                    initialization.Message);
            }

            string userId = firebaseService.CurrentUserId;
            if (string.IsNullOrEmpty(userId))
            {
                return SaveDataReadResult.Failure(
                    SaveDataFailureReason.AuthenticationRequired,
                    "로그인한 Firebase 사용자가 없습니다.");
            }

            FirestoreSaveDataStore cloudStore =
                new FirestoreSaveDataStore(firebaseService.Firestore);
            SaveDataReadResult cloudResult = await cloudStore.LoadAsync(userId, definition);
            if (cloudResult.IsSuccess)
            {
                cloudResult.Envelope.pendingCloudUpload = false;
                await localStore.SaveAsync(userId, definition, cloudResult.Envelope);
                return cloudResult;
            }

            SaveDataReadResult localResult = await localStore.LoadAsync(userId, definition);
            if (localResult.IsSuccess)
            {
                return SaveDataReadResult.Success(
                    localResult.Envelope,
                    false,
                    "Firestore 대신 로컬 캐시를 불러왔습니다.");
            }

            return cloudResult.FailureReason == SaveDataFailureReason.NotFound
                ? localResult
                : cloudResult;
        }
    }
}

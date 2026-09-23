using System;
using System.Threading;
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
        private readonly SemaphoreSlim operationGate = new SemaphoreSlim(1, 1);

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
            string payloadJson, string expectedUserId = null)
        {
            string userId = expectedUserId ?? firebaseService.CurrentUserId;
            await operationGate.WaitAsync();
            try { return await SaveCoreAsync(category, payloadJson, userId); }
            finally { operationGate.Release(); }
        }

        /// <summary>호출 시점의 계정을 고정하고 로컬 저장을 먼저 완료합니다.</summary>
        private async Task<SaveDataOperationResult> SaveCoreAsync(
            SaveDataCategory category, string payloadJson, string userId)
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

            if (string.IsNullOrEmpty(userId) || userId != firebaseService.CurrentUserId)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.AuthenticationRequired,
                    "로그인한 Firebase 사용자가 없습니다.");
            }

            SaveDataReadResult localResult = await localStore.LoadAsync(userId, definition);
            if (!localResult.IsSuccess && localResult.FailureReason != SaveDataFailureReason.NotFound)
                return localResult;

            long localRevision = localResult.IsSuccess
                ? localResult.Envelope.revision
                : 0;
            long nextRevision = checked(localRevision + 1);

            SaveDataEnvelope envelope = SaveDataEnvelope.Create(
                payloadJson,
                definition.SchemaVersion,
                nextRevision,
                true);
            envelope.baseCloudRevision = !localResult.IsSuccess ? 0 :
                !localResult.Envelope.pendingCloudUpload ? localRevision :
                localResult.Envelope.baseCloudRevision >= 0 ? localResult.Envelope.baseCloudRevision : Math.Max(0, localRevision - 1);
            SaveDataOperationResult cacheResult = await localStore.SaveAsync(
                userId,
                definition,
                envelope);
            if (!cacheResult.IsSuccess)
            {
                return cacheResult;
            }

            FirebaseInitializationResult initialization = await firebaseService.InitializeAsync();
            if (!initialization.IsSuccess || userId != firebaseService.CurrentUserId)
            {
                return SaveDataOperationResult.Success(
                    false,
                    "Firestore 상태를 확인하지 못해 로컬 캐시에만 저장했습니다.");
            }

            SaveDataOperationResult cloudSaveResult = await new FirestoreSaveDataStore(firebaseService.Firestore).SaveAsync(
                userId,
                definition,
                envelope);
            if (!cloudSaveResult.IsSuccess)
            {
                if (cloudSaveResult.FailureReason == SaveDataFailureReason.Conflict) return cloudSaveResult;
                return SaveDataOperationResult.Success(
                    false,
                    "로컬 캐시에 저장했지만 Firestore 업로드는 완료하지 못했습니다.");
            }

            envelope.pendingCloudUpload = false;
            envelope.baseCloudRevision = envelope.revision;
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
        public async Task<SaveDataReadResult> LoadAsync(SaveDataCategory category, string expectedUserId = null)
        {
            string userId = expectedUserId ?? firebaseService.CurrentUserId;
            await operationGate.WaitAsync();
            try { return await LoadCoreAsync(category, userId); }
            finally { operationGate.Release(); }
        }

        /// <summary>미전송 로컬 데이터를 보존하고 확인한 서버 버전에만 재전송합니다.</summary>
        private async Task<SaveDataReadResult> LoadCoreAsync(SaveDataCategory category, string userId)
        {
            SaveDataDefinition definition = SaveDataCatalog.GetDefinition(category);
            if (definition.StorageLocation == SaveStorageLocation.LocalOnly)
            {
                return await localStore.LoadAsync(string.Empty, definition);
            }

            if (string.IsNullOrEmpty(userId) || userId != firebaseService.CurrentUserId)
            {
                return SaveDataReadResult.Failure(
                    SaveDataFailureReason.AuthenticationRequired,
                    "로그인한 Firebase 사용자가 없습니다.");
            }

            SaveDataReadResult localResult = await localStore.LoadAsync(userId, definition);
            if (!localResult.IsSuccess && localResult.FailureReason != SaveDataFailureReason.NotFound)
                return localResult;
            FirebaseInitializationResult initialization = await firebaseService.InitializeAsync();
            if (!initialization.IsSuccess)
                return localResult.IsSuccess ? SaveDataReadResult.Success(localResult.Envelope, false, initialization.Message)
                    : SaveDataReadResult.Failure(SaveDataFailureReason.FirebaseUnavailable, initialization.Message);
            if (userId != firebaseService.CurrentUserId)
                return SaveDataReadResult.Failure(SaveDataFailureReason.AuthenticationRequired, "저장 계정이 변경되었습니다.");
            FirestoreSaveDataStore cloudStore =
                new FirestoreSaveDataStore(firebaseService.Firestore);
            if (localResult.IsSuccess && localResult.Envelope.pendingCloudUpload)
            {
                SaveDataOperationResult upload = await cloudStore.SaveAsync(userId, definition, localResult.Envelope);
                if (userId != firebaseService.CurrentUserId)
                    return SaveDataReadResult.Failure(SaveDataFailureReason.AuthenticationRequired, "저장 계정이 변경되었습니다.");
                if (upload.FailureReason == SaveDataFailureReason.Conflict)
                    return SaveDataReadResult.Failure(upload.FailureReason, upload.Message);
                if (!upload.IsSuccess)
                    return SaveDataReadResult.Success(localResult.Envelope, false, "업로드 대기 중인 로컬 저장을 불러왔습니다.");
                localResult.Envelope.pendingCloudUpload = false;
                localResult.Envelope.baseCloudRevision = localResult.Envelope.revision;
                var cached = await localStore.SaveAsync(userId, definition, localResult.Envelope);
                return cached.IsSuccess ? SaveDataReadResult.Success(localResult.Envelope, true)
                    : SaveDataReadResult.Failure(cached.FailureReason, cached.Message);
            }
            SaveDataReadResult cloudResult = await cloudStore.LoadAsync(userId, definition);
            if (userId != firebaseService.CurrentUserId)
                return SaveDataReadResult.Failure(SaveDataFailureReason.AuthenticationRequired, "저장 계정이 변경되었습니다.");
            if (cloudResult.IsSuccess)
            {
                if (localResult.IsSuccess && localResult.Envelope.revision > cloudResult.Envelope.revision)
                    return SaveDataReadResult.Failure(SaveDataFailureReason.Conflict, "서버 버전이 로컬보다 이전입니다. 양쪽 데이터를 보존했습니다.");
                cloudResult.Envelope.pendingCloudUpload = false;
                cloudResult.Envelope.baseCloudRevision = cloudResult.Envelope.revision;
                var cached = await localStore.SaveAsync(userId, definition, cloudResult.Envelope);
                return cached.IsSuccess ? cloudResult : SaveDataReadResult.Failure(cached.FailureReason, cached.Message);
            }

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

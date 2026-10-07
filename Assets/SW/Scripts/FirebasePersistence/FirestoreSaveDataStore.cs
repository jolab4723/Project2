using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Firestore;

namespace Core
{
    public sealed class FirestoreSaveDataStore : ISaveDataStore
    {
        private readonly FirebaseFirestore firestore;

        /// <summary>
        /// 준비된 Firestore 인스턴스를 사용해 사용자별 저장 문서를 관리합니다.
        /// </summary>
        public FirestoreSaveDataStore(FirebaseFirestore firestore)
        {
            this.firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
        }

        /// <summary>
        /// `players/{uid}/saveData/{documentId}` 경로에서 저장 봉투를 불러옵니다.
        /// </summary>
        public async Task<SaveDataReadResult> LoadAsync(
            string userId,
            SaveDataDefinition definition)
        {
            SaveDataOperationResult validation = ValidateRequest(userId, definition);
            if (!validation.IsSuccess)
            {
                return SaveDataReadResult.Failure(validation.FailureReason, validation.Message);
            }

            try
            {
                DocumentSnapshot snapshot = await GetDocument(userId, definition)
                    .GetSnapshotAsync(Source.Server);
                if (!snapshot.Exists)
                {
                    return SaveDataReadResult.Failure(
                        SaveDataFailureReason.NotFound,
                        "Firestore 저장 문서가 없습니다.");
                }

                SaveDataEnvelope envelope = new SaveDataEnvelope
                {
                    schemaVersion = Convert.ToInt32(snapshot.GetValue<long>("schemaVersion")),
                    revision = snapshot.GetValue<long>("revision"),
                    updatedAtUtc = snapshot.GetValue<string>("updatedAtUtc"),
                    payloadJson = snapshot.GetValue<string>("payloadJson"),
                    pendingCloudUpload = false
                };

                return SaveDataReadResult.Success(envelope, true);
            }
            catch (FirebaseException exception)
            {
                return SaveDataReadResult.Failure(
                    SaveDataFailureReason.CloudAccessFailed,
                    $"Firestore 불러오기 실패: {exception.Message}");
            }
            catch (Exception exception)
            {
                return SaveDataReadResult.Failure(
                    SaveDataFailureReason.Unknown,
                    exception.Message);
            }
        }

        /// <summary>
        /// 사용자별 Firestore 문서에 공통 저장 봉투를 기록합니다.
        /// </summary>
        public async Task<SaveDataOperationResult> SaveAsync(
            string userId,
            SaveDataDefinition definition,
            SaveDataEnvelope envelope)
        {
            SaveDataOperationResult validation = ValidateRequest(userId, definition);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            if (envelope == null || string.IsNullOrWhiteSpace(envelope.payloadJson))
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.InvalidRequest,
                    "Firestore에 저장할 데이터가 비어 있습니다.");
            }

            try
            {
                var fields = new Dictionary<string, object>
                {
                    { "schemaVersion", envelope.schemaVersion },
                    { "revision", envelope.revision },
                    { "updatedAtUtc", envelope.updatedAtUtc },
                    { "payloadJson", envelope.payloadJson },
                    { "serverUpdatedAt", FieldValue.ServerTimestamp }
                };

                // 재시도 중 다른 기기의 쓰기가 들어와도 읽었던 버전에 대해서만 확정한다.
                DocumentReference document = GetDocument(userId, definition);
                return await firestore.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot current = await transaction.GetSnapshotAsync(document);
                    long revision = current.Exists ? current.GetValue<long>("revision") : 0;
                    if (current.Exists && revision == envelope.revision &&
                        current.GetValue<string>("payloadJson") == envelope.payloadJson)
                        return SaveDataOperationResult.Success(true);
                    long expected = envelope.baseCloudRevision >= 0
                        ? envelope.baseCloudRevision : Math.Max(0, envelope.revision - 1);
                    if (revision != expected)
                        return SaveDataOperationResult.Failure(SaveDataFailureReason.Conflict,
                            "다른 기기의 저장이 변경되었습니다. 로컬 데이터를 보존했습니다.");
                    transaction.Set(document, fields);
                    return SaveDataOperationResult.Success(true);
                });
            }
            catch (FirebaseException exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.CloudAccessFailed,
                    $"Firestore 저장 실패: {exception.Message}");
            }
            catch (Exception exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.Unknown,
                    exception.Message);
            }
        }

        /// <summary>
        /// 사용자별 Firestore 저장 문서를 삭제합니다.
        /// </summary>
        public async Task<SaveDataOperationResult> DeleteAsync(
            string userId,
            SaveDataDefinition definition)
        {
            SaveDataOperationResult validation = ValidateRequest(userId, definition);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            try
            {
                await GetDocument(userId, definition).DeleteAsync();
                return SaveDataOperationResult.Success(true);
            }
            catch (FirebaseException exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.CloudAccessFailed,
                    $"Firestore 삭제 실패: {exception.Message}");
            }
            catch (Exception exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.Unknown,
                    exception.Message);
            }
        }

        /// <summary>
        /// 사용자 ID와 데이터 설정이 Firestore 문서 경로로 사용 가능한지 확인합니다.
        /// </summary>
        private static SaveDataOperationResult ValidateRequest(
            string userId,
            SaveDataDefinition definition)
        {
            if (definition == null)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.InvalidRequest,
                    "저장 데이터 설정이 없습니다.");
            }

            if (string.IsNullOrWhiteSpace(userId) || userId.Contains("/"))
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.InvalidRequest,
                    "유효한 Firebase 사용자 ID가 필요합니다.");
            }

            return SaveDataOperationResult.Success();
        }

        /// <summary>
        /// 사용자와 데이터 종류에 해당하는 Firestore 문서 참조를 만듭니다.
        /// </summary>
        private DocumentReference GetDocument(
            string userId,
            SaveDataDefinition definition)
        {
            return firestore
                .Collection("players")
                .Document(userId)
                .Collection("saveData")
                .Document(definition.DocumentId);
        }
    }
}

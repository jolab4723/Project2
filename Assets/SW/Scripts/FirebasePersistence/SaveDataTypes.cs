using System;

namespace Core
{
    public enum SaveDataCategory
    {
        PlayerProfile,
        Gameplay,
        Quest,
        SystemOptions,
        StageMap,
        MultiplayerCheckpoint
    }

    public enum SaveStorageLocation
    {
        LocalOnly,
        CloudWithLocalCache
    }

    public enum SaveDataFailureReason
    {
        None,
        NotFound,
        InvalidRequest,
        FileAccessFailed,
        SerializationFailed,
        FirebaseUnavailable,
        AuthenticationRequired,
        CloudAccessFailed,
        Conflict,
        Unknown
    }

    [Serializable]
    public sealed class SaveDataEnvelope
    {
        public int schemaVersion;
        public long revision;
        public string updatedAtUtc;
        public string payloadJson;
        public bool pendingCloudUpload;
        // 오프라인에서 여러 번 저장해도 마지막으로 확인한 서버 버전을 보존한다.
        public long baseCloudRevision = -1;

        /// <summary>
        /// 기존 저장 DTO의 JSON을 공통 저장 형식으로 감싸 새 봉투를 만듭니다.
        /// </summary>
        public static SaveDataEnvelope Create(
            string payloadJson,
            int schemaVersion,
            long revision,
            bool pendingCloudUpload)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                throw new ArgumentException("저장할 JSON이 비어 있습니다.", nameof(payloadJson));
            }

            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            return new SaveDataEnvelope
            {
                schemaVersion = schemaVersion,
                revision = revision,
                updatedAtUtc = DateTime.UtcNow.ToString("O"),
                payloadJson = payloadJson,
                pendingCloudUpload = pendingCloudUpload
            };
        }
    }

    public sealed class SaveDataDefinition
    {
        public SaveDataCategory Category { get; }
        public string DocumentId { get; }
        public string LocalFileName { get; }
        public SaveStorageLocation StorageLocation { get; }
        public int SchemaVersion { get; }

        /// <summary>
        /// 한 종류의 저장 데이터가 사용할 문서와 로컬 파일 규칙을 정의합니다.
        /// </summary>
        public SaveDataDefinition(
            SaveDataCategory category,
            string documentId,
            string localFileName,
            SaveStorageLocation storageLocation,
            int schemaVersion)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentException("문서 ID가 비어 있습니다.", nameof(documentId));
            }

            if (string.IsNullOrWhiteSpace(localFileName))
            {
                throw new ArgumentException("로컬 파일명이 비어 있습니다.", nameof(localFileName));
            }

            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            Category = category;
            DocumentId = documentId;
            LocalFileName = localFileName;
            StorageLocation = storageLocation;
            SchemaVersion = schemaVersion;
        }
    }

    public class SaveDataOperationResult
    {
        public bool IsSuccess { get; }
        public bool IsCloudSynchronized { get; }
        public SaveDataFailureReason FailureReason { get; }
        public string Message { get; }

        protected SaveDataOperationResult(
            bool isSuccess,
            bool isCloudSynchronized,
            SaveDataFailureReason failureReason,
            string message)
        {
            IsSuccess = isSuccess;
            IsCloudSynchronized = isCloudSynchronized;
            FailureReason = failureReason;
            Message = message;
        }

        /// <summary>
        /// 저장소 작업이 정상 완료된 결과를 만듭니다.
        /// </summary>
        public static SaveDataOperationResult Success(
            bool isCloudSynchronized = true,
            string message = "")
        {
            return new SaveDataOperationResult(
                true,
                isCloudSynchronized,
                SaveDataFailureReason.None,
                message ?? string.Empty);
        }

        /// <summary>
        /// 저장소 작업이 실패한 이유와 설명을 담은 결과를 만듭니다.
        /// </summary>
        public static SaveDataOperationResult Failure(
            SaveDataFailureReason failureReason,
            string message)
        {
            return new SaveDataOperationResult(
                false,
                false,
                failureReason,
                message ?? string.Empty);
        }
    }

    public sealed class SaveDataReadResult : SaveDataOperationResult
    {
        public SaveDataEnvelope Envelope { get; }

        private SaveDataReadResult(
            bool isSuccess,
            bool isCloudSynchronized,
            SaveDataFailureReason failureReason,
            string message,
            SaveDataEnvelope envelope)
            : base(isSuccess, isCloudSynchronized, failureReason, message)
        {
            Envelope = envelope;
        }

        /// <summary>
        /// 저장 데이터를 정상적으로 읽은 결과를 만듭니다.
        /// </summary>
        public static SaveDataReadResult Success(
            SaveDataEnvelope envelope,
            bool isCloudSynchronized = true,
            string message = "")
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            return new SaveDataReadResult(
                true,
                isCloudSynchronized,
                SaveDataFailureReason.None,
                message ?? string.Empty,
                envelope);
        }

        /// <summary>
        /// 저장 데이터를 읽지 못한 이유와 설명을 담은 결과를 만듭니다.
        /// </summary>
        public static new SaveDataReadResult Failure(
            SaveDataFailureReason failureReason,
            string message)
        {
            return new SaveDataReadResult(
                false,
                false,
                failureReason,
                message ?? string.Empty,
                null);
        }
    }
}

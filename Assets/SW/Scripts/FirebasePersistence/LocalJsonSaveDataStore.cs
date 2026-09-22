using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Core
{
    public sealed class LocalJsonSaveDataStore : ISaveDataStore
    {
        private const string DeviceFolderName = "device";
        private const string PlayerFolderName = "players";

        private readonly string rootDirectory;

        /// <summary>
        /// 지정한 루트 폴더에 로컬 저장 데이터와 클라우드 복구 캐시를 관리합니다.
        /// </summary>
        public LocalJsonSaveDataStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("저장 루트 폴더가 비어 있습니다.", nameof(rootDirectory));
            }

            this.rootDirectory = Path.GetFullPath(rootDirectory);
        }

        /// <summary>
        /// Unity의 영구 데이터 경로를 사용하는 기본 로컬 저장소를 만듭니다.
        /// </summary>
        public static LocalJsonSaveDataStore CreateDefault()
        {
            string defaultRoot = Path.Combine(
                Application.persistentDataPath,
                "FirebaseMigration");
            return new LocalJsonSaveDataStore(defaultRoot);
        }

        /// <summary>
        /// 지정한 사용자와 데이터 종류에 해당하는 JSON 저장 파일을 불러옵니다.
        /// </summary>
        public Task<SaveDataReadResult> LoadAsync(
            string userId,
            SaveDataDefinition definition)
        {
            return Task.Run(() => LoadInternal(userId, definition));
        }

        /// <summary>
        /// 저장 봉투를 임시 파일에 먼저 기록한 뒤 대상 파일로 교체합니다.
        /// </summary>
        public Task<SaveDataOperationResult> SaveAsync(
            string userId,
            SaveDataDefinition definition,
            SaveDataEnvelope envelope)
        {
            return Task.Run(() => SaveInternal(userId, definition, envelope));
        }

        /// <summary>
        /// 지정한 사용자와 데이터 종류에 해당하는 로컬 파일과 백업 파일을 삭제합니다.
        /// </summary>
        public Task<SaveDataOperationResult> DeleteAsync(
            string userId,
            SaveDataDefinition definition)
        {
            return Task.Run(() => DeleteInternal(userId, definition));
        }

        /// <summary>
        /// 저장 파일을 읽고 JSON 봉투로 변환하며 오류를 공통 결과로 바꿉니다.
        /// </summary>
        private SaveDataReadResult LoadInternal(
            string userId,
            SaveDataDefinition definition)
        {
            SaveDataOperationResult validation = ValidateRequest(userId, definition);
            if (!validation.IsSuccess)
            {
                return SaveDataReadResult.Failure(validation.FailureReason, validation.Message);
            }

            string filePath = GetFilePath(userId, definition);
            string backupPath = filePath + ".bak";
            if (!File.Exists(filePath) && !File.Exists(backupPath))
            {
                return SaveDataReadResult.Failure(
                    SaveDataFailureReason.NotFound,
                    "저장 파일이 없습니다.");
            }

            SaveDataReadResult primaryResult = ReadEnvelope(filePath);
            if (primaryResult.IsSuccess || !File.Exists(backupPath))
            {
                return primaryResult;
            }

            SaveDataReadResult backupResult = ReadEnvelope(backupPath);
            if (!backupResult.IsSuccess)
            {
                return primaryResult;
            }

            return SaveDataReadResult.Success(
                backupResult.Envelope,
                false,
                "기본 저장 파일을 읽지 못해 백업 파일을 불러왔습니다.");
        }

        /// <summary>
        /// 지정한 파일을 저장 봉투로 읽고 직렬화 및 파일 접근 오류를 공통 결과로 바꿉니다.
        /// </summary>
        private static SaveDataReadResult ReadEnvelope(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return SaveDataReadResult.Failure(
                    SaveDataFailureReason.NotFound,
                    "저장 파일이 없습니다.");
            }

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                SaveDataEnvelope envelope = JsonUtility.FromJson<SaveDataEnvelope>(json);
                if (!IsValidEnvelope(envelope))
                {
                    return SaveDataReadResult.Failure(
                        SaveDataFailureReason.SerializationFailed,
                        "저장 파일의 공통 형식이 올바르지 않습니다.");
                }

                return SaveDataReadResult.Success(envelope);
            }
            catch (ArgumentException exception)
            {
                return SaveDataReadResult.Failure(
                    SaveDataFailureReason.SerializationFailed,
                    exception.Message);
            }
            catch (IOException exception)
            {
                return SaveDataReadResult.Failure(
                    SaveDataFailureReason.FileAccessFailed,
                    exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return SaveDataReadResult.Failure(
                    SaveDataFailureReason.FileAccessFailed,
                    exception.Message);
            }
            catch (Exception exception)
            {
                return SaveDataReadResult.Failure(
                    SaveDataFailureReason.Unknown,
                    exception.Message);
            }
        }

        /// <summary>
        /// 저장 봉투를 JSON으로 변환하고 기존 파일을 백업한 뒤 안전하게 교체합니다.
        /// </summary>
        private SaveDataOperationResult SaveInternal(
            string userId,
            SaveDataDefinition definition,
            SaveDataEnvelope envelope)
        {
            SaveDataOperationResult validation = ValidateRequest(userId, definition);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            if (!IsValidEnvelope(envelope))
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.InvalidRequest,
                    "저장할 공통 형식이 올바르지 않습니다.");
            }

            string filePath = GetFilePath(userId, definition);
            string directoryPath = Path.GetDirectoryName(filePath);
            string temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string backupPath = filePath + ".bak";

            try
            {
                Directory.CreateDirectory(directoryPath);
                string json = JsonUtility.ToJson(envelope, true);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

                if (File.Exists(filePath))
                {
                    File.Replace(temporaryPath, filePath, backupPath);
                }
                else
                {
                    File.Move(temporaryPath, filePath);
                }

                return SaveDataOperationResult.Success();
            }
            catch (ArgumentException exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.SerializationFailed,
                    exception.Message);
            }
            catch (IOException exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.FileAccessFailed,
                    exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.FileAccessFailed,
                    exception.Message);
            }
            catch (Exception exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.Unknown,
                    exception.Message);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        /// <summary>
        /// 저장 파일과 자동 생성된 백업 파일이 있으면 함께 삭제합니다.
        /// </summary>
        private SaveDataOperationResult DeleteInternal(
            string userId,
            SaveDataDefinition definition)
        {
            SaveDataOperationResult validation = ValidateRequest(userId, definition);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            string filePath = GetFilePath(userId, definition);

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                string backupPath = filePath + ".bak";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                return SaveDataOperationResult.Success();
            }
            catch (IOException exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.FileAccessFailed,
                    exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.FileAccessFailed,
                    exception.Message);
            }
            catch (Exception exception)
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.Unknown,
                    exception.Message);
            }
        }

        /// <summary>
        /// 저장 종류와 사용자 ID가 해당 저장 위치 규칙에 맞는지 확인합니다.
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

            if (definition.StorageLocation == SaveStorageLocation.CloudWithLocalCache &&
                !IsValidUserId(userId))
            {
                return SaveDataOperationResult.Failure(
                    SaveDataFailureReason.InvalidRequest,
                    "클라우드 캐시에는 유효한 사용자 ID가 필요합니다.");
            }

            return SaveDataOperationResult.Success();
        }

        /// <summary>
        /// 사용자 ID가 비어 있지 않고 경로 구분 문자를 포함하지 않는지 확인합니다.
        /// </summary>
        private static bool IsValidUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return userId != "." &&
                   userId != ".." &&
                   userId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                   !userId.Contains("/") &&
                   !userId.Contains("\\");
        }

        /// <summary>
        /// 로컬 전용 데이터와 사용자별 클라우드 캐시의 실제 파일 경로를 계산합니다.
        /// </summary>
        private string GetFilePath(
            string userId,
            SaveDataDefinition definition)
        {
            if (definition.StorageLocation == SaveStorageLocation.LocalOnly)
            {
                return Path.Combine(rootDirectory, DeviceFolderName, definition.LocalFileName);
            }

            return Path.Combine(
                rootDirectory,
                PlayerFolderName,
                userId,
                definition.LocalFileName);
        }

        /// <summary>
        /// 저장 봉투에 필요한 버전, 시각, JSON 본문이 모두 들어 있는지 확인합니다.
        /// </summary>
        private static bool IsValidEnvelope(SaveDataEnvelope envelope)
        {
            return envelope != null &&
                   envelope.schemaVersion >= 1 &&
                   envelope.revision >= 0 &&
                   !string.IsNullOrWhiteSpace(envelope.updatedAtUtc) &&
                   !string.IsNullOrWhiteSpace(envelope.payloadJson);
        }
    }
}

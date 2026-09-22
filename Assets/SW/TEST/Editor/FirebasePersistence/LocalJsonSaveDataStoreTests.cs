using System;
using System.IO;
using NUnit.Framework;

namespace Core.Tests
{
    public sealed class LocalJsonSaveDataStoreTests
    {
        private string temporaryRoot;

        /// <summary>
        /// 각 테스트가 실제 사용자 저장 폴더를 건드리지 않도록 임시 폴더를 만듭니다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "Project2FirebaseTests",
                Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// 테스트가 만든 임시 저장 파일을 정리합니다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, true);
            }
        }

        /// <summary>
        /// 사용자별 로컬 캐시에 저장한 봉투를 같은 값으로 다시 읽는지 확인합니다.
        /// </summary>
        [Test]
        public void SaveAndLoad_CloudCache_RoundTripsEnvelope()
        {
            LocalJsonSaveDataStore store = new LocalJsonSaveDataStore(temporaryRoot);
            SaveDataDefinition definition = SaveDataCatalog.GetDefinition(
                SaveDataCategory.PlayerProfile);
            SaveDataEnvelope expected = SaveDataEnvelope.Create(
                "{\"nickname\":\"테스트 사용자\"}",
                definition.SchemaVersion,
                3,
                true);

            SaveDataOperationResult saveResult = store
                .SaveAsync("firebase-user-1", definition, expected)
                .GetAwaiter()
                .GetResult();
            SaveDataReadResult loadResult = store
                .LoadAsync("firebase-user-1", definition)
                .GetAwaiter()
                .GetResult();

            Assert.That(saveResult.IsSuccess, Is.True, saveResult.Message);
            Assert.That(loadResult.IsSuccess, Is.True, loadResult.Message);
            Assert.That(loadResult.Envelope.schemaVersion, Is.EqualTo(expected.schemaVersion));
            Assert.That(loadResult.Envelope.revision, Is.EqualTo(expected.revision));
            Assert.That(loadResult.Envelope.payloadJson, Is.EqualTo(expected.payloadJson));
            Assert.That(loadResult.Envelope.pendingCloudUpload, Is.True);
        }

        /// <summary>
        /// 같은 데이터 종류라도 서로 다른 Firebase 사용자 캐시가 섞이지 않는지 확인합니다.
        /// </summary>
        [Test]
        public void SaveAndLoad_DifferentUsers_KeepSeparateCloudCaches()
        {
            LocalJsonSaveDataStore store = new LocalJsonSaveDataStore(temporaryRoot);
            SaveDataDefinition definition = SaveDataCatalog.GetDefinition(
                SaveDataCategory.PlayerProfile);
            SaveDataEnvelope firstUserEnvelope = SaveDataEnvelope.Create(
                "{\"nickname\":\"첫 번째 사용자\"}",
                definition.SchemaVersion,
                1,
                false);
            SaveDataEnvelope secondUserEnvelope = SaveDataEnvelope.Create(
                "{\"nickname\":\"두 번째 사용자\"}",
                definition.SchemaVersion,
                1,
                false);

            store.SaveAsync("firebase-user-1", definition, firstUserEnvelope)
                .GetAwaiter()
                .GetResult();
            store.SaveAsync("firebase-user-2", definition, secondUserEnvelope)
                .GetAwaiter()
                .GetResult();

            SaveDataReadResult firstUserResult = store
                .LoadAsync("firebase-user-1", definition)
                .GetAwaiter()
                .GetResult();
            SaveDataReadResult secondUserResult = store
                .LoadAsync("firebase-user-2", definition)
                .GetAwaiter()
                .GetResult();

            Assert.That(firstUserResult.IsSuccess, Is.True, firstUserResult.Message);
            Assert.That(secondUserResult.IsSuccess, Is.True, secondUserResult.Message);
            Assert.That(
                firstUserResult.Envelope.payloadJson,
                Is.EqualTo(firstUserEnvelope.payloadJson));
            Assert.That(
                secondUserResult.Envelope.payloadJson,
                Is.EqualTo(secondUserEnvelope.payloadJson));
        }

        /// <summary>
        /// 기본 저장 파일이 손상되면 직전 정상 저장본인 백업 파일을 불러오는지 확인합니다.
        /// </summary>
        [Test]
        public void Load_CorruptedPrimaryFile_UsesBackupEnvelope()
        {
            LocalJsonSaveDataStore store = new LocalJsonSaveDataStore(temporaryRoot);
            SaveDataDefinition definition = SaveDataCatalog.GetDefinition(
                SaveDataCategory.PlayerProfile);
            SaveDataEnvelope firstEnvelope = SaveDataEnvelope.Create(
                "{\"nickname\":\"첫 저장\"}",
                definition.SchemaVersion,
                1,
                false);
            SaveDataEnvelope secondEnvelope = SaveDataEnvelope.Create(
                "{\"nickname\":\"두 번째 저장\"}",
                definition.SchemaVersion,
                2,
                false);

            SaveDataOperationResult firstSaveResult = store
                .SaveAsync("firebase-user-1", definition, firstEnvelope)
                .GetAwaiter()
                .GetResult();
            SaveDataOperationResult secondSaveResult = store
                .SaveAsync("firebase-user-1", definition, secondEnvelope)
                .GetAwaiter()
                .GetResult();

            string primaryPath = Path.Combine(
                temporaryRoot,
                "players",
                "firebase-user-1",
                definition.LocalFileName);
            File.WriteAllText(primaryPath, "{ 손상된 JSON");

            SaveDataReadResult loadResult = store
                .LoadAsync("firebase-user-1", definition)
                .GetAwaiter()
                .GetResult();

            Assert.That(firstSaveResult.IsSuccess, Is.True, firstSaveResult.Message);
            Assert.That(secondSaveResult.IsSuccess, Is.True, secondSaveResult.Message);
            Assert.That(loadResult.IsSuccess, Is.True, loadResult.Message);
            Assert.That(loadResult.IsCloudSynchronized, Is.False);
            Assert.That(loadResult.Envelope.revision, Is.EqualTo(firstEnvelope.revision));
            Assert.That(loadResult.Envelope.payloadJson, Is.EqualTo(firstEnvelope.payloadJson));
        }

        /// <summary>
        /// 로컬 전용 데이터는 로그인 사용자 ID 없이도 저장되는지 확인합니다.
        /// </summary>
        [Test]
        public void Save_LocalOnlyData_DoesNotRequireUserId()
        {
            LocalJsonSaveDataStore store = new LocalJsonSaveDataStore(temporaryRoot);
            SaveDataDefinition definition = SaveDataCatalog.GetDefinition(
                SaveDataCategory.SystemOptions);
            SaveDataEnvelope envelope = SaveDataEnvelope.Create(
                "{\"masterVolume\":0.5}",
                definition.SchemaVersion,
                1,
                false);

            SaveDataOperationResult result = store
                .SaveAsync(string.Empty, definition, envelope)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.True, result.Message);
        }

        /// <summary>
        /// 사용자 폴더를 벗어날 수 있는 잘못된 사용자 ID를 거부하는지 확인합니다.
        /// </summary>
        [TestCase("../other-user")]
        [TestCase("..")]
        public void Save_CloudCacheWithInvalidUserId_ReturnsInvalidRequest(string invalidUserId)
        {
            LocalJsonSaveDataStore store = new LocalJsonSaveDataStore(temporaryRoot);
            SaveDataDefinition definition = SaveDataCatalog.GetDefinition(
                SaveDataCategory.Gameplay);
            SaveDataEnvelope envelope = SaveDataEnvelope.Create(
                "{}",
                definition.SchemaVersion,
                1,
                true);

            SaveDataOperationResult result = store
                .SaveAsync(invalidUserId, definition, envelope)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(SaveDataFailureReason.InvalidRequest));
        }

        /// <summary>
        /// 등록된 핵심 영구 데이터가 클라우드 캐시 대상으로 분류되어 있는지 확인합니다.
        /// </summary>
        [TestCase(SaveDataCategory.PlayerProfile)]
        [TestCase(SaveDataCategory.Gameplay)]
        [TestCase(SaveDataCategory.Quest)]
        public void Catalog_PersistentData_UsesCloudWithLocalCache(SaveDataCategory category)
        {
            SaveDataDefinition definition = SaveDataCatalog.GetDefinition(category);

            Assert.That(
                definition.StorageLocation,
                Is.EqualTo(SaveStorageLocation.CloudWithLocalCache));
        }
    }
}

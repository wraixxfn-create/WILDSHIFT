using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildshift.Persistence;
using Wildshift.Persistence.Model;
using Wildshift.World.Events;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies the file-level behaviour of the local save foundation: a valid round trip, a missing
    /// file, malformed data, an incompatible schema version, write throttling, and the rule that a
    /// failed load never silently destroys the file that could not be read.
    /// </summary>
    public sealed class LocalSaveServiceTests
    {
        private const string ValidJsonTemplate =
            "{{\"schemaVersion\":{0},\"savedAtUtc\":\"\",\"applicationVersion\":\"\"," +
            "\"player\":{{\"positionX\":1.0,\"positionY\":2.0,\"positionZ\":3.0,\"yawDegrees\":90.0}}," +
            "\"regions\":[{{\"stableId\":\"nacre/coast/north\",\"testValue\":7}}],\"events\":[]}}";

        private string _directory;
        private SaveLocation _location;
        private double _clockSeconds;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "WildshiftSaveTests", Guid.NewGuid().ToString("N"));
            _location = new SaveLocation(_directory, SaveLocation.DefaultFileName);
            _clockSeconds = 1000d;

            // The failure paths under test deliberately log errors; they are the expected behaviour here.
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void DefaultLocationLivesUnderThePersistentDataPathAndHidesPathsFromPlayers()
        {
            SaveLocation defaultLocation = SaveLocation.CreateDefault();

            Assert.That(defaultLocation.DirectoryPath, Does.StartWith(Application.persistentDataPath));
            Assert.That(defaultLocation.FilePath, Does.EndWith(SaveLocation.DefaultFileName));
            Assert.That(defaultLocation.TemporaryFilePath, Is.Not.EqualTo(defaultLocation.FilePath));
            Assert.That(defaultLocation.BackupFilePath, Is.Not.EqualTo(defaultLocation.FilePath));
            Assert.That(defaultLocation.UserFacingDescription, Does.Not.Contain(Application.persistentDataPath));
        }

        [Test]
        public void SavesValidPrototypeStateAndLoadsItBack()
        {
            LocalSaveService service = CreateService();
            SaveGameData saved = CreatePayload();

            SaveResult saveResult = service.Save(saved);

            Assert.That(saveResult.IsSuccess, Is.True, saveResult.DeveloperMessage);
            Assert.That(File.Exists(_location.FilePath), Is.True);
            Assert.That(File.Exists(_location.TemporaryFilePath), Is.False, "The temporary file must not survive a save.");
            Assert.That(saveResult.UserMessage, Does.Not.Contain(_directory));

            LoadResult loadResult = service.Load();

            Assert.That(loadResult.IsSuccess, Is.True, loadResult.DeveloperMessage);
            SaveGameData loaded = loadResult.Data;
            Assert.That(loaded.SchemaVersion, Is.EqualTo(SaveSchema.CurrentVersion));
            Assert.That(loaded.Player.PositionX, Is.EqualTo(12.5f).Within(0.0001f));
            Assert.That(loaded.Player.PositionY, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(loaded.Player.PositionZ, Is.EqualTo(-4.25f).Within(0.0001f));
            Assert.That(loaded.Player.YawDegrees, Is.EqualTo(135f).Within(0.0001f));
            Assert.That(loaded.Regions.Length, Is.EqualTo(2));
            Assert.That(loaded.Regions[0].StableId, Is.EqualTo("nacre/coast/north"));
            Assert.That(loaded.Regions[0].TestValue, Is.EqualTo(3));
            Assert.That(loaded.Regions[1].StableId, Is.EqualTo("nacre/reef/south"));
            Assert.That(loaded.Events.Length, Is.EqualTo(2));
            Assert.That(loaded.Events[0].Id, Is.EqualTo("event-a"));
            Assert.That(loaded.Events[1].Id, Is.EqualTo("event-b"));
            Assert.That(loaded.Events[1].Parameters.Length, Is.EqualTo(1));
            Assert.That(loaded.Events[1].Parameters[0].Id, Is.EqualTo("units-extracted"));
            Assert.That(loaded.Events[1].Parameters[0].Value, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void SavedFileUsesTheDocumentedSchemaFieldNames()
        {
            LocalSaveService service = CreateService();

            Assert.That(service.Save(CreatePayload()).IsSuccess, Is.True);

            string json = File.ReadAllText(_location.FilePath);
            Assert.That(json, Does.Contain("\"schemaVersion\""));
            Assert.That(json, Does.Contain("\"player\""));
            Assert.That(json, Does.Contain("\"regions\""));
            Assert.That(json, Does.Contain("\"events\""));
            Assert.That(json, Does.Contain("\"stableId\""));
            Assert.That(json, Does.Contain("\"elapsedWorldTime\""));
        }

        [Test]
        public void MissingSaveFileIsReportedWithoutCreatingOneOrBlockingSaves()
        {
            LocalSaveService service = CreateService();

            Assert.That(service.HasSaveFile, Is.False);

            LoadResult loadResult = service.Load();

            Assert.That(loadResult.Status, Is.EqualTo(LoadStatus.NoSaveFile));
            Assert.That(loadResult.Data, Is.Null);
            Assert.That(File.Exists(_location.FilePath), Is.False, "Loading must not create a file.");
            Assert.That(service.IsOverwriteBlocked, Is.False, "A first run must still be able to save.");
            Assert.That(service.Save(CreatePayload()).IsSuccess, Is.True);
        }

        [Test]
        public void MalformedSaveDataIsRejectedAndTheFileIsLeftUntouched()
        {
            const string garbage = "{ this is not valid json";
            WriteRawSaveFile(garbage);
            LocalSaveService service = CreateService();

            LoadResult loadResult = service.Load();

            Assert.That(loadResult.Status, Is.EqualTo(LoadStatus.Malformed));
            Assert.That(loadResult.Data, Is.Null);
            Assert.That(loadResult.UserMessage, Does.Not.Contain(_directory));
            Assert.That(File.ReadAllText(_location.FilePath), Is.EqualTo(garbage));
        }

        [Test]
        public void AnEmptySaveFileIsTreatedAsMalformedRatherThanAsEmptyState()
        {
            WriteRawSaveFile(string.Empty);
            LocalSaveService service = CreateService();

            Assert.That(service.Load().Status, Is.EqualTo(LoadStatus.Malformed));
        }

        [Test]
        public void ANewerSchemaVersionIsReportedInsteadOfBeingInterpreted()
        {
            WriteRawSaveFile(string.Format(ValidJsonTemplate, SaveSchema.CurrentVersion + 1));
            LocalSaveService service = CreateService();

            LoadResult loadResult = service.Load();

            Assert.That(loadResult.Status, Is.EqualTo(LoadStatus.IncompatibleVersion));
            Assert.That(loadResult.Data, Is.Null);
            Assert.That(loadResult.DeveloperMessage, Does.Contain("newer build"));
        }

        [Test]
        public void AMissingSchemaVersionIsReportedAsIncompatible()
        {
            WriteRawSaveFile("{\"player\":{\"positionX\":1.0},\"regions\":[],\"events\":[]}");
            LocalSaveService service = CreateService();

            LoadResult loadResult = service.Load();

            Assert.That(loadResult.Status, Is.EqualTo(LoadStatus.IncompatibleVersion));
            Assert.That(loadResult.Data, Is.Null);
        }

        [Test]
        public void ParseableButInconsistentDataIsRejectedByValidation()
        {
            WriteRawSaveFile(
                "{\"schemaVersion\":1,\"player\":{\"positionX\":0.0,\"positionY\":0.0,\"positionZ\":0.0," +
                "\"yawDegrees\":0.0},\"regions\":[{\"stableId\":\"nacre/coast/north\",\"testValue\":1}," +
                "{\"stableId\":\"nacre/coast/north\",\"testValue\":2}],\"events\":[]}");
            LocalSaveService service = CreateService();

            LoadResult loadResult = service.Load();

            Assert.That(loadResult.Status, Is.EqualTo(LoadStatus.InvalidData));
            Assert.That(loadResult.Data, Is.Null);
        }

        [Test]
        public void AFailedLoadBlocksOverwritingTheFileUntilItIsExplicitlyAllowed()
        {
            const string garbage = "{ this is not valid json";
            WriteRawSaveFile(garbage);
            LocalSaveService service = CreateService();
            Assert.That(service.Load().IsSuccess, Is.False);
            Assert.That(service.IsOverwriteBlocked, Is.True);

            SaveResult blocked = service.Save(CreatePayload());

            Assert.That(blocked.Status, Is.EqualTo(SaveStatus.OverwriteBlocked));
            Assert.That(File.ReadAllText(_location.FilePath), Is.EqualTo(garbage),
                "A failed load must never destroy the file it could not read.");

            service.AllowOverwriteAfterFailedLoad();
            Assert.That(service.IsOverwriteBlocked, Is.False);

            SaveResult allowed = service.Save(CreatePayload());

            Assert.That(allowed.IsSuccess, Is.True, allowed.DeveloperMessage);
            Assert.That(File.ReadAllText(_location.FilePath), Is.Not.EqualTo(garbage));
            Assert.That(File.Exists(_location.BackupFilePath), Is.True);
            Assert.That(File.ReadAllText(_location.BackupFilePath), Is.EqualTo(garbage),
                "The unreadable file must survive as a backup for manual recovery.");
        }

        [Test]
        public void ASuccessfulLoadClearsAnEarlierOverwriteBlock()
        {
            WriteRawSaveFile("not json");
            LocalSaveService service = CreateService();
            Assert.That(service.Load().IsSuccess, Is.False);
            Assert.That(service.IsOverwriteBlocked, Is.True);

            service.AllowOverwriteAfterFailedLoad();
            Assert.That(service.Save(CreatePayload()).IsSuccess, Is.True);
            Assert.That(service.Load().IsSuccess, Is.True);

            Assert.That(service.IsOverwriteBlocked, Is.False);
        }

        [Test]
        public void TheReplacedSaveIsKeptAsABackupThatCanBeLoadedExplicitly()
        {
            LocalSaveService service = CreateService();
            Assert.That(service.Save(CreatePayload(testValue: 3)).IsSuccess, Is.True);

            Assert.That(service.Save(CreatePayload(testValue: 42)).IsSuccess, Is.True);

            Assert.That(service.HasBackupFile, Is.True);
            LoadResult current = service.Load();
            LoadResult previous = service.LoadBackup();
            Assert.That(current.IsSuccess, Is.True, current.DeveloperMessage);
            Assert.That(previous.IsSuccess, Is.True, previous.DeveloperMessage);
            Assert.That(current.Data.Regions[0].TestValue, Is.EqualTo(42));
            Assert.That(previous.Data.Regions[0].TestValue, Is.EqualTo(3),
                "The previous valid save must still be recoverable after it was replaced.");
        }

        [Test]
        public void SavesCloserThanTheMinimumIntervalAreSkippedInsteadOfWritten()
        {
            LocalSaveService service = CreateService(minimumSecondsBetweenSaves: 5d);
            Assert.That(service.Save(CreatePayload(testValue: 1)).IsSuccess, Is.True);

            _clockSeconds += 0.016d; // One frame later.
            SaveResult throttled = service.Save(CreatePayload(testValue: 2));

            Assert.That(throttled.Status, Is.EqualTo(SaveStatus.Throttled));
            Assert.That(service.Load().Data.Regions[0].TestValue, Is.EqualTo(1));
            Assert.That(File.Exists(_location.BackupFilePath), Is.False, "A throttled save must not touch the disk.");

            _clockSeconds += 5d;
            Assert.That(service.Save(CreatePayload(testValue: 2)).IsSuccess, Is.True);
            Assert.That(service.Load().Data.Regions[0].TestValue, Is.EqualTo(2));
        }

        [Test]
        public void ADeliberateSaveCanBypassTheWriteInterval()
        {
            LocalSaveService service = CreateService(minimumSecondsBetweenSaves: 60d);
            Assert.That(service.Save(CreatePayload(testValue: 1)).IsSuccess, Is.True);

            SaveResult forced = service.Save(CreatePayload(testValue: 2), ignoreWriteInterval: true);

            Assert.That(forced.IsSuccess, Is.True, forced.DeveloperMessage);
            Assert.That(service.Load().Data.Regions[0].TestValue, Is.EqualTo(2));
        }

        [Test]
        public void AnInvalidPayloadIsRejectedBeforeAnythingIsWritten()
        {
            LocalSaveService service = CreateService();

            SaveResult result = service.Save(new SaveGameData());

            Assert.That(result.Status, Is.EqualTo(SaveStatus.InvalidData));
            Assert.That(File.Exists(_location.FilePath), Is.False);
            Assert.That(result.UserMessage, Does.Not.Contain(_directory));
        }

        [Test]
        public void ANullPayloadIsRejectedWithoutThrowing()
        {
            LocalSaveService service = CreateService();

            Assert.That(service.Save(null).Status, Is.EqualTo(SaveStatus.InvalidData));
            Assert.That(File.Exists(_location.FilePath), Is.False);
        }

        [Test]
        public void LoadingABackupThatDoesNotExistIsReportedAsAMissingFile()
        {
            LocalSaveService service = CreateService();

            Assert.That(service.LoadBackup().Status, Is.EqualTo(LoadStatus.NoSaveFile));
        }

        private LocalSaveService CreateService(double minimumSecondsBetweenSaves = 0d)
        {
            return new LocalSaveService(_location, () => _clockSeconds, minimumSecondsBetweenSaves);
        }

        private void WriteRawSaveFile(string contents)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_location.FilePath, contents);
        }

        private static SaveGameData CreatePayload(int testValue = 3)
        {
            return new SaveGameData(
                new PlayerSaveData(12.5f, 1f, -4.25f, 135f),
                new[]
                {
                    new RegionSaveData("nacre/coast/north", testValue),
                    new RegionSaveData("nacre/reef/south", -2),
                },
                new[]
                {
                    new WorldEventSaveData(
                        "event-a",
                        (int)PlayerActionEventType.RegionEntered,
                        1.5d,
                        "nacre/coast/north",
                        null,
                        false,
                        0f,
                        Array.Empty<WorldEventParameterSaveData>()),
                    new WorldEventSaveData(
                        "event-b",
                        (int)PlayerActionEventType.ResourceExtraction,
                        9d,
                        "nacre/coast/north",
                        "nacre/dev/test-object",
                        true,
                        3f,
                        new[] { new WorldEventParameterSaveData("units-extracted", 3f) }),
                });
        }
    }
}

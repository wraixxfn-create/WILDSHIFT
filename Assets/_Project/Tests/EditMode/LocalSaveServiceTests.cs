using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wildshift.Persistence;
using Wildshift.World;
using Wildshift.World.Events;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies the local save foundation against real files in a throwaway temporary directory:
    /// valid round trips, missing files, malformed data, version mismatches, region/event
    /// consistency, backup recovery, and the guarantee that a failed load never writes.
    /// </summary>
    public sealed class LocalSaveServiceTests
    {
        private const string FileName = LocalSaveService.DefaultFileName;
        private const string BackupSuffix = ".bak";
        private const string TemporarySuffix = ".tmp";

        private string _directory;
        private string _savePath;
        private string _backupPath;
        private readonly List<RegionDefinition> _definitions = new List<RegionDefinition>();

        [SetUp]
        public void SetUp()
        {
            // These tests deliberately exercise handled failures, which the save service reports
            // through WildshiftLog.Error, and JsonUtility logs its own error for unusable JSON.
            // Unity's test runner fails a test on an unexpected error log, so this fixture allows
            // them; the tests assert the returned status and error text instead.
            LogAssert.ignoreFailingMessages = true;

            _directory = Path.Combine(Path.GetTempPath(), "wildshift-save-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _savePath = Path.Combine(_directory, FileName);
            _backupPath = _savePath + BackupSuffix;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }

            foreach (RegionDefinition definition in _definitions)
            {
                if (definition != null)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }

            _definitions.Clear();
        }

        [Test]
        public void SavingThenLoadingRestoresTheValidPrototypeState()
        {
            LocalSaveService service = CreateService();
            GameSaveData save = CreateSampleSave(new Vector3(1.5f, 2f, -3.25f), Quaternion.Euler(0f, 90f, 0f));

            SaveLoadStatus saveStatus = service.TrySave(save, out string saveError);
            Assert.That(saveStatus, Is.EqualTo(SaveLoadStatus.Success), saveError);
            Assert.That(saveError, Is.Null);
            Assert.That(service.SaveFileExists(), Is.True);

            SaveLoadStatus loadStatus = service.TryLoad(out GameSaveData loaded, out string loadError);
            Assert.That(loadStatus, Is.EqualTo(SaveLoadStatus.Success), loadError);
            Assert.That(loadError, Is.Null);
            Assert.That(loaded, Is.Not.Null);

            Assert.That(loaded.SchemaVersion, Is.EqualTo(GameSaveData.CurrentSchemaVersion));
            Assert.That(loaded.HasSupportedSchemaVersion, Is.True);
            Assert.That(loaded.GameVersion, Is.EqualTo(save.GameVersion));
            Assert.That(loaded.TryGetSavedAtUtc(out DateTime savedAt), Is.True);
            Assert.That(savedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(5)));

            AssertPosition(loaded.Player.Position, new Vector3(1.5f, 2f, -3.25f));
            AssertOrientation(loaded.Player.Orientation, Quaternion.Euler(0f, 90f, 0f));

            Assert.That(loaded.Regions.Count, Is.EqualTo(2));
            Assert.That(loaded.WorldEvents.Count, Is.EqualTo(3));
        }

        [Test]
        public void LoadingWithoutAnySaveFileReportsNoSaveFileInsteadOfFailing()
        {
            LocalSaveService service = CreateService();
            Assert.That(service.SaveFileExists(), Is.False);

            SaveLoadStatus status = service.TryLoad(out GameSaveData loaded, out string error);

            Assert.That(status, Is.EqualTo(SaveLoadStatus.NoSaveFile));
            Assert.That(loaded, Is.Null);
            Assert.That(error, Does.Contain("No save file exists"));
            Assert.That(Directory.GetFiles(_directory), Is.Empty, "A load must not create files.");
        }

        [Test]
        public void AnEmptySaveFileIsReportedAsMalformedData()
        {
            File.WriteAllText(_savePath, string.Empty);
            LocalSaveService service = CreateService();

            SaveLoadStatus status = service.TryLoad(out GameSaveData loaded, out string error);

            Assert.That(status, Is.EqualTo(SaveLoadStatus.MalformedData));
            Assert.That(loaded, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void UnparseableSaveDataIsRejectedWithoutApplyingOrOverwritingTheFile()
        {
            const string garbage = "this is not a WILDSHIFT save file at all";
            File.WriteAllText(_savePath, garbage);
            LocalSaveService service = CreateService();

            SaveLoadStatus status = service.TryLoad(out GameSaveData loaded, out string error);

            // JsonUtility reports unusable JSON differently between Unity versions (a null result or a
            // thrown parse exception), so the exact status is not pinned down here; what must hold is
            // that the load neither succeeds nor destroys the file it failed on.
            Assert.That(status, Is.Not.EqualTo(SaveLoadStatus.Success));
            Assert.That(status, Is.Not.EqualTo(SaveLoadStatus.NoSaveFile));
            Assert.That(loaded, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllText(_savePath), Is.EqualTo(garbage),
                "A failed load must leave the save exactly as it was.");
            Assert.That(File.Exists(_backupPath), Is.False, "A failed load must not create a backup.");
            Assert.That(File.Exists(_savePath + TemporarySuffix), Is.False, "A load must not write temporary files.");
        }

        [Test]
        public void ASaveFromAnUnsupportedSchemaVersionIsReportedWithoutInterpretingItsData()
        {
            GameSaveData sample = CreateSampleSave(Vector3.zero, Quaternion.identity);
            File.WriteAllText(_savePath, JsonUtility.ToJson(WithSchemaVersion(sample, 999), true));
            LocalSaveService service = CreateService();

            SaveLoadStatus status = service.TryLoad(out GameSaveData loaded, out string error);

            Assert.That(status, Is.EqualTo(SaveLoadStatus.IncompatibleVersion));
            Assert.That(loaded, Is.Null);
            Assert.That(error, Does.Contain("999"));
            Assert.That(error, Does.Contain(GameSaveData.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)));
        }

        [Test]
        public void ASaveFromAnOlderUnsupportedSchemaVersionIsReportedAsIncompatible()
        {
            GameSaveData sample = CreateSampleSave(Vector3.zero, Quaternion.identity);
            File.WriteAllText(_savePath, JsonUtility.ToJson(WithSchemaVersion(sample, 0), true));
            LocalSaveService service = CreateService();

            SaveLoadStatus status = service.TryLoad(out GameSaveData loaded, out string error);

            Assert.That(status, Is.EqualTo(SaveLoadStatus.IncompatibleVersion));
            Assert.That(loaded, Is.Null);
            Assert.That(error, Does.Contain("version 0"));
        }

        [Test]
        public void ASaveWithDuplicatedRegionIdsIsRejectedOnLoad()
        {
            List<RegionSaveData> duplicatedRegions = new List<RegionSaveData>
            {
                new RegionSaveData("nacre/coast/north", 1),
                new RegionSaveData("nacre/coast/north", 2),
            };
            File.WriteAllText(_savePath, JsonUtility.ToJson(CreateSave(duplicatedRegions, new List<PlayerActionEvent>()), true));
            LocalSaveService service = CreateService();

            SaveLoadStatus status = service.TryLoad(out GameSaveData loaded, out string error);

            Assert.That(status, Is.EqualTo(SaveLoadStatus.InvalidData));
            Assert.That(loaded, Is.Null);
            Assert.That(error, Does.Contain("more than once"));
        }

        [Test]
        public void ASaveWithANonChronologicalWorldEventHistoryIsRejectedOnLoad()
        {
            List<PlayerActionEvent> unorderedEvents = new List<PlayerActionEvent>
            {
                new PlayerActionEvent("ev-late", PlayerActionEventType.RegionEntered, 10d),
                new PlayerActionEvent("ev-early", PlayerActionEventType.RegionEntered, 2d),
            };
            File.WriteAllText(_savePath, JsonUtility.ToJson(CreateSave(new List<RegionSaveData>(), unorderedEvents), true));
            LocalSaveService service = CreateService();

            SaveLoadStatus status = service.TryLoad(out GameSaveData loaded, out string error);

            Assert.That(status, Is.EqualTo(SaveLoadStatus.InvalidData));
            Assert.That(loaded, Is.Null);
            Assert.That(error, Does.Contain("world event 1"));
        }

        [Test]
        public void SavingInvalidDataIsRejectedAndWritesNoFile()
        {
            LocalSaveService service = CreateService();
            GameSaveData invalid = CreateSave(
                new List<RegionSaveData>(),
                new List<PlayerActionEvent>(),
                new PlayerSaveData(new Vector3(float.NaN, 0f, 0f), Quaternion.identity));

            SaveLoadStatus status = service.TrySave(invalid, out string error);

            Assert.That(status, Is.EqualTo(SaveLoadStatus.InvalidData));
            Assert.That(error, Does.Contain("position"));
            Assert.That(service.SaveFileExists(), Is.False);
        }

        [Test]
        public void SavingWithoutAnySaveDataIsRejected()
        {
            LocalSaveService service = CreateService();

            SaveLoadStatus status = service.TrySave(null, out string error);

            Assert.That(status, Is.EqualTo(SaveLoadStatus.InvalidArgument));
            Assert.That(error, Does.Contain("no save data"));
            Assert.That(service.SaveFileExists(), Is.False);
        }

        [Test]
        public void SavingTwiceKeepsThePreviousSaveAsABackupAndLeavesNoTemporaryFile()
        {
            LocalSaveService service = CreateService();
            Assert.That(service.TrySave(CreateSampleSave(new Vector3(1f, 2f, 3f), Quaternion.identity), out string firstError),
                Is.EqualTo(SaveLoadStatus.Success), firstError);
            string firstContents = File.ReadAllText(_savePath);

            Assert.That(service.TrySave(CreateSampleSave(new Vector3(4f, 5f, 6f), Quaternion.identity), out string secondError),
                Is.EqualTo(SaveLoadStatus.Success), secondError);

            Assert.That(File.ReadAllText(_backupPath), Is.EqualTo(firstContents),
                "The previous save must be preserved as a backup.");
            Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(2),
                "Only the save and its backup should remain on disk.");
            Assert.That(File.Exists(_savePath + TemporarySuffix), Is.False);
        }

        [Test]
        public void ACorruptedPrimarySaveRecoversFromTheBackupAndNothingIsOverwritten()
        {
            LocalSaveService service = CreateService();
            SaveTwoGenerations(service);
            string backupContents = File.ReadAllText(_backupPath);

            const string corrupted = "{ interrupted mid-write";
            File.WriteAllText(_savePath, corrupted);

            SaveLoadStatus status = service.TryLoad(out GameSaveData loaded, out string error);

            Assert.That(status, Is.EqualTo(SaveLoadStatus.RecoveredFromBackup));
            Assert.That(error, Is.Null);
            Assert.That(loaded, Is.Not.Null);
            AssertPosition(loaded.Player.Position, new Vector3(1f, 2f, 3f));
            Assert.That(File.ReadAllText(_savePath), Is.EqualTo(corrupted),
                "Recovering from the backup must not rewrite the primary file.");
            Assert.That(File.ReadAllText(_backupPath), Is.EqualTo(backupContents));
        }

        [Test]
        public void AMissingPrimarySaveRecoversFromTheBackup()
        {
            LocalSaveService service = CreateService();
            SaveTwoGenerations(service);
            File.Delete(_savePath);
            Assert.That(service.SaveFileExists(), Is.False);

            SaveLoadStatus status = service.TryLoad(out GameSaveData loaded, out string error);

            Assert.That(status, Is.EqualTo(SaveLoadStatus.RecoveredFromBackup));
            Assert.That(loaded, Is.Not.Null);
            AssertPosition(loaded.Player.Position, new Vector3(1f, 2f, 3f));
        }

        [Test]
        public void SaveLoadKeepsRegionIdsAndWorldEventOrderingConsistent()
        {
            LocalSaveService service = CreateService();
            GameSaveData save = CreateSampleSave(Vector3.zero, Quaternion.identity);
            Assert.That(service.TrySave(save, out string saveError), Is.EqualTo(SaveLoadStatus.Success), saveError);

            Assert.That(service.TryLoad(out GameSaveData loaded, out string loadError),
                Is.EqualTo(SaveLoadStatus.Success), loadError);

            Assert.That(loaded.Regions.Count, Is.EqualTo(2));
            Assert.That(loaded.Regions[0].StableId, Is.EqualTo("nacre/basin/central"));
            Assert.That(loaded.Regions[0].TestValue, Is.EqualTo(7));
            Assert.That(loaded.Regions[1].StableId, Is.EqualTo("nacre/coast/north"));
            Assert.That(loaded.Regions[1].TestValue, Is.EqualTo(41));

            Assert.That(loaded.WorldEvents.Count, Is.EqualTo(3));
            Assert.That(loaded.WorldEvents[0].Id, Is.EqualTo("ev-alpha"));
            Assert.That(loaded.WorldEvents[0].EventType, Is.EqualTo(PlayerActionEventType.RegionEntered));
            Assert.That(loaded.WorldEvents[0].RegionId, Is.EqualTo("nacre/coast/north"));
            Assert.That(loaded.WorldEvents[1].Id, Is.EqualTo("ev-beta"));
            Assert.That(loaded.WorldEvents[1].TargetId, Is.EqualTo("nacre/dev/test-object"));
            Assert.That(loaded.WorldEvents[2].Id, Is.EqualTo("ev-gamma"));
            Assert.That(loaded.WorldEvents[2].HasMagnitude, Is.True);
            Assert.That(loaded.WorldEvents[2].Magnitude, Is.EqualTo(4f));

            for (int index = 1; index < loaded.WorldEvents.Count; index++)
            {
                Assert.That(loaded.WorldEvents[index].ElapsedWorldTime,
                    Is.GreaterThanOrEqualTo(loaded.WorldEvents[index - 1].ElapsedWorldTime),
                    "Saved world events must stay in chronological order.");
            }

            // The restored history must be acceptable to a live recorder, in the order it was saved.
            PlayerActionEventRecorder restored = new PlayerActionEventRecorder();
            for (int index = 0; index < loaded.WorldEvents.Count; index++)
            {
                Assert.That(restored.TryRecord(loaded.WorldEvents[index], out _, out string recordError),
                    Is.True, recordError);
            }

            Assert.That(restored.Count, Is.EqualTo(3));
        }

        [Test]
        public void AWriteThatCannotReachTheDiskIsReportedWithoutDestroyingAnything()
        {
            // A path whose parent component is an ordinary file cannot be created on any platform.
            string blocker = Path.Combine(_directory, "blocker");
            File.WriteAllText(blocker, "not a directory");
            LocalSaveService service = new LocalSaveService(Path.Combine(blocker, "saves"), FileName);

            SaveLoadStatus status = service.TrySave(CreateSampleSave(Vector3.zero, Quaternion.identity), out string error);

            Assert.That(status, Is.EqualTo(SaveLoadStatus.WriteFailed));
            Assert.That(error, Is.Not.Empty);
            Assert.That(File.Exists(blocker), Is.True);
        }

        [Test]
        public void SavePathsStayInsideTheConfiguredDirectory()
        {
            LocalSaveService service = CreateService();

            Assert.That(service.SaveDirectory, Is.EqualTo(_directory));
            Assert.That(service.SaveFilePath, Is.EqualTo(Path.Combine(_directory, FileName)));
            Assert.That(service.BackupFilePath, Is.EqualTo(_savePath + BackupSuffix));
        }

        [Test]
        public void TheServiceRejectsAnUnusableFileName()
        {
            Assert.Throws<ArgumentException>(() => new LocalSaveService(_directory, " "));
            Assert.Throws<ArgumentException>(() => new LocalSaveService(_directory, "nested" + Path.DirectorySeparatorChar + "save.json"));
        }

        private LocalSaveService CreateService()
        {
            return new LocalSaveService(_directory, FileName);
        }

        private void SaveTwoGenerations(LocalSaveService service)
        {
            Assert.That(service.TrySave(CreateSampleSave(new Vector3(1f, 2f, 3f), Quaternion.identity), out string firstError),
                Is.EqualTo(SaveLoadStatus.Success), firstError);
            Assert.That(service.TrySave(CreateSampleSave(new Vector3(4f, 5f, 6f), Quaternion.identity), out string secondError),
                Is.EqualTo(SaveLoadStatus.Success), secondError);
        }

        private GameSaveData CreateSampleSave(Vector3 position, Quaternion orientation)
        {
            return GameSaveMapper.Capture(position, orientation, CreateWorldState(), CreateRecorder());
        }

        private static GameSaveData CreateSave(
            List<RegionSaveData> regions,
            List<PlayerActionEvent> worldEvents,
            PlayerSaveData player = null)
        {
            return new GameSaveData(
                GameSaveData.CurrentSchemaVersion,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                "unit-test",
                player ?? new PlayerSaveData(Vector3.zero, Quaternion.identity),
                regions,
                worldEvents);
        }

        private static GameSaveData WithSchemaVersion(GameSaveData source, int schemaVersion)
        {
            return new GameSaveData(
                schemaVersion,
                source.SavedAtUtc,
                source.GameVersion,
                source.Player,
                source.Regions,
                source.WorldEvents);
        }

        private WorldStateService CreateWorldState()
        {
            WorldStateService worldState = new WorldStateService();
            Assert.That(worldState.TryRegisterRegion(CreateDefinition("nacre/coast/north", "North Coast", 1), out string firstError),
                Is.True, firstError);
            Assert.That(worldState.TryRegisterRegion(CreateDefinition("nacre/basin/central", "Central Basin", 2), out string secondError),
                Is.True, secondError);
            Assert.That(worldState.TryUpdateTestValue("nacre/coast/north", 41, out string updateError), Is.True, updateError);
            Assert.That(worldState.TryUpdateTestValue("nacre/basin/central", 7, out updateError), Is.True, updateError);
            return worldState;
        }

        private static PlayerActionEventRecorder CreateRecorder()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            Record(recorder, new PlayerActionEvent(
                "ev-alpha", PlayerActionEventType.RegionEntered, 1.5d, regionId: "nacre/coast/north"));
            Record(recorder, new PlayerActionEvent(
                "ev-beta", PlayerActionEventType.ObjectInteraction, 2.25d, targetId: "nacre/dev/test-object"));
            Record(recorder, new PlayerActionEvent(
                "ev-gamma", PlayerActionEventType.ResourceExtraction, 9.5d,
                regionId: "nacre/basin/central", magnitude: 4f));
            return recorder;
        }

        private static void Record(PlayerActionEventRecorder recorder, PlayerActionEvent playerActionEvent)
        {
            Assert.That(recorder.TryRecord(playerActionEvent, out _, out string error), Is.True, error);
        }

        private RegionDefinition CreateDefinition(string stableId, string displayName, int initialTestValue)
        {
            RegionDefinition definition = ScriptableObject.CreateInstance<RegionDefinition>();
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("_stableId").stringValue = stableId;
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_initialTestValue").intValue = initialTestValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _definitions.Add(definition);
            return definition;
        }

        private static void AssertPosition(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-4f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-4f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-4f));
        }

        private static void AssertOrientation(Quaternion actual, Quaternion expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-4f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-4f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-4f));
            Assert.That(actual.w, Is.EqualTo(expected.w).Within(1e-4f));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wildshift.Environment.Samples;
using Wildshift.Persistence;
using Wildshift.Player.Movement;
using Wildshift.Player.Regions;
using Wildshift.World;
using Wildshift.World.Clock;
using Wildshift.World.Events;
using Wildshift.World.Regions;

namespace Wildshift.Tests
{
    /// <summary>
    /// Runs the prototype save controller against scene-shaped objects built in code. It covers the required
    /// sequence (save, collect the sample, save again, move, load) and the failure cases: missing, malformed,
    /// unsupported-version, unknown-region, and unusable-position data. The save file lives in a temporary
    /// directory, and nothing is read from or written to the real save location.
    /// </summary>
    public sealed class PrototypeSaveControllerTests
    {
        private const string WestRegionId = "nacre/test/save-west";
        private const string EastRegionId = "nacre/test/save-east";
        private const string SampleId = "nacre/sample/save-test-a";
        private static readonly Vector3 WestPosition = new Vector3(-4f, 1f, 1.5f);
        private static readonly Quaternion WestRotation = Quaternion.Euler(0f, 90f, 0f);
        private static readonly Vector3 EastPosition = new Vector3(6f, 1f, -3f);

        private WorldRegionTestObjects _regionObjects;
        private string _directory;
        private LocalSaveService _saveService;
        private GameObject _playerObject;
        private CharacterController _characterController;
        private PlayerRegionAssociation _playerRegion;
        private WorldClockHost _clockHost;
        private PlayerActionEventRecorderHost _recorderHost;
        private EnvironmentalSampleInteractable _sample;
        private EnvironmentalSampleDefinition _sampleDefinition;
        private Transform _safeSpawn;
        private PrototypeSaveController _controller;
        private GameObject _controllerObject;

        [SetUp]
        public void SetUp()
        {
            // Recovery notes are logged as warnings, and the controller reports refused setups as errors. The tests
            // assert the returned results and the scene state instead of the log text.
            LogAssert.ignoreFailingMessages = true;

            _directory = Path.Combine(Path.GetTempPath(), "wildshift-controller-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _saveService = new LocalSaveService(_directory, LocalSaveService.DefaultFileName);

            _regionObjects = new WorldRegionTestObjects();
            RegionDefinition west = _regionObjects.CreateDefinition(WestRegionId, "West");
            RegionDefinition east = _regionObjects.CreateDefinition(EastRegionId, "East");
            WorldRegionCatalog catalog = _regionObjects.CreateCatalog(west, east);

            GameObject locatorObject = _regionObjects.CreateObject("Save Test Region Locator");
            locatorObject.SetActive(false);
            WorldRegionLocator locator = locatorObject.AddComponent<WorldRegionLocator>();
            WorldRegionTestObjects.SetLocatorCatalog(locator, catalog);
            WorldRegionVolume westVolume = _regionObjects.CreateVolume(
                locatorObject.transform, west, new Vector3(-5f, 0f, 0f), new Vector3(10f, 10f, 10f), 0);
            WorldRegionVolume eastVolume = _regionObjects.CreateVolume(
                locatorObject.transform, east, new Vector3(5f, 0f, 0f), new Vector3(10f, 10f, 10f), 0);
            WorldRegionTestObjects.SetLocatorVolumes(locator, new[] { westVolume, eastVolume });
            locatorObject.SetActive(true);

            _playerObject = _regionObjects.CreateObject("Save Test Player");
            _playerObject.SetActive(false);
            _playerObject.transform.position = WestPosition;
            _characterController = _playerObject.AddComponent<CharacterController>();
            _playerObject.AddComponent<ThirdPersonPlayerMovement>();
            _playerRegion = _playerObject.AddComponent<PlayerRegionAssociation>();
            SetObjectReference(_playerRegion, "_regionLocator", locator);
            _playerObject.SetActive(true);

            // The clock and the event recorder share one object, as they do in the scene.
            GameObject servicesObject = _regionObjects.CreateObject("Save Test World Services");
            servicesObject.SetActive(false);
            _clockHost = servicesObject.AddComponent<WorldClockHost>();
            _recorderHost = servicesObject.AddComponent<PlayerActionEventRecorderHost>();
            servicesObject.SetActive(true);

            _sampleDefinition = ScriptableObject.CreateInstance<EnvironmentalSampleDefinition>();
            SetDefinitionField(_sampleDefinition, "_stableId", SampleId);
            SetDefinitionField(_sampleDefinition, "_displayName", "Save test soil");

            GameObject sampleObject = _regionObjects.CreateObject("Save Test Sample");
            sampleObject.SetActive(false);
            sampleObject.transform.position = new Vector3(5f, 0f, 0f);
            sampleObject.AddComponent<BoxCollider>();
            _sample = sampleObject.AddComponent<EnvironmentalSampleInteractable>();
            SetObjectReference(_sample, "_definition", _sampleDefinition);
            SetObjectReference(_sample, "_clockHost", _clockHost);
            SetObjectReference(_sample, "_eventRecorderHost", _recorderHost);
            sampleObject.SetActive(true);

            GameObject safeSpawnObject = _regionObjects.CreateObject("Save Test Safe Spawn");
            safeSpawnObject.transform.position = new Vector3(-6f, 1.05f, 2f);
            _safeSpawn = safeSpawnObject.transform;

            _controllerObject = _regionObjects.CreateObject("Save Test Controller");
            _controllerObject.SetActive(false);
            _controller = _controllerObject.AddComponent<PrototypeSaveController>();
            SerializedObject serialized = new SerializedObject(_controller);
            serialized.FindProperty("_regionCatalog").objectReferenceValue = catalog;
            serialized.FindProperty("_playerRegion").objectReferenceValue = _playerRegion;
            serialized.FindProperty("_playerMovement").objectReferenceValue = _playerObject.GetComponent<ThirdPersonPlayerMovement>();
            serialized.FindProperty("_clockHost").objectReferenceValue = _clockHost;
            serialized.FindProperty("_eventRecorderHost").objectReferenceValue = _recorderHost;
            serialized.FindProperty("_safeSpawn").objectReferenceValue = _safeSpawn;
            SerializedProperty samples = serialized.FindProperty("_samples");
            samples.arraySize = 1;
            samples.GetArrayElementAtIndex(0).objectReferenceValue = _sample;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _controllerObject.SetActive(true);
            _controller.UseSaveServiceForTests(_saveService);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            _regionObjects.DestroyAll();
            if (_sampleDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(_sampleDefinition);
            }

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void SaveCollectSaveMoveLoadRestoresPoseRegionSampleAndBoundedHistory()
        {
            PlaceAndRefresh(WestPosition, WestRotation);
            _clockHost.Clock.AdvanceManually(10d);

            SaveOperationResult first = _controller.SaveGame();
            Assert.That(first.Status, Is.EqualTo(SaveLoadStatus.Success), first.Message);

            _sample.Interact(_playerObject);
            Assert.That(_sample.IsCollected, Is.True);
            Assert.That(_recorderHost.Recorder.Count, Is.EqualTo(1));

            _clockHost.Clock.AdvanceManually(5d);
            SaveOperationResult second = _controller.SaveGame();
            Assert.That(second.Status, Is.EqualTo(SaveLoadStatus.Success), second.Message);

            // Move elsewhere, advance time, and record an event after the save, so the load must undo all of it.
            PlaceAndRefresh(EastPosition, Quaternion.identity);
            Assert.That(_playerRegion.CurrentRegionId, Is.EqualTo(EastRegionId));
            _clockHost.Clock.AdvanceManually(20d);
            Assert.That(_recorderHost.Recorder.TryRecord(
                    new PlayerActionEvent(PlayerActionEvent.NewId(), PlayerActionEventType.RegionEntered, 25d, regionId: EastRegionId),
                    out _, out string postSaveError),
                Is.True, postSaveError);

            SaveOperationResult loaded = _controller.LoadGame();
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Success), loaded.Message);
            Assert.That(loaded.HasRecoveries, Is.False, string.Join(" | ", loaded.Recoveries));

            Assert.That(_playerObject.transform.position.x, Is.EqualTo(WestPosition.x).Within(1e-4f));
            Assert.That(_playerObject.transform.position.y, Is.EqualTo(WestPosition.y).Within(1e-4f));
            Assert.That(_playerObject.transform.position.z, Is.EqualTo(WestPosition.z).Within(1e-4f));
            Assert.That(Quaternion.Angle(_playerObject.transform.rotation, WestRotation), Is.LessThan(0.01f));

            Assert.That(_playerRegion.CurrentRegionId, Is.EqualTo(WestRegionId),
                "The region association is recomputed from the restored position.");

            Assert.That(_sample.IsCollected, Is.True, "The sample collected before the second save stays collected.");
            Assert.That(_sample.CanInteract(_playerObject), Is.False);

            IReadOnlyList<PlayerActionEvent> history = _recorderHost.Recorder.GetRecentEvents(10);
            Assert.That(history.Count, Is.EqualTo(1),
                "Only the collection record from before the second save may remain: the post-save event must be gone.");
            Assert.That(history[0].TargetId, Is.EqualTo(SampleId));
            Assert.That(history[0].EventType, Is.EqualTo(PlayerActionEventType.ResourceExtraction));
            Assert.That(history[0].RegionId, Is.EqualTo(WestRegionId),
                "The collection keeps the region the player was in when it happened.");

            Assert.That(_clockHost.Clock.ElapsedTime, Is.EqualTo(15d).Within(1e-6d),
                "The world clock is restored to the saved time.");

            _sample.Interact(_playerObject);
            Assert.That(_recorderHost.Recorder.Count, Is.EqualTo(1),
                "Interacting with the restored, collected sample must not record a second collection.");

            Assert.That(_recorderHost.Recorder.TryRecord(
                    new PlayerActionEvent(PlayerActionEvent.NewId(), PlayerActionEventType.RegionEntered, 15.5d, regionId: WestRegionId),
                    out _, out string afterLoadError),
                Is.True, "New events after a load must be accepted at the restored clock: " + afterLoadError);
        }

        [Test]
        public void LoadingAnEarlierSaveMakesTheSampleCollectableAgain()
        {
            PlaceAndRefresh(WestPosition, WestRotation);
            Assert.That(_controller.SaveGame().IsSuccess, Is.True);

            _sample.Interact(_playerObject);
            Assert.That(_sample.IsCollected, Is.True);

            SaveOperationResult loaded = _controller.LoadGame();

            Assert.That(loaded.IsSuccess, Is.True, loaded.Message);
            Assert.That(_sample.IsCollected, Is.False, "The earlier save predates the collection.");
            Assert.That(_sample.CanInteract(_playerObject), Is.True);
            Assert.That(_recorderHost.Recorder.Count, Is.EqualTo(0));

            _sample.Interact(_playerObject);
            Assert.That(_recorderHost.Recorder.Count, Is.EqualTo(1));
        }

        [Test]
        public void SavesCarryOnlyTheBoundedNewestEventHistory()
        {
            PlaceAndRefresh(WestPosition, WestRotation);
            int total = GameSaveData.DefaultMaxWorldEvents + 40;
            for (int index = 0; index < total; index++)
            {
                Assert.That(_recorderHost.Recorder.TryRecord(
                        new PlayerActionEvent(PlayerActionEvent.NewId(), PlayerActionEventType.RegionEntered,
                            1d + index * 0.5d, regionId: WestRegionId),
                        out _, out string error),
                    Is.True, error);
            }

            Assert.That(_controller.SaveGame().IsSuccess, Is.True);

            Assert.That(_saveService.TryLoad(out GameSaveData saved, out string loadError),
                Is.EqualTo(SaveLoadStatus.Success), loadError);
            Assert.That(saved.WorldEvents.Count, Is.EqualTo(GameSaveData.DefaultMaxWorldEvents),
                "The save must hold at most the default bounded history, never the whole log.");
            Assert.That(saved.WorldEvents[saved.WorldEvents.Count - 1].ElapsedWorldTime,
                Is.EqualTo(1d + (total - 1) * 0.5d).Within(1e-9d), "The newest events are the ones kept.");
        }

        [Test]
        public void LoadingWithNoSaveFileReportsItAndLeavesTheSessionAlone()
        {
            PlaceAndRefresh(EastPosition, Quaternion.identity);
            _sample.Interact(_playerObject);

            SaveOperationResult result = _controller.LoadGame();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.NoSaveFile));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Is.Not.Empty);
            Assert.That(_playerObject.transform.position.x, Is.EqualTo(EastPosition.x).Within(1e-4f));
            Assert.That(_sample.IsCollected, Is.True, "A refused load must not touch the sample.");
            Assert.That(_recorderHost.Recorder.Count, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(_directory), Is.Empty, "A load must not create files.");
        }

        [Test]
        public void MalformedSaveDataIsRejectedAndTheFileIsLeftUntouched()
        {
            const string garbage = "{ this is not a WILDSHIFT save";
            string path = _saveService.SaveFilePath;
            File.WriteAllText(path, garbage);
            PlaceAndRefresh(EastPosition, Quaternion.identity);

            SaveOperationResult result = _controller.LoadGame();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Status, Is.Not.EqualTo(SaveLoadStatus.NoSaveFile));
            Assert.That(File.ReadAllText(path), Is.EqualTo(garbage), "A rejected load must never rewrite the file.");
            Assert.That(_playerObject.transform.position.x, Is.EqualTo(EastPosition.x).Within(1e-4f));
        }

        [Test]
        public void SaveDataFromAnUnsupportedVersionIsRejectedWithoutBeingApplied()
        {
            GameSaveData future = new GameSaveData(
                999,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                "future-build",
                new PlayerSaveData(WestPosition, WestRotation),
                new List<RegionSaveData>(),
                new List<PlayerActionEvent>());
            File.WriteAllText(_saveService.SaveFilePath, JsonUtility.ToJson(future, true));
            PlaceAndRefresh(EastPosition, Quaternion.identity);

            SaveOperationResult result = _controller.LoadGame();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.IncompatibleVersion));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(_playerObject.transform.position.x, Is.EqualTo(EastPosition.x).Within(1e-4f));
        }

        [Test]
        public void ASavedRegionThisBuildDoesNotRegisterIsIgnoredAndTheRegionComesFromThePosition()
        {
            SaveRaw(new Vector3(-4f, 1f, 1f), Quaternion.identity, "nacre/test/removed-region", Array.Empty<string>());
            PlaceAndRefresh(EastPosition, Quaternion.identity);

            SaveOperationResult result = _controller.LoadGame();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Success), result.Message);
            Assert.That(ContainsNote(result, "not in this build's region catalog"), Is.True,
                "The unknown region must be reported as a recovery.");
            Assert.That(_playerRegion.CurrentRegionId, Is.EqualTo(WestRegionId));
            Assert.That(_playerObject.transform.position.x, Is.EqualTo(-4f).Within(1e-4f));
        }

        [Test]
        public void APositionInsideSolidGeometryIsReplacedBySafeSpawn()
        {
            Vector3 blocked = new Vector3(-4f, 1f, 1f);
            SaveRaw(blocked, Quaternion.identity, null, Array.Empty<string>());

            GameObject wall = _regionObjects.CreateObject("Save Test Wall");
            wall.transform.position = blocked;
            wall.AddComponent<BoxCollider>().size = new Vector3(3f, 3f, 3f);
            Physics.SyncTransforms();
            PlaceAndRefresh(EastPosition, Quaternion.identity);

            SaveOperationResult result = _controller.LoadGame();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Success), result.Message);
            Assert.That(ContainsNote(result, "safe spawn point"), Is.True, "The recovery must be reported.");
            Assert.That(_playerObject.transform.position.x, Is.EqualTo(_safeSpawn.position.x).Within(1e-4f));
            Assert.That(_playerObject.transform.position.z, Is.EqualTo(_safeSpawn.position.z).Within(1e-4f));
        }

        [Test]
        public void APositionBelowTheWorldIsReplacedBySafeSpawn()
        {
            SaveRaw(new Vector3(-4f, -50f, 1f), Quaternion.identity, null, Array.Empty<string>());
            PlaceAndRefresh(EastPosition, Quaternion.identity);

            SaveOperationResult result = _controller.LoadGame();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Success), result.Message);
            Assert.That(ContainsNote(result, "safe spawn point"), Is.True);
            Assert.That(_playerObject.transform.position.y, Is.EqualTo(_safeSpawn.position.y).Within(1e-4f));
        }

        [Test]
        public void ASavedSampleThatIsNotInThisSceneIsIgnoredWithANote()
        {
            SaveRaw(WestPosition, WestRotation, null, new[] { "nacre/sample/not-in-this-scene" });
            PlaceAndRefresh(EastPosition, Quaternion.identity);

            SaveOperationResult result = _controller.LoadGame();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Success), result.Message);
            Assert.That(ContainsNote(result, "not in this scene"), Is.True);
            Assert.That(_sample.IsCollected, Is.False);
        }

        [Test]
        public void AControllerWhoseWiringIsIncompleteRefusesToSaveOrLoad()
        {
            SerializedObject serialized = new SerializedObject(_controller);
            serialized.FindProperty("_safeSpawn").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SaveOperationResult save = _controller.SaveGame();
            SaveOperationResult load = _controller.LoadGame();

            Assert.That(save.IsSuccess, Is.False);
            Assert.That(save.Status, Is.EqualTo(SaveLoadStatus.InvalidArgument));
            Assert.That(load.IsSuccess, Is.False);
            Assert.That(Directory.GetFiles(_directory), Is.Empty, "A refused save must not write a file.");
        }

        private void PlaceAndRefresh(Vector3 position, Quaternion rotation)
        {
            _playerObject.transform.SetPositionAndRotation(position, rotation);
            _playerRegion.RefreshRegionAssociation();
        }

        /// <summary>Writes a save file the way the game would, with the given player block and sample list.</summary>
        private void SaveRaw(Vector3 position, Quaternion rotation, string regionId, IReadOnlyList<string> sampleIds)
        {
            GameSaveData save = new GameSaveData(
                GameSaveData.CurrentSchemaVersion,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                "test",
                new PlayerSaveData(position, rotation, regionId),
                new List<RegionSaveData>(),
                new List<PlayerActionEvent>(),
                0d,
                sampleIds);
            Assert.That(_saveService.TrySave(save, out string error), Is.EqualTo(SaveLoadStatus.Success), error);
        }

        private static bool ContainsNote(SaveOperationResult result, string fragment)
        {
            foreach (string note in result.Recoveries)
            {
                if (note.Contains(fragment))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetDefinitionField(ScriptableObject definition, string propertyName, string value)
        {
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

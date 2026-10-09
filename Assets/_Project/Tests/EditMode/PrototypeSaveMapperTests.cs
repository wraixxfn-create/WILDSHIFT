using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wildshift.Persistence;
using Wildshift.Persistence.Model;
using Wildshift.World;
using Wildshift.World.Events;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies that live prototype state survives a capture/restore cycle: region values stay with
    /// their stable IDs, the world-event history keeps its order and details, and mismatched or
    /// already-used targets are reported instead of silently misapplied.
    /// </summary>
    public sealed class PrototypeSaveMapperTests
    {
        private readonly List<RegionDefinition> _definitions = new List<RegionDefinition>();
        private readonly List<GameObject> _gameObjects = new List<GameObject>();
        private string _directory;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            foreach (RegionDefinition definition in _definitions)
            {
                if (definition != null)
                {
                    Object.DestroyImmediate(definition);
                }
            }

            _definitions.Clear();

            foreach (GameObject gameObject in _gameObjects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            _gameObjects.Clear();

            if (!string.IsNullOrEmpty(_directory) && Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }

            _directory = null;
        }

        [Test]
        public void CaptureStoresRegionStateKeyedByStableIdAndNotByDisplayName()
        {
            WorldStateService worldState = CreateWorldState(
                ("nacre/coast/north", "North Coast", 1),
                ("nacre/reef/south", "South Reef", 2));
            Assert.That(worldState.TryUpdateTestValue("nacre/coast/north", 11, out string updateError),
                Is.True, updateError);

            SaveGameData data = PrototypeSaveMapper.CaptureState(new PlayerSaveData(), worldState, null);

            Assert.That(data.SchemaVersion, Is.EqualTo(SaveSchema.CurrentVersion));
            Assert.That(data.Regions.Length, Is.EqualTo(2));
            Assert.That(data.Regions[0].StableId, Is.EqualTo("nacre/coast/north"));
            Assert.That(data.Regions[0].TestValue, Is.EqualTo(11));
            Assert.That(data.Regions[1].StableId, Is.EqualTo("nacre/reef/south"));
            Assert.That(data.Regions[1].TestValue, Is.EqualTo(2));
        }

        [Test]
        public void RegionValuesReturnToTheMatchingStableIdRegardlessOfRegistrationOrder()
        {
            WorldStateService session = CreateWorldState(
                ("nacre/coast/north", "North Coast", 0),
                ("nacre/reef/south", "South Reef", 0),
                ("nacre/valley/west", "West Valley", 0));
            Assert.That(session.TryUpdateTestValue("nacre/coast/north", 11, out string firstError), Is.True, firstError);
            Assert.That(session.TryUpdateTestValue("nacre/reef/south", 22, out string secondError), Is.True, secondError);
            Assert.That(session.TryUpdateTestValue("nacre/valley/west", 33, out string thirdError), Is.True, thirdError);
            SaveGameData data = PrototypeSaveMapper.CaptureState(new PlayerSaveData(), session, null);

            // A later session may register the same regions in a different order; IDs are the identity.
            WorldStateService reloaded = CreateWorldState(
                ("nacre/valley/west", "West Valley", 0),
                ("nacre/coast/north", "North Coast", 0),
                ("nacre/reef/south", "South Reef", 0));

            Assert.That(
                PrototypeSaveMapper.TryRestoreWorldState(data, reloaded, null, out SaveRestoreSummary summary,
                    out string restoreError),
                Is.True, restoreError);

            Assert.That(summary.AppliedRegionCount, Is.EqualTo(3));
            Assert.That(summary.SkippedRegionCount, Is.EqualTo(0));
            AssertRegionValue(reloaded, "nacre/coast/north", 11);
            AssertRegionValue(reloaded, "nacre/reef/south", 22);
            AssertRegionValue(reloaded, "nacre/valley/west", 33);
        }

        [Test]
        public void ASavedRegionThatNoLongerExistsIsSkippedInsteadOfAppliedElsewhere()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(),
                new[]
                {
                    new RegionSaveData("nacre/coast/north", 11),
                    new RegionSaveData("nacre/retired/region", 99),
                },
                Array.Empty<WorldEventSaveData>());
            WorldStateService worldState = CreateWorldState(("nacre/coast/north", "North Coast", 0));

            Assert.That(
                PrototypeSaveMapper.TryRestoreWorldState(data, worldState, null, out SaveRestoreSummary summary,
                    out string restoreError),
                Is.True, restoreError);

            Assert.That(summary.AppliedRegionCount, Is.EqualTo(1));
            Assert.That(summary.SkippedRegionCount, Is.EqualTo(1));
            AssertRegionValue(worldState, "nacre/coast/north", 11);
        }

        [Test]
        public void EventHistoryKeepsItsOrderAndDetailsThroughCaptureAndRestore()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            RecordEvent(recorder, "event-a", PlayerActionEventType.RegionEntered, 1d);
            RecordEvent(recorder, "event-b", PlayerActionEventType.ObjectInteraction, 4.5d);
            Assert.That(
                recorder.TryRecord(
                    new PlayerActionEvent(
                        "event-c",
                        PlayerActionEventType.ResourceExtraction,
                        9d,
                        "nacre/coast/north",
                        "nacre/dev/test-object",
                        3f,
                        new[] { new PlayerActionEventParameter("units-extracted", 3f) }),
                    out PlayerActionEvent _,
                    out string recordError),
                Is.True, recordError);

            SaveGameData data = PrototypeSaveMapper.CaptureState(new PlayerSaveData(), null, recorder);

            Assert.That(data.Events.Length, Is.EqualTo(3));
            Assert.That(data.Events[0].Id, Is.EqualTo("event-a"));
            Assert.That(data.Events[2].Id, Is.EqualTo("event-c"));

            PlayerActionEventRecorder restored = new PlayerActionEventRecorder();
            Assert.That(
                PrototypeSaveMapper.TryRestoreWorldState(data, null, restored, out SaveRestoreSummary summary,
                    out string restoreError),
                Is.True, restoreError);

            Assert.That(summary.RestoredEventCount, Is.EqualTo(3));
            IReadOnlyList<PlayerActionEvent> events = restored.GetRecentEvents(restored.Count);
            Assert.That(events.Count, Is.EqualTo(3));
            Assert.That(events[0].Id, Is.EqualTo("event-a"));
            Assert.That(events[1].Id, Is.EqualTo("event-b"));
            Assert.That(events[2].Id, Is.EqualTo("event-c"));
            Assert.That(events[0].ElapsedWorldTime, Is.EqualTo(1d).Within(0.0001d));
            Assert.That(events[2].ElapsedWorldTime, Is.EqualTo(9d).Within(0.0001d));
            Assert.That(events[2].EventType, Is.EqualTo(PlayerActionEventType.ResourceExtraction));
            Assert.That(events[2].RegionId, Is.EqualTo("nacre/coast/north"));
            Assert.That(events[2].TargetId, Is.EqualTo("nacre/dev/test-object"));
            Assert.That(events[2].HasMagnitude, Is.True);
            Assert.That(events[2].Magnitude, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(events[2].Parameters.Count, Is.EqualTo(1));
            Assert.That(events[2].Parameters[0].Id, Is.EqualTo("units-extracted"));
        }

        [Test]
        public void OptionalEventIdsThatWereAbsentStayAbsentAfterRestore()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            RecordEvent(recorder, "event-a", PlayerActionEventType.ObjectInteraction, 2d, regionId: null);
            SaveGameData data = PrototypeSaveMapper.CaptureState(new PlayerSaveData(), null, recorder);
            Assert.That(data.Events[0].RegionId, Is.Empty);

            PlayerActionEventRecorder restored = new PlayerActionEventRecorder();
            Assert.That(
                PrototypeSaveMapper.TryRestoreWorldState(data, null, restored, out SaveRestoreSummary _,
                    out string restoreError),
                Is.True, restoreError);

            PlayerActionEvent restoredEvent = restored.GetRecentEvents(1)[0];
            Assert.That(restoredEvent.HasRegionId, Is.False);
            Assert.That(restoredEvent.RegionId, Is.Null);
            Assert.That(restoredEvent.HasMagnitude, Is.False);
        }

        [Test]
        public void OnlyTheNewestEventsAreCapturedWhenTheBudgetIsSmallerThanTheHistory()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            RecordEvent(recorder, "event-a", PlayerActionEventType.RegionEntered, 1d);
            RecordEvent(recorder, "event-b", PlayerActionEventType.RegionEntered, 2d);
            RecordEvent(recorder, "event-c", PlayerActionEventType.RegionEntered, 3d);

            SaveGameData data = PrototypeSaveMapper.CaptureState(
                new PlayerSaveData(), null, recorder, maxEventCount: 2);

            Assert.That(data.Events.Length, Is.EqualTo(2));
            Assert.That(data.Events[0].Id, Is.EqualTo("event-b"));
            Assert.That(data.Events[1].Id, Is.EqualTo("event-c"));
        }

        [Test]
        public void RestoringIntoAnAlreadyUsedRecorderIsRefused()
        {
            PlayerActionEventRecorder source = new PlayerActionEventRecorder();
            RecordEvent(source, "event-a", PlayerActionEventType.RegionEntered, 1d);
            SaveGameData data = PrototypeSaveMapper.CaptureState(new PlayerSaveData(), null, source);
            PlayerActionEventRecorder used = new PlayerActionEventRecorder();
            RecordEvent(used, "event-z", PlayerActionEventType.RegionEntered, 50d);

            bool restored = PrototypeSaveMapper.TryRestoreWorldState(
                data, null, used, out SaveRestoreSummary _, out string restoreError);

            Assert.That(restored, Is.False);
            Assert.That(restoreError, Does.Contain("fresh recorder"));
            Assert.That(used.Count, Is.EqualTo(1), "A refused restore must not change the existing log.");
        }

        [Test]
        public void RestoringAnUnvalidatedPayloadIsRefused()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(),
                new[]
                {
                    new RegionSaveData("nacre/coast/north", 1),
                    new RegionSaveData("nacre/coast/north", 2),
                },
                Array.Empty<WorldEventSaveData>());
            WorldStateService worldState = CreateWorldState(("nacre/coast/north", "North Coast", 7));

            bool restored = PrototypeSaveMapper.TryRestoreWorldState(
                data, worldState, null, out SaveRestoreSummary _, out string restoreError);

            Assert.That(restored, Is.False);
            Assert.That(restoreError, Is.Not.Null.And.Not.Empty);
            AssertRegionValue(worldState, "nacre/coast/north", 7);
        }

        [Test]
        public void PlayerPlacementRoundTripsThroughTheSaveModel()
        {
            GameObject player = CreateGameObject("Player");
            player.transform.SetPositionAndRotation(new Vector3(3.5f, 1.25f, -8f), Quaternion.Euler(0f, 215f, 0f));

            PlayerSaveData captured = PrototypeSaveMapper.CapturePlayer(player.transform);
            GameObject reloadedPlayer = CreateGameObject("ReloadedPlayer");

            Assert.That(PrototypeSaveMapper.TryApplyPlayer(captured, reloadedPlayer.transform, out string applyError),
                Is.True, applyError);

            Assert.That(reloadedPlayer.transform.position.x, Is.EqualTo(3.5f).Within(0.001f));
            Assert.That(reloadedPlayer.transform.position.y, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(reloadedPlayer.transform.position.z, Is.EqualTo(-8f).Within(0.001f));
            Assert.That(reloadedPlayer.transform.eulerAngles.y, Is.EqualTo(215f).Within(0.01f));
        }

        [Test]
        public void ApplyingPlayerPlacementMovesACharacterControllerCapsule()
        {
            GameObject player = CreateGameObject("ControlledPlayer");
            CharacterController controller = player.AddComponent<CharacterController>();
            Assert.That(controller.enabled, Is.True);

            Assert.That(
                PrototypeSaveMapper.TryApplyPlayer(
                    new PlayerSaveData(10f, 2f, 5f, 90f), player.transform, out string applyError),
                Is.True, applyError);

            Assert.That(player.transform.position.x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(player.transform.position.y, Is.EqualTo(2f).Within(0.001f));
            Assert.That(player.transform.position.z, Is.EqualTo(5f).Within(0.001f));
            Assert.That(controller.enabled, Is.True, "The controller must be re-enabled after the teleport.");
        }

        [Test]
        public void ApplyingAPlacementWithoutATransformIsReportedWithoutThrowing()
        {
            Assert.That(PrototypeSaveMapper.TryApplyPlayer(new PlayerSaveData(), null, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void StateSurvivesAFullCaptureSaveLoadRestoreCycle()
        {
            _directory = Path.Combine(Path.GetTempPath(), "WildshiftSaveTests", Guid.NewGuid().ToString("N"));
            LocalSaveService service = new LocalSaveService(
                new SaveLocation(_directory, SaveLocation.DefaultFileName), () => 0d, 0d);

            WorldStateService session = CreateWorldState(
                ("nacre/coast/north", "North Coast", 0),
                ("nacre/reef/south", "South Reef", 0));
            Assert.That(session.TryUpdateTestValue("nacre/coast/north", 11, out string firstError), Is.True, firstError);
            Assert.That(session.TryUpdateTestValue("nacre/reef/south", 22, out string secondError), Is.True, secondError);
            PlayerActionEventRecorder sessionEvents = new PlayerActionEventRecorder();
            RecordEvent(sessionEvents, "event-a", PlayerActionEventType.RegionEntered, 1d);
            RecordEvent(sessionEvents, "event-b", PlayerActionEventType.WildlifeDisturbance, 7d);
            GameObject player = CreateGameObject("Player");
            player.transform.SetPositionAndRotation(new Vector3(-2f, 0.5f, 6f), Quaternion.Euler(0f, 45f, 0f));

            SaveGameData captured = PrototypeSaveMapper.CaptureState(
                PrototypeSaveMapper.CapturePlayer(player.transform), session, sessionEvents);
            Assert.That(service.Save(captured).IsSuccess, Is.True);

            LoadResult loadResult = service.Load();
            Assert.That(loadResult.IsSuccess, Is.True, loadResult.DeveloperMessage);

            WorldStateService reloadedSession = CreateWorldState(
                ("nacre/coast/north", "North Coast", 0),
                ("nacre/reef/south", "South Reef", 0));
            PlayerActionEventRecorder reloadedEvents = new PlayerActionEventRecorder();
            GameObject reloadedPlayer = CreateGameObject("ReloadedPlayer");

            Assert.That(
                PrototypeSaveMapper.TryRestoreWorldState(loadResult.Data, reloadedSession, reloadedEvents,
                    out SaveRestoreSummary summary, out string restoreError),
                Is.True, restoreError);
            Assert.That(
                PrototypeSaveMapper.TryApplyPlayer(loadResult.Data.Player, reloadedPlayer.transform, out string applyError),
                Is.True, applyError);

            Assert.That(summary.AppliedRegionCount, Is.EqualTo(2));
            Assert.That(summary.RestoredEventCount, Is.EqualTo(2));
            AssertRegionValue(reloadedSession, "nacre/coast/north", 11);
            AssertRegionValue(reloadedSession, "nacre/reef/south", 22);
            IReadOnlyList<PlayerActionEvent> events = reloadedEvents.GetRecentEvents(reloadedEvents.Count);
            Assert.That(events[0].Id, Is.EqualTo("event-a"));
            Assert.That(events[1].Id, Is.EqualTo("event-b"));
            Assert.That(events[1].EventType, Is.EqualTo(PlayerActionEventType.WildlifeDisturbance));
            Assert.That(reloadedPlayer.transform.position.z, Is.EqualTo(6f).Within(0.001f));
            Assert.That(reloadedPlayer.transform.eulerAngles.y, Is.EqualTo(45f).Within(0.01f));
        }

        private static void AssertRegionValue(WorldStateService worldState, string stableId, int expectedValue)
        {
            Assert.That(worldState.TryGetRegion(stableId, out RegionState state, out string error), Is.True, error);
            Assert.That(state.TestValue, Is.EqualTo(expectedValue), $"Region '{stableId}' has the wrong value.");
        }

        private static void RecordEvent(
            PlayerActionEventRecorder recorder,
            string id,
            PlayerActionEventType eventType,
            double elapsedWorldTime,
            string regionId = "nacre/coast/north")
        {
            bool recorded = recorder.TryRecord(
                new PlayerActionEvent(id, eventType, elapsedWorldTime, regionId),
                out PlayerActionEvent _,
                out string error);
            Assert.That(recorded, Is.True, error);
        }

        private WorldStateService CreateWorldState(params (string StableId, string DisplayName, int InitialValue)[] regions)
        {
            WorldStateService service = new WorldStateService();
            foreach ((string stableId, string displayName, int initialValue) in regions)
            {
                RegionDefinition definition = CreateDefinition(stableId, displayName, initialValue);
                Assert.That(service.TryRegisterRegion(definition, out string error), Is.True, error);
            }

            return service;
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

        private GameObject CreateGameObject(string name)
        {
            GameObject created = new GameObject(name);
            _gameObjects.Add(created);
            return created;
        }
    }
}

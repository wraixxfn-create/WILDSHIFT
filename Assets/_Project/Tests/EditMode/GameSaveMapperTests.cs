using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wildshift.Persistence;
using Wildshift.World;
using Wildshift.World.Events;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies the boundary between live runtime state and the save data model: what a capture
    /// contains, how a save is applied back through the owning systems, and which mistakes are
    /// rejected before any state is changed.
    /// </summary>
    public sealed class GameSaveMapperTests
    {
        private readonly List<RegionDefinition> _definitions = new List<RegionDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (RegionDefinition definition in _definitions)
            {
                if (definition != null)
                {
                    Object.DestroyImmediate(definition);
                }
            }

            _definitions.Clear();
        }

        [Test]
        public void CaptureStampsTheCurrentSchemaVersionBuildAndTimestamp()
        {
            GameSaveData save = GameSaveMapper.Capture(
                Vector3.zero, Quaternion.identity, CreateWorldState(), CreateRecorder());

            Assert.That(save.SchemaVersion, Is.EqualTo(GameSaveData.CurrentSchemaVersion));
            Assert.That(save.HasSupportedSchemaVersion, Is.True);
            Assert.That(save.GameVersion, Is.EqualTo(Application.version));
            Assert.That(save.TryGetSavedAtUtc(out DateTime savedAt), Is.True);
            Assert.That(savedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(5)));
            Assert.That(save.TryValidate(out string error), Is.True, error);
        }

        [Test]
        public void CaptureCarriesThePlayerTransformAsPlainValues()
        {
            Vector3 position = new Vector3(-2.5f, 10f, 0.75f);
            Quaternion orientation = Quaternion.Euler(15f, 200f, 0f);

            GameSaveData save = GameSaveMapper.Capture(
                position, orientation, CreateWorldState(), CreateRecorder());

            Assert.That(save.Player.Position, Is.EqualTo(position));
            Assert.That(save.Player.Orientation.x, Is.EqualTo(orientation.x).Within(1e-5f));
            Assert.That(save.Player.Orientation.y, Is.EqualTo(orientation.y).Within(1e-5f));
            Assert.That(save.Player.Orientation.z, Is.EqualTo(orientation.z).Within(1e-5f));
            Assert.That(save.Player.Orientation.w, Is.EqualTo(orientation.w).Within(1e-5f));
        }

        [Test]
        public void CaptureOrdersRegionsByStableIdSoIdenticalStateProducesIdenticalData()
        {
            WorldStateService worldState = CreateWorldState();

            GameSaveData first = GameSaveMapper.Capture(Vector3.zero, Quaternion.identity, worldState, CreateRecorder());
            GameSaveData second = GameSaveMapper.Capture(Vector3.zero, Quaternion.identity, worldState, CreateRecorder());

            Assert.That(first.Regions.Count, Is.EqualTo(2));
            Assert.That(first.Regions[0].StableId, Is.EqualTo("nacre/basin/central"));
            Assert.That(first.Regions[0].TestValue, Is.EqualTo(7));
            Assert.That(first.Regions[1].StableId, Is.EqualTo("nacre/coast/north"));
            Assert.That(first.Regions[1].TestValue, Is.EqualTo(41));
            Assert.That(second.Regions[0].StableId, Is.EqualTo(first.Regions[0].StableId));
            Assert.That(second.Regions[1].StableId, Is.EqualTo(first.Regions[1].StableId));
        }

        [Test]
        public void CaptureKeepsWorldEventsChronologicalAndCapsTheStoredHistory()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder(historyLimit: 16);
            for (int index = 1; index <= 20; index++)
            {
                Assert.That(recorder.TryRecord(new PlayerActionEvent(
                        $"ev-{index}", PlayerActionEventType.RegionEntered, index * 1.5d), out _, out string error),
                    Is.True, error);
            }

            GameSaveData save = GameSaveMapper.Capture(
                Vector3.zero, Quaternion.identity, CreateWorldState(), recorder, maxWorldEvents: 5);

            Assert.That(save.WorldEvents.Count, Is.EqualTo(5));
            Assert.That(save.WorldEvents[0].Id, Is.EqualTo("ev-16"));
            Assert.That(save.WorldEvents[4].Id, Is.EqualTo("ev-20"));
            for (int index = 1; index < save.WorldEvents.Count; index++)
            {
                Assert.That(save.WorldEvents[index].ElapsedWorldTime,
                    Is.GreaterThanOrEqualTo(save.WorldEvents[index - 1].ElapsedWorldTime));
            }
        }

        [Test]
        public void CaptureRejectsMissingRuntimeState()
        {
            WorldStateService worldState = CreateWorldState();
            PlayerActionEventRecorder recorder = CreateRecorder();

            Assert.Throws<ArgumentNullException>(() => GameSaveMapper.Capture(
                Vector3.zero, Quaternion.identity, null, recorder));
            Assert.Throws<ArgumentNullException>(() => GameSaveMapper.Capture(
                Vector3.zero, Quaternion.identity, worldState, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => GameSaveMapper.Capture(
                Vector3.zero, Quaternion.identity, worldState, recorder, maxWorldEvents: -1));
        }

        [Test]
        public void ApplyRestoresRegionValuesWorldEventsAndThePlayerTransform()
        {
            GameSaveData save = GameSaveMapper.Capture(
                new Vector3(3f, 1.5f, -8f), Quaternion.Euler(0f, 45f, 0f), CreateWorldState(), CreateRecorder());

            // A fresh session starts from the authored values (1 and 2), so restored values are visible.
            WorldStateService session = CreateFreshWorldState();
            PlayerActionEventRecorder sessionRecorder = new PlayerActionEventRecorder();
            Assert.That(session.TryGetRegion("nacre/coast/north", out RegionState coastBefore, out string beforeError),
                Is.True, beforeError);
            Assert.That(coastBefore.TestValue, Is.EqualTo(1));

            bool applied = GameSaveMapper.TryApply(save, session, sessionRecorder, out PlayerSaveData player, out string error);

            Assert.That(applied, Is.True, error);
            Assert.That(error, Is.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(player.Position, Is.EqualTo(new Vector3(3f, 1.5f, -8f)));
            Assert.That(player.Orientation.y, Is.EqualTo(Quaternion.Euler(0f, 45f, 0f).y).Within(1e-5f));

            Assert.That(session.TryGetRegion("nacre/coast/north", out RegionState coast, out string coastError),
                Is.True, coastError);
            Assert.That(coast.TestValue, Is.EqualTo(41));
            Assert.That(session.TryGetRegion("nacre/basin/central", out RegionState basin, out string basinError),
                Is.True, basinError);
            Assert.That(basin.TestValue, Is.EqualTo(7));

            Assert.That(sessionRecorder.Count, Is.EqualTo(3));
            Assert.That(sessionRecorder.GetRecentEvents(10)[0].Id, Is.EqualTo("ev-alpha"));
            Assert.That(sessionRecorder.GetRecentEvents(10)[1].Id, Is.EqualTo("ev-beta"));
            Assert.That(sessionRecorder.GetRecentEvents(10)[2].Id, Is.EqualTo("ev-gamma"));
        }

        [Test]
        public void ApplySkipsSavedRegionsThisSessionHasNotRegistered()
        {
            GameSaveData save = GameSaveMapper.Capture(
                Vector3.zero, Quaternion.identity, CreateWorldState(), CreateRecorder());

            WorldStateService session = new WorldStateService();
            Assert.That(session.TryRegisterRegion(CreateDefinition("nacre/coast/north", "North Coast", 1), out string registerError),
                Is.True, registerError);

            bool applied = GameSaveMapper.TryApply(
                save, session, new PlayerActionEventRecorder(), out PlayerSaveData player, out string error);

            Assert.That(applied, Is.True, error);
            Assert.That(player, Is.Not.Null);
            Assert.That(session.RegisteredRegionCount, Is.EqualTo(1), "An unknown saved region must not be invented.");
            Assert.That(session.TryGetRegion("nacre/coast/north", out RegionState coast, out string getError),
                Is.True, getError);
            Assert.That(coast.TestValue, Is.EqualTo(41));
        }

        [Test]
        public void ApplyRejectsAnInvalidSaveWithoutChangingTheSession()
        {
            GameSaveData invalid = new GameSaveData(
                GameSaveData.CurrentSchemaVersion,
                DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                "unit-test",
                new PlayerSaveData(new Vector3(float.PositiveInfinity, 0f, 0f), Quaternion.identity),
                new List<RegionSaveData> { new RegionSaveData("nacre/coast/north", 99) },
                new List<PlayerActionEvent>());

            WorldStateService session = CreateWorldState();
            PlayerActionEventRecorder sessionRecorder = new PlayerActionEventRecorder();

            bool applied = GameSaveMapper.TryApply(invalid, session, sessionRecorder, out PlayerSaveData player, out string error);

            Assert.That(applied, Is.False);
            Assert.That(player, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(session.TryGetRegion("nacre/coast/north", out RegionState coast, out string getError),
                Is.True, getError);
            Assert.That(coast.TestValue, Is.EqualTo(41), "A rejected save must not change live state.");
            Assert.That(sessionRecorder.Count, Is.EqualTo(0));
        }

        [Test]
        public void ApplyRejectsANullSaveAndMissingRuntimeState()
        {
            WorldStateService session = CreateWorldState();
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            GameSaveData save = GameSaveMapper.Capture(Vector3.zero, Quaternion.identity, session, recorder);

            Assert.That(GameSaveMapper.TryApply(null, session, recorder, out PlayerSaveData player, out string error),
                Is.False);
            Assert.That(player, Is.Null);
            Assert.That(error, Does.Contain("null"));

            Assert.Throws<ArgumentNullException>(() => GameSaveMapper.TryApply(
                save, null, recorder, out _, out _));
            Assert.Throws<ArgumentNullException>(() => GameSaveMapper.TryApply(
                save, session, null, out _, out _));
        }

        private WorldStateService CreateFreshWorldState()
        {
            WorldStateService worldState = new WorldStateService();
            Assert.That(worldState.TryRegisterRegion(CreateDefinition("nacre/coast/north", "North Coast", 1), out string firstError),
                Is.True, firstError);
            Assert.That(worldState.TryRegisterRegion(CreateDefinition("nacre/basin/central", "Central Basin", 2), out string secondError),
                Is.True, secondError);
            return worldState;
        }

        private WorldStateService CreateWorldState()
        {
            WorldStateService worldState = CreateFreshWorldState();
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
    }
}

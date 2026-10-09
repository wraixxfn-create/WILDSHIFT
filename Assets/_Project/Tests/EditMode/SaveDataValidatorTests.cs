using System;
using NUnit.Framework;
using Wildshift.Persistence;
using Wildshift.Persistence.Model;
using Wildshift.World.Events;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies that loaded data is checked before it is applied: structure, IDs, numbers, and the
    /// chronological ordering of the saved world-event history.
    /// </summary>
    public sealed class SaveDataValidatorTests
    {
        [Test]
        public void AcceptsAWellFormedPayload()
        {
            SaveGameData data = CreateValidPayload();

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.True, error);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void AcceptsAnEmptyButVersionedPayload()
        {
            SaveGameData data = new SaveGameData(new PlayerSaveData(), null, null);

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.True, error);
        }

        [Test]
        public void RejectsANullPayload()
        {
            Assert.That(SaveDataValidator.TryValidate(null, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void RejectsAPayloadWithoutASchemaVersion()
        {
            // The default-constructed payload is what a JSON file missing "schemaVersion" deserializes to.
            SaveGameData data = new SaveGameData();

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("schema version"));
        }

        [Test]
        public void RejectsANonFinitePlayerPosition()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(float.NaN, 0f, 0f, 0f),
                Array.Empty<RegionSaveData>(),
                Array.Empty<WorldEventSaveData>());

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("position"));
        }

        [Test]
        public void RejectsANonFinitePlayerYaw()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(0f, 0f, 0f, float.PositiveInfinity),
                Array.Empty<RegionSaveData>(),
                Array.Empty<WorldEventSaveData>());

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("yaw"));
        }

        [Test]
        public void RejectsABlankRegionId()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(),
                new[] { new RegionSaveData("   ", 4) },
                Array.Empty<WorldEventSaveData>());

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("stable ID"));
        }

        [Test]
        public void RejectsDuplicateRegionIds()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(),
                new[]
                {
                    new RegionSaveData("nacre/coast/north", 1),
                    new RegionSaveData("nacre/coast/north", 2),
                },
                Array.Empty<WorldEventSaveData>());

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("nacre/coast/north"));
        }

        [Test]
        public void RejectsAnUnknownEventType()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(),
                Array.Empty<RegionSaveData>(),
                new[] { CreateEvent("event-a", 9999, 1d) });

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("unknown event type"));
        }

        [Test]
        public void RejectsTheUnsetEventType()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(),
                Array.Empty<RegionSaveData>(),
                new[] { CreateEvent("event-a", (int)PlayerActionEventType.None, 1d) });

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("unknown event type"));
        }

        [Test]
        public void RejectsOutOfOrderEvents()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(),
                Array.Empty<RegionSaveData>(),
                new[]
                {
                    CreateEvent("event-a", (int)PlayerActionEventType.RegionEntered, 10d),
                    CreateEvent("event-b", (int)PlayerActionEventType.RegionEntered, 4d),
                });

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("chronological"));
        }

        [Test]
        public void AcceptsEventsThatShareATimestamp()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(),
                Array.Empty<RegionSaveData>(),
                new[]
                {
                    CreateEvent("event-a", (int)PlayerActionEventType.RegionEntered, 4d),
                    CreateEvent("event-b", (int)PlayerActionEventType.ObjectInteraction, 4d),
                });

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.True, error);
        }

        [Test]
        public void RejectsDuplicateEventIds()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(),
                Array.Empty<RegionSaveData>(),
                new[]
                {
                    CreateEvent("event-a", (int)PlayerActionEventType.RegionEntered, 1d),
                    CreateEvent("event-a", (int)PlayerActionEventType.RegionEntered, 2d),
                });

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("more than once"));
        }

        [Test]
        public void RejectsANegativeEventTime()
        {
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(),
                Array.Empty<RegionSaveData>(),
                new[] { CreateEvent("event-a", (int)PlayerActionEventType.RegionEntered, -1d) });

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("non-negative"));
        }

        [Test]
        public void RejectsANegativeMagnitude()
        {
            WorldEventSaveData savedEvent = new WorldEventSaveData(
                "event-a",
                (int)PlayerActionEventType.ResourceExtraction,
                1d,
                null,
                null,
                true,
                -3f,
                Array.Empty<WorldEventParameterSaveData>());
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(), Array.Empty<RegionSaveData>(), new[] { savedEvent });

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("magnitude"));
        }

        [Test]
        public void RejectsDuplicateEventParameterIds()
        {
            WorldEventSaveData savedEvent = new WorldEventSaveData(
                "event-a",
                (int)PlayerActionEventType.ResourceExtraction,
                1d,
                null,
                null,
                true,
                3f,
                new[]
                {
                    new WorldEventParameterSaveData("units-extracted", 1f),
                    new WorldEventParameterSaveData("units-extracted", 2f),
                });
            SaveGameData data = new SaveGameData(
                new PlayerSaveData(), Array.Empty<RegionSaveData>(), new[] { savedEvent });

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("units-extracted"));
        }

        [Test]
        public void RejectsTooManySavedEvents()
        {
            WorldEventSaveData[] events = new WorldEventSaveData[SaveSchema.MaxSavedEvents + 1];
            for (int index = 0; index < events.Length; index++)
            {
                events[index] = CreateEvent($"event-{index}", (int)PlayerActionEventType.RegionEntered, index);
            }

            SaveGameData data = new SaveGameData(new PlayerSaveData(), Array.Empty<RegionSaveData>(), events);

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Does.Contain("at most"));
        }

        [Test]
        public void ToleratesMissingCollectionsBecauseJsonMayOmitThem()
        {
            SaveGameData data = new SaveGameData(null, null, null);

            Assert.That(SaveDataValidator.TryValidate(data, out string error), Is.True, error);
            Assert.That(data.Regions, Is.Not.Null.And.Empty);
            Assert.That(data.Events, Is.Not.Null.And.Empty);
            Assert.That(data.Player, Is.Not.Null);
        }

        private static SaveGameData CreateValidPayload()
        {
            return new SaveGameData(
                new PlayerSaveData(12.5f, 1f, -4.25f, 180f),
                new[]
                {
                    new RegionSaveData("nacre/coast/north", 3),
                    new RegionSaveData("nacre/reef/south", -2),
                },
                new[]
                {
                    CreateEvent("event-a", (int)PlayerActionEventType.RegionEntered, 1.5d),
                    CreateEvent("event-b", (int)PlayerActionEventType.ObjectInteraction, 9d),
                });
        }

        private static WorldEventSaveData CreateEvent(string id, int eventType, double elapsedWorldTime)
        {
            return new WorldEventSaveData(
                id,
                eventType,
                elapsedWorldTime,
                "nacre/coast/north",
                null,
                false,
                0f,
                Array.Empty<WorldEventParameterSaveData>());
        }
    }
}

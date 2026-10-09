using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildshift.World.Events;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies the event record itself: generated IDs, defensive parameter copying, Unity JSON
    /// serializability with optional fields, and the development-only example events.
    /// </summary>
    public sealed class PlayerActionEventModelTests
    {
        [Test]
        public void NewIdProducesDistinctNonEmptyIds()
        {
            string first = PlayerActionEvent.NewId();
            string second = PlayerActionEvent.NewId();

            Assert.That(first, Is.Not.Empty);
            Assert.That(second, Is.Not.Empty);
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void EventsCopyTheirParameterCollection()
        {
            List<PlayerActionEventParameter> parameters = new List<PlayerActionEventParameter>
            {
                new PlayerActionEventParameter("alpha", 1f),
            };

            PlayerActionEvent playerActionEvent = new PlayerActionEvent(
                "ev-1", PlayerActionEventType.ResourceExtraction, 1d, parameters: parameters);

            parameters.Add(new PlayerActionEventParameter("beta", 2f));

            Assert.That(playerActionEvent.Parameters.Count, Is.EqualTo(1),
                "Changing the caller's collection after construction must not change the record.");
            Assert.That(playerActionEvent.Parameters[0].Id, Is.EqualTo("alpha"));
            Assert.That(playerActionEvent.Parameters[0].Value, Is.EqualTo(1f));
        }

        [Test]
        public void EventsRoundTripThroughJsonUtility()
        {
            PlayerActionEvent playerActionEvent = new PlayerActionEvent(
                "ev-42",
                PlayerActionEventType.ResourceExtraction,
                33.5d,
                regionId: "nacre/coast/north",
                targetId: "nacre/node/iron-1",
                magnitude: 4f,
                parameters: new[]
                {
                    new PlayerActionEventParameter("units-extracted", 4f),
                });

            string json = JsonUtility.ToJson(playerActionEvent);
            PlayerActionEvent copy = JsonUtility.FromJson<PlayerActionEvent>(json);

            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.Id, Is.EqualTo("ev-42"));
            Assert.That(copy.EventType, Is.EqualTo(PlayerActionEventType.ResourceExtraction));
            Assert.That(copy.ElapsedWorldTime, Is.EqualTo(33.5d).Within(1e-9));
            Assert.That(copy.RegionId, Is.EqualTo("nacre/coast/north"));
            Assert.That(copy.TargetId, Is.EqualTo("nacre/node/iron-1"));
            Assert.That(copy.HasMagnitude, Is.True);
            Assert.That(copy.Magnitude, Is.EqualTo(4f));
            Assert.That(copy.Parameters.Count, Is.EqualTo(1));
            Assert.That(copy.Parameters[0].Id, Is.EqualTo("units-extracted"));
            Assert.That(copy.Parameters[0].Value, Is.EqualTo(4f));
        }

        [Test]
        public void OptionalFieldsStayAbsentAfterSerialization()
        {
            PlayerActionEvent playerActionEvent = new PlayerActionEvent(
                "ev-minimal", PlayerActionEventType.Violence, 1d);

            string json = JsonUtility.ToJson(playerActionEvent);
            PlayerActionEvent copy = JsonUtility.FromJson<PlayerActionEvent>(json);

            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.Id, Is.EqualTo("ev-minimal"));
            Assert.That(copy.HasRegionId, Is.False);
            Assert.That(copy.RegionId, Is.Null);
            Assert.That(copy.HasTargetId, Is.False);
            Assert.That(copy.TargetId, Is.Null);
            Assert.That(copy.HasMagnitude, Is.False);
            Assert.That(copy.Parameters, Is.Empty);
        }

        [Test]
        public void DevelopmentExamplesRecordSuccessfully()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();

            PlayerActionEvent regionEntry = DevelopmentPlayerActionEvents.CreateTestRegionEntry(1d);
            Assert.That(recorder.TryRecord(regionEntry, out _, out string entryError), Is.True, entryError);
            Assert.That(regionEntry.EventType, Is.EqualTo(PlayerActionEventType.RegionEntered));
            Assert.That(regionEntry.RegionId, Is.EqualTo(DevelopmentPlayerActionEvents.TestRegionId));
            Assert.That(regionEntry.HasTargetId, Is.False);

            PlayerActionEvent interaction = DevelopmentPlayerActionEvents.CreateTestObjectInteraction(2d);
            Assert.That(recorder.TryRecord(interaction, out _, out string interactionError), Is.True, interactionError);
            Assert.That(interaction.EventType, Is.EqualTo(PlayerActionEventType.ObjectInteraction));
            Assert.That(interaction.TargetId, Is.EqualTo(DevelopmentPlayerActionEvents.TestObjectId));
            Assert.That(interaction.HasRegionId, Is.False);

            PlayerActionEvent extraction = DevelopmentPlayerActionEvents.CreateSimulatedResourceExtraction(3d);
            Assert.That(recorder.TryRecord(extraction, out _, out string extractionError), Is.True, extractionError);
            Assert.That(extraction.EventType, Is.EqualTo(PlayerActionEventType.ResourceExtraction));
            Assert.That(extraction.RegionId, Is.EqualTo(DevelopmentPlayerActionEvents.TestRegionId));
            Assert.That(extraction.TargetId, Is.EqualTo(DevelopmentPlayerActionEvents.TestObjectId));
            Assert.That(extraction.HasMagnitude, Is.True);
            Assert.That(extraction.Magnitude, Is.EqualTo(3f));
            Assert.That(extraction.Parameters.Count, Is.EqualTo(1));
            Assert.That(extraction.Parameters[0].Id,
                Is.EqualTo(DevelopmentPlayerActionEvents.ExtractedUnitsParameterId));
            Assert.That(extraction.Parameters[0].Value, Is.EqualTo(3f));

            PlayerActionEvent customExtraction = DevelopmentPlayerActionEvents.CreateSimulatedResourceExtraction(4d, 10f);
            Assert.That(recorder.TryRecord(customExtraction, out _, out string customError), Is.True, customError);
            Assert.That(customExtraction.Magnitude, Is.EqualTo(10f));

            Assert.That(recorder.Count, Is.EqualTo(4));
            Assert.That(recorder.GetRecentEvents(10, PlayerActionEventType.ResourceExtraction).Count, Is.EqualTo(2));
            Assert.That(recorder.GetRecentEvents(10, DevelopmentPlayerActionEvents.TestRegionId).Count, Is.EqualTo(3));
        }
    }
}

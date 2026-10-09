using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wildshift.World.Events;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies chronological ordering, region and event-type filtering, bounded history, and
    /// rejection of invalid events without changing the log.
    /// </summary>
    public sealed class PlayerActionEventRecorderTests
    {
        [Test]
        public void RecordsEventsInChronologicalOrder()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            Assert.That(recorder.TryRecord(CreateEvent("ev-1", PlayerActionEventType.RegionEntered, 10d,
                regionId: "nacre/coast/north"), out _, out string firstError), Is.True, firstError);
            Assert.That(recorder.TryRecord(CreateEvent("ev-2", PlayerActionEventType.Violence, 11.5d,
                targetId: "nacre/creature/a"), out _, out string secondError), Is.True, secondError);
            Assert.That(recorder.TryRecord(CreateEvent("ev-3", PlayerActionEventType.ResourceExtraction, 12d,
                regionId: "nacre/coast/north"), out _, out string thirdError), Is.True, thirdError);

            Assert.That(recorder.Count, Is.EqualTo(3));
            Assert.That(recorder.RecordedEventCount, Is.EqualTo(3));

            IReadOnlyList<PlayerActionEvent> events = recorder.GetRecentEvents(10);
            Assert.That(events.Count, Is.EqualTo(3));
            Assert.That(events[0].Id, Is.EqualTo("ev-1"));
            Assert.That(events[1].Id, Is.EqualTo("ev-2"));
            Assert.That(events[2].Id, Is.EqualTo("ev-3"));
        }

        [Test]
        public void EqualTimestampsKeepInsertionOrder()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            Assert.That(recorder.TryRecord(CreateEvent("first", PlayerActionEventType.ObjectInteraction, 7d), out _, out string firstError), Is.True, firstError);
            Assert.That(recorder.TryRecord(CreateEvent("second", PlayerActionEventType.ObjectInteraction, 7d), out _, out string secondError), Is.True, secondError);

            IReadOnlyList<PlayerActionEvent> events = recorder.GetRecentEvents(10);
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].Id, Is.EqualTo("first"));
            Assert.That(events[1].Id, Is.EqualTo("second"));
        }

        [Test]
        public void EventsEarlierThanTheLastRecordedEventAreRejectedWithoutChanges()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            Assert.That(recorder.TryRecord(CreateEvent("ev-1", PlayerActionEventType.Violence, 5d), out _, out string firstError), Is.True, firstError);

            bool recorded = recorder.TryRecord(CreateEvent("ev-2", PlayerActionEventType.Violence, 4d),
                out PlayerActionEvent recordedEvent, out string error);

            Assert.That(recorded, Is.False);
            Assert.That(recordedEvent, Is.Null);
            Assert.That(error, Does.Contain("chronological"));
            Assert.That(recorder.Count, Is.EqualTo(1));
            Assert.That(recorder.RecordedEventCount, Is.EqualTo(1));

            // A rejected event must not move the time watermark: the same instant stays acceptable.
            Assert.That(recorder.TryRecord(CreateEvent("ev-3", PlayerActionEventType.Sabotage, 5d), out _, out string retryError), Is.True, retryError);
            Assert.That(recorder.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetRecentEventsReturnsOnlyTheNewestTail()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            for (int index = 1; index <= 5; index++)
            {
                Assert.That(recorder.TryRecord(CreateEvent($"ev-{index}", PlayerActionEventType.ObjectInteraction, index),
                    out _, out string error), Is.True, error);
            }

            IReadOnlyList<PlayerActionEvent> newestTwo = recorder.GetRecentEvents(2);
            Assert.That(newestTwo.Count, Is.EqualTo(2));
            Assert.That(newestTwo[0].Id, Is.EqualTo("ev-4"));
            Assert.That(newestTwo[1].Id, Is.EqualTo("ev-5"));
        }

        [Test]
        public void EventsCanBeFilteredByRegionId()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            Assert.That(recorder.TryRecord(CreateEvent("coast-1", PlayerActionEventType.RegionEntered, 1d,
                regionId: "nacre/coast/north"), out _, out string firstError), Is.True, firstError);
            Assert.That(recorder.TryRecord(CreateEvent("none-1", PlayerActionEventType.Violence, 2d,
                targetId: "nacre/creature/a"), out _, out string secondError), Is.True, secondError);
            Assert.That(recorder.TryRecord(CreateEvent("reef-1", PlayerActionEventType.RegionEntered, 3d,
                regionId: "nacre/reef/south"), out _, out string thirdError), Is.True, thirdError);
            Assert.That(recorder.TryRecord(CreateEvent("coast-2", PlayerActionEventType.ResourceExtraction, 4d,
                regionId: "nacre/coast/north"), out _, out string fourthError), Is.True, fourthError);

            IReadOnlyList<PlayerActionEvent> coastEvents = recorder.GetRecentEvents(10, "nacre/coast/north");
            Assert.That(coastEvents.Count, Is.EqualTo(2));
            Assert.That(coastEvents[0].Id, Is.EqualTo("coast-1"));
            Assert.That(coastEvents[1].Id, Is.EqualTo("coast-2"));

            Assert.That(recorder.GetRecentEvents(10, "nacre/reef/south"), Has.Count.EqualTo(1));
            Assert.That(recorder.GetRecentEvents(10, "nacre/unknown").Count, Is.EqualTo(0));
            Assert.That(recorder.GetRecentEvents(10, null).Count, Is.EqualTo(0));
            Assert.That(recorder.GetRecentEvents(10, "  ").Count, Is.EqualTo(0));
        }

        [Test]
        public void EventsCanBeFilteredByEventType()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            Assert.That(recorder.TryRecord(CreateEvent("v-1", PlayerActionEventType.Violence, 1d), out _, out string firstError), Is.True, firstError);
            Assert.That(recorder.TryRecord(CreateEvent("s-1", PlayerActionEventType.SettlementAssistance, 2d), out _, out string secondError), Is.True, secondError);
            Assert.That(recorder.TryRecord(CreateEvent("v-2", PlayerActionEventType.Violence, 3d), out _, out string thirdError), Is.True, thirdError);

            IReadOnlyList<PlayerActionEvent> violence = recorder.GetRecentEvents(10, PlayerActionEventType.Violence);
            Assert.That(violence.Count, Is.EqualTo(2));
            Assert.That(violence[0].Id, Is.EqualTo("v-1"));
            Assert.That(violence[1].Id, Is.EqualTo("v-2"));
            Assert.That(recorder.GetRecentEvents(10, PlayerActionEventType.WildlifeDisturbance).Count, Is.EqualTo(0));
        }

        [Test]
        public void EventTypeFilterRejectsUndefinedEventTypes()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();

            Assert.That(() => recorder.GetRecentEvents(10, PlayerActionEventType.None),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => recorder.GetRecentEvents(10, (PlayerActionEventType)999),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void GetRecentEventsValidatesMaxCount()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            Assert.That(recorder.TryRecord(CreateEvent("ev-1", PlayerActionEventType.Violence, 1d), out _, out string firstError), Is.True, firstError);
            Assert.That(recorder.TryRecord(CreateEvent("ev-2", PlayerActionEventType.Violence, 2d), out _, out string secondError), Is.True, secondError);

            Assert.That(() => recorder.GetRecentEvents(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(recorder.GetRecentEvents(0).Count, Is.EqualTo(0));
            Assert.That(recorder.GetRecentEvents(99).Count, Is.EqualTo(2));
        }

        [Test]
        public void QuerySnapshotsAreNotChangedByLaterRecording()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            Assert.That(recorder.TryRecord(CreateEvent("ev-1", PlayerActionEventType.Violence, 1d), out _, out string error), Is.True, error);

            IReadOnlyList<PlayerActionEvent> snapshot = recorder.GetRecentEvents(10);
            Assert.That(recorder.TryRecord(CreateEvent("ev-2", PlayerActionEventType.Violence, 2d), out _, out string secondError), Is.True, secondError);

            Assert.That(snapshot.Count, Is.EqualTo(1));
            Assert.That(snapshot[0].Id, Is.EqualTo("ev-1"));
            Assert.That(recorder.Count, Is.EqualTo(2));
        }

        [Test]
        public void HistoryLimitTrimsTheOldestEvents()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder(3);
            for (int index = 1; index <= 5; index++)
            {
                Assert.That(recorder.TryRecord(CreateEvent($"ev-{index}", PlayerActionEventType.ObjectInteraction, index),
                    out _, out string error), Is.True, error);
            }

            Assert.That(recorder.HistoryLimit, Is.EqualTo(3));
            Assert.That(recorder.Count, Is.EqualTo(3), "The history must stay bounded at the configured limit.");
            Assert.That(recorder.RecordedEventCount, Is.EqualTo(5));

            IReadOnlyList<PlayerActionEvent> retained = recorder.GetRecentEvents(10);
            Assert.That(retained.Count, Is.EqualTo(3));
            Assert.That(retained[0].Id, Is.EqualTo("ev-3"));
            Assert.That(retained[1].Id, Is.EqualTo("ev-4"));
            Assert.That(retained[2].Id, Is.EqualTo("ev-5"));
        }

        [Test]
        public void SustainedRecordingKeepsTheRetainedHistoryBounded()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder(16);
            for (int index = 0; index < 2000; index++)
            {
                Assert.That(recorder.TryRecord(CreateEvent($"loop-{index}", PlayerActionEventType.RegionEntered, index),
                    out _, out string error), Is.True, error);
            }

            IReadOnlyList<PlayerActionEvent> retained = recorder.GetRecentEvents(int.MaxValue);
            Assert.That(recorder.Count, Is.EqualTo(16));
            Assert.That(retained.Count, Is.EqualTo(16));
            Assert.That(retained[0].Id, Is.EqualTo("loop-1984"), "The oldest events must be the ones dropped.");
            Assert.That(retained[15].Id, Is.EqualTo("loop-1999"));
            Assert.That(recorder.RecordedEventCount, Is.EqualTo(2000));
        }

        [Test]
        public void RetiredIdsMayBeReusedAfterTheirEventsWereTrimmed()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder(2);
            Assert.That(recorder.TryRecord(CreateEvent("retired", PlayerActionEventType.Violence, 1d), out _, out string firstError), Is.True, firstError);
            Assert.That(recorder.TryRecord(CreateEvent("other", PlayerActionEventType.Violence, 2d), out _, out string secondError), Is.True, secondError);
            Assert.That(recorder.TryRecord(CreateEvent("third", PlayerActionEventType.Violence, 3d), out _, out string thirdError), Is.True, thirdError);

            // "retired" is no longer retained, so its ID no longer collides.
            Assert.That(recorder.TryRecord(CreateEvent("retired", PlayerActionEventType.Violence, 4d),
                out _, out string fourthError), Is.True, fourthError);

            IReadOnlyList<PlayerActionEvent> retained = recorder.GetRecentEvents(10);
            Assert.That(recorder.Count, Is.EqualTo(2));
            Assert.That(retained[0].Id, Is.EqualTo("third"));
            Assert.That(retained[1].Id, Is.EqualTo("retired"));
        }

        [Test]
        public void DuplicateRetainedIdsAreRejectedWithoutChanges()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            Assert.That(recorder.TryRecord(CreateEvent("dup", PlayerActionEventType.Violence, 1d), out _, out string firstError), Is.True, firstError);

            bool recorded = recorder.TryRecord(CreateEvent("dup", PlayerActionEventType.Sabotage, 2d),
                out PlayerActionEvent recordedEvent, out string error);

            Assert.That(recorded, Is.False);
            Assert.That(recordedEvent, Is.Null);
            Assert.That(error, Does.Contain("unique"));
            Assert.That(recorder.Count, Is.EqualTo(1));
            Assert.That(recorder.RecordedEventCount, Is.EqualTo(1));
        }

        [Test]
        public void NullEventsAreRejected()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();

            bool recorded = recorder.TryRecord(null, out PlayerActionEvent recordedEvent, out string error);

            Assert.That(recorded, Is.False);
            Assert.That(recordedEvent, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(recorder.Count, Is.EqualTo(0));
        }

        [Test]
        public void EventsWithBlankStableIdsAreRejected([Values("", "   ")] string stableId)
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();

            bool recorded = recorder.TryRecord(CreateEvent(stableId, PlayerActionEventType.Violence, 1d),
                out _, out string error);

            Assert.That(recorded, Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(recorder.Count, Is.EqualTo(0));
        }

        [Test]
        public void EventsWithUndefinedEventTypesAreRejected([Values] bool useNone)
        {
            PlayerActionEventType eventType = useNone ? PlayerActionEventType.None : (PlayerActionEventType)999;
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();

            bool recorded = recorder.TryRecord(CreateEvent("ev-1", eventType, 1d), out _, out string error);

            Assert.That(recorded, Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(recorder.Count, Is.EqualTo(0));
        }

        [Test]
        public void EventsWithInvalidElapsedWorldTimesAreRejected(
            [Values(double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d)] double elapsedWorldTime)
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();

            bool recorded = recorder.TryRecord(CreateEvent("ev-1", PlayerActionEventType.Violence, elapsedWorldTime),
                out _, out string error);

            Assert.That(recorded, Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(recorder.Count, Is.EqualTo(0));
        }

        [Test]
        public void EventsWithBlankRegionOrTargetIdsAreRejected()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();

            bool withBlankRegion = recorder.TryRecord(CreateEvent("ev-1", PlayerActionEventType.Violence, 1d,
                regionId: "  "), out _, out string regionError);
            Assert.That(withBlankRegion, Is.False);
            Assert.That(regionError, Is.Not.Empty);

            bool withBlankTarget = recorder.TryRecord(CreateEvent("ev-2", PlayerActionEventType.Violence, 1d,
                targetId: ""), out _, out string targetError);
            Assert.That(withBlankTarget, Is.False);
            Assert.That(targetError, Is.Not.Empty);

            Assert.That(recorder.Count, Is.EqualTo(0));
            Assert.That(recorder.RecordedEventCount, Is.EqualTo(0));
        }

        [Test]
        public void EventsWithInvalidMagnitudesAreRejected(
            [Values(float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0.5f)] float magnitude)
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();

            bool recorded = recorder.TryRecord(CreateEvent("ev-1", PlayerActionEventType.ResourceExtraction, 1d,
                magnitude: magnitude), out _, out string error);

            Assert.That(recorded, Is.False);
            Assert.That(error, Does.Contain("magnitude"));
            Assert.That(recorder.Count, Is.EqualTo(0));
        }

        [Test]
        public void AZeroMagnitudeIsValid()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();

            Assert.That(recorder.TryRecord(CreateEvent("ev-1", PlayerActionEventType.SettlementAssistance, 1d,
                magnitude: 0f), out PlayerActionEvent recorded, out string error), Is.True, error);
            Assert.That(recorded.HasMagnitude, Is.True);
            Assert.That(recorded.Magnitude, Is.EqualTo(0f));
        }

        [Test]
        public void EventsBeyondTheParameterLimitAreRejected()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();
            Assert.That(recorder.TryRecord(CreateEvent("ev-max", PlayerActionEventType.Violence, 1d,
                parameters: CreateParameters(PlayerActionEventRecorder.MaxParametersPerEvent)), out _,
                out string acceptedError), Is.True, acceptedError);

            bool recorded = recorder.TryRecord(CreateEvent("ev-over", PlayerActionEventType.Violence, 2d,
                parameters: CreateParameters(PlayerActionEventRecorder.MaxParametersPerEvent + 1)),
                out _, out string error);

            Assert.That(recorded, Is.False);
            Assert.That(error, Does.Contain("parameters"));
            Assert.That(recorder.Count, Is.EqualTo(1));
        }

        [Test]
        public void EventsWithInvalidParametersAreRejected()
        {
            PlayerActionEventRecorder recorder = new PlayerActionEventRecorder();

            bool withNullParameter = recorder.TryRecord(CreateEvent("ev-null", PlayerActionEventType.Violence, 1d,
                parameters: new List<PlayerActionEventParameter>
                {
                    new PlayerActionEventParameter("alpha", 1f),
                    null,
                }), out _, out string nullParameterError);
            Assert.That(withNullParameter, Is.False);
            Assert.That(nullParameterError, Is.Not.Empty);

            bool withBlankParameterId = recorder.TryRecord(CreateEvent("ev-blank", PlayerActionEventType.Violence, 1d,
                parameters: new List<PlayerActionEventParameter>
                {
                    new PlayerActionEventParameter("  ", 1f),
                }), out _, out string blankParameterIdError);
            Assert.That(withBlankParameterId, Is.False);
            Assert.That(blankParameterIdError, Is.Not.Empty);

            bool withDuplicateParameterIds = recorder.TryRecord(CreateEvent("ev-duplicate", PlayerActionEventType.Violence, 1d,
                parameters: new List<PlayerActionEventParameter>
                {
                    new PlayerActionEventParameter("alpha", 1f),
                    new PlayerActionEventParameter("alpha", 2f),
                }), out _, out string duplicateParameterIdError);
            Assert.That(withDuplicateParameterIds, Is.False);
            Assert.That(duplicateParameterIdError, Does.Contain("'alpha'"));

            bool withNanParameterValue = recorder.TryRecord(CreateEvent("ev-nan", PlayerActionEventType.Violence, 1d,
                parameters: new List<PlayerActionEventParameter>
                {
                    new PlayerActionEventParameter("alpha", float.NaN),
                }), out _, out string nanParameterValueError);
            Assert.That(withNanParameterValue, Is.False);
            Assert.That(nanParameterValueError, Is.Not.Empty);

            Assert.That(recorder.Count, Is.EqualTo(0));
            Assert.That(recorder.RecordedEventCount, Is.EqualTo(0));
        }

        [Test]
        public void NonPositiveHistoryLimitsAreRejected(
            [Values(0, -3)] int historyLimit)
        {
            Assert.That(() => new PlayerActionEventRecorder(historyLimit),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static PlayerActionEvent CreateEvent(string id, PlayerActionEventType eventType, double elapsedWorldTime,
            string regionId = null, string targetId = null, float? magnitude = null,
            IReadOnlyList<PlayerActionEventParameter> parameters = null)
        {
            return new PlayerActionEvent(id, eventType, elapsedWorldTime, regionId, targetId, magnitude, parameters);
        }

        private static IReadOnlyList<PlayerActionEventParameter> CreateParameters(int count)
        {
            List<PlayerActionEventParameter> parameters = new List<PlayerActionEventParameter>(count);
            for (int index = 0; index < count; index++)
            {
                parameters.Add(new PlayerActionEventParameter($"parameter-{index}", index));
            }

            return parameters;
        }
    }
}

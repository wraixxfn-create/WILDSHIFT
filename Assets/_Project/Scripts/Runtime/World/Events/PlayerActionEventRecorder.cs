using System;
using System.Collections.Generic;

namespace Wildshift.World.Events
{
    /// <summary>
    /// In-memory recorder of validated <see cref="PlayerActionEvent"/> records kept in chronological
    /// order. It is a plain C# object owned by whoever constructs it — not a global singleton — and
    /// it only stores records and answers queries: recording an event never changes world, ecology,
    /// or faction state, and no other system is notified. The history is bounded by a configurable
    /// limit with the oldest events dropped first, so recording cannot grow memory without end.
    /// </summary>
    public sealed class PlayerActionEventRecorder
    {
        /// <summary>History limit used when a caller does not configure one explicitly.</summary>
        public const int DefaultHistoryLimit = 512;

        /// <summary>Maximum number of structured parameters accepted on one event; keeps records small.</summary>
        public const int MaxParametersPerEvent = 8;

        private readonly Queue<PlayerActionEvent> _events = new Queue<PlayerActionEvent>();
        private readonly HashSet<string> _retainedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly int _historyLimit;
        private double _lastElapsedWorldTime = double.NegativeInfinity;
        private long _recordedEventCount;

        /// <summary>
        /// Creates a recorder with a bounded history.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when historyLimit is below one.</exception>
        public PlayerActionEventRecorder(int historyLimit = DefaultHistoryLimit)
        {
            if (historyLimit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(historyLimit), historyLimit,
                    "The player action event history limit must be at least one.");
            }

            _historyLimit = historyLimit;
        }

        /// <summary>Maximum number of events retained; once full, recording drops the oldest event.</summary>
        public int HistoryLimit => _historyLimit;

        /// <summary>Number of events currently retained; never more than <see cref="HistoryLimit"/>.</summary>
        public int Count => _events.Count;

        /// <summary>Total events accepted since this recorder was created, including trimmed ones.</summary>
        public long RecordedEventCount => _recordedEventCount;

        /// <summary>
        /// Validates and records one event. Required fields (a stable ID, a defined event type other
        /// than <see cref="PlayerActionEventType.None"/>, and a finite non-negative elapsed world
        /// time) are checked; optional region and target IDs must not be blank (use null instead); a
        /// magnitude must be finite and non-negative; and at most <see cref="MaxParametersPerEvent"/>
        /// parameters are accepted, each with a unique non-blank ID and a finite value. The event time
        /// must not be earlier than the time of the previous recorded event, and the ID must not
        /// duplicate an event this recorder still retains. A rejected event changes nothing and never
        /// throws for an ordinary validation failure.
        /// </summary>
        public bool TryRecord(PlayerActionEvent candidateEvent, out PlayerActionEvent recordedEvent, out string error)
        {
            recordedEvent = null;

            if (candidateEvent == null)
            {
                error = "Cannot record a null player action event.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(candidateEvent.Id))
            {
                error = "Cannot record a player action event whose stable ID is empty or whitespace; " +
                        "assign one with PlayerActionEvent.NewId().";
                return false;
            }

            if (candidateEvent.EventType == PlayerActionEventType.None ||
                !Enum.IsDefined(typeof(PlayerActionEventType), candidateEvent.EventType))
            {
                error = $"Cannot record event '{candidateEvent.Id}' because event type " +
                        $"'{candidateEvent.EventType}' is not a defined player action event type.";
                return false;
            }

            double elapsedWorldTime = candidateEvent.ElapsedWorldTime;
            if (double.IsNaN(elapsedWorldTime) || double.IsInfinity(elapsedWorldTime))
            {
                error = $"Cannot record event '{candidateEvent.Id}' because its elapsed world time is not a finite number.";
                return false;
            }

            if (elapsedWorldTime < 0d)
            {
                error = $"Cannot record event '{candidateEvent.Id}' because its elapsed world time " +
                        $"{elapsedWorldTime} is negative.";
                return false;
            }

            if (elapsedWorldTime < _lastElapsedWorldTime)
            {
                error = $"Cannot record event '{candidateEvent.Id}' because its elapsed world time " +
                        $"{elapsedWorldTime} is earlier than the last recorded event at {_lastElapsedWorldTime}; " +
                        "events must stay chronological.";
                return false;
            }

            if (candidateEvent.HasRegionId && string.IsNullOrWhiteSpace(candidateEvent.RegionId))
            {
                error = $"Cannot record event '{candidateEvent.Id}' because its region ID is empty or " +
                        "whitespace; leave the region out instead.";
                return false;
            }

            if (candidateEvent.HasTargetId && string.IsNullOrWhiteSpace(candidateEvent.TargetId))
            {
                error = $"Cannot record event '{candidateEvent.Id}' because its target ID is empty or " +
                        "whitespace; leave the target out instead.";
                return false;
            }

            if (candidateEvent.HasMagnitude)
            {
                float magnitude = candidateEvent.Magnitude;
                if (float.IsNaN(magnitude) || float.IsInfinity(magnitude))
                {
                    error = $"Cannot record event '{candidateEvent.Id}' because its magnitude is not a finite number.";
                    return false;
                }

                if (magnitude < 0f)
                {
                    error = $"Cannot record event '{candidateEvent.Id}' because its magnitude {magnitude} is negative.";
                    return false;
                }
            }

            if (!ValidateParameters(candidateEvent, out error))
            {
                return false;
            }

            if (_retainedIds.Contains(candidateEvent.Id))
            {
                error = $"Cannot record event '{candidateEvent.Id}' because an event with that ID is " +
                        "still retained; event IDs must be unique.";
                return false;
            }

            _events.Enqueue(candidateEvent);
            _retainedIds.Add(candidateEvent.Id);

            // Drop oldest events first so the retained history (and therefore memory) stays bounded.
            while (_events.Count > _historyLimit)
            {
                PlayerActionEvent droppedEvent = _events.Dequeue();
                _retainedIds.Remove(droppedEvent.Id);
            }

            _lastElapsedWorldTime = elapsedWorldTime;
            _recordedEventCount++;
            recordedEvent = candidateEvent;
            error = null;
            return true;
        }

        /// <summary>
        /// Returns up to maxCount retained events in chronological order (oldest first, newest last);
        /// returns all retained events when maxCount is at least <see cref="Count"/>. The result is a
        /// snapshot: recording afterwards does not change it, and changing it does not touch the log.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when maxCount is negative.</exception>
        public IReadOnlyList<PlayerActionEvent> GetRecentEvents(int maxCount)
        {
            ValidateMaxCount(maxCount);
            return Collect(maxCount, null, null);
        }

        /// <summary>
        /// Returns up to maxCount retained events of one event type in chronological order (oldest
        /// first, newest last).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when maxCount is negative.</exception>
        /// <exception cref="ArgumentException">Thrown when eventType is None or not a defined value.</exception>
        public IReadOnlyList<PlayerActionEvent> GetRecentEvents(int maxCount, PlayerActionEventType eventType)
        {
            ValidateMaxCount(maxCount);
            if (eventType == PlayerActionEventType.None ||
                !Enum.IsDefined(typeof(PlayerActionEventType), eventType))
            {
                throw new ArgumentException(
                    $"'{eventType}' is not a defined player action event type to filter by.", nameof(eventType));
            }

            return Collect(maxCount, eventType, null);
        }

        /// <summary>
        /// Returns up to maxCount retained events that name the given stable region ID, in
        /// chronological order (oldest first, newest last). A null, empty, or whitespace region ID
        /// always yields an empty list because the recorder never accepts a blank region ID.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when maxCount is negative.</exception>
        public IReadOnlyList<PlayerActionEvent> GetRecentEvents(int maxCount, string regionId)
        {
            ValidateMaxCount(maxCount);
            if (string.IsNullOrWhiteSpace(regionId))
            {
                return new List<PlayerActionEvent>();
            }

            return Collect(maxCount, null, regionId);
        }

        private bool ValidateParameters(PlayerActionEvent candidateEvent, out string error)
        {
            IReadOnlyList<PlayerActionEventParameter> parameters = candidateEvent.Parameters;
            if (parameters.Count > MaxParametersPerEvent)
            {
                error = $"Cannot record event '{candidateEvent.Id}' with {parameters.Count} parameters; " +
                        $"at most {MaxParametersPerEvent} are allowed.";
                return false;
            }

            HashSet<string> seenParameterIds = null;
            for (int index = 0; index < parameters.Count; index++)
            {
                PlayerActionEventParameter parameter = parameters[index];
                if (parameter == null)
                {
                    error = $"Cannot record event '{candidateEvent.Id}' because parameter {index} is null.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(parameter.Id))
                {
                    error = $"Cannot record event '{candidateEvent.Id}' because parameter {index} has an " +
                            "empty or whitespace ID.";
                    return false;
                }

                if (float.IsNaN(parameter.Value) || float.IsInfinity(parameter.Value))
                {
                    error = $"Cannot record event '{candidateEvent.Id}' because parameter " +
                            $"'{parameter.Id}' is not a finite number.";
                    return false;
                }

                if (seenParameterIds == null)
                {
                    seenParameterIds = new HashSet<string>(StringComparer.Ordinal);
                }

                if (!seenParameterIds.Add(parameter.Id))
                {
                    error = $"Cannot record event '{candidateEvent.Id}' because parameter ID " +
                            $"'{parameter.Id}' appears more than once.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private IReadOnlyList<PlayerActionEvent> Collect(int maxCount, PlayerActionEventType? eventType, string regionId)
        {
            List<PlayerActionEvent> matches = new List<PlayerActionEvent>();
            foreach (PlayerActionEvent recordedEvent in _events)
            {
                if (eventType.HasValue && recordedEvent.EventType != eventType.Value)
                {
                    continue;
                }

                if (regionId != null &&
                    (!recordedEvent.HasRegionId ||
                     !string.Equals(recordedEvent.RegionId, regionId, StringComparison.Ordinal)))
                {
                    continue;
                }

                matches.Add(recordedEvent);
            }

            if (matches.Count > maxCount)
            {
                matches.RemoveRange(0, matches.Count - maxCount);
            }

            return matches;
        }

        private static void ValidateMaxCount(int maxCount)
        {
            if (maxCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount,
                    "Cannot request a negative number of recent player action events.");
            }
        }
    }
}

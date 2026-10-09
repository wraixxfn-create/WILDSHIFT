using System;
using System.Collections.Generic;
using Wildshift.Persistence.Model;
using Wildshift.World.Events;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Checks that a <see cref="SaveGameData"/> payload is structurally sound before anything is
    /// written to disk or applied to live systems. It is a pure function over data: it never touches
    /// the filesystem, never logs, and never repairs a payload in place.
    /// </summary>
    /// <remarks>
    /// Treat every loaded file as untrusted input. Deserialization alone proves nothing: Unity's JSON
    /// reader silently fills missing fields with defaults, so a file can parse and still be nonsense.
    /// Schema-version compatibility is checked separately by <see cref="SaveSchema"/> before this runs.
    /// </remarks>
    public static class SaveDataValidator
    {
        /// <summary>
        /// Returns true when the payload can be safely applied. On failure, <paramref name="error"/>
        /// describes the first problem found in developer-readable terms and contains no filesystem path.
        /// </summary>
        public static bool TryValidate(SaveGameData data, out string error)
        {
            if (data == null)
            {
                error = "The save payload is null.";
                return false;
            }

            if (data.SchemaVersion <= 0)
            {
                error = $"The save payload declares schema version {data.SchemaVersion}; it must be positive.";
                return false;
            }

            if (!ValidatePlayer(data.Player, out error))
            {
                return false;
            }

            if (!ValidateRegions(data.Regions, out error))
            {
                return false;
            }

            return ValidateEvents(data.Events, out error);
        }

        private static bool ValidatePlayer(PlayerSaveData player, out string error)
        {
            // Player is normalized to a non-null default by the model, so only the numbers can be bad.
            if (!IsFinite(player.PositionX) || !IsFinite(player.PositionY) || !IsFinite(player.PositionZ))
            {
                error = "The saved player position contains a value that is not a finite number.";
                return false;
            }

            if (!IsFinite(player.YawDegrees))
            {
                error = "The saved player yaw is not a finite number.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateRegions(RegionSaveData[] regions, out string error)
        {
            if (regions.Length > SaveSchema.MaxSavedRegions)
            {
                error = $"The save payload contains {regions.Length} regions; at most " +
                        $"{SaveSchema.MaxSavedRegions} are allowed.";
                return false;
            }

            HashSet<string> seenRegionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < regions.Length; index++)
            {
                RegionSaveData region = regions[index];
                if (region == null)
                {
                    error = $"Saved region {index} is null.";
                    return false;
                }

                if (!IsUsableId(region.StableId))
                {
                    error = $"Saved region {index} has an empty, whitespace, or over-long stable ID.";
                    return false;
                }

                if (!seenRegionIds.Add(region.StableId))
                {
                    error = $"Stable region ID '{region.StableId}' appears more than once in the save payload.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool ValidateEvents(WorldEventSaveData[] events, out string error)
        {
            if (events.Length > SaveSchema.MaxSavedEvents)
            {
                error = $"The save payload contains {events.Length} world events; at most " +
                        $"{SaveSchema.MaxSavedEvents} are allowed.";
                return false;
            }

            HashSet<string> seenEventIds = new HashSet<string>(StringComparer.Ordinal);
            double previousElapsedWorldTime = double.NegativeInfinity;

            for (int index = 0; index < events.Length; index++)
            {
                WorldEventSaveData savedEvent = events[index];
                if (savedEvent == null)
                {
                    error = $"Saved world event {index} is null.";
                    return false;
                }

                if (!IsUsableId(savedEvent.Id))
                {
                    error = $"Saved world event {index} has an empty, whitespace, or over-long ID.";
                    return false;
                }

                if (!seenEventIds.Add(savedEvent.Id))
                {
                    error = $"World event ID '{savedEvent.Id}' appears more than once in the save payload.";
                    return false;
                }

                if (!IsKnownEventType(savedEvent.EventType))
                {
                    error = $"Saved world event '{savedEvent.Id}' uses unknown event type {savedEvent.EventType}; " +
                            "this build cannot interpret it.";
                    return false;
                }

                double elapsedWorldTime = savedEvent.ElapsedWorldTime;
                if (double.IsNaN(elapsedWorldTime) || double.IsInfinity(elapsedWorldTime) || elapsedWorldTime < 0d)
                {
                    error = $"Saved world event '{savedEvent.Id}' has an elapsed world time that is not a " +
                            "finite, non-negative number.";
                    return false;
                }

                // The saved list is a chronological history; out-of-order entries would be rejected by
                // the live recorder, so they are caught here instead of half-applying the history.
                if (elapsedWorldTime < previousElapsedWorldTime)
                {
                    error = $"Saved world event '{savedEvent.Id}' at {elapsedWorldTime} is earlier than the " +
                            $"preceding entry at {previousElapsedWorldTime}; saved events must stay chronological.";
                    return false;
                }

                previousElapsedWorldTime = elapsedWorldTime;

                if (!IsAbsentOrUsableId(savedEvent.RegionId))
                {
                    error = $"Saved world event '{savedEvent.Id}' has a whitespace or over-long region ID; " +
                            "leave it empty instead.";
                    return false;
                }

                if (!IsAbsentOrUsableId(savedEvent.TargetId))
                {
                    error = $"Saved world event '{savedEvent.Id}' has a whitespace or over-long target ID; " +
                            "leave it empty instead.";
                    return false;
                }

                if (savedEvent.HasMagnitude && (!IsFinite(savedEvent.Magnitude) || savedEvent.Magnitude < 0f))
                {
                    error = $"Saved world event '{savedEvent.Id}' has a magnitude that is not a finite, " +
                            "non-negative number.";
                    return false;
                }

                if (!ValidateEventParameters(savedEvent, out error))
                {
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool ValidateEventParameters(WorldEventSaveData savedEvent, out string error)
        {
            WorldEventParameterSaveData[] parameters = savedEvent.Parameters;
            if (parameters.Length > SaveSchema.MaxEventParameters)
            {
                error = $"Saved world event '{savedEvent.Id}' has {parameters.Length} parameters; at most " +
                        $"{SaveSchema.MaxEventParameters} are allowed.";
                return false;
            }

            HashSet<string> seenParameterIds = null;
            for (int index = 0; index < parameters.Length; index++)
            {
                WorldEventParameterSaveData parameter = parameters[index];
                if (parameter == null)
                {
                    error = $"Parameter {index} of saved world event '{savedEvent.Id}' is null.";
                    return false;
                }

                if (!IsUsableId(parameter.Id))
                {
                    error = $"Parameter {index} of saved world event '{savedEvent.Id}' has an empty, " +
                            "whitespace, or over-long ID.";
                    return false;
                }

                if (!IsFinite(parameter.Value))
                {
                    error = $"Parameter '{parameter.Id}' of saved world event '{savedEvent.Id}' is not a " +
                            "finite number.";
                    return false;
                }

                if (seenParameterIds == null)
                {
                    seenParameterIds = new HashSet<string>(StringComparer.Ordinal);
                }

                if (!seenParameterIds.Add(parameter.Id))
                {
                    error = $"Parameter ID '{parameter.Id}' appears more than once on saved world event " +
                            $"'{savedEvent.Id}'.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool IsKnownEventType(int eventType)
        {
            if (eventType == (int)PlayerActionEventType.None)
            {
                return false;
            }

            return Enum.IsDefined(typeof(PlayerActionEventType), eventType);
        }

        private static bool IsUsableId(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && id.Length <= SaveSchema.MaxIdLength;
        }

        private static bool IsAbsentOrUsableId(string id)
        {
            // Unity's JSON writer stores an absent optional ID as an empty string, so empty means "none".
            return string.IsNullOrEmpty(id) || IsUsableId(id);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

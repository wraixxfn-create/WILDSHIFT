using System;
using System.Collections.Generic;
using UnityEngine;
using Wildshift.Core.Diagnostics;
using Wildshift.Persistence.Model;
using Wildshift.World;
using Wildshift.World.Events;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Translates between the live prototype systems (player transform, <see cref="WorldStateService"/>,
    /// <see cref="PlayerActionEventRecorder"/>) and the durable <see cref="SaveGameData"/> model.
    /// This is the only place that knows both sides: the save model stays free of gameplay types, and
    /// the gameplay systems stay free of file-format concerns.
    /// </summary>
    /// <remarks>
    /// Only state that exists and changes today is mapped: player placement, the region test value,
    /// and the retained world-event history. Nothing here serializes scenes, GameObjects, components,
    /// or object graphs — every captured value is a number or a stable ID.
    /// </remarks>
    public static class PrototypeSaveMapper
    {
        /// <summary>Reads player placement from a transform. Only yaw is kept; pitch and roll are not owned by the character.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the transform is null.</exception>
        public static PlayerSaveData CapturePlayer(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                throw new ArgumentNullException(nameof(playerTransform));
            }

            Vector3 position = playerTransform.position;
            return new PlayerSaveData(position.x, position.y, position.z, playerTransform.eulerAngles.y);
        }

        /// <summary>Builds player placement from plain values, for callers that do not own a transform.</summary>
        public static PlayerSaveData CapturePlayer(Vector3 position, float yawDegrees)
        {
            return new PlayerSaveData(position.x, position.y, position.z, yawDegrees);
        }

        /// <summary>
        /// Copies the current prototype state into a new, fully versioned save payload. Null systems
        /// are captured as empty sections so the prototype can save before everything is wired up.
        /// </summary>
        /// <param name="player">Player placement, usually from <see cref="CapturePlayer(Transform)"/>.</param>
        /// <param name="worldState">Region-state owner, or null when no world state exists yet.</param>
        /// <param name="eventRecorder">World-event log, or null when no log exists yet.</param>
        /// <param name="maxEventCount">
        /// How much of the event history to keep, clamped to <see cref="SaveSchema.MaxSavedEvents"/>.
        /// A save keeps a limited, newest-first-trimmed history, not the whole log.
        /// </param>
        public static SaveGameData CaptureState(
            PlayerSaveData player,
            WorldStateService worldState,
            PlayerActionEventRecorder eventRecorder,
            int maxEventCount = SaveSchema.MaxSavedEvents)
        {
            int eventBudget = Mathf.Clamp(maxEventCount, 0, SaveSchema.MaxSavedEvents);

            return new SaveGameData(
                player ?? new PlayerSaveData(),
                CaptureRegions(worldState),
                CaptureEvents(eventRecorder, eventBudget),
                DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                Application.version);
        }

        /// <summary>
        /// Moves a player transform to a saved placement. A <see cref="CharacterController"/> is
        /// disabled across the move because it otherwise overrides direct transform writes.
        /// </summary>
        public static bool TryApplyPlayer(PlayerSaveData player, Transform playerTransform, out string error)
        {
            if (player == null)
            {
                error = "Cannot apply a null player placement.";
                return false;
            }

            if (playerTransform == null)
            {
                error = "Cannot apply a player placement without a player transform.";
                return false;
            }

            CharacterController characterController = playerTransform.GetComponent<CharacterController>();
            bool reenableController = characterController != null && characterController.enabled;
            if (reenableController)
            {
                characterController.enabled = false;
            }

            playerTransform.SetPositionAndRotation(player.Position, player.Rotation);

            if (reenableController)
            {
                characterController.enabled = true;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Applies a validated payload's world sections to live systems. Region values are matched by
        /// stable ID; a saved region that is no longer registered is counted as skipped rather than
        /// applied to something else. Events are re-recorded oldest first into a pristine recorder.
        /// </summary>
        /// <param name="data">A payload that came from a successful load, or was just captured.</param>
        /// <param name="worldState">Region-state owner to update, or null to skip the region section.</param>
        /// <param name="eventRecorder">
        /// Event log to refill. It must not have recorded anything yet: restoring into a used log
        /// would mix two histories and could break chronological order.
        /// </param>
        /// <param name="summary">Counts describing what was applied; meaningful only on success.</param>
        /// <param name="error">
        /// Reason the restore failed; null on success and always free of filesystem paths. Every
        /// precondition (null, invalid payload, already-used recorder) is checked before anything is
        /// touched, so a failure for those reasons changes nothing.
        /// </param>
        public static bool TryRestoreWorldState(
            SaveGameData data,
            WorldStateService worldState,
            PlayerActionEventRecorder eventRecorder,
            out SaveRestoreSummary summary,
            out string error)
        {
            summary = default;

            if (data == null)
            {
                error = "Cannot restore a null save payload.";
                return false;
            }

            // Never trust a payload that has not been validated; a caller may hand over data from a
            // source other than LocalSaveService.Load.
            if (!SaveDataValidator.TryValidate(data, out string validationError))
            {
                error = $"Cannot restore the save payload: {validationError}";
                return false;
            }

            if (eventRecorder != null && eventRecorder.RecordedEventCount > 0)
            {
                error = "Cannot restore world events into an event recorder that has already recorded " +
                        "events; create a fresh recorder for the loaded session.";
                return false;
            }

            int appliedRegions = 0;
            int skippedRegions = 0;
            if (worldState != null)
            {
                foreach (RegionSaveData region in data.Regions)
                {
                    if (worldState.TryUpdateTestValue(region.StableId, region.TestValue, out string regionError))
                    {
                        appliedRegions++;
                        continue;
                    }

                    skippedRegions++;
                    WildshiftLog.Warning(
                        $"Skipped saved region '{region.StableId}' while restoring: {regionError}");
                }
            }

            int restoredEvents = 0;
            if (eventRecorder != null)
            {
                foreach (WorldEventSaveData savedEvent in data.Events)
                {
                    PlayerActionEvent rebuiltEvent = RebuildEvent(savedEvent);
                    if (eventRecorder.TryRecord(rebuiltEvent, out PlayerActionEvent _, out string eventError))
                    {
                        restoredEvents++;
                        continue;
                    }

                    // Validation already proved the history is consistent, so a rejection here means
                    // the data and the recorder disagree; stop instead of restoring half a history.
                    error = $"Failed to restore world event '{savedEvent.Id}': {eventError}";
                    return false;
                }
            }

            summary = new SaveRestoreSummary(appliedRegions, skippedRegions, restoredEvents);
            error = null;
            return true;
        }

        private static RegionSaveData[] CaptureRegions(WorldStateService worldState)
        {
            if (worldState == null)
            {
                return Array.Empty<RegionSaveData>();
            }

            IReadOnlyList<RegionState> regions = worldState.GetRegisteredRegions();
            int captureCount = Mathf.Min(regions.Count, SaveSchema.MaxSavedRegions);
            if (captureCount < regions.Count)
            {
                WildshiftLog.Warning(
                    $"Only the first {captureCount} of {regions.Count} registered regions fit in a save; " +
                    "raise SaveSchema.MaxSavedRegions when the world legitimately grows.");
            }

            List<RegionSaveData> captured = new List<RegionSaveData>(captureCount);
            for (int index = 0; index < captureCount; index++)
            {
                RegionState region = regions[index];
                if (region == null || string.IsNullOrWhiteSpace(region.StableId))
                {
                    continue;
                }

                // Only runtime state is stored; the display name stays in the authored definition.
                captured.Add(new RegionSaveData(region.StableId, region.TestValue));
            }

            return captured.ToArray();
        }

        private static WorldEventSaveData[] CaptureEvents(PlayerActionEventRecorder eventRecorder, int maxEventCount)
        {
            if (eventRecorder == null || maxEventCount <= 0)
            {
                return Array.Empty<WorldEventSaveData>();
            }

            IReadOnlyList<PlayerActionEvent> events = eventRecorder.GetRecentEvents(maxEventCount);
            WorldEventSaveData[] captured = new WorldEventSaveData[events.Count];
            for (int index = 0; index < events.Count; index++)
            {
                captured[index] = CaptureEvent(events[index]);
            }

            return captured;
        }

        private static WorldEventSaveData CaptureEvent(PlayerActionEvent sourceEvent)
        {
            IReadOnlyList<PlayerActionEventParameter> sourceParameters = sourceEvent.Parameters;
            WorldEventParameterSaveData[] parameters = sourceParameters.Count == 0
                ? Array.Empty<WorldEventParameterSaveData>()
                : new WorldEventParameterSaveData[sourceParameters.Count];

            for (int index = 0; index < sourceParameters.Count; index++)
            {
                PlayerActionEventParameter parameter = sourceParameters[index];
                parameters[index] = new WorldEventParameterSaveData(parameter.Id, parameter.Value);
            }

            return new WorldEventSaveData(
                sourceEvent.Id,
                (int)sourceEvent.EventType,
                sourceEvent.ElapsedWorldTime,
                sourceEvent.RegionId,
                sourceEvent.TargetId,
                sourceEvent.HasMagnitude,
                sourceEvent.Magnitude,
                parameters);
        }

        private static PlayerActionEvent RebuildEvent(WorldEventSaveData savedEvent)
        {
            WorldEventParameterSaveData[] savedParameters = savedEvent.Parameters;
            PlayerActionEventParameter[] parameters = savedParameters.Length == 0
                ? Array.Empty<PlayerActionEventParameter>()
                : new PlayerActionEventParameter[savedParameters.Length];

            for (int index = 0; index < savedParameters.Length; index++)
            {
                WorldEventParameterSaveData parameter = savedParameters[index];
                parameters[index] = new PlayerActionEventParameter(parameter.Id, parameter.Value);
            }

            return new PlayerActionEvent(
                savedEvent.Id,
                (PlayerActionEventType)savedEvent.EventType,
                savedEvent.ElapsedWorldTime,
                NullIfEmpty(savedEvent.RegionId),
                NullIfEmpty(savedEvent.TargetId),
                savedEvent.HasMagnitude ? savedEvent.Magnitude : (float?)null,
                parameters);
        }

        private static string NullIfEmpty(string value)
        {
            // An absent optional ID round-trips through JSON as an empty string; the live event model
            // expects null for "not present".
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}

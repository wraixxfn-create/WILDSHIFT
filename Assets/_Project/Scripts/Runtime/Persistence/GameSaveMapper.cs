using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Wildshift.Core.Diagnostics;
using Wildshift.World;
using Wildshift.World.Events;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Translates between live runtime state and <see cref="GameSaveData"/>. This is the only place
    /// that knows both sides, so neither the world/event systems nor the save service has to know
    /// about the other: <c>Wildshift.Persistence</c> depends on the data models it persists, never
    /// the other way round. Capturing reads state; applying writes it back through the owning
    /// systems' own APIs, so no saved value bypasses their validation.
    /// </summary>
    public static class GameSaveMapper
    {
        /// <summary>
        /// Captures the current prototype state into a new versioned save snapshot stamped with the
        /// current schema version, the current UTC time, and the running build. The world event
        /// slice is the newest <paramref name="maxWorldEvents"/> records, oldest first; region
        /// entries are ordered by stable ID so two captures of identical state produce identical data.
        /// Only stable IDs, enums, and numbers are captured — never transforms, components, or names.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when worldState or eventRecorder is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when maxWorldEvents is negative.</exception>
        public static GameSaveData Capture(
            Vector3 playerPosition,
            Quaternion playerOrientation,
            WorldStateService worldState,
            PlayerActionEventRecorder eventRecorder,
            int maxWorldEvents = GameSaveData.DefaultMaxWorldEvents)
        {
            if (worldState == null)
            {
                throw new ArgumentNullException(nameof(worldState));
            }

            if (eventRecorder == null)
            {
                throw new ArgumentNullException(nameof(eventRecorder));
            }

            if (maxWorldEvents < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWorldEvents), maxWorldEvents,
                    "Cannot capture a negative number of world events.");
            }

            IReadOnlyList<RegionState> regions = worldState.GetAllRegions();
            List<RegionSaveData> regionData = new List<RegionSaveData>(regions.Count);
            for (int index = 0; index < regions.Count; index++)
            {
                RegionState region = regions[index];
                regionData.Add(new RegionSaveData(region.StableId, region.TestValue));
            }

            // PlayerActionEvent records are immutable, so the snapshot can hold the recorder's own instances.
            IReadOnlyList<PlayerActionEvent> events = eventRecorder.GetRecentEvents(maxWorldEvents);
            List<PlayerActionEvent> eventData = new List<PlayerActionEvent>(events.Count);
            for (int index = 0; index < events.Count; index++)
            {
                eventData.Add(events[index]);
            }

            return new GameSaveData(
                GameSaveData.CurrentSchemaVersion,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                Application.version,
                new PlayerSaveData(playerPosition, playerOrientation),
                regionData,
                eventData);
        }

        /// <summary>
        /// Validates a save and then applies it to live runtime state: region values are pushed through
        /// <c>WorldStateService</c>, and world events are re-recorded through
        /// <c>PlayerActionEventRecorder</c> so the restored history passes the same checks as a live
        /// one. Saved region IDs that this world session has not registered are skipped and reported
        /// in the log rather than failing the load, because the set of registered regions is authored
        /// configuration. Nothing is written to disk here.
        ///
        /// Apply into a freshly created world-state service and recorder (the state of a new session).
        /// Applying into a session that already holds data can fail part way through, in which case
        /// the returned error reports how many events were restored before it stopped.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when worldState or eventRecorder is null.</exception>
        public static bool TryApply(
            GameSaveData save,
            WorldStateService worldState,
            PlayerActionEventRecorder eventRecorder,
            out PlayerSaveData player,
            out string error)
        {
            player = null;

            if (save == null)
            {
                error = "Cannot apply a null save.";
                return false;
            }

            if (worldState == null)
            {
                throw new ArgumentNullException(nameof(worldState));
            }

            if (eventRecorder == null)
            {
                throw new ArgumentNullException(nameof(eventRecorder));
            }

            // Validation happens before anything is mutated, so a rejected save leaves the session untouched.
            if (!save.TryValidate(out error))
            {
                return false;
            }

            int appliedRegions = 0;
            List<string> skippedRegionIds = null;
            IReadOnlyList<RegionSaveData> regions = save.Regions;
            for (int index = 0; index < regions.Count; index++)
            {
                RegionSaveData region = regions[index];
                if (worldState.TryUpdateTestValue(region.StableId, region.TestValue, out _))
                {
                    appliedRegions++;
                }
                else
                {
                    skippedRegionIds = skippedRegionIds ?? new List<string>();
                    skippedRegionIds.Add(region.StableId);
                }
            }

            int appliedEvents = 0;
            IReadOnlyList<PlayerActionEvent> worldEvents = save.WorldEvents;
            for (int index = 0; index < worldEvents.Count; index++)
            {
                if (!eventRecorder.TryRecord(worldEvents[index], out _, out string recordError))
                {
                    error = $"World event {index} of the save could not be restored after {appliedEvents} " +
                            $"earlier event(s) were: {recordError}";
                    return false;
                }

                appliedEvents++;
            }

            if (skippedRegionIds != null)
            {
                WildshiftLog.Warning(
                    $"The save named {skippedRegionIds.Count} region(s) this session has not registered, " +
                    $"so their state was skipped: {string.Join(", ", skippedRegionIds)}.");
            }

            WildshiftLog.Info(
                $"Applied save (schema v{save.SchemaVersion}, saved {save.SavedAtUtc}): " +
                $"{appliedRegions} region(s) restored, {skippedRegionIds?.Count ?? 0} skipped, " +
                $"{appliedEvents} world event(s) restored.");

            player = save.Player;
            error = null;
            return true;
        }
    }
}

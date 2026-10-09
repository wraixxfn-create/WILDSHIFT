using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using UnityEngine;
using Wildshift.World.Events;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Versioned snapshot of the small amount of prototype state WILDSHIFT can save today: the
    /// player's position and orientation, the registered region state, and a bounded slice of the
    /// player-action event history. This is a data-transfer object only. It never holds
    /// <see cref="GameObject"/> references, components, hierarchy paths, scene names, or whole
    /// scenes, and it does not touch the disk: <see cref="LocalSaveService"/> owns file access and
    /// <see cref="GameSaveMapper"/> owns translation to and from live runtime state.
    /// </summary>
    [Serializable]
    public sealed class GameSaveData
    {
        /// <summary>
        /// Schema version written by this build. Raise it whenever the shape or meaning of the saved
        /// data changes, and widen <see cref="OldestSupportedSchemaVersion"/> only once a migration
        /// from that older version exists.
        /// </summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Oldest schema version this build can still read. Saves below it are reported, never guessed at.</summary>
        public const int OldestSupportedSchemaVersion = 1;

        /// <summary>Number of world events a save keeps unless the caller asks for a different slice.</summary>
        public const int DefaultMaxWorldEvents = 128;

        /// <summary>
        /// Hard ceiling on stored world events accepted by validation. It exists so a corrupt or
        /// hostile file cannot make a load allocate unbounded memory; it is deliberately far above
        /// <see cref="DefaultMaxWorldEvents"/> so raising the normal cap is not a breaking change.
        /// </summary>
        public const int MaxSupportedWorldEvents = 4096;

        /// <summary>Hard ceiling on stored region entries accepted by validation.</summary>
        public const int MaxSupportedRegions = 1024;

        [SerializeField] private int _schemaVersion;
        [SerializeField] private string _savedAtUtc;
        [SerializeField] private string _gameVersion;
        [SerializeField] private PlayerSaveData _player;
        [SerializeField] private RegionSaveData[] _regions;
        [SerializeField] private PlayerActionEvent[] _worldEvents;

        [NonSerialized] private ReadOnlyCollection<RegionSaveData> _regionView;
        [NonSerialized] private ReadOnlyCollection<PlayerActionEvent> _worldEventView;

        /// <summary>
        /// Creates a save snapshot. Field validation happens in <see cref="TryValidate"/>, so callers
        /// cannot accidentally build half-valid data that looks usable.
        /// </summary>
        internal GameSaveData(
            int schemaVersion,
            string savedAtUtc,
            string gameVersion,
            PlayerSaveData player,
            IReadOnlyList<RegionSaveData> regions,
            IReadOnlyList<PlayerActionEvent> worldEvents)
        {
            _schemaVersion = schemaVersion;
            _savedAtUtc = savedAtUtc;
            _gameVersion = gameVersion;
            _player = player;
            _regions = Copy(regions);
            _worldEvents = Copy(worldEvents);
        }

        /// <summary>Parameterless constructor for Unity serialization only; do not call directly.</summary>
        internal GameSaveData()
        {
        }

        /// <summary>Schema version declared by this save.</summary>
        public int SchemaVersion => _schemaVersion;

        /// <summary>
        /// Round-trip ("o") UTC timestamp captured when the save was written. Diagnostics only; it is
        /// not gameplay state and gameplay code must not depend on it.
        /// </summary>
        public string SavedAtUtc => _savedAtUtc;

        /// <summary>
        /// Build identifier captured when the save was written (<c>Application.version</c>), used to
        /// explain an incompatible save. Optional and never required by validation.
        /// </summary>
        public string GameVersion => _gameVersion;

        /// <summary>Saved player position and orientation; required in every supported schema version.</summary>
        public PlayerSaveData Player => _player;

        /// <summary>Read-only, possibly empty region entries ordered by stable ID; never null.</summary>
        public IReadOnlyList<RegionSaveData> Regions => View(_regions, ref _regionView);

        /// <summary>Read-only, possibly empty world events in chronological order (oldest first); never null.</summary>
        public IReadOnlyList<PlayerActionEvent> WorldEvents => View(_worldEvents, ref _worldEventView);

        /// <summary>True when this save declares a schema version this build can read.</summary>
        public bool HasSupportedSchemaVersion => IsSupportedSchemaVersion(_schemaVersion);

        /// <summary>True when the given schema version is inside the supported range.</summary>
        public static bool IsSupportedSchemaVersion(int schemaVersion)
        {
            return schemaVersion >= OldestSupportedSchemaVersion && schemaVersion <= CurrentSchemaVersion;
        }

        /// <summary>
        /// Reads the captured timestamp. Returns false when the value is missing or unreadable, which
        /// <see cref="TryValidate"/> treats as invalid data.
        /// </summary>
        public bool TryGetSavedAtUtc(out DateTime savedAtUtc)
        {
            savedAtUtc = default;
            if (string.IsNullOrWhiteSpace(_savedAtUtc))
            {
                return false;
            }

            return DateTime.TryParse(
                _savedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out savedAtUtc);
        }

        /// <summary>
        /// Validates everything a load must trust before the data is applied: a supported schema
        /// version, a readable timestamp, a usable player block, region entries with unique
        /// non-blank stable IDs, and a world event history that a live recorder would accept
        /// (valid records, chronological, no duplicated IDs). Returns false with a descriptive error
        /// instead of throwing, and never changes the data.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (!IsSupportedSchemaVersion(_schemaVersion))
            {
                error = $"save schema version {_schemaVersion} is not supported; " +
                        $"this build reads versions {OldestSupportedSchemaVersion} to {CurrentSchemaVersion}";
                return false;
            }

            if (!TryGetSavedAtUtc(out _))
            {
                error = "the save does not contain a readable UTC 'saved at' timestamp";
                return false;
            }

            if (_player == null)
            {
                error = "the save is missing its player data block";
                return false;
            }

            if (!_player.TryValidate(out error))
            {
                return false;
            }

            if (!TryValidateRegions(out error))
            {
                return false;
            }

            if (!TryValidateWorldEvents(out error))
            {
                return false;
            }

            error = null;
            return true;
        }

        private bool TryValidateRegions(out string error)
        {
            if (_regions == null)
            {
                error = "the save is missing its region data collection";
                return false;
            }

            if (_regions.Length > MaxSupportedRegions)
            {
                error = $"the save contains {_regions.Length} regions; at most {MaxSupportedRegions} are supported";
                return false;
            }

            HashSet<string> seenIds = null;
            for (int index = 0; index < _regions.Length; index++)
            {
                RegionSaveData region = _regions[index];
                if (region == null)
                {
                    error = $"region entry {index} in the save is null";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(region.StableId))
                {
                    error = $"region entry {index} in the save has an empty or whitespace stable ID";
                    return false;
                }

                if (seenIds == null)
                {
                    seenIds = new HashSet<string>(StringComparer.Ordinal);
                }

                if (!seenIds.Add(region.StableId))
                {
                    error = $"the save contains region '{region.StableId}' more than once";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private bool TryValidateWorldEvents(out string error)
        {
            if (_worldEvents == null)
            {
                error = "the save is missing its world event collection";
                return false;
            }

            if (_worldEvents.Length > MaxSupportedWorldEvents)
            {
                error = $"the save contains {_worldEvents.Length} world events; " +
                        $"at most {MaxSupportedWorldEvents} are supported";
                return false;
            }

            // Replaying the stored history through a throwaway recorder reuses the event system's own
            // validation, so a save that passes this check can always be restored into a live recorder.
            PlayerActionEventRecorder probe = new PlayerActionEventRecorder(Math.Max(1, _worldEvents.Length));
            for (int index = 0; index < _worldEvents.Length; index++)
            {
                if (!probe.TryRecord(_worldEvents[index], out _, out string eventError))
                {
                    error = $"world event {index} in the save is unusable: {eventError}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static T[] Copy<T>(IReadOnlyList<T> source) where T : class
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<T>();
            }

            T[] copy = new T[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return copy;
        }

        private static IReadOnlyList<T> View<T>(T[] source, ref ReadOnlyCollection<T> cachedView) where T : class
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<T>();
            }

            if (cachedView == null)
            {
                cachedView = Array.AsReadOnly(source);
            }

            return cachedView;
        }
    }
}

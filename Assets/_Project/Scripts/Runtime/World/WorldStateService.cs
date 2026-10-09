using System;
using System.Collections.Generic;

namespace Wildshift.World
{
    /// <summary>
    /// Owns the in-memory runtime state for the regions explicitly registered with this instance.
    /// Construct one service for each world/session that needs isolated state; this class does not
    /// discover scenes, generate regions, or act as a global singleton.
    /// </summary>
    public sealed class WorldStateService
    {
        private readonly Dictionary<string, RegionState> _regions =
            new Dictionary<string, RegionState>(StringComparer.Ordinal);

        /// <summary>Number of regions explicitly registered with this service.</summary>
        public int RegisteredRegionCount => _regions.Count;

        /// <summary>
        /// Creates a fresh runtime state from an authored region definition.
        /// Duplicate or blank stable IDs are rejected without changing existing state.
        /// </summary>
        public bool TryRegisterRegion(RegionDefinition definition, out string error)
        {
            if (definition == null)
            {
                error = "Cannot register a null region definition.";
                return false;
            }

            string stableId = definition.StableId;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                error = "Cannot register a region whose stable ID is empty or whitespace.";
                return false;
            }

            if (_regions.ContainsKey(stableId))
            {
                error = $"A region with stable ID '{stableId}' is already registered.";
                return false;
            }

            // RegionState is a new object per registration. Never attach changing values to the shared asset.
            _regions.Add(stableId, definition.CreateInitialState());
            error = null;
            return true;
        }

        /// <summary>
        /// Looks up a region by stable ID. Returns false and a descriptive error for invalid or unknown IDs;
        /// it does not throw for an ordinary lookup failure.
        /// </summary>
        public bool TryGetRegion(string stableId, out RegionState state, out string error)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                error = "Cannot retrieve a region with an empty or whitespace stable ID.";
                return false;
            }

            if (_regions.TryGetValue(stableId, out state))
            {
                error = null;
                return true;
            }

            error = $"No region with stable ID '{stableId}' is registered.";
            return false;
        }

        /// <summary>
        /// Returns a snapshot list of every registered region state, ordered by stable ID so callers
        /// such as persistence produce the same output for the same state. The list itself is a copy;
        /// the states inside it are still owned by this service and must not be shared between sessions.
        /// </summary>
        public IReadOnlyList<RegionState> GetAllRegions()
        {
            List<RegionState> snapshot = new List<RegionState>(_regions.Count);
            foreach (KeyValuePair<string, RegionState> registered in _regions)
            {
                snapshot.Add(registered.Value);
            }

            snapshot.Sort((left, right) => string.CompareOrdinal(left.StableId, right.StableId));
            return snapshot;
        }

        /// <summary>
        /// Updates the foundation-only test value on a registered region.
        /// Returns false with an error message if the region is unknown.
        /// </summary>
        public bool TryUpdateTestValue(string stableId, int value, out string error)
        {
            if (!TryGetRegion(stableId, out RegionState state, out error))
            {
                return false;
            }

            state.SetTestValue(value);
            return true;
        }
    }
}

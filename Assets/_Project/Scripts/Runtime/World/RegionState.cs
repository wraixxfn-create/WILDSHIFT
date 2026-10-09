using System;
using UnityEngine;

namespace Wildshift.World
{
    /// <summary>
    /// Mutable, serializable runtime data for one registered world region.
    /// A state is created from a <see cref="RegionDefinition"/> and is owned by one
    /// <see cref="WorldStateService"/>; it is never stored back into the shared definition asset.
    /// Add future ecology, settlement/faction influence, environmental-change, or persistent-event data here
    /// (or in serializable subobjects) once those systems define their data contracts; no such model is added yet.
    /// </summary>
    [Serializable]
    public sealed class RegionState
    {
        [SerializeField] private string _stableId;
        [SerializeField] private string _displayName;
        [SerializeField] private int _testValue;

        /// <summary>Stable region key copied from its authored definition.</summary>
        public string StableId => _stableId;

        /// <summary>Optional display label copied from its authored definition.</summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Simple foundation-only mutable value used to verify region-state ownership and updates.
        /// It is not an ecological or gameplay metric.
        /// </summary>
        public int TestValue => _testValue;

        internal RegionState(string stableId, string displayName, int testValue)
        {
            _stableId = stableId;
            _displayName = displayName;
            _testValue = testValue;
        }

        internal void SetTestValue(int value)
        {
            _testValue = value;
        }
    }
}

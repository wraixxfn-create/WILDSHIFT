using System;
using UnityEngine;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Durable state of one registered world region. It stores the region's stable ID (the same
    /// vocabulary as <c>RegionDefinition.StableId</c>) plus the mutable values a save owns today.
    /// Authored configuration — the display label and the starting test value — is deliberately not
    /// persisted: those live in the shared <c>RegionDefinition</c> asset and re-reading them from a
    /// save would let an old file overwrite current authoring.
    /// </summary>
    [Serializable]
    public sealed class RegionSaveData
    {
        [SerializeField] private string _stableId;
        [SerializeField] private int _testValue;

        /// <summary>Creates a region block; validation happens when the owning save is validated.</summary>
        internal RegionSaveData(string stableId, int testValue)
        {
            _stableId = stableId;
            _testValue = testValue;
        }

        /// <summary>Parameterless constructor for Unity serialization only; do not call directly.</summary>
        internal RegionSaveData()
        {
        }

        /// <summary>Stable region ID this block belongs to; required and unique within one save.</summary>
        public string StableId => _stableId;

        /// <summary>
        /// Foundation-only mutable region value carried over from <c>RegionState.TestValue</c>.
        /// Future ecology, settlement, faction, or environmental-change data is added as new fields
        /// here rather than replacing this one.
        /// </summary>
        public int TestValue => _testValue;
    }
}

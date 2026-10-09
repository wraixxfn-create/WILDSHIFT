using System;
using UnityEngine;

namespace Wildshift.Persistence.Model
{
    /// <summary>
    /// Durable record of the changing state of one region, keyed by its stable region ID.
    /// Authored data (display label, starting values) is deliberately not saved: it is reloaded from
    /// the <c>RegionDefinition</c> asset, so designers can change it without invalidating saves.
    /// </summary>
    /// <remarks>
    /// Only the fields that exist and change at runtime today are stored. Future ecology, settlement,
    /// faction, or environmental-change data is added here as new, defaulted fields once those
    /// systems own a data contract.
    /// </remarks>
    [Serializable]
    public sealed class RegionSaveData
    {
        [SerializeField] private string stableId;
        [SerializeField] private int testValue;

        /// <summary>Creates an empty record; used by the JSON deserializer.</summary>
        public RegionSaveData()
        {
        }

        /// <summary>Creates a record for one region's runtime state.</summary>
        public RegionSaveData(string stableId, int testValue)
        {
            this.stableId = stableId;
            this.testValue = testValue;
        }

        /// <summary>Stable authored region ID; the identity used to match this record back to a region.</summary>
        public string StableId => stableId;

        /// <summary>Foundation-only mutable value owned by the region's runtime state.</summary>
        public int TestValue => testValue;
    }
}

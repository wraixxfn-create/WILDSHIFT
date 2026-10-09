using System;
using UnityEngine;

namespace Wildshift.Persistence.Model
{
    /// <summary>
    /// Durable copy of one recorded world event (a <c>PlayerActionEvent</c>) from the limited history
    /// a save keeps. Records hold stable IDs and numbers only, and the saved order is the chronological
    /// order of the live log: entry 0 is the oldest retained event.
    /// </summary>
    /// <remarks>
    /// <see cref="EventType"/> is stored as the integer value of <c>PlayerActionEventType</c>. Those
    /// values are explicitly numbered and must never be renumbered or reused; add new kinds with new
    /// numbers. Unknown numbers are rejected at load instead of being guessed at.
    /// </remarks>
    [Serializable]
    public sealed class WorldEventSaveData
    {
        [SerializeField] private string id;
        [SerializeField] private int eventType;
        [SerializeField] private double elapsedWorldTime;
        [SerializeField] private string regionId;
        [SerializeField] private string targetId;
        [SerializeField] private bool hasMagnitude;
        [SerializeField] private float magnitude;
        [SerializeField] private WorldEventParameterSaveData[] parameters;

        /// <summary>Creates an empty record; used by the JSON deserializer.</summary>
        public WorldEventSaveData()
        {
        }

        /// <summary>
        /// Creates a record. Absent optional IDs are stored as empty strings because the JSON writer
        /// does not distinguish null from empty; readers treat empty as "not present".
        /// </summary>
        public WorldEventSaveData(
            string id,
            int eventType,
            double elapsedWorldTime,
            string regionId,
            string targetId,
            bool hasMagnitude,
            float magnitude,
            WorldEventParameterSaveData[] parameters)
        {
            this.id = id;
            this.eventType = eventType;
            this.elapsedWorldTime = elapsedWorldTime;
            this.regionId = regionId ?? string.Empty;
            this.targetId = targetId ?? string.Empty;
            this.hasMagnitude = hasMagnitude;
            this.magnitude = hasMagnitude ? magnitude : 0f;
            this.parameters = parameters ?? Array.Empty<WorldEventParameterSaveData>();
        }

        /// <summary>Stable event ID; unique within one save file.</summary>
        public string Id => id;

        /// <summary>Integer value of the event kind; see the remarks on this type.</summary>
        public int EventType => eventType;

        /// <summary>Elapsed world time in seconds when the event happened; non-negative and non-decreasing across the list.</summary>
        public double ElapsedWorldTime => elapsedWorldTime;

        /// <summary>Stable region ID, or an empty string when the event names no region.</summary>
        public string RegionId => regionId;

        /// <summary>Stable target ID, or an empty string when the event names no target.</summary>
        public string TargetId => targetId;

        /// <summary>True when <see cref="Magnitude"/> carries a meaningful value.</summary>
        public bool HasMagnitude => hasMagnitude;

        /// <summary>Non-negative amount related to the event; meaningful only when <see cref="HasMagnitude"/> is true.</summary>
        public float Magnitude => magnitude;

        /// <summary>Structured parameters; never null when read through this property.</summary>
        public WorldEventParameterSaveData[] Parameters =>
            parameters ?? Array.Empty<WorldEventParameterSaveData>();
    }
}

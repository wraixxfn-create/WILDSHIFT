using System;
using UnityEngine;

namespace Wildshift.Persistence.Model
{
    /// <summary>
    /// Root of one WILDSHIFT save file: a versioned, flat data-transfer object containing only
    /// numbers, strings, and arrays of other save records. It is never a view of live game state —
    /// a mapper copies owned runtime state into it before a save and reads it back after a load.
    /// </summary>
    /// <remarks>
    /// Unity's JSON writer uses the serialized field names, so the private field names in this file
    /// (<c>schemaVersion</c>, <c>player</c>, <c>regions</c>, <c>events</c>, ...) are the on-disk keys.
    /// Renaming a field is a format change: bump <see cref="SaveSchema.CurrentVersion"/> when you do.
    /// Never add Unity object references, scene names, hierarchy paths, or absolute filesystem paths here.
    /// </remarks>
    [Serializable]
    public sealed class SaveGameData
    {
        [SerializeField] private int schemaVersion;
        [SerializeField] private string savedAtUtc;
        [SerializeField] private string applicationVersion;
        [SerializeField] private PlayerSaveData player;
        [SerializeField] private RegionSaveData[] regions;
        [SerializeField] private WorldEventSaveData[] events;

        /// <summary>Creates an empty record; used by the JSON deserializer.</summary>
        public SaveGameData()
        {
        }

        /// <summary>
        /// Creates a save payload stamped with the current schema version. Null sections are stored
        /// as empty defaults so a reader never has to deal with partially missing structure.
        /// </summary>
        public SaveGameData(
            PlayerSaveData player,
            RegionSaveData[] regions,
            WorldEventSaveData[] events,
            string savedAtUtc = null,
            string applicationVersion = null)
        {
            schemaVersion = SaveSchema.CurrentVersion;
            this.savedAtUtc = savedAtUtc ?? string.Empty;
            this.applicationVersion = applicationVersion ?? string.Empty;
            this.player = player ?? new PlayerSaveData();
            this.regions = regions ?? Array.Empty<RegionSaveData>();
            this.events = events ?? Array.Empty<WorldEventSaveData>();
        }

        /// <summary>
        /// Schema version the file was written with. It is the first thing a loader checks; a file
        /// whose version this build does not support is reported, never reinterpreted.
        /// </summary>
        public int SchemaVersion => schemaVersion;

        /// <summary>
        /// Round-trip ("O") UTC timestamp of the save, or an empty string. Informational metadata for
        /// developers and future save listings; it is never used to decide gameplay outcomes.
        /// </summary>
        public string SavedAtUtc => savedAtUtc;

        /// <summary>Application version that wrote the file, or an empty string; informational only.</summary>
        public string ApplicationVersion => applicationVersion;

        /// <summary>Player placement; never null when read through this property.</summary>
        public PlayerSaveData Player => player ?? new PlayerSaveData();

        /// <summary>Saved region states, keyed by stable region ID; never null when read through this property.</summary>
        public RegionSaveData[] Regions => regions ?? Array.Empty<RegionSaveData>();

        /// <summary>Saved world-event history in chronological order; never null when read through this property.</summary>
        public WorldEventSaveData[] Events => events ?? Array.Empty<WorldEventSaveData>();

        /// <summary>Short debug description; deliberately contains no filesystem path.</summary>
        public override string ToString()
        {
            return $"save v{schemaVersion} ({Regions.Length} regions, {Events.Length} events)";
        }
    }
}

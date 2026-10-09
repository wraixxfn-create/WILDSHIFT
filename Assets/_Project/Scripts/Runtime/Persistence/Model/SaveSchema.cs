using Wildshift.World.Events;

namespace Wildshift.Persistence.Model
{
    /// <summary>
    /// Version and size constants that define the on-disk WILDSHIFT save format.
    /// Everything that decides whether a file can be interpreted lives here so the rules are in one
    /// place: <see cref="SaveGameData"/> only carries data, and the save service only applies these rules.
    /// </summary>
    public static class SaveSchema
    {
        /// <summary>
        /// Version written by this build. Increase it whenever a change would make an older build
        /// misread a new file (renamed/removed fields, changed units, changed meaning of a value).
        /// Purely additive, safely defaulted fields do not need a new version.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Oldest version this build can still read. There is no migration step yet, so it equals
        /// <see cref="CurrentVersion"/>; raise it only together with a documented migration path.
        /// </summary>
        public const int MinimumSupportedVersion = 1;

        /// <summary>Upper bound on saved regions; keeps a single prototype file small and bounded.</summary>
        public const int MaxSavedRegions = 512;

        /// <summary>Upper bound on saved world events; the save keeps a limited history, not the full log.</summary>
        public const int MaxSavedEvents = 256;

        /// <summary>Upper bound on parameters per saved event; mirrors the in-memory recorder limit.</summary>
        public const int MaxEventParameters = PlayerActionEventRecorder.MaxParametersPerEvent;

        /// <summary>Upper bound on any stable ID stored in a save; rejects absurd or hostile input early.</summary>
        public const int MaxIdLength = 256;

        /// <summary>True when a file claiming this schema version can be read by this build.</summary>
        public static bool IsSupportedVersion(int schemaVersion)
        {
            return schemaVersion >= MinimumSupportedVersion && schemaVersion <= CurrentVersion;
        }

        /// <summary>
        /// Returns a developer-readable reason why a schema version cannot be read, or null when the
        /// version is supported. The text never contains a filesystem path.
        /// </summary>
        public static string DescribeIncompatibility(int schemaVersion)
        {
            if (schemaVersion <= 0)
            {
                return $"The save data declares schema version {schemaVersion}; a valid save declares a " +
                       $"positive version (this build writes version {CurrentVersion}).";
            }

            if (schemaVersion > CurrentVersion)
            {
                return $"The save data was written by a newer build (schema version {schemaVersion}); " +
                       $"this build understands up to version {CurrentVersion}.";
            }

            if (schemaVersion < MinimumSupportedVersion)
            {
                return $"The save data uses retired schema version {schemaVersion}; this build can only " +
                       $"read versions {MinimumSupportedVersion} to {CurrentVersion} and no migration exists.";
            }

            return null;
        }
    }
}

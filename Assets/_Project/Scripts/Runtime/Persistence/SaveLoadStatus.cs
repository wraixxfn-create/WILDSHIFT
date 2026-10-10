namespace Wildshift.Persistence
{
    /// <summary>
    /// Outcome of one explicit save or load operation. The value is the user-facing surface of the
    /// save system: gameplay UI should present one of these states (for example "Saved", "No save
    /// found", "This save is from another version") and must never show a filesystem path.
    /// </summary>
    public enum SaveLoadStatus
    {
        /// <summary>The save was written, or the primary save file was loaded successfully.</summary>
        Success = 0,

        /// <summary>
        /// Load only: no save file exists yet. This is the normal state on a first run and is not a
        /// failure, so it is reported at info level rather than as an error.
        /// </summary>
        NoSaveFile = 1,

        /// <summary>
        /// Load only: the primary save file was missing or unusable, so the previous save kept as a
        /// backup was loaded instead. The player still has usable state, but the newer file needs
        /// attention. Nothing is written while this happens.
        /// </summary>
        RecoveredFromBackup = 2,

        /// <summary>The caller passed invalid arguments, for example null save data.</summary>
        InvalidArgument = 3,

        /// <summary>
        /// The data could be parsed but failed content validation (missing blocks, blank or
        /// duplicated stable IDs, non-finite numbers, or an event history that is not chronological).
        /// The data was not applied.
        /// </summary>
        InvalidData = 4,

        /// <summary>
        /// The file exists but is not a readable WILDSHIFT save object (empty, truncated, or not
        /// valid save JSON). The file was left untouched.
        /// </summary>
        MalformedData = 5,

        /// <summary>
        /// The save declares a schema version outside the supported range. The bytes were
        /// deliberately not interpreted, because guessing at an unknown layout would silently
        /// produce wrong state.
        /// </summary>
        IncompatibleVersion = 6,

        /// <summary>The file exists but its bytes could not be read from disk.</summary>
        ReadFailed = 7,

        /// <summary>The save could not be written to disk. Any previous save is still intact.</summary>
        WriteFailed = 8,

        /// <summary>
        /// The request was refused because another save or load is still running. The refused request
        /// read and wrote nothing, so the caller can try again once the running one has finished.
        /// </summary>
        OperationInProgress = 9,
    }
}

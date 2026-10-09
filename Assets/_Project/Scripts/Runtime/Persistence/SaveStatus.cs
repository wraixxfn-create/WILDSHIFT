namespace Wildshift.Persistence
{
    /// <summary>Outcome of one explicit save request.</summary>
    public enum SaveStatus
    {
        /// <summary>The file was written and now contains the requested payload.</summary>
        Success = 0,

        /// <summary>Nothing was written because the previous save happened too recently.</summary>
        Throttled = 1,

        /// <summary>Nothing was written because the payload failed validation.</summary>
        InvalidData = 2,

        /// <summary>
        /// Nothing was written because the last load failed and the existing file has not been
        /// released for overwriting yet.
        /// </summary>
        OverwriteBlocked = 3,

        /// <summary>The filesystem rejected the write; the previous file was left as it was.</summary>
        WriteFailed = 4,
    }

    /// <summary>Outcome of one explicit load request.</summary>
    public enum LoadStatus
    {
        /// <summary>A valid, compatible save was read and validated.</summary>
        Success = 0,

        /// <summary>No save file exists yet; the normal first-run case, not an error.</summary>
        NoSaveFile = 1,

        /// <summary>The file exists but could not be read from disk.</summary>
        ReadFailed = 2,

        /// <summary>The file was read but is not parseable save JSON.</summary>
        Malformed = 3,

        /// <summary>The file parsed but declares a schema version this build must not interpret.</summary>
        IncompatibleVersion = 4,

        /// <summary>The file parsed but its contents failed validation.</summary>
        InvalidData = 5,
    }
}

using Wildshift.Persistence.Model;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Result of one save request. Carries a machine-checkable <see cref="Status"/>, a detailed
    /// developer message, and a short player-safe message.
    /// </summary>
    public readonly struct SaveResult
    {
        private SaveResult(SaveStatus status, string developerMessage, string userMessage)
        {
            Status = status;
            DeveloperMessage = developerMessage;
            UserMessage = userMessage;
        }

        /// <summary>What happened.</summary>
        public SaveStatus Status { get; }

        /// <summary>True only when the save file now holds the requested payload.</summary>
        public bool IsSuccess => Status == SaveStatus.Success;

        /// <summary>Detailed message for logs and the console; may name a file path.</summary>
        public string DeveloperMessage { get; }

        /// <summary>Short message safe to show in gameplay UI; never contains a filesystem path.</summary>
        public string UserMessage { get; }

        internal static SaveResult Success(string developerMessage)
        {
            return new SaveResult(SaveStatus.Success, developerMessage, "Game saved.");
        }

        internal static SaveResult Failure(SaveStatus status, string developerMessage, string userMessage)
        {
            return new SaveResult(status, developerMessage, userMessage);
        }
    }

    /// <summary>
    /// Result of one load request. <see cref="Data"/> is non-null only when <see cref="IsSuccess"/>
    /// is true, so a failed load can never be applied by accident.
    /// </summary>
    public readonly struct LoadResult
    {
        private LoadResult(LoadStatus status, SaveGameData data, string developerMessage, string userMessage)
        {
            Status = status;
            Data = data;
            DeveloperMessage = developerMessage;
            UserMessage = userMessage;
        }

        /// <summary>What happened.</summary>
        public LoadStatus Status { get; }

        /// <summary>True only when a valid, compatible payload was read.</summary>
        public bool IsSuccess => Status == LoadStatus.Success;

        /// <summary>Validated payload on success; null otherwise.</summary>
        public SaveGameData Data { get; }

        /// <summary>Detailed message for logs and the console; may name a file path.</summary>
        public string DeveloperMessage { get; }

        /// <summary>Short message safe to show in gameplay UI; never contains a filesystem path.</summary>
        public string UserMessage { get; }

        internal static LoadResult Success(SaveGameData data, string developerMessage)
        {
            return new LoadResult(LoadStatus.Success, data, developerMessage, "Save loaded.");
        }

        internal static LoadResult Failure(LoadStatus status, string developerMessage, string userMessage)
        {
            return new LoadResult(status, null, developerMessage, userMessage);
        }
    }
}

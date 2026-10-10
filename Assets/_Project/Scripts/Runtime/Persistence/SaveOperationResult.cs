using System;
using System.Collections.Generic;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Outcome of one explicit save or load request made through the prototype save controller. It holds
    /// only player-presentable text: the status, a one-line message, and any recovery notes. It never holds
    /// a file path, because a save directory reveals the local account name and machine layout.
    /// </summary>
    public readonly struct SaveOperationResult
    {
        private static readonly IReadOnlyList<string> NoRecoveries = Array.Empty<string>();

        /// <summary>Creates a result. A null recovery list is treated as "no recoveries".</summary>
        public SaveOperationResult(SaveLoadStatus status, string message, IReadOnlyList<string> recoveries)
        {
            Status = status;
            Message = message ?? string.Empty;
            Recoveries = recoveries ?? NoRecoveries;
        }

        /// <summary>Outcome of the file-level operation, or of the request if it was refused before reaching the file.</summary>
        public SaveLoadStatus Status { get; }

        /// <summary>One-line, player-presentable summary such as "Saved." or "No save found."</summary>
        public string Message { get; }

        /// <summary>
        /// Notes about recovery that happened while completing the request, such as a safe-spawn placement or a
        /// region that this build does not register. Empty when everything was used as saved. Never null.
        /// </summary>
        public IReadOnlyList<string> Recoveries { get; }

        /// <summary>True when the save was written, or the session was restored (even from the backup).</summary>
        public bool IsSuccess => Status == SaveLoadStatus.Success || Status == SaveLoadStatus.RecoveredFromBackup;

        /// <summary>True when at least one recovery note was produced.</summary>
        public bool HasRecoveries => Recoveries.Count > 0;
    }
}
